var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("PingClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5001/");
});

var app = builder.Build();

app.MapGet("/bad", async () =>
{
    using var client = new HttpClient();
    Console.WriteLine("sending request to /bad");

    return await client.GetStringAsync("http://localhost:5001/ping");
});

app.MapGet("/better", (HttpClient client) =>
{
    Console.WriteLine("sending request to /better");
    return client.GetStringAsync("http://localhost:5001/ping");
});

app.MapGet("/best", (IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient("PingClient");
    Console.WriteLine("sending request to /best");
    return client.GetStringAsync("ping");
});

app.Run();