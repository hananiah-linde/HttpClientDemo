// =============================================================================
// PingService Unit Tests — mocking a typed HTTP client dependency
//
// This is the everyday pattern: you have a service class (PingService) that
// depends on IPingClient. You want to test PingService's own logic in
// isolation, without any HTTP infrastructure.
//
// Because the endpoint (v4) depends on IPingClient — the interface — rather
// than PingClient or HttpClient directly, NSubstitute can generate a
// substitute in one line. No FakeHttpMessageHandler, no WebApplicationFactory,
// no base address. The test doesn't know HTTP exists.
//
// Compare this to v3: if PingService took IHttpClientFactory instead, the test
// would still have to construct a fake handler and wire up an HttpClient just
// to return a plain string. The interface breaks that coupling.
// =============================================================================

namespace WebApi.Tests;

public class PingServiceTests
{
    [Fact]
    public async Task IsAliveAsync_WhenPingReturnsPong_ReturnsTrue()
    {
        // NSubstitute generates the substitute — no hand-rolled fake class needed.
        var pingClient = Substitute.For<IPingClient>();
        pingClient.PingAsync().Returns("pong");

        var service = new PingService(pingClient);

        var result = await service.IsAliveAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsAliveAsync_WhenPingReturnsUnexpectedValue_ReturnsFalse()
    {
        var pingClient = Substitute.For<IPingClient>();
        // Return something other than "pong" to exercise the false branch.
        pingClient.PingAsync().Returns("unexpected");

        var service = new PingService(pingClient);

        var result = await service.IsAliveAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsAliveAsync_WhenPingThrows_ExceptionPropagates()
    {
        var pingClient = Substitute.For<IPingClient>();
        pingClient.PingAsync().Throws(new HttpRequestException("Network failure"));

        var service = new PingService(pingClient);

        await Assert.ThrowsAsync<HttpRequestException>(() => service.IsAliveAsync());
    }

    [Fact]
    public async Task IsAliveAsync_AlwaysCallsPingExactlyOnce()
    {
        var pingClient = Substitute.For<IPingClient>();
        pingClient.PingAsync().Returns("pong");

        var service = new PingService(pingClient);
        await service.IsAliveAsync();

        // Received() verifies the interaction — useful for catching bugs where
        // the service calls the client 0 times (wrong branch) or multiple times
        // (accidental retry loop).
        await pingClient.Received(1).PingAsync();
    }
}
