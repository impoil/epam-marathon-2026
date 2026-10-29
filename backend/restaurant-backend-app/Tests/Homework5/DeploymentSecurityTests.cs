using Amazon.DynamoDBv2.DataModel;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Entities;
using APIHandler.Helpers;
using APIHandler.Repositories.Locations;
using APIHandler.Services.GuestSessions;
using Moq;

namespace APIHandler.Tests.Homework5;

[CollectionDefinition("Deployment environment", DisableParallelization = true)]
public class DeploymentEnvironmentCollection { }

[Collection("Deployment environment")]
public class DeploymentSecurityTests
{
    [Theory]
    [InlineData("Authorization", "Bearer ")]
    [InlineData("authorization", "bearer ")]
    [InlineData("AUTHORIZATION", "BEARER ")]
    public async Task GuestAuthorizationAcceptsCaseInsensitiveHttpHeaders(string header, string scheme)
    {
        var service = new Mock<IGuestSessionService>();
        service.Setup(value => value.IsAuthorizedAsync("valid-session-token", "session-id")).ReturnsAsync(true);
        var authorization = new GuestAuthorizationService(service.Object);
        var response = await authorization.ValidateAsync(new APIGatewayProxyRequest
        {
            Headers = new Dictionary<string, string> { [header] = scheme + "valid-session-token" }
        }, Mock.Of<ILambdaContext>(), "session-id");
        Assert.Null(response);
        service.VerifyAll();
    }

    [Fact]
    public async Task MissingGuestHeaderReturns401()
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(value => value.Logger).Returns(Mock.Of<ILambdaLogger>());
        var authorization = new GuestAuthorizationService(Mock.Of<IGuestSessionService>());
        var response = await authorization.ValidateAsync(new APIGatewayProxyRequest { Headers = null }, context.Object, "session-id");
        Assert.Equal(401, response!.StatusCode);
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("")]
    [InlineData("===")]
    public async Task MalformedGuestTokenIsRejectedBeforeDatabaseAccess(string token)
    {
        var service = new GuestSessionService(null!, null!, null!, null!, null!, null!);
        Assert.False(await service.IsAuthorizedAsync(token, "session-id"));
    }

    [Fact]
    public void ProductionResponsesUseConfiguredOriginAndNeverCacheTokens()
    {
        var previous = Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGIN");
        try
        {
            Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGIN", "https://deployment.example.test");
            var response = ResponseBuilder.CreateSuccessResponse(new { idToken = "private-test-token" });
            Assert.Equal("https://deployment.example.test", response.Headers["Access-Control-Allow-Origin"]);
            Assert.Equal("no-store", response.Headers["Cache-Control"]);
            var other = ResponseBuilder.CreateSuccessResponse("next request");
            response.Headers["Access-Control-Allow-Origin"] = "https://attacker.example.test";
            Assert.Equal("https://deployment.example.test", other.Headers["Access-Control-Allow-Origin"]);
        }
        finally { Environment.SetEnvironmentVariable("CORS_ALLOWED_ORIGIN", previous); }
    }

    [Fact]
    public async Task LocationsScanUsesNamespacedTable()
    {
        TestEnvironment.Initialize();
        var context = new Mock<IDynamoDBContext>();
        var search = new Mock<AsyncSearch<Location>>();
        search.Setup(value => value.GetRemainingAsync(default)).ReturnsAsync([]);
        context.Setup(value => value.ScanAsync<Location>(It.IsAny<IEnumerable<ScanCondition>>(),
            It.Is<ScanConfig>(config => config.OverrideTableName == "hw5-locationsTable"))).Returns(search.Object);
        await new LocationRepository(context.Object).GetAllLocationsAsync();
        context.VerifyAll();
    }
}
