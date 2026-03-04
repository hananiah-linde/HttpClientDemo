// =============================================================================
// v3 & v4 Endpoint Integration Tests
//
// Both endpoints are tested via WebApplicationFactory<Program>, which runs the
// real ASP.NET pipeline in-memory. We override only the outbound dependency.
//
// KEY CONTRAST — what we must fake differs between v3 and v4:
//
//   v3 endpoint  →  depends on IHttpClientFactory
//                   IHttpClientFactory.CreateClient() returns HttpClient (concrete).
//                   We must still reach inside and swap the HttpMessageHandler.
//                   Tests remain HTTP-aware: they deal with handlers and status codes.
//
//   v4 endpoint  →  depends on IPingClient (our domain interface).
//                   NSubstitute creates a substitute in one line — no hand-rolled
//                   fakes, no HTTP handler, no status codes. The test only cares
//                   about what the endpoint does with the string it receives.
//
// This difference is the main reason to add an interface to your typed client.
// =============================================================================

namespace WebApi.Tests;

public class EndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // =========================================================================
    // v3 — Named client via IHttpClientFactory
    //
    // To stub the downstream we must configure the primary HttpMessageHandler.
    // The test has to know that "pong" is a string body on a 200 response, and
    // that a 500 becomes an exception. HTTP concepts leak into the test.
    // NSubstitute doesn't help here — IHttpClientFactory.CreateClient() returns
    // a concrete HttpClient, so we still need a FakeHttpMessageHandler.
    // =========================================================================

    [Fact]
    public async Task V3_WhenDownstreamReturnsPong_EndpointReturnsOkWithPong()
    {
        // Must create an HTTP-level fake — handler, status code, body.
        var fakeHandler = new FakeHttpMessageHandler("pong");

        var client = _factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services =>
            {
                services.AddHttpClient(HttpClientNames.TargetApi)
                    .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/v3");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("v3", body);
        Assert.Contains("pong", body);
    }

    [Fact]
    public async Task V3_WhenDownstreamFails_EndpointReturns500()
    {
        var fakeHandler = new FakeHttpMessageHandler("error", HttpStatusCode.InternalServerError);

        var client = _factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services =>
            {
                services.AddHttpClient(HttpClientNames.TargetApi)
                    .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/v3");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    // =========================================================================
    // v4 — Typed client behind IPingClient, using NSubstitute
    //
    // Substitute.For<IPingClient>() generates a full substitute at runtime.
    // No hand-rolled FakePingClient or ThrowingPingClient class needed.
    //
    // Extra capability over manual fakes: Received() lets us assert that the
    // endpoint actually called PingAsync() — not just that the response was ok.
    // =========================================================================

    [Fact]
    public async Task V4_WhenDownstreamReturnsPong_EndpointReturnsOkWithPong()
    {
        // One line to create a substitute — NSubstitute generates the implementation.
        var pingClient = Substitute.For<IPingClient>();
        pingClient.PingAsync().Returns("pong");

        var client = _factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services =>
            {
                // Register the substitute directly — no HTTP involved at all.
                services.AddSingleton(pingClient);
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/v4");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("v4", body);
        Assert.Contains("pong", body);
        // Verify the endpoint actually delegated to the client — not just that
        // the response happened to be correct.
        await pingClient.Received(1).PingAsync();
    }

    [Fact]
    public async Task V4_WhenDownstreamFails_EndpointReturns500()
    {
        var pingClient = Substitute.For<IPingClient>();
        // Throws() is from NSubstitute.ExceptionExtensions — clean and readable.
        pingClient.PingAsync().Throws(new InvalidOperationException("Downstream unavailable"));

        var client = _factory.WithWebHostBuilder(host =>
        {
            host.ConfigureTestServices(services =>
            {
                services.AddSingleton(pingClient);
            });
        }).CreateClient();

        var response = await client.GetAsync("/api/v4");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }
}
