using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using ILogger = Serilog.ILogger;

await Host.CreateDefaultBuilder(args)
    .ConfigureLogging(ctx => ctx.ClearProviders())
    .UseSerilog((ctx, logConfig) => logConfig.ReadFrom.Configuration(ctx.Configuration))
    .ConfigureServices(
        services =>
        {
            services.AddSerilog();
            services.AddHostedService<Worker>();
        })
    .Build()
    .RunAsync();

internal class Worker(ILogger logger, IConfiguration config) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.Information("App started");
        using var client = CreateClient(config);

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.Information("Received random number from API: {RandomNumber}", content);

            await Task.Delay(1000, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                break;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.Information("App shutting down!");

        return Task.CompletedTask;
    }

    private static HttpClient CreateClient(IConfiguration config)
    {
        var apiUrl = config.GetValue<string>("ApiUrl");

        if (apiUrl is null)
            throw new InvalidOperationException("ApiUrl is not configured");

        return new HttpClient() { BaseAddress = new Uri(apiUrl) };
    }
}