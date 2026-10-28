using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.Auth;
using APIHandler.Repositories.Dishes;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using APIHandler.Services.Locations;
using Moq;

namespace APIHandler.Tests.Homework5;

public class GroupPreOrderServiceTests
{
    private readonly Mock<IGroupReservationRepository> groups = new();
    private readonly Mock<IGuestSessionRepository> sessions = new();
    private readonly Mock<IReservationRepository> reservations = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IDishRepository> dishes = new();
    private readonly GuestSessionService guests;
    private readonly GroupReservationService service;
    private readonly Reservation reservation = new()
    {
        Id = "r", CustomerId = "owner", TableId = "table", WaiterId = "waiter",
        Status = ReservationStatus.Reserved, GuestCount = 5
    };
    private readonly GroupReservation group = new()
    {
        GroupReservationId = "g", ReservationId = "r", ReservationToken = "invite", Status = GroupReservationStatus.Open
    };
    private readonly Order order = new()
    {
        OrderId = "o", CustomerId = "owner", ReservationId = "r", ReservationAddress = "address",
        ReservationTime = "time", Dishes = [new OrderItem { DishId = "old", Quantity = 9 }], Status = OrderStatus.Created
    };
    private readonly List<GuestSession> participants = [];

    public GroupPreOrderServiceTests()
    {
        groups.Setup(r => r.GetGroupReservationByIdAsync("g")).ReturnsAsync(() => group);
        groups.Setup(r => r.GetByReservationIdAsync("r")).ReturnsAsync(() => group);
        reservations.Setup(r => r.GetReservationByIdAsync("r")).ReturnsAsync(() => reservation);
        sessions.Setup(r => r.GetAllByGroupReservationIdAsync("g")).ReturnsAsync(() => participants.ToList());
        sessions.Setup(r => r.GetByGuestSessionId(It.IsAny<string>()))
            .ReturnsAsync((string id) => participants.FirstOrDefault(s => s.Id == id));
        orders.Setup(r => r.GetOrderByReservationIdAsync("r")).ReturnsAsync(() => order);
        dishes.Setup(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>())).ReturnsAsync(
            [new Dish { Id = "d1", Name = "Salad", Price = 180 }, new Dish { Id = "d2", Name = "Soup", Price = 220 }]);
        guests = new(groups.Object, sessions.Object, dishes.Object, reservations.Object,
            Mock.Of<IUserRepository>(), Mock.Of<ITableRepository>());
        service = new(groups.Object, Mock.Of<ITableRepository>(), reservations.Object, sessions.Object,
            Mock.Of<IUserService>(), Mock.Of<ILocationService>(), orders.Object, guests);
    }

    private GuestSession Add(string id, bool submitted = false, bool owner = false, params OrderItem[] items)
    {
        var session = new GuestSession
        {
            Id = id, GroupReservationId = "g", GuestName = owner ? "Owner" : id,
            IsOwnerSession = owner, OwnerUserId = owner ? "owner" : null, IsSubmitted = submitted, Dishes = items.ToList()
        };
        participants.Add(session);
        return session;
    }

    private Task Invoke(string operation, string caller = "owner") => operation switch
    {
        "list" => guests.GetAllSessionsAsync(caller, "g"),
        "owner" => guests.GetOwnerSessionIdAsync(caller, "r"),
        "delete" => guests.DeleteGuestSessionAsync(caller, "g", "guest"),
        _ => service.SubmitPreOrderAsync(caller, "g", "r")
    };

    private void NoWrites()
    {
        groups.Verify(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Never);
        groups.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
        sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
        sessions.Verify(r => r.SaveAsync(It.IsAny<GuestSession>()), Times.Never);
        orders.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        Assert.Equal(GroupReservationStatus.Open, group.Status);
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal("old", Assert.Single(order.Dishes).DishId);
    }

    [Fact]
    public async Task List_MapsOwnerAndGuestsAndIncludesUnsubmittedTotals()
    {
        Add("owner-session", true, true, new OrderItem { DishId = "d1", Quantity = 2 });
        Add("guest", false, false, new OrderItem { DishId = "d2", Quantity = 1 });
        Add("empty");
        var result = await guests.GetAllSessionsAsync("owner", "g");
        var rows = result.Participants.ToArray();
        Assert.Equal(3, rows.Length);
        Assert.Equal("Owner", rows[0].Type.ToString());
        Assert.Equal("Guest", rows[1].Type.ToString());
        Assert.True(rows[0].IsSubmitted);
        Assert.False(rows[1].IsSubmitted);
        Assert.Equal("Salad", Assert.Single(rows[0].Items).Dish.Name);
        Assert.Equal(360, rows[0].Total);
        Assert.Equal(220, rows[1].Total);
        Assert.Empty(rows[2].Items);
        Assert.Equal(580, result.Total);
        NoWrites();
    }

    [Fact]
    public async Task List_EmptyGroup_ReturnsZero()
    {
        var result = await guests.GetAllSessionsAsync("owner", "g");
        Assert.Empty(result.Participants);
        Assert.Equal(0, result.Total);
        dishes.Verify(r => r.GetDishesByIdsAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Theory]
    [InlineData("list")]
    [InlineData("owner")]
    [InlineData("delete")]
    [InlineData("submit")]
    public async Task MissingReservation_ReturnsNotFoundWithoutWrites(string operation)
    {
        reservations.Setup(r => r.GetReservationByIdAsync("r")).ReturnsAsync((Reservation?)null);
        await Assert.ThrowsAsync<ReservationNotFoundException>(() => Invoke(operation));
        NoWrites();
    }

    [Theory]
    [InlineData("list")]
    [InlineData("owner")]
    [InlineData("delete")]
    [InlineData("submit")]
    public async Task MissingGroup_ReturnsNotFoundWithoutWrites(string operation)
    {
        groups.Setup(r => r.GetGroupReservationByIdAsync("g")).ReturnsAsync((GroupReservation?)null);
        groups.Setup(r => r.GetByReservationIdAsync("r")).ReturnsAsync((GroupReservation?)null);
        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => Invoke(operation));
        NoWrites();
    }

    [Theory]
    [InlineData("list", "other")]
    [InlineData("owner", "other")]
    [InlineData("delete", "other")]
    [InlineData("submit", "other")]
    [InlineData("list", "")]
    [InlineData("owner", "")]
    [InlineData("delete", "")]
    [InlineData("submit", "")]
    public async Task NonOwner_IsForbiddenWithoutReadingSessionsOrWriting(string operation, string caller)
    {
        await Assert.ThrowsAsync<ActionForbiddenException>(() => Invoke(operation, caller));
        sessions.Verify(r => r.GetAllByGroupReservationIdAsync(It.IsAny<string>()), Times.Never);
        NoWrites();
    }

    [Fact]
    public async Task OwnerId_ReturnsOnlyCallersOwnerSession()
    {
        Add("guest");
        Add("foreign-owner", owner: true).OwnerUserId = "other";
        Add("own", owner: true);
        var result = await guests.GetOwnerSessionIdAsync("owner", "r");
        Assert.Equal("own", result.GuestSessionId);
        NoWrites();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnerId_MissingOrForeignOwnerSession_IsNotFound(bool foreign)
    {
        if (foreign) Add("foreign", owner: true).OwnerUserId = "other";
        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => Invoke("owner"));
        NoWrites();
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("submit")]
    public async Task ReservationGroupMismatch_IsBadRequest(string operation)
    {
        group.ReservationId = "other";
        await Assert.ThrowsAsync<ValidationException>(() => Invoke(operation));
        NoWrites();
    }

    [Fact]
    public async Task Submit_AggregatesSubmittedSessionsAndAtomicallyReplacesOrder()
    {
        Add("one", true, false, new OrderItem { DishId = "d1", Quantity = 2 }, new OrderItem { DishId = "d2", Quantity = 2 });
        var owner = Add("own", true, true, new OrderItem { DishId = "d1", Quantity = 1 });
        Add("draft", false, false, new OrderItem { DishId = "d1", Quantity = 100 });
        Add("foreign", true, false, new OrderItem { DishId = "d1", Quantity = 200 }).GroupReservationId = "other";
        Order? savedOrder = null;
        GroupReservation? savedGroup = null;
        groups.Setup(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()))
            .Callback<GroupReservation, Order>((g, o) => { savedGroup = g; savedOrder = o; }).Returns(Task.CompletedTask);
        var result = await service.SubmitPreOrderAsync("owner", "g", "r");
        Assert.Equal(new[] { new SubmittedDishResponse("d1", 3), new SubmittedDishResponse("d2", 2) }, result);
        Assert.Equal(OrderStatus.SubmittedByCustomer, savedOrder!.Status);
        Assert.Equal(GroupReservationStatus.Submitted, savedGroup!.Status);
        Assert.Equal(2, savedOrder.Dishes.Count);
        Assert.Equal(order.ReservationAddress, savedOrder.ReservationAddress);
        Assert.Equal(order.CustomerId, savedOrder.CustomerId);
        Assert.Equal("old", Assert.Single(order.Dishes).DishId);
        Assert.Equal(1, Assert.Single(owner.Dishes).Quantity);
        orders.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        groups.Verify(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Once);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("draft")]
    [InlineData("empty")]
    [InlineData("null-dishes")]
    [InlineData("quantity")]
    [InlineData("dish-id")]
    [InlineData("missing-order")]
    public async Task Submit_InvalidContentOrMissingOrder_IsBadRequestWithoutWrites(string scenario)
    {
        if (scenario != "none")
        {
            var session = Add("guest", scenario != "draft", false, new OrderItem { DishId = "d1", Quantity = 1 });
            if (scenario == "empty") session.Dishes.Clear();
            if (scenario == "null-dishes") session.Dishes = null!;
            if (scenario == "quantity") session.Dishes[0].Quantity = 0;
            if (scenario == "dish-id") session.Dishes[0].DishId = " ";
        }
        if (scenario == "missing-order") orders.Setup(r => r.GetOrderByReservationIdAsync("r")).ReturnsAsync((Order?)null);
        await Assert.ThrowsAsync<ValidationException>(() => Invoke("submit"));
        NoWrites();
    }

    [Theory]
    [InlineData(OrderStatus.Submitted)]
    [InlineData(OrderStatus.SubmittedByCustomer)]
    [InlineData(OrderStatus.Cancelled)]
    public async Task Submit_NonCreatedOrder_IsBadRequest(OrderStatus status)
    {
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = 1 });
        order.Status = status;
        await Assert.ThrowsAsync<ValidationException>(() => Invoke("submit"));
        groups.Verify(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Never);
        Assert.Equal(status, order.Status);
        Assert.Equal("old", Assert.Single(order.Dishes).DishId);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("reservation")]
    public async Task Submit_ForeignOrder_IsForbidden(string field)
    {
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = 1 });
        if (field == "customer") order.CustomerId = "other";
        else order.ReservationId = "other";
        await Assert.ThrowsAsync<ActionForbiddenException>(() => Invoke("submit"));
        NoWrites();
    }

    [Theory]
    [InlineData("submit", GroupReservationStatus.Submitted)]
    [InlineData("submit", GroupReservationStatus.Closed)]
    [InlineData("delete", GroupReservationStatus.Submitted)]
    [InlineData("delete", GroupReservationStatus.Closed)]
    public async Task ClosedOrSubmittedGroup_RejectsFurtherChanges(string operation, GroupReservationStatus status)
    {
        group.Status = status;
        await Assert.ThrowsAnyAsync<ValidationException>(() => Invoke(operation));
        sessions.Verify(r => r.DeleteAsync(It.IsAny<GuestSession>()), Times.Never);
        groups.Verify(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Never);
        Assert.Equal(status, group.Status);
    }

    [Theory]
    [InlineData(ReservationStatus.Cancelled)]
    [InlineData(ReservationStatus.Completed)]
    [InlineData(ReservationStatus.InProgress)]
    public async Task Submit_InactiveReservation_IsBadRequest(ReservationStatus status)
    {
        reservation.Status = status;
        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => Invoke("submit"));
        NoWrites();
    }

    [Fact]
    public async Task Submit_TransactionFailure_DoesNotMutateLoadedEntities()
    {
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = 1 });
        groups.Setup(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("storage unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke("submit"));
        Assert.Equal(OrderStatus.Created, order.Status);
        Assert.Equal(GroupReservationStatus.Open, group.Status);
        Assert.Equal("old", Assert.Single(order.Dishes).DishId);
    }

    [Fact]
    public async Task Submit_RepeatRejectedWithoutSecondWrite()
    {
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = 1 });
        groups.Setup(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()))
            .Callback(() => group.Status = GroupReservationStatus.Submitted).Returns(Task.CompletedTask);
        await Invoke("submit");
        await Assert.ThrowsAsync<GroupReservationSubmittedException>(() => Invoke("submit"));
        groups.Verify(r => r.SubmitPreOrderAsync(It.IsAny<GroupReservation>(), It.IsAny<Order>()), Times.Once);
    }

    [Fact]
    public async Task Delete_ReturnsRecalculatedSnapshotEvenIfGroupIndexIsStale_AndRepeatIs404()
    {
        Add("own", true, true, new OrderItem { DishId = "d2", Quantity = 1 });
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = 2 });
        var deleted = false;
        sessions.Setup(r => r.GetByGuestSessionId("guest")).ReturnsAsync(() => deleted ? null : participants[1]);
        sessions.Setup(r => r.DeleteAsync(It.IsAny<GuestSession>())).Callback(() => deleted = true).Returns(Task.CompletedTask);
        var result = await guests.DeleteGuestSessionAsync("owner", "g", "guest");
        Assert.Equal("own", Assert.Single(result.Participants).Id);
        Assert.Equal(220, result.Total);
        await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => Invoke("delete"));
        sessions.Verify(r => r.DeleteAsync(It.Is<GuestSession>(s => s.Id == "guest")), Times.Once);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("owner")]
    [InlineData("missing-dish")]
    public async Task Delete_InvalidTargetOrSnapshot_DoesNotDelete(string scenario)
    {
        if (scenario != "missing")
        {
            var target = Add("guest", owner: scenario == "owner");
            if (scenario == "foreign") target.GroupReservationId = "other";
            if (scenario == "missing-dish") Add("remaining", false, false, new OrderItem { DishId = "missing", Quantity = 1 });
        }
        if (scenario == "missing") await Assert.ThrowsAsync<GuestSessionNotFoundException>(() => Invoke("delete"));
        else await Assert.ThrowsAsync<ValidationException>(() => Invoke("delete"));
        NoWrites();
    }

    [Fact]
    public async Task CreateGroup_CreatesOwnerSessionInExistingLifecycle()
    {
        var created = await service.CreateGroupReservationAsync("r");
        sessions.Verify(r => r.SaveAsync(It.Is<GuestSession>(s => s.IsOwnerSession && s.OwnerUserId == "owner"
            && s.GroupReservationId == created.GroupReservationId && s.Dishes.Count == 0 && !s.IsSubmitted)), Times.Once);
    }

    [Fact]
    public async Task Status_DoesNotCountPersistedOwnerSessionTwice()
    {
        Add("own", owner: true);
        Add("guest");
        var result = await guests.GetGroupReservationStatusAsync("g");
        Assert.Equal(2, result.CurrentParticipants);
    }

    [Fact]
    public async Task Submit_QuantityOverflow_IsBadRequestWithoutWrites()
    {
        Add("guest", true, false, new OrderItem { DishId = "d1", Quantity = int.MaxValue },
            new OrderItem { DishId = "d1", Quantity = 1 });
        await Assert.ThrowsAsync<ValidationException>(() => Invoke("submit"));
        NoWrites();
    }

    [Fact]
    public async Task Delete_RepositoryFailure_DoesNotChangeParticipants()
    {
        Add("guest");
        Add("other");
        sessions.Setup(r => r.DeleteAsync(It.IsAny<GuestSession>())).ThrowsAsync(new InvalidOperationException("storage unavailable"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Invoke("delete"));
        Assert.Equal(2, participants.Count);
        Assert.Equal(GroupReservationStatus.Open, group.Status);
    }
}
