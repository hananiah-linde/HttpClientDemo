var builder = WebApplication.CreateBuilder(args);
var staticClient = new HttpClient();
var betterStaticClient = new HttpClient(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2)
});

builder.Services.AddHttpClient(HttpClientNames.PingClient, client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

builder.Services.AddHttpClient<PingTypedClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

var app = builder.Build();

app.MapGet("/v1", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Calling API with new HttpClient Instance");
    using var client = new HttpClient();
    return await client.GetStringAsync("https://httpbin.org/get");
});

app.MapGet("/v2", async (ILogger<Program> logger) =>
{
    logger.LogInformation("Calling API with static HttpClient Instance");
    return await staticClient.GetStringAsync("http://localhost:5001/ping");
});

app.MapGet("/v3", async () =>
{
    return await betterStaticClient.GetStringAsync("http://localhost:5001/ping");
});

app.MapGet("/v4", async (IHttpClientFactory factory) =>
{
    var client = factory.CreateClient(HttpClientNames.PingClient);
    return await client.GetStringAsync("ping");
});

app.MapGet("/v5", async (PingTypedClient client) => await client.GetPingAsync());

app.Run();


public class PingTypedClient(HttpClient client)
{
    public async Task<string> GetPingAsync()
    {
        return await client.GetStringAsync("ping");
    }
}

public static class HttpClientNames
{
    public const string PingClient = "PingClient";
}