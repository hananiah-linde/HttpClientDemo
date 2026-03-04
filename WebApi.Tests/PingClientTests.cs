// =============================================================================
// PingClient Unit Tests — testing the concrete HTTP implementation
//
// These tests sit at the HTTP boundary: they verify that PingClient talks to
// the right URL and correctly maps the response. FakeHttpMessageHandler is
// appropriate here because the thing under test IS the HTTP interaction.
//
// These tests are NOT about the endpoint — they're about PingClient itself.
// =============================================================================

namespace WebApi.Tests;

public class PingClientTests
{
    // -------------------------------------------------------------------------
    // Helper: build a PingClient wired to a fake handler, no DI required.
    // -------------------------------------------------------------------------
    private static (PingClient client, FakeHttpMessageHandler handler) BuildClient(
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var handler = new FakeHttpMessageHandler(responseBody, statusCode);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fake-target/")
        };

        return (new PingClient(httpClient), handler);
    }

    [Fact]
    public async Task PingAsync_WhenDownstreamReturnsOk_ReturnsPong()
    {
        var (client, _) = BuildClient("pong");

        var result = await client.PingAsync();

        Assert.Equal("pong", result);
    }

    // Verifies PingClient owns the path — a regression guard if "/ping" ever
    // gets renamed. This test belongs here, NOT in endpoint tests, because it
    // is testing PingClient's internal HTTP behaviour.
    [Fact]
    public async Task PingAsync_CallsCorrectPath()
    {
        var (client, handler) = BuildClient("pong");

        await client.PingAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/ping", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task PingAsync_WhenDownstreamReturnsError_ThrowsHttpRequestException()
    {
        var (client, _) = BuildClient("Internal Server Error", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.PingAsync());
    }
}
