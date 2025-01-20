using System;
using System.Text.Json;
using System.Text.Json.Serialization;

#nullable enable
namespace CollapseLauncher.Helper.JsonConverter
{
    internal class SanitizeUrlStringConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Current type token is not a string!");

            if (reader.ValueSpan.Length == 0) return null;

            string unescapedString = Extension.ReturnUnescapedData(reader.ValueSpan);
            string stripped = Extension.StripTabsAndNewlinesUtf8(unescapedString);
            return stripped;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
