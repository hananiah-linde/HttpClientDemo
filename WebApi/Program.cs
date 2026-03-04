// =============================================================================
// HttpClient Demo - WebApi
// Demonstrates the evolution of HttpClient usage in .NET, from naive to best
// practice. Each versioned endpoint calls TargetApi's /ping endpoint.
// Run TargetApi first: it listens on http://localhost:5001
// =============================================================================

var builder = WebApplication.CreateBuilder(args);

const string TargetApiBase = "http://localhost:5001";

// ---------------------------------------------------------------------------
// v3 & v4 setup: register IHttpClientFactory with a named client
// ---------------------------------------------------------------------------
builder.Services.AddHttpClient(HttpClientNames.TargetApi, client =>
{
    client.BaseAddress = new Uri(TargetApiBase);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// v4: typed client registration - the interface/implementation overload means
// callers depend on IPingClient, not the concrete class. DI and the factory
// still wire everything up automatically.
builder.Services.AddHttpClient<IPingClient, PingClient>(client =>
{
    client.BaseAddress = new Uri(TargetApiBase);
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// v1 - new HttpClient() per request
//
// PITFALLS:
//   - Creates a new socket for every request.
//   - Sockets linger in TIME_WAIT state after closing (up to ~4 minutes on
//     most OS). Under any real load you will exhaust available ports and get
//     SocketException: "Only one usage of each socket address is permitted."
//   - Also bypasses all DI configuration, retries, delegating handlers, etc.
//
// Use this ONLY in throwaway scripts or one-shot CLIs, never in a server.
// ---------------------------------------------------------------------------
app.MapGet("/api/v1", async () =>
{
    // A new HttpClient (and therefore a new socket) is created on every call.
    using var client = new HttpClient();
    var response = await client.GetStringAsync($"{TargetApiBase}/ping");
    return Results.Ok(new { version = "v1 - new HttpClient() per request (BAD)", response });
});

// ---------------------------------------------------------------------------
// v2 - static / shared HttpClient
//
// IMPROVEMENT over v1:
//   - One socket is reused across requests, no port exhaustion.
//
// PITFALL:
//   - The static instance captures DNS at first use. If the downstream
//     service's IP changes (rolling deploy, k8s pod restart, DNS TTL expiry)
//     the client keeps talking to the old address until the process restarts.
//   - No lifecycle management, retries, or DI integration.
//   - Singleton state means configuration changes affect all callers globally.
//
// Acceptable for simple console apps or lambdas; not great for long-running
// services with dynamic infrastructure.
// ---------------------------------------------------------------------------
app.MapGet("/api/v2", async () =>
{
    var response = await StaticClients.Shared.GetStringAsync($"{TargetApiBase}/ping");
    return Results.Ok(new { version = "v2 - static HttpClient (better, but DNS blind)", response });
});

// ---------------------------------------------------------------------------
// v3 - IHttpClientFactory (named client)
//
// IMPROVEMENT over v2:
//   - Factory manages a pool of HttpMessageHandler instances.
//   - Handlers are recycled on a configurable interval (default 2 min),
//     which picks up DNS changes without leaking sockets.
//   - Centralized configuration (base address, timeouts, headers, retry
//     policies via Polly) in one place at startup.
//   - HttpClient instances returned by the factory are cheap to create and
//     should NOT be stored long-term — let the factory manage lifetimes.
//
// MINOR PITFALL:
//   - Still requires a string "name" to look up the client; typos cause
//     runtime errors. Typed clients (v4) solve this.
// ---------------------------------------------------------------------------
app.MapGet("/api/v3", async (IHttpClientFactory factory) =>
{
    // CreateClient returns a *new* HttpClient each time, but the underlying
    // HttpMessageHandler (socket pool) is shared and managed by the factory.
    var client = factory.CreateClient(HttpClientNames.TargetApi);
    var response = await client.GetStringAsync("/ping");
    return Results.Ok(new { version = "v3 - IHttpClientFactory named client (recommended)", response });
});

// ---------------------------------------------------------------------------
// v4 - Typed HttpClient behind an interface (IPingClient)
//
// IMPROVEMENT over v3:
//   - Wraps HttpClient in a strongly-typed service class. No magic strings.
//   - Injected directly via DI — discoverable, testable, mockable.
//   - Encapsulates all HTTP logic (URL paths, serialization, error handling)
//     behind a clean domain API.
//   - The factory still manages handler lifetimes under the hood.
//
// TESTABILITY UPGRADE (the big win):
//   - The endpoint depends on IPingClient, not HttpClient or IHttpClientFactory.
//   - In tests you can register a FakePingClient : IPingClient that returns a
//     hardcoded string. Zero HTTP concepts, zero fake handlers, zero network.
//   - v3 can't do this: IHttpClientFactory.CreateClient() returns HttpClient
//     (a concrete class), so tests must always deal with a fake handler.
//
// This is the recommended pattern for production code.
// ---------------------------------------------------------------------------
app.MapGet("/api/v4", async (IPingClient pingClient) =>
{
    var response = await pingClient.PingAsync();
    return Results.Ok(new { version = "v4 - Typed HttpClient with interface (best practice)", response });
});

app.Run();

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// Holds the static HttpClient used by v2.
/// Defined as a static class to make the singleton lifetime explicit.
/// </summary>
static class StaticClients
{
    // SocketsHttpHandler gives us fine-grained control over connection pooling.
    // PooledConnectionLifetime tells the handler to recycle connections after
    // this period, partially mitigating the DNS-staleness issue — but this is
    // manual plumbing that IHttpClientFactory handles automatically.
    public static readonly HttpClient Shared = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2)
    });
}

/// <summary>
/// Domain interface for the ping operation.
/// The endpoint depends on this, not on PingClient or HttpClient directly.
/// Swap in any implementation (real, fake, stub) without touching the endpoint.
/// </summary>
interface IPingClient
{
    Task<string> PingAsync();
}

/// <summary>
/// Typed HTTP client for the TargetApi service.
/// Registered with AddHttpClient&lt;IPingClient, PingClient&gt;() so the factory
/// manages its HttpClient's handler lifetime automatically.
/// </summary>
class PingClient(HttpClient client) : IPingClient
{
    public async Task<string> PingAsync() =>
        await client.GetStringAsync("/ping");
}

/// <summary>
/// A domain service that consumes IPingClient.
/// This is the typical real-world pattern: a service class with its own logic
/// that happens to depend on a typed HTTP client via its interface.
/// PingService knows nothing about HTTP — it just calls PingAsync() and
/// interprets the result. That logic is what we want to unit test.
/// </summary>
class PingService(IPingClient pingClient)
{
    public async Task<bool> IsAliveAsync()
    {
        var response = await pingClient.PingAsync();
        return response == "pong";
    }
}

static class HttpClientNames
{
    public const string TargetApi = "TargetApi";
}

// Required for WebApplicationFactory<Program> in integration tests.
// Top-level statement programs generate an internal Program class by default;
// this partial declaration makes it visible to the test project.
public partial class Program { }