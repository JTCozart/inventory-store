using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryTracker.App.Infrastructure;

internal static class AppJsonOptions
{
    internal static readonly JsonSerializerOptions Web = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
