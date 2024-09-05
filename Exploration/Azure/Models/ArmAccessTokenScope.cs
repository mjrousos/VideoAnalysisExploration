using System.Text.Json.Serialization;

namespace Exploration.Azure.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmAccessTokenScope
{
    Account,
    Project,
    Video
}
