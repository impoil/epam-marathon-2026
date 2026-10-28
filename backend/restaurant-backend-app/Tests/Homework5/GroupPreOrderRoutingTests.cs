using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Amazon.S3;
using APIHandler.Handlers;
using APIHandler.Handlers.GroupReservations;
using APIHandler.Handlers.GuestSessions;
using APIHandler.Handlers.Reservations;
using APIHandler.Routing;
using APIHandler.Services;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.GuestSessions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;

namespace APIHandler.Tests.Homework5;

public class GroupPreOrderRoutingTests
{
    [Fact]
    public void AllFourRoutesResolveThroughRegistryAndMatchCognitoGatewayConfiguration()
    {
        TestEnvironment.Initialize();
        var services = new ServiceCollection();
        var assembly = typeof(IApiRequestHandler).Assembly;
        foreach (var type in assembly.GetTypes().Where(t => t.IsInterface &&
                     (t.Namespace?.StartsWith("APIHandler.Services") == true || t.Namespace?.StartsWith("APIHandler.Repositories") == true)))
        {
            var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type))!;
            services.AddSingleton(type, mock.Object);
        }
        services.AddSingleton(Mock.Of<IDynamoDBContext>());
        services.AddSingleton(new S3ImageService(Mock.Of<IAmazonS3>()));
        using var cognito = new AmazonCognitoIdentityProviderClient(new AnonymousAWSCredentials(), RegionEndpoint.EUNorth1);
        services.AddSingleton(cognito);
        // Resolve the real changed services through DI while keeping every AWS repository mocked.
        services.AddSingleton<IGuestSessionService, GuestSessionService>();
        services.AddSingleton<IGroupReservationService, GroupReservationService>();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        var routes = RouteRegistry.BuildRoutes(provider);
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestEnvironment.BackendRoot, "deployment_resources.json")));
        var gateway = config.RootElement.EnumerateObject().Single(p =>
            p.Value.GetProperty("resource_type").GetString() == "api_gateway").Value.GetProperty("resources");
        var expected = new (string Method, string Template, string Path, Type Handler)[]
        {
            ("GET", "/guest/groups/{groupReservationId}/sessions", "/guest/groups/g/sessions", typeof(GetAllGuestSessionsHandler)),
            ("GET", "/reservations/{reservationId}/guest-session-id", "/reservations/r/guest-session-id", typeof(GetOwnerSessionIdHandler)),
            ("POST", "/group-reservations/{groupReservationId}/submit", "/group-reservations/g/submit", typeof(SubmitGroupReservationHandler)),
            ("DELETE", "/guest/groups/{groupReservationId}/sessions/{guestSessionId}", "/guest/groups/g/sessions/s", typeof(DeleteGuestSessionHandler))
        };
        foreach (var e in expected)
        {
            var matching = Assert.Single(routes.Where(r => r.Route.Method == e.Method && r.Match(e.Path) != null));
            Assert.Equal(e.Template, matching.Route.Template);
            Assert.IsType(e.Handler, matching.Handler);
            var parameters = matching.Match(e.Path)!;
            Assert.True(parameters.ContainsKey(e.Template.Contains("groupReservationId") ? "groupReservationId" : "reservationId"));
            var operation = gateway.GetProperty(e.Template).GetProperty(e.Method);
            Assert.Equal("cognito_authorizer", operation.GetProperty("authorization_type").GetString());
            Assert.True(operation.GetProperty("enable_proxy").GetBoolean());
            Assert.Equal("api-handler", operation.GetProperty("lambda_name").GetString());
        }
        Assert.False(gateway.TryGetProperty("/guest/group/{groupReservationId}/sessions", out _));
    }
}
