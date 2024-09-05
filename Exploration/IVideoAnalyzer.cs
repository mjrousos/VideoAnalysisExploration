internal interface IVideoAnalyzer
{
    public Task AnalyzeAsync(string inputPath, string? name = null, string? description = null, CancellationToken ct = default);
}