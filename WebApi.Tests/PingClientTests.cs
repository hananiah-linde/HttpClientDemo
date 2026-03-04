// =============================================================================
// v4 Unit Tests — PingClient (Typed HttpClient)
//
// This is the key testability advantage of typed clients over the named-client
// approach (v3):
//
//   v3  →  endpoint depends on IHttpClientFactory (interface) — you CAN test
//           it, but you must either mock the factory or use WebApplicationFactory.
//
//   v4  →  PingClient depends on HttpClient (concrete class) injected via DI.
//           To unit-test it, just hand it an HttpClient backed by a fake handler.
//           No web host, no factory, no DI container needed at all.
//
// Pure unit tests like these run in microseconds and have zero network I/O.
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

        // Provide a BaseAddress so relative paths ("/ping") resolve correctly.
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://fake-target/")
        };

        return (new PingClient(httpClient), handler);
    }

    // -------------------------------------------------------------------------
    // Happy-path: downstream returns "pong"
    // -------------------------------------------------------------------------
    [Fact]
    public async Task PingAsync_WhenDownstreamReturnsOk_ReturnsPong()
    {
        var (client, _) = BuildClient("pong");

        var result = await client.PingAsync();

        Assert.Equal("pong", result);
    }

    // -------------------------------------------------------------------------
    // Verify the right URL path is called — typed clients own their paths, so
    // a test can catch regressions if someone changes "/ping" to "/health".
    // -------------------------------------------------------------------------
    [Fact]
    public async Task PingAsync_CallsCorrectPath()
    {
        var (client, handler) = BuildClient("pong");

        await client.PingAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/ping", request.RequestUri!.AbsolutePath);
    }

    // -------------------------------------------------------------------------
    // Sad-path: downstream returns a non-success status code.
    // HttpClient.GetStringAsync throws HttpRequestException for 4xx/5xx.
    // The test documents (and locks in) that behaviour for callers.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task PingAsync_WhenDownstreamReturnsError_ThrowsHttpRequestException()
    {
        var (client, _) = BuildClient("Internal Server Error", HttpStatusCode.InternalServerError);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.PingAsync());
    }
}
