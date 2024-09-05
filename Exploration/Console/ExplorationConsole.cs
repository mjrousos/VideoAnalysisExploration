using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Exploration.Console;

internal class ExplorationConsole : BackgroundService
{
    private readonly ILogger<ExplorationConsole> _logger;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly IVideoAnalyzer _videoAnalyzer;
    private readonly RuntimeOptions _options;

    public ExplorationConsole(ILogger<ExplorationConsole> logger, IHostApplicationLifetime hostLifetime, IOptions<RuntimeOptions> options, IVideoAnalyzer videoAnalyzer)
    {
        _logger = logger;
        _hostLifetime = hostLifetime;
        _videoAnalyzer = videoAnalyzer;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.InputArguments is null || _options.InputArguments.Length < 1)
        {
            _logger.LogError("Input path is required");
            return;
        }

        var inputPath = _options.InputArguments[0];

        _logger.LogInformation($"Analyzing {inputPath}");

        if (!File.Exists(inputPath))
        {
            _logger.LogError($"Input path {inputPath} does not exist");
            return;
        }

        await _videoAnalyzer.AnalyzeAsync(inputPath);

        _logger.LogInformation("Analysis complete");

        _hostLifetime.StopApplication();
    }
}