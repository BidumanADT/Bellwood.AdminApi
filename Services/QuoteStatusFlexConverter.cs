using System.Text.Json;
using System.Text.Json.Serialization;
using Bellwood.AdminApi.Models;

namespace Bellwood.AdminApi.Services;

public sealed class QuoteStatusFlexConverter : JsonConverter<QuoteStatus>
{
    public override QuoteStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Allow numeric or string
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt32(out var n) && Enum.IsDefined(typeof(QuoteStatus), n))
                return (QuoteStatus)n;
            throw new JsonException("Invalid numeric QuoteStatus");
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? "";
            // Accept friendly alternatives used in UI/old files
            switch (s.Trim().ToLowerInvariant())
            {
                case "pending": return QuoteStatus.InReview;
                case "quoted": return QuoteStatus.Sent;
                default:
                    // Try exact enum names
                    if (Enum.TryParse<QuoteStatus>(s, ignoreCase: true, out var parsed))
                        return parsed;
                    throw new JsonException($"Invalid QuoteStatus value '{s}'");
            }
        }

        throw new JsonException("Unexpected token for QuoteStatus");
    }

    public override void Write(Utf8JsonWriter writer, QuoteStatus value, JsonSerializerOptions options)
    {
        // Always write enum as string
        writer.WriteStringValue(value.ToString());
    }
}
