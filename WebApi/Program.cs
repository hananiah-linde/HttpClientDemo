var builder = WebApplication.CreateBuilder(args);

// --- SETUP FOR NAMED AND TYPED CLIENTS ---
builder.Services.AddHttpClient("PingClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

builder.Services.AddHttpClient<PingTypedClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});
// ------------------------------------------

var app = builder.Build();

// 1. THE BAD: Instantiating HttpClient every time.
// This leads to "Socket Exhaustion" because sockets are not closed immediately after Dispose.
app.MapGet("/bad", async () =>
{
    using var client = new HttpClient();
    Console.WriteLine("Sending request via NEW HttpClient instance...");
    return await client.GetStringAsync("http://localhost:5001/ping");
});

// 2. THE BETTER: Using a static HttpClient.
// This reuses the same connection, avoiding socket exhaustion.
// PROBLEM: It doesn't respect DNS changes because the connection stays open indefinitely.
// Note: In a real app, you'd define this in a separate class or as a singleton.
// static readonly HttpClient _staticClient = new HttpClient(); 
// app.MapGet("/better-static", async () =>
// {
//     Console.WriteLine("Sending request via STATIC HttpClient...");
//     return await _staticClient.GetStringAsync("http://localhost:5001/ping");
// });

// 3. THE EVEN BETTER: Static HttpClient with SocketsHttpHandler.
// This allows reusing connections while also forcing them to rotate to respect DNS changes.
/*
static readonly HttpClient _handlerClient = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2) // Rotates connections for DNS updates
});
app.MapGet("/even-better", async () =>
{
    Console.WriteLine("Sending request via STATIC HttpClient with SocketsHttpHandler...");
    return await _handlerClient.GetStringAsync("http://localhost:5001/ping");
});
*/

// 4. THE GREAT: IHttpClientFactory (Named Client).
// Manages the underlying HttpMessageHandler lifecycle to solve both socket exhaustion and DNS issues.
app.MapGet("/great-factory", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient("PingClient");
    Console.WriteLine("Sending request via NAMED IHttpClientFactory...");
    return await client.GetStringAsync("ping");
});

// 5. THE BEST: Typed Client.
// Provides a clean, type-safe API for interacting with a specific service.
app.MapGet("/best-typed", async (PingTypedClient client) =>
{
    Console.WriteLine("Sending request via TYPED Client...");
    return await client.GetPingAsync();
});

app.Run();

// --- TYPED CLIENT DEFINITION ---
public class PingTypedClient(HttpClient client)
{
    public async Task<string> GetPingAsync()
    {
        return await client.GetStringAsync("ping");
    }
}