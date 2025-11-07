using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

/// <summary>
/// Allows BookingStatus to be serialized as strings and accepts both numeric/string on deserialization.
/// Handles friendly names like "Pending" → "Requested".
/// </summary>
public sealed class BookingStatusFlexConverter : JsonConverter<BookingStatus>
{
    public override BookingStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Allow numeric or string
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(BookingStatus), n))
                return (BookingStatus)n;
            throw new JsonException("Invalid numeric BookingStatus");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? "";
            // Accept friendly alternatives
            switch (s.Trim().ToLowerInvariant())
            {
                case "pending": return BookingStatus.Requested;
                case "approved": return BookingStatus.Confirmed;
                case "active": return BookingStatus.InProgress;
                case "finished": return BookingStatus.Completed;
                default:
                    // Try exact enum names
                    if (Enum.TryParse<BookingStatus>(s, ignoreCase: true, out var parsed))
                        return parsed;
                    throw new JsonException($"Invalid BookingStatus value '{s}'");
            }
        }

        throw new JsonException("Unexpected token for BookingStatus");
    }

    public override void Write(Utf8JsonWriter writer, BookingStatus value, JsonSerializerOptions options)
    {
        // Always write enum as string
        writer.WriteStringValue(value.ToString());
    }
}