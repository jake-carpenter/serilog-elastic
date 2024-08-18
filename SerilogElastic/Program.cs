var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.UseHttpsRedirection();

app.MapGet("/info", () => Results.Ok());
app.MapGet("/warn", () => Results.Ok());
app.MapGet("/error", () => Results.Ok());
app.Run();
