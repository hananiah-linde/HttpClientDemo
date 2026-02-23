var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/bad", () =>
{
    using var client = new HttpClient();

});

app.MapGet("/better", () => "This is a good endpoint");

app.MapGet("/best", () => "This is the best endpoint");

app.Run();