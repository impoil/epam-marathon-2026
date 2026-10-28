using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using APIHandler.Services.GuestSessions;
using System;
using System.Collections.Generic;
using System.Linq;
using APIHandler.Constants.Orders;
using System.Threading.Tasks;

namespace APIHandler.Services.GroupReservations;

public class GroupReservationService(
    IGroupReservationRepository groupReservationRepository,
    ITableRepository tableRepository,
    IReservationRepository reservationRepository,
    IGuestSessionRepository guestSessionRepository,
    IUserService userService,
    ILocationService locationService,
    IOrderRepository orderRepository,
    IGuestSessionService guestSessionService) : IGroupReservationService
{
    private const string DefaultRestaurantName = "Green & Tasty";

    public async Task<IReadOnlyList<SubmittedDishResponse>> SubmitPreOrderAsync(
        string userId, string groupReservationId, string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        if (string.IsNullOrWhiteSpace(userId) || reservation.CustomerId != userId)
            throw new ActionForbiddenException("Only the reservation owner can submit the group pre-order.");
        var group = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId)
            ?? throw new GroupReservationNotFoundException();
        if (group.ReservationId != reservationId)
            throw new ValidationException("Group reservation does not belong to this reservation.");
        if (group.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();
        if (group.Status != GroupReservationStatus.Open || reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException();

        var sessions = await guestSessionRepository.GetAllByGroupReservationIdAsync(groupReservationId);
        var submitted = sessions.Where(s => s.GroupReservationId == groupReservationId && s.IsSubmitted).ToArray();
        if (submitted.Length == 0)
            throw new ValidationException("There are no submitted guest sessions.");
        var items = submitted.SelectMany(s => s.Dishes ?? []).ToArray();
        if (items.Length == 0)
            throw new ValidationException("Submitted guest sessions contain no dishes.");
        if (items.Any(d => string.IsNullOrWhiteSpace(d.DishId) || d.Quantity <= 0))
            throw new ValidationException("Submitted dishes must have an id and a positive quantity.");
        // Materialize and validate before making any changes to loaded entities.
        SubmittedDishResponse[] dishes;
        try
        {
            dishes = items.GroupBy(d => d.DishId, StringComparer.Ordinal)
                .Select(g => new SubmittedDishResponse(g.Key, g.Sum(d => d.Quantity))).ToArray();
        }
        catch (OverflowException)
        {
            throw new ValidationException("The total quantity for a dish is too large.");
        }

        var order = await orderRepository.GetOrderByReservationIdAsync(reservationId)
            ?? throw new ValidationException("Order not found.");
        if (order.CustomerId != userId || order.ReservationId != reservationId)
            throw new ActionForbiddenException("Order does not belong to this reservation owner.");
        if (order.Status != OrderStatus.Created)
            throw new ValidationException("Only an order in Created status can be submitted.");

        // Detached copies prevent failed writes from mutating repository-returned objects.
        var submittedOrder = new Order
        {
            OrderId = order.OrderId, CustomerId = order.CustomerId,
            ReservationId = order.ReservationId, ReservationAddress = order.ReservationAddress,
            ReservationTime = order.ReservationTime, Status = OrderStatus.SubmittedByCustomer,
            Dishes = dishes.Select(d => new OrderItem
            {
                DishId = d.DishId, Quantity = d.Quantity, State = OrderItemState.Pending
            }).ToList()
        };
        var submittedGroup = new GroupReservation
        {
            GroupReservationId = group.GroupReservationId, ReservationId = group.ReservationId,
            ReservationToken = group.ReservationToken, Status = GroupReservationStatus.Submitted
        };
        await groupReservationRepository.SubmitPreOrderAsync(submittedGroup, submittedOrder);
        return dishes;
    }

    public async Task<GetGroupReservationResponse> GetGroupReservationByTokenAsync(string token)
    {
        GroupReservation groupReservation = await groupReservationRepository.GetGroupReservationByTokenAsync(token)
            ?? throw new GroupReservationNotFoundException();

        if (groupReservation.Status != GroupReservationStatus.Open)
            throw new GroupReservationExpiredException();

        Task<Reservation?> reservationTask = reservationRepository.GetReservationByIdAsync(groupReservation.ReservationId);
        Task<int> countTask = guestSessionRepository.CountByGroupReservationId(groupReservation.GroupReservationId);

        Reservation reservation = await reservationTask ?? throw new ValidationException("Associated reservation not found.");
        int joinedGuestsCount = await countTask;

        if (joinedGuestsCount + 1 >= reservation.GuestCount)
            throw new GuestLimitExceededException();

        Table table = await tableRepository.GetTableAsync(reservation.TableId)
            ?? throw new InvalidOperationException("Associated table not found.");

        Location? location = await locationService.GetLocationByIdAsync(table.LocationId);
        string restaurantName = DefaultRestaurantName;
        string address = location?.Address ?? "Unknown Address";

        string inviterName = "Unknown";
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            inviterName = await userService.GetUserName(reservation.CustomerId) ?? "Unknown";
        }
        return new GetGroupReservationResponse(
            groupReservation.GroupReservationId,
            table.LocationId,
            restaurantName,
            address,
            reservation.StartTime,
            reservation.EndTime,
            reservation.GuestCount,
            table.TableNumber,
            inviterName);
    }

    public async Task<GroupReservation> GetGroupReservationAsync(string groupReservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetGroupReservationByIdAsync(groupReservationId);
        return groupReservation ?? throw new InvalidOperationException($"Group reservation with ID {groupReservationId} not found.");
    }

    public async Task<GroupReservation> CreateGroupReservationAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException("Reservation not found.");
        var groupReservation = new GroupReservation
        {
            GroupReservationId = Guid.NewGuid().ToString(),
            ReservationId = reservationId,
            ReservationToken = Guid.NewGuid().ToString("N"),
            Status = GroupReservationStatus.Open,
        };

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
        if (!string.IsNullOrWhiteSpace(reservation.CustomerId))
        {
            await guestSessionService.CreateGroupOwnerSessionAsync(reservation.CustomerId, groupReservation.GroupReservationId);
        }
        return groupReservation;
    }

    public async Task CreateGroupReservationIfDoesNotExistAsync(string userId, string reservationId)
    {
        GroupReservation? group = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (group is not null)
            return;

        await CreateGroupReservationAsync(reservationId);
    }

    public async Task UpdateGroupReservationStatusAsync(string groupReservationId, GroupReservationStatus status)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        groupReservation.Status = status;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task DeleteGroupReservationAsync(string groupReservationId)
    {
        var groupReservation = await GetGroupReservationAsync(groupReservationId);

        await groupReservationRepository.DeleteGroupReservationAsync(groupReservation);
    }

    public async Task CloseGroupReservationAsync(string reservationId)
    {
        GroupReservation? groupReservation = await groupReservationRepository.GetByReservationIdAsync(reservationId);
        if (groupReservation is null)
            return;

        groupReservation.Status = GroupReservationStatus.Closed;

        await groupReservationRepository.SaveGroupReservationAsync(groupReservation);
    }

    public async Task<GetGroupInviteTokenResponse> GetGroupReservationInviteTokenAsync(string reservationId)
    {
        var reservation = await reservationRepository.GetReservationByIdAsync(reservationId)
            ?? throw new ReservationNotFoundException($"Reservation {reservationId} was not found.");

        if (reservation.GuestCount < 2)
            throw new ValidationException("Retrieving invitation token for the reservation with only one guest is not permitted.");

        if (reservation.Status != ReservationStatus.Reserved)
            throw new GroupReservationExpiredException("The group reservation is expired.");

        GroupReservation groupReservation = (await groupReservationRepository.GetByReservationIdAsync(reservationId))!;

        if (groupReservation.Status == GroupReservationStatus.Submitted)
            throw new GroupReservationSubmittedException();

        if (groupReservation.Status == GroupReservationStatus.Closed)
            throw new GroupReservationExpiredException();

        return new GetGroupInviteTokenResponse(groupReservation.ReservationToken);
    }
}

