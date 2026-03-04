// =============================================================================
// v3 & v4 Integration Tests — full endpoint pipeline via WebApplicationFactory
//
// WebApplicationFactory<Program> spins up the real ASP.NET host in-memory
// (no listening socket, no network), giving us:
//   - Real middleware pipeline
//   - Real DI container
//   - Real routing
//
// We replace only the outbound HttpMessageHandler so our tests never touch
// the real TargetApi. Everything else is production code.
//
// Why test at this level as well as unit level?
//   - Catches wiring bugs: wrong registration, wrong base address, wrong path.
//   - Validates the JSON shape the endpoint returns, not just the service logic.
//   - Gives confidence that the DI configuration actually works end-to-end.
// =============================================================================

namespace WebApi.Tests;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // -------------------------------------------------------------------------
    // Helper: create a factory variant with a stubbed outbound handler.
    //
    // ConfigureTestServices runs AFTER the real Program.cs service registrations,
    // so we can override just the handler — base address, timeout, etc. all come
    // from the real registration.
    // -------------------------------------------------------------------------
    private HttpClient BuildTestHttpClient(FakeHttpMessageHandler fakeHandler)
    {
        return _factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services =>
            {
                // Override the named client's primary handler (used by v3).
                services.AddHttpClient(HttpClientNames.TargetApi)
                    .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

                // Override the typed client's primary handler (used by v4).
                services.AddHttpClient<PingClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
            });
        }).CreateClient();
    }

    // =========================================================================
    // v3 — Named client via IHttpClientFactory
    // =========================================================================

    [Fact]
    public async Task V3_WhenDownstreamReturnsPong_EndpointReturnsOkWithExpectedBody()
    {
        var fakeHandler = new FakeHttpMessageHandler("pong");
        var client = BuildTestHttpClient(fakeHandler);

        var response = await client.GetAsync("/api/v3");

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("v3", body);
        Assert.Contains("pong", body);
    }

    [Fact]
    public async Task V3_WhenDownstreamFails_EndpointReturns500()
    {
        // The endpoint itself doesn't catch HttpRequestException, so an error
        // from the downstream propagates as a 500 from our API.
        var fakeHandler = new FakeHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var client = BuildTestHttpClient(fakeHandler);

        var response = await client.GetAsync("/api/v3");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // =========================================================================
    // v4 — Typed client (PingClient)
    // =========================================================================

    [Fact]
    public async Task V4_WhenDownstreamReturnsPong_EndpointReturnsOkWithExpectedBody()
    {
        var fakeHandler = new FakeHttpMessageHandler("pong");
        var client = BuildTestHttpClient(fakeHandler);

        var response = await client.GetAsync("/api/v4");

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("v4", body);
        Assert.Contains("pong", body);
    }

    [Fact]
    public async Task V4_WhenDownstreamFails_EndpointReturns500()
    {
        var fakeHandler = new FakeHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var client = BuildTestHttpClient(fakeHandler);

        var response = await client.GetAsync("/api/v4");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // =========================================================================
    // Cross-cutting: both endpoints call the same fake — proves both routes
    // reach the downstream and neither is hard-coding a raw URL at the endpoint
    // level (the handler capture confirms the path used).
    // =========================================================================

    [Theory]
    [InlineData("/api/v3")]
    [InlineData("/api/v4")]
    public async Task BothVersions_CallDownstreamPingPath(string endpoint)
    {
        var fakeHandler = new FakeHttpMessageHandler("pong");
        var client = BuildTestHttpClient(fakeHandler);

        await client.GetAsync(endpoint);

        var request = Assert.Single(fakeHandler.Requests);
        Assert.Equal("/ping", request.RequestUri!.AbsolutePath);
    }
}
