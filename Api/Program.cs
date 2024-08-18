using Microsoft.AspNetCore.Mvc;
using Serilog;
using ILogger = Serilog.ILogger;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Host.UseSerilog((ctx, logConfig) => logConfig.ReadFrom.Configuration(ctx.Configuration));
var app = builder.Build();
app.UseHttpsRedirection();
app.UseSerilogRequestLogging();

app.MapGet(
    "/",
    ([FromServices] ILogger logger) =>
    {
        var random = new Random();
        var randomNumber = random.Next();
        logger.Information("Generated a random number of {RandomNumber}", randomNumber);

        return Results.Ok(randomNumber);
    });

app.Run();