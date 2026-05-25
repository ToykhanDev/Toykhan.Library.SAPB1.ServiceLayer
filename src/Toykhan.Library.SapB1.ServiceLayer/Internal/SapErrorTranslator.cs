using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toykhan.Library.SapB1.ServiceLayer.Internal;

internal sealed class SapErrorTranslator : ISapErrorTranslator
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new SapErrorCodeConverter(), new SapErrorMessageConverter() }
    };

    public SapServiceLayerException Translate(string responseBody, Exception? cause = null)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return new SapServiceLayerException("unknown", "SAP Service Layer returned an empty error response.", cause);

        try
        {
            var envelope = JsonSerializer.Deserialize<SapErrorEnvelope>(responseBody, _jsonOptions);
            if (envelope?.Error is { } err)
                return new SapServiceLayerException(err.Code, err.Message, cause);
        }
        catch
        {
            // JSON parse hatası — ham yanıtın bir kısmını mesaja ekle
        }

        var preview = responseBody.Length > 200
            ? responseBody.Substring(0, 200) + "..."
            : responseBody;
        return new SapServiceLayerException("unknown", $"SAP Service Layer error: {preview}", cause);
    }

    // ─── Private DTOs ──────────────────────────────────────────────────────────

    private sealed class SapErrorEnvelope
    {
        [JsonPropertyName("error")]
        public SapErrorBody? Error { get; set; }
    }

    private sealed class SapErrorBody
    {
        [JsonConverter(typeof(SapErrorCodeConverter))]
        [JsonPropertyName("code")]
        public string Code { get; set; } = "unknown";

        [JsonConverter(typeof(SapErrorMessageConverter))]
        [JsonPropertyName("message")]
        public string Message { get; set; } = "Unknown SAP error.";
    }

    // ─── JSON Converters ───────────────────────────────────────────────────────

    /// <summary>
    /// SAP bazen <c>"code": 100</c> (number), bazen <c>"code": "-2028"</c> (string) gönderir.
    /// Her ikisini de string'e normalize eder.
    /// </summary>
    private sealed class SapErrorCodeConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString() : reader.GetDecimal().ToString(),
                JsonTokenType.String => reader.GetString() ?? "unknown",
                _ => "unknown"
            };
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    /// <summary>
    /// SAP bazen <c>"message": {"value": "...", "lang": "en"}</c> (nesne),
    /// bazen <c>"message": "..."</c> (düz string) gönderir.
    /// Her ikisini de düz string'e normalize eder.
    /// </summary>
    private sealed class SapErrorMessageConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return reader.GetString() ?? string.Empty;

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                string? value = null;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propName = reader.GetString();
                        reader.Read();
                        if (string.Equals(propName, "value", StringComparison.OrdinalIgnoreCase))
                            value = reader.GetString();
                    }
                }
                return value ?? string.Empty;
            }

            // Beklenmeyen token — kalan token'ları tüket
            reader.Skip();
            return string.Empty;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }
}
