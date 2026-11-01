using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Constants;
using APIHandler.Entities;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.Reservations;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.Locations;
using APIHandler.Services.Orders;
using APIHandler.Services.Reservations;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Homework5;

public class OwnerAccessDeploymentTests
{
    private static APIGatewayProxyRequest Request(string caller) => new()
    {
        PathParameters = new Dictionary<string, string> { ["reservationId"] = "reservation" },
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext { Authorizer = new APIGatewayCustomAuthorizerContext
            { Claims = new Dictionary<string, string> { ["sub"] = caller } } }
    };
    private static ILambdaContext Context()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(value => value.Logger).Returns(Mock.Of<ILambdaLogger>());
        return context.Object;
    }

    [Fact]
    public async Task ForeignCustomerCannotReadReservation()
    {
        var reservations = new Mock<IReservationService>();
        reservations.Setup(value => value.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation
            { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table" });
        var handler = new GetReservationByIdHandler(Mock.Of<IOrderService>(), Mock.Of<ILocationService>(), reservations.Object);
        var response = await handler.HandleRequest(Request("foreign"), Context());
        Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task ForeignCustomerCannotRetrieveInviteOrQueryGroup()
    {
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(value => value.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation
            { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table" });
        var groups = new Mock<IGroupReservationRepository>(MockBehavior.Strict);
        var handler = new GetInviteTokenHandler(reservations.Object, groups.Object,
            Mock.Of<ITableRepository>(), Mock.Of<ILocationService>(), Mock.Of<IUserService>());
        var response = await handler.HandleRequest(Request("foreign"), Context());
        Assert.Equal(403, response.StatusCode);
        groups.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(GroupReservationStatus.Open)]
    [InlineData(GroupReservationStatus.Submitted)]
    [InlineData(GroupReservationStatus.Closed)]
    public async Task OwnerCanReadReviewMetadataForEveryGroupStatus(GroupReservationStatus status)
    {
        var reservations = new Mock<IReservationRepository>();
        reservations.Setup(value => value.GetReservationByIdAsync("reservation")).ReturnsAsync(new Reservation
            { Id = "reservation", CustomerId = "owner", WaiterId = "waiter", TableId = "table", GuestCount = 3 });
        var groups = new Mock<IGroupReservationRepository>();
        groups.Setup(value => value.GetByReservationIdAsync("reservation")).ReturnsAsync(new GroupReservation
            { GroupReservationId = "group", ReservationId = "reservation", ReservationToken = "private-test-invite", Status = status });
        var tables = new Mock<ITableRepository>();
        tables.Setup(value => value.GetTableAsync("table")).ReturnsAsync(new Table { Id = "table", LocationId = "location", TableNumber = 1 });
        var handler = new GetInviteTokenHandler(reservations.Object, groups.Object,
            tables.Object, Mock.Of<ILocationService>(), Mock.Of<IUserService>());
        var response = await handler.HandleRequest(Request("owner"), Context());
        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal("group", json.RootElement.GetProperty("invite").GetProperty("GroupReservationId").GetString());
        Assert.Equal("private-test-invite", json.RootElement.GetProperty("token").GetString());
    }
}
