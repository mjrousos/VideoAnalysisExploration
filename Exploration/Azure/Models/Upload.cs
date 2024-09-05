using System.Text.Json.Serialization;

namespace Exploration.Azure.Models;

public class Upload
{
    [JsonPropertyName("id")]
    public required string VideoId { get; set; }

    [JsonPropertyName("state")]
    public required UploadState State { get; set; }
}