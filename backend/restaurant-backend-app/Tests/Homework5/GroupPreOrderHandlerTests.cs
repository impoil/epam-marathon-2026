using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Exceptions;
using APIHandler.Handlers;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Handlers.Reservations;
using APIHandler.Models.GroupReservation;
using APIHandler.Models.GuestSessions;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Homework5;

public class GroupPreOrderHandlerTests
{
    private readonly Mock<IGuestSessionService> guests = new(MockBehavior.Strict);
    private readonly Mock<IGroupReservationService> groups = new(MockBehavior.Strict);
    private readonly ILambdaContext context;
    private readonly GetAllSessionsResponse snapshot = new(
        [new Participant("s", ParticipantType.Guest, "Andrii",
            [new ParticipantItem(new ParticipantDish("d1", "Salad", 180), 2)], 360, true)], 360);

    public GroupPreOrderHandlerTests()
    {
        var mockContext = new Mock<ILambdaContext>();
        mockContext.SetupGet(c => c.Logger).Returns(Mock.Of<ILambdaLogger>());
        context = mockContext.Object;
        guests.Setup(s => s.GetAllSessionsAsync("owner", "g")).ReturnsAsync(snapshot);
        guests.Setup(s => s.GetOwnerSessionIdAsync("owner", "r")).ReturnsAsync(new OwnerSessionResponse("own"));
        guests.Setup(s => s.DeleteGuestSessionAsync("owner", "g", "s")).ReturnsAsync(snapshot);
        groups.Setup(s => s.SubmitPreOrderAsync("owner", "g", "r"))
            .ReturnsAsync([new SubmittedDishResponse("d1", 3)]);
    }

    private IApiRequestHandler Handler(int operation) => operation switch
    {
        0 => new GetAllGuestSessionsHandler(guests.Object),
        1 => new GetOwnerSessionIdHandler(guests.Object),
        2 => new SubmitGroupReservationHandler(groups.Object),
        _ => new DeleteGuestSessionHandler(guests.Object)
    };

    private static APIGatewayProxyRequest Request() => new()
    {
        PathParameters = new Dictionary<string, string>
        {
            ["groupReservationId"] = "g", ["reservationId"] = "r", ["guestSessionId"] = "s"
        },
        Body = "{\"reservationId\":\"r\",\"userId\":\"attacker\"}",
        RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
        {
            Authorizer = new APIGatewayCustomAuthorizerContext
            {
                Claims = new Dictionary<string, string> { ["sub"] = "owner" }
            }
        }
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Success_UsesCognitoSubjectAndSerializesExactHomeworkContract(int operation)
    {
        var response = await Handler(operation).HandleRequest(Request(), context);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json", response.Headers["Content-Type"]);
        using var json = JsonDocument.Parse(response.Body);
        var body = json.RootElement;
        if (operation is 0 or 3)
        {
            Assert.Equal(new[] { "participants", "total" }, body.EnumerateObject().Select(p => p.Name));
            var participant = body.GetProperty("participants")[0];
            Assert.Equal(new[] { "id", "type", "name", "items", "total", "isSubmitted" },
                participant.EnumerateObject().Select(p => p.Name));
            Assert.Equal("Guest", participant.GetProperty("type").GetString());
            Assert.True(participant.GetProperty("isSubmitted").GetBoolean());
            var item = participant.GetProperty("items")[0];
            Assert.Equal(new[] { "dish", "quantity" }, item.EnumerateObject().Select(p => p.Name));
            Assert.Equal(new[] { "id", "name", "price" }, item.GetProperty("dish").EnumerateObject().Select(p => p.Name));
            Assert.Equal(360, body.GetProperty("total").GetDecimal());
        }
        else if (operation == 1)
            Assert.Equal("{\"guestSessionId\":\"own\"}", response.Body);
        else
            Assert.Equal("[{\"dishId\":\"d1\",\"quantity\":3}]", response.Body);
    }

    [Theory]
    [InlineData(0, "missing")]
    [InlineData(1, "missing")]
    [InlineData(2, "missing")]
    [InlineData(3, "missing")]
    [InlineData(0, "blank")]
    [InlineData(1, "blank")]
    [InlineData(2, "blank")]
    [InlineData(3, "blank")]
    [InlineData(0, "null-context")]
    [InlineData(1, "null-context")]
    [InlineData(2, "null-context")]
    [InlineData(3, "null-context")]
    public async Task NoTrustedSubject_Returns401EvenWithForgedBodyAndAuthorizationHeader(int operation, string scenario)
    {
        var request = Request();
        request.Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer forged-token" };
        if (scenario == "missing") request.RequestContext.Authorizer.Claims.Clear();
        if (scenario == "blank") request.RequestContext.Authorizer.Claims["sub"] = " ";
        if (scenario == "null-context") request.RequestContext = null;
        var response = await Handler(operation).HandleRequest(request, context);
        Assert.Equal(401, response.StatusCode);
        guests.VerifyNoOtherCalls();
        groups.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, "groupReservationId")]
    [InlineData(1, "reservationId")]
    [InlineData(2, "groupReservationId")]
    [InlineData(3, "groupReservationId")]
    [InlineData(3, "guestSessionId")]
    public async Task MissingPath_Returns400WithoutServiceCall(int operation, string key)
    {
        var request = Request();
        request.PathParameters.Remove(key);
        var response = await Handler(operation).HandleRequest(request, context);
        Assert.Equal(400, response.StatusCode);
        guests.VerifyNoOtherCalls();
        groups.VerifyNoOtherCalls();
    }

    public static IEnumerable<object[]> ServiceErrors() =>
        from operation in new[] { 0, 1, 2, 3 }
        from error in new[] { "reservation", "group", "session", "forbidden", "validation" }
        select new object[] { operation, error, error == "forbidden" ? 403 : error == "validation" ? 400 : 404 };

    [Theory]
    [MemberData(nameof(ServiceErrors))]
    public async Task ServiceErrors_MapToExpectedHttpStatus(int operation, string error, int expected)
    {
        Exception ex = error switch
        {
            "reservation" => new ReservationNotFoundException("missing"),
            "group" => new GroupReservationNotFoundException(),
            "session" => new GuestSessionNotFoundException("missing"),
            "forbidden" => new ActionForbiddenException("owner only"),
            _ => new ValidationException("invalid")
        };
        guests.Setup(s => s.GetAllSessionsAsync("owner", "g")).ThrowsAsync(ex);
        guests.Setup(s => s.GetOwnerSessionIdAsync("owner", "r")).ThrowsAsync(ex);
        guests.Setup(s => s.DeleteGuestSessionAsync("owner", "g", "s")).ThrowsAsync(ex);
        groups.Setup(s => s.SubmitPreOrderAsync("owner", "g", "r")).ThrowsAsync(ex);
        var response = await Handler(operation).HandleRequest(Request(), context);
        Assert.Equal(expected, response.StatusCode);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(JsonValueKind.Object, json.RootElement.ValueKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"reservationId\":\" \"}")]
    [InlineData("{\"reservationId\":42}")]
    [InlineData("[]")]
    public async Task Submit_InvalidBody_Returns400WithoutWrites(string? body)
    {
        var request = Request();
        request.Body = body;
        var response = await Handler(2).HandleRequest(request, context);
        Assert.Equal(400, response.StatusCode);
        groups.VerifyNoOtherCalls();
    }
}
