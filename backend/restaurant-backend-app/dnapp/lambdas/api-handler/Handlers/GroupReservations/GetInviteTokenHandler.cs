using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Helpers;
using APIHandler.Models.GuestReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using System.Threading.Tasks;

namespace APIHandler.Handlers.GroupReservations;

// Owner metadata remains readable when the public invite is full or closed.
public class GetInviteTokenHandler(
    IReservationRepository reservations, IGroupReservationRepository groups,
    ITableRepository tables, ILocationService locations, IUserService users) : IApiRequestHandler
{
    public async Task<APIGatewayProxyResponse> HandleRequest(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var caller = UserContextHelper.GetAuthenticatedUserId(request);
        if (caller is null)
            return ResponseBuilder.CreateErrorResponse(401, "Authentication required.", context.Logger);
        if (!request.TryGetPathValue("reservationId", out string? reservationId))
            return ResponseBuilder.CreateErrorResponse(400, "Reservation ID is required.", context.Logger);
        var reservation = await reservations.GetReservationByIdAsync(reservationId);
        if (reservation is null)
            return ResponseBuilder.CreateErrorResponse(404, "Reservation not found.", context.Logger);
        if (reservation.CustomerId != caller)
            return ResponseBuilder.CreateErrorResponse(403, "Only the reservation owner can access the invite.", context.Logger);
        var group = await groups.GetByReservationIdAsync(reservationId);
        if (group is null)
            return ResponseBuilder.CreateErrorResponse(404, "Group reservation not found.", context.Logger);
        var table = await tables.GetTableAsync(reservation.TableId);
        if (table is null)
            return ResponseBuilder.CreateErrorResponse(404, "Table not found.", context.Logger);
        var location = await locations.GetLocationByIdAsync(table.LocationId);
        var invite = new GetGroupReservationResponse(group.GroupReservationId, table.LocationId,
            "Green & Tasty", location?.Address ?? "Unknown Address", reservation.StartTime,
            reservation.EndTime, reservation.GuestCount, table.TableNumber,
            await users.GetUserName(caller) ?? "Owner");
        return ResponseBuilder.CreateSuccessResponse(new { token = group.ReservationToken, invite });
    }
}
