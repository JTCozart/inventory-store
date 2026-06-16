using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InventoryStore.App.Infrastructure;

/// <summary>
/// Serializes <see cref="DateTime"/> values as explicit UTC ISO-8601 (with a trailing 'Z'). Every
/// timestamp in the app is stored in UTC, but values round-tripped through SQLite come back as
/// <see cref="DateTimeKind.Unspecified"/>, which System.Text.Json would otherwise emit without a
/// zone designator — causing the browser to parse them as local time. Pinning the 'Z' here lets the
/// client be the single authority that converts UTC into the configured display zone.
/// </summary>
internal sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(
            DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture));
}
