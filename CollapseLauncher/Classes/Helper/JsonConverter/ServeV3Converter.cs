#nullable enable
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CollapseLauncher.Helper.JsonConverter
{
    internal class ServeV3StringConverter : JsonConverter<string>
    {
        public override bool CanConvert(Type type) => true;

        public override string Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) => Extension.GetServeV3String(reader);

        public override void Write(
                Utf8JsonWriter writer,
                string baseType,
                JsonSerializerOptions options)
        {
            throw new JsonException("Serializing is not supported!");
        }
    }

    public class ServeV3StringListConverter : JsonConverter<List<string>?>
    {
        public override bool CanConvert(Type type) => true;

        public override List<string>? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // Initialize and check if the token is a start of an array
            List<string>? returnValue = null;
            if (reader.TokenType != JsonTokenType.StartArray) // Throw if it's not
                throw new JsonException("The start token of the JSON field is not a start of an array!");

            // Read the next value or token
            reader.Read();

            // If the token is a string, initialize the list
            if (reader.TokenType == JsonTokenType.String)
                returnValue = [];

            // Loop and read the value if the token is currently a string
            while (reader.TokenType == JsonTokenType.String)
            {
                // Try retrieve the data if it's a raw string or a ServeV3 string
                string returnString = Extension.GetServeV3String(reader);
                returnValue?.Add(returnString); // Add the string
                reader.Read(); // Read the next token
            }

            // If the token is not an end of an array, then throw
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException("The end token of the JSON field is not an end of an array!");

            // Return the list
            return returnValue;
        }

        public override void Write(
                Utf8JsonWriter writer,
                List<string>? baseType,
                JsonSerializerOptions options)
        {
            throw new JsonException("Serializing is not supported!");
        }
    }
}
