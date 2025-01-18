﻿using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

#nullable enable
namespace CollapseLauncher
{
    internal static partial class JSONSerializerHelper
    {
        private const short MAX_ALLOWED_RENT_BUFFER = (4 << 10) - 1;
        private const byte MIN_ALLOWED_CHAR_LENGTH = 2;

        private static readonly JavaScriptEncoder jsonEncoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        private static readonly JsonWriterOptions jsonWriterOptions = new()
        {
            Indented = false,
            Encoder = jsonEncoder
        };
        private static readonly JsonWriterOptions jsonWriterOptionsIndented = new()
        {
            Indented = true,
            Encoder = jsonEncoder
        };
        private static readonly JsonReaderOptions jsonReaderOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };
        private static readonly JsonNodeOptions jsonNodeOptions = new()
        {
            PropertyNameCaseInsensitive = false // Restricted to use the exact property name case
        };
        private static readonly JsonDocumentOptions jsonDocumentOptions = new()
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };

        private static readonly ArrayBufferWriter<byte> jsonBufferWriter   = new(4 << 10);
        private static readonly Utf8JsonWriter          jsonWriter         = new(jsonBufferWriter, jsonWriterOptions);
        private static readonly Utf8JsonWriter          jsonWriterIndented = new(jsonBufferWriter, jsonWriterOptionsIndented);

        internal static T? Deserialize<T>(this string data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : class => InnerDeserialize(data, typeInfo, defaultType);

        internal static T? Deserialize<T>(this string data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : struct => InnerDeserialize(data, typeInfo, defaultType);

        internal static JsonNode? DeserializeAsJsonNode(this string data)
            => InnerDeserializeAsJsonNode(data);

        internal static T? Deserialize<T>(this ReadOnlySpan<char> data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : class => InnerDeserialize(data, typeInfo, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<char> data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : struct => InnerDeserialize(data, typeInfo, defaultType);

        internal static JsonNode? DeserializeAsJsonNode(this ReadOnlySpan<char> data)
            => InnerDeserializeAsJsonNode(data);

        internal static T? Deserialize<T>(this ReadOnlySpan<byte> data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : class => InnerDeserialize(data, typeInfo, defaultType);

        internal static T? Deserialize<T>(this ReadOnlySpan<byte> data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : struct => InnerDeserialize(data, typeInfo, defaultType);

        internal static JsonNode? DeserializeAsJsonNode(this ReadOnlySpan<byte> data)
            => InnerDeserializeAsJsonNode(data);

        internal static T? Deserialize<T>(this Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : class => InnerDeserializeStream(data, typeInfo, defaultType);

        internal static T? Deserialize<T>(this Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType = null)
            where T : struct => InnerDeserializeStream(data, typeInfo, defaultType);

        internal static JsonNode? DeserializeAsJsonNode(this Stream data)
            => InnerDeserializeStreamAsJsonNode(data);

        private static T? InnerDeserializeStream<T>(Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType)
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return defaultType ?? default;

            // Try deserialize. If it returns a null, then return the default value
            return JsonSerializer.Deserialize(data, typeInfo) ?? defaultType ?? default;
        }

        private static T? InnerDeserialize<T>(ReadOnlySpan<char> data, JsonTypeInfo<T?> typeInfo, T? defaultType)
        {
            // Check if the data length is less than 2 bytes (assuming the buffer is "{}"), then return default value
            if (data.Length <= MIN_ALLOWED_CHAR_LENGTH) return defaultType ?? default;

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<char> dataTrimmed = data.TrimEnd((char)0x00);

            // Get the temporary buffer
            int tempBufferLength = dataTrimmed.Length * 2;
            bool IsUseRentBuffer = tempBufferLength <= MAX_ALLOWED_RENT_BUFFER;
            byte[] tempBuffer = IsUseRentBuffer ? ArrayPool<byte>.Shared.Rent(tempBufferLength) : new byte[tempBufferLength];

            try
            {
                // Convert the char[] buffer into byte[]
                int bufferWritten = Encoding.UTF8.GetBytes(dataTrimmed, tempBuffer);
                // Start deserialize and return
                return InnerDeserialize(tempBuffer.AsSpan(0, bufferWritten), typeInfo, defaultType);
            }
            finally
            {
                // Once the process is completed, then return the rented buffer (if it's being used)
                if (IsUseRentBuffer) ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        private static T? InnerDeserialize<T>(ReadOnlySpan<byte> data, JsonTypeInfo<T?> typeInfo, T? defaultType)
        {
            // Check if the data length is less than 2 bytes (assuming the buffer is "{}"), then return default value
            if (data.Length <= MIN_ALLOWED_CHAR_LENGTH) return defaultType ?? default;

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<byte> dataTrimmed = data.TrimEnd((byte)0x00);
            Utf8JsonReader jsonReader = new Utf8JsonReader(dataTrimmed, jsonReaderOptions);

            // Try deserialize. If it returns a null, then return the default value
            return JsonSerializer.Deserialize(ref jsonReader, typeInfo) ?? defaultType ?? default;
        }

        private static JsonNode? InnerDeserializeStreamAsJsonNode(Stream data)
        {
            // Check if the data length is 0, then return default value
            if (data.Length == 0) return JsonNode.Parse("{}", jsonNodeOptions);

            // Try deserialize to JSON Node
            return JsonNode.Parse(data, jsonNodeOptions, jsonDocumentOptions);
        }

        private static JsonNode? InnerDeserializeAsJsonNode(ReadOnlySpan<char> data)
        {
            // Check if the data length is less than 2 bytes (assuming the buffer is "{}"), then return default value
            if (data.Length <= MIN_ALLOWED_CHAR_LENGTH) return JsonNode.Parse("{}", jsonNodeOptions);

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<char> dataTrimmed = data.TrimEnd((char)0x00);

            // Get the temporary buffer
            int tempBufferLength = dataTrimmed.Length * 2;
            bool IsUseRentBuffer = tempBufferLength <= MAX_ALLOWED_RENT_BUFFER;
            byte[] tempBuffer = IsUseRentBuffer ? ArrayPool<byte>.Shared.Rent(tempBufferLength) : new byte[tempBufferLength];

            try
            {
                // Convert the char[] buffer into byte[]
                int bufferWritten = Encoding.UTF8.GetBytes(dataTrimmed, tempBuffer);
                // Start deserialize and return
                return InnerDeserializeAsJsonNode(tempBuffer.AsSpan(0, bufferWritten));
            }
            finally
            {
                // Once the process is completed, then return the rented buffer (if it's being used)
                if (IsUseRentBuffer) ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        private static JsonNode? InnerDeserializeAsJsonNode(ReadOnlySpan<byte> data)
        {
            // Check if the data length is less than 2 bytes (assuming the buffer is "{}"), then return default value
            if (data.Length <= MIN_ALLOWED_CHAR_LENGTH) return JsonNode.Parse("{}", jsonNodeOptions);

            // Try trimming the \0 char at the end of the data
            ReadOnlySpan<byte> dataTrimmed = data.TrimEnd((byte)0x00);
            Utf8JsonReader jsonReader = new Utf8JsonReader(dataTrimmed, jsonReaderOptions);

            // Try deserialize to JSON Node
            return JsonNode.Parse(ref jsonReader, jsonNodeOptions);
        }

        internal static string Serialize<T>(this T? value, JsonTypeInfo<T?> typeInfo, bool isIncludeNullEndChar = true, bool isWriteIndented = false)
            => InnerSerialize(value, typeInfo, isIncludeNullEndChar, isWriteIndented);

        internal static string SerializeJsonNode<T>(this JsonNode? node, JsonTypeInfo<T?> typeInfo, bool isIncludeNullEndChar = true, bool isWriteIndented = false)
            => InnerSerializeJsonNode(node, typeInfo, isIncludeNullEndChar, isWriteIndented);

        private static string InnerSerialize<T>(this T? data, JsonTypeInfo<T?> typeInfo, bool isIncludeNullEndChar, bool isWriteIndented)
        {
            const string _defaultValue = "{}";
            // Check if the data is null, then return default value
            if (data == null) return _defaultValue;

            // Lock the buffer
            lock (jsonBufferWriter)
            {
                // Clear the writer and its buffer
                jsonBufferWriter.Clear();

                // SANITY CHECK: Check if the buffer is already zero-ed
                if (jsonBufferWriter.WrittenCount != 0) throw new InvalidOperationException("Illegal Fault: The buffer hasn't been zeroed!");

                // Reset the writer state
                if (isWriteIndented) jsonWriterIndented.Reset();
                else jsonWriter.Reset();

                // Assign the writer
                Utf8JsonWriter writer = isWriteIndented ? ref jsonWriterIndented : ref jsonWriter;

                // Lock the writer
                lock (writer)
                {
                    // Try serialize the type into JSON string
                    JsonSerializer.Serialize(writer, data, typeInfo);

                    // Flush the writer
                    writer.Flush();

                    // Write the buffer to string
                    ReadOnlySpan<byte> buffer = jsonBufferWriter.WrittenSpan;
                    string returnValue = Encoding.UTF8.GetString(buffer);

                    // If the serialization accepts \0 char at the end of the return, then return it with \0
                    // Otherwise, return as is.
                    return isIncludeNullEndChar ? returnValue + '\0' : returnValue;
                }
            }
        }

        private static string InnerSerializeJsonNode<T>(this JsonNode? node, JsonTypeInfo<T?> typeInfo, bool isIncludeNullEndChar, bool isWriteIndented)
        {
            const string _defaultValue = "{}";
            // Check if the node is null, then return default value
            if (node == null) return _defaultValue;

            // Lock the buffer
            lock (jsonBufferWriter)
            {
                // Clear the writer and its buffer
                jsonBufferWriter.Clear();

                // SANITY CHECK: Check if the buffer is already zero-ed
                if (jsonBufferWriter.WrittenCount != 0) throw new InvalidOperationException("Illegal Fault: The buffer hasn't been zeroed!");

                // Reset the writer state
                if (isWriteIndented) jsonWriterIndented.Reset();
                else jsonWriter.Reset();

                // Assign the writer
                Utf8JsonWriter writer = isWriteIndented ? ref jsonWriterIndented : ref jsonWriter;

                // Lock the writer
                lock (writer)
                {
                    // Try serialize the JSON Node into JSON string
                    node.WriteTo(writer, typeInfo.Options);

                    // Flush the writer
                    writer.Flush();

                    // Write the buffer to string
                    ReadOnlySpan<byte> buffer = jsonBufferWriter.WrittenSpan;
                    string returnValue = Encoding.UTF8.GetString(buffer);

                    // If the serialization accepts \0 char at the end of the return, then return it with \0
                    // Otherwise, return as is.
                    return isIncludeNullEndChar ? returnValue + '\0' : returnValue;
                }
            }
        }
    }
}
