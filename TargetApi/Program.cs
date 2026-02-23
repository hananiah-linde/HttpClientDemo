var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/ping", async () =>
{
    await Task.Delay(2000);
    return "pong";
});

app.Run();