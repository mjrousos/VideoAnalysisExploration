using Azure.Core;
using Azure.Identity;
using Exploration.Azure.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Exploration.Azure;

internal class AzureVideoIndexerAnalyzer : IVideoAnalyzer
{
    private const string ApiVersion = "2024-01-01";
    private const string OutputPath = "VideoIndex.json";

    private readonly AzureVideoIndexerOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureVideoIndexerAnalyzer> _logger;
    private string? _accessToken;
    private JsonSerializerOptions _serializerOptions;

    public AzureVideoIndexerAnalyzer(IOptions<AzureVideoIndexerOptions> options, HttpClient httpClient, ILogger<AzureVideoIndexerAnalyzer> logger)
    {
        _options = options.Value;
        _httpClient = httpClient;
        _logger = logger;
        _serializerOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            AllowTrailingCommas = true
        };
    }

    public async Task AnalyzeAsync(string inputPath, string? name = null, string? description = null, CancellationToken ct = default)
    {
        var queryParameters = HttpUtility.ParseQueryString(string.Empty);
        queryParameters["name"] = name ?? Path.GetFileNameWithoutExtension(inputPath);
        queryParameters["description"] = description ?? "Video uploaded for analysis";
        queryParameters["privacy"] = "private";
        queryParameters["videoUrl"] = await UploadVideoAsync(inputPath, ct);
        queryParameters["language"] = "English";
        queryParameters["accessToken"] = await GetAccessTokenAsync(ct);
        queryParameters["useManagedIdentityToDownloadVideo"] = "true";
        queryParameters["retentionPeriod"] = "1";
        queryParameters["preventDuplicates"] = "false";

        var uploadUrl = $"https://api.videoindexer.ai/{_options.Location}/Accounts/{_options.AccountId}/Videos?{queryParameters}";
        var response = await _httpClient.PostAsync(uploadUrl, new StringContent(string.Empty), ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to upload video: {StatusCode}", response.StatusCode);
            return;
        }

        var upload = await JsonSerializer.DeserializeAsync<Upload>(await response.Content.ReadAsStreamAsync(ct), _serializerOptions, ct);

        if (upload is null)
        {
            _logger.LogError("Failed to deserialize video state response");
            return;
        }

        _logger.LogInformation("Video upload started. Video ID: {VideoId}, video state: {VideoState}", upload.VideoId, upload.State);

        while (upload.State != UploadState.Processed && upload.State != UploadState.Failed)
        {
            if (ct.IsCancellationRequested)
            {
                _logger.LogWarning("Analysis cancelled");
                return;
            }

            _logger.LogInformation("Video state: {VideoState}", upload.State);
            await Task.Delay(10_000, ct);
            upload.State = await GetVideoUploadState(upload.VideoId, ct);
        }

        if (upload.State == UploadState.Failed)
        {
            _logger.LogError("Video upload failed");
        }
        else if (upload.State == UploadState.Processed)
        {
            _logger.LogInformation("Video processing complete");
            await GetVideoIndexAsync(upload.VideoId, ct);
        }
    }

    private async Task GetVideoIndexAsync(string videoId, CancellationToken ct)
    {
        var queryParameters = HttpUtility.ParseQueryString(string.Empty);
        queryParameters["accessToken"] = await GetAccessTokenAsync(ct);
        queryParameters["language"] = "English";

        var requestUrl = $"https://api.videoindexer.ai/{_options.Location}/Accounts/{_options.AccountId}/Videos/{videoId}/Index?{queryParameters}";
        var response = await _httpClient.GetAsync(requestUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get video state: {StatusCode}", response.StatusCode);
            return;
        }

        _logger.LogInformation("Video index retrieved");
        using var fs = new FileStream(OutputPath, FileMode.Create, FileAccess.Write, FileShare.None);
        var responseStream = await response.Content.ReadAsStreamAsync(ct);
        await responseStream.CopyToAsync(fs, ct);
        _logger.LogInformation("Video index saved to {OutputPath}", OutputPath);
    }

    private async Task<UploadState> GetVideoUploadState(string videoId, CancellationToken ct)
    {
        var queryParameters = HttpUtility.ParseQueryString(string.Empty);
        queryParameters["accessToken"] = await GetAccessTokenAsync(ct);
        queryParameters["language"] = "English";

        var requestUrl = $"https://api.videoindexer.ai/{_options.Location}/Accounts/{_options.AccountId}/Videos/{videoId}/Index?{queryParameters}";
        var response = await _httpClient.GetAsync(requestUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get video state: {StatusCode}", response.StatusCode);
            return UploadState.Failed;
        }

        var upload = await JsonSerializer.DeserializeAsync<Upload>(await response.Content.ReadAsStreamAsync(ct), _serializerOptions, ct);

        if (upload is null)
        {
            _logger.LogError("Failed to deserialize video state response");
            return UploadState.Failed;
        }

        return upload.State;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is null)
        {
            _logger.LogInformation("Retrieving access token");
            var credentials = new DefaultAzureCredential();
            var armTokenRequestContext = new TokenRequestContext(["https://management.azure.com/.default"]);
            var armTokenResult = await credentials.GetTokenAsync(armTokenRequestContext, ct);

            var accessTokenRequestBody = new AccessTokenRequest
            {
                PermissionType = ArmAccessTokenPermission.Contributor,
                Scope = ArmAccessTokenScope.Account,
            };

            var accessTokenRequestContent = new StringContent(JsonSerializer.Serialize(accessTokenRequestBody, _serializerOptions));
            accessTokenRequestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            var accessTokenRequestUrl = 
                $"https://management.azure.com/subscriptions/{_options.SubscriptionId}/" +
                $"resourceGroups/{_options.ResourceGroup}/" + 
                $"providers/Microsoft.VideoIndexer/accounts/{_options.AccountName}/" + 
                $"generateAccessToken?api-version={ApiVersion}";

            var accessTokenRequestMessage = new HttpRequestMessage(HttpMethod.Post, accessTokenRequestUrl);
            accessTokenRequestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", armTokenResult.Token);
            accessTokenRequestMessage.Content = accessTokenRequestContent;

            var jsonResponse = await _httpClient.SendAsync(accessTokenRequestMessage, ct);
            
            if (jsonResponse.IsSuccessStatusCode)
            {
                var jsonContent = await JsonSerializer.DeserializeAsync<GenerateAccessTokenResponse>(await jsonResponse.Content.ReadAsStreamAsync(ct), _serializerOptions, cancellationToken: ct);
                if (jsonContent is null)
                {
                    _logger.LogError("Failed to deserialize access token response");
                }
                else
                {
                    _accessToken = jsonContent.AccessToken;
                    _logger.LogInformation("Retrieved access token");
                }
            }
            else
            {
                _logger.LogError("Failed to get access token: {StatusCode}", jsonResponse.StatusCode);
            }
        }

        if (_accessToken is null)
        {
            throw new InvalidOperationException("Failed to get access token");
        }
        else
        {
            return _accessToken;
        }
    }

    public Task<string> UploadVideoAsync(string inputPath, CancellationToken ct)
    {
        // TODO - Upload video to Azure Blob Storage.
        // For now, just return the URL of an already uploaded video.
        return Task.FromResult("https://mikerouailearning.blob.core.windows.net/videos-to-analyze/Diagnosing memory leaks in .NET apps.mkv");
    }
}
