using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace Shekinah.Api.FunctionalTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact(DisplayName = "GET_health_live_es_anonimo_y_responde_200")]
    public async Task Health_live_es_publico()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET_users_sin_token_responde_401")]
    public async Task Endpoint_protegido_sin_token_responde_401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/users", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }
}
