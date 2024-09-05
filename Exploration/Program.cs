using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Exploration.Console;
using Exploration;
using Exploration.Azure;
using Microsoft.Extensions.Configuration;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddUserSecrets<Program>();
        builder.Services.AddLogging(builder => builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.SingleLine = false;
            options.TimestampFormat = "HH:mm:ss ";
        }));
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<IVideoAnalyzer, AzureVideoIndexerAnalyzer>();
        builder.Services.Configure<AzureVideoIndexerOptions>(builder.Configuration.GetSection("AzureVideoIndexer"));
        builder.Services.AddHostedService<ExplorationConsole>();
        builder.Services.Configure<RuntimeOptions>(o => o.InputArguments = args);

        var host = builder.Build();
        await host.RunAsync();
    }
}