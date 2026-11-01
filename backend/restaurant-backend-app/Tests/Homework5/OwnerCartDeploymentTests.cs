using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Constants.Dishes;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Services.Dishes;
using APIHandler.Services.Notifications;
using APIHandler.Services.Orders;
using Moq;

namespace APIHandler.Tests.Homework5;

public class OwnerCartDeploymentTests
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IReservationRepository> reservations = new();
    private readonly Mock<IGroupReservationRepository> groups = new();
    private readonly Mock<IGuestSessionRepository> sessions = new();
    private readonly Mock<IDishService> dishes = new();
    private readonly Order order = new() { OrderId = "order", CustomerId = "owner", ReservationId = "reservation",
        ReservationAddress = "demo", ReservationTime = "demo", Status = OrderStatus.Created,
        Dishes = [new OrderItem { DishId = "dish", Quantity = 1 }] };
    private readonly GuestSession session = new() { Id = "owner-session", GuestName = "Owner",
        GroupReservationId = "group", IsOwnerSession = true, OwnerUserId = "owner", Dishes = [] };
    private readonly GroupReservation group = new() { GroupReservationId = "group", ReservationId = "reservation",
        ReservationToken = "test-invite", Status = GroupReservationStatus.Open };
    private OrderService Service => new(orders.Object, reservations.Object, dishes.Object,
        Mock.Of<INotificationService>(), groups.Object, sessions.Object);
    private static ILambdaContext Context()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(value => value.Logger).Returns(Mock.Of<ILambdaLogger>());
        return context.Object;
    }
    public OwnerCartDeploymentTests()
    {
        reservations.Setup(value => value.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation
            { Id = "reservation", TableId = "table", CustomerId = "owner", WaiterId = "waiter",
              GuestCount = 3, Status = ReservationStatus.Reserved, StartTime = DateTime.UtcNow.AddDays(1) });
        orders.Setup(value => value.GetOrderByReservationIdAsync("reservation")).ReturnsAsync(order);
        groups.Setup(value => value.GetByReservationIdAsync("reservation")).ReturnsAsync(group);
        sessions.Setup(value => value.GetAllByGroupReservationIdAsync("group")).ReturnsAsync([session]);
    }

    [Fact]
    public async Task GroupOwnerSubmitConfirmsParticipantCartAndLeavesRealOrderCreated()
    {
        await Service.SubmitPreOrderAsync("owner", "reservation", Context());
        Assert.True(session.IsSubmitted);
        Assert.Equal("dish", Assert.Single(session.Dishes).DishId);
        Assert.Equal(OrderStatus.Created, order.Status);
        orders.Verify(value => value.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        sessions.Verify(value => value.SaveAsync(session), Times.Once);
    }

    [Fact]
    public async Task AddingOrderCopiesDraftCartToOwnerSession()
    {
        orders.Setup(value => value.GetOrderByReservationIdAsync("reservation")).ReturnsAsync((Order?)null);
        await Service.AddOrderAsync(order);
        Assert.False(session.IsSubmitted);
        Assert.Equal(1, Assert.Single(session.Dishes).Quantity);
        Assert.NotSame(order.Dishes[0], session.Dishes[0]);
    }

    [Fact]
    public async Task EditingCartInvalidatesPreviousParticipantConfirmation()
    {
        session.IsSubmitted = true;
        dishes.Setup(value => value.GetDishByIdAsync("dish")).ReturnsAsync(new Dish
            { Id = "dish", State = DishState.Available });
        await Service.EditPreOrderAsync("owner", "reservation", [new OrderItem { DishId = "dish", Quantity = 2 }], Context());
        Assert.False(session.IsSubmitted);
        Assert.Equal(2, Assert.Single(session.Dishes).Quantity);
    }

    [Theory]
    [InlineData(GroupReservationStatus.Submitted)]
    [InlineData(GroupReservationStatus.Closed)]
    public async Task ClosedGroupCannotBeReopenedViaLegacyUnsubmit(GroupReservationStatus state)
    {
        group.Status = state;
        await Assert.ThrowsAsync<ValidationException>(() => Service.UnSubmitPreOrderAsync("owner", "reservation", Context()));
        orders.Verify(value => value.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        sessions.Verify(value => value.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }

    [Fact]
    public async Task SingleReservationKeepsExistingSubmitBehavior()
    {
        groups.Setup(value => value.GetByReservationIdAsync("reservation")).ReturnsAsync((GroupReservation?)null);
        await Service.SubmitPreOrderAsync("owner", "reservation", Context());
        Assert.Equal(OrderStatus.SubmittedByCustomer, order.Status);
        sessions.Verify(value => value.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
    }
}
