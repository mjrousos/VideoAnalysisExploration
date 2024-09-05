using System.Text.Json.Serialization;

namespace Exploration.Azure.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ArmAccessTokenPermission
{
    Reader,
    Contributor,
    Owner,
}
