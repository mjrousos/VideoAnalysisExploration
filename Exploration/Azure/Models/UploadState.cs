using System.Text.Json.Serialization;

namespace Exploration.Azure.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadState
{
    Uploaded,
    Processing,
    Processed,
    Failed
}
