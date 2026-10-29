using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using APIHandler.Routing;
using Moq;
using Xunit;

namespace Deployment.Tests;

public class StartupTests
{
    private static readonly string Root = FindRoot();
    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "routes.json"))) directory = directory.Parent;
        return directory!.FullName;
    }

    static StartupTests()
    {
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root,
            "../../backend/restaurant-backend-app/dnapp/lambdas/api-handler/lambda_config.json")));
        foreach (var variable in config.RootElement.GetProperty("env_variables").EnumerateObject())
            Environment.SetEnvironmentVariable(variable.Name, "hw6-test-" + variable.Name);
        Environment.SetEnvironmentVariable("REGION", "eu-north-1");
        Environment.SetEnvironmentVariable("AWS_EC2_METADATA_DISABLED", "true");
    }

    [Fact]
    public void RealCompositionRootExactlyMatchesGatewayAllowlist()
    {
        var manifest = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(Path.Combine(Root, "routes.json")))!;
        var routes = RouteRegistry.GetAllRoutes().Select(route => route.Route.Method + " " + route.Route.Template).Order().ToArray();
        var expected = manifest.SelectMany(path => path.Value.Keys.Select(method => method + " " + path.Key)).Order().ToArray();
        Assert.Equal(expected, routes);
    }

    [Theory]
    [InlineData("POST", "/waiters")]
    [InlineData("POST", "/register")]
    [InlineData("GET", "/reports")]
    [InlineData("POST", "/locations")]
    public async Task ManagementRoutesAreAbsentFromTheActualPackage(string method, string path)
    {
        var context = new Mock<ILambdaContext>();
        context.SetupGet(value => value.Logger).Returns(Mock.Of<ILambdaLogger>());
        var response = await new APIHandler.Function().FunctionHandler(new APIGatewayProxyRequest
            { HttpMethod = method, Path = path }, context.Object);
        Assert.Equal(404, response.StatusCode);
    }
}
