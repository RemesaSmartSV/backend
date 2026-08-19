using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemesaSmartSV.Converters;

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();
        return value.Kind switch
        {
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value
        };
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
}