using System.Text.Json.Serialization;

namespace Exploration.Azure.Models;

public class GenerateAccessTokenResponse
{
    [JsonPropertyName("accessToken")]
    public required string AccessToken { get; set; }
}
