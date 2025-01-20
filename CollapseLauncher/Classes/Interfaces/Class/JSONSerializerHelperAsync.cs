using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher
{
    internal static partial class JsonSerializerHelper
    {
        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType = null, CancellationToken token = default)
            where T : class => await InnerDeserializeStreamAsync(data, typeInfo, defaultType, token);

        internal static async ValueTask<T?> DeserializeAsync<T>(this Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType = null, CancellationToken token = default)
            where T : struct => await InnerDeserializeStreamAsync(data, typeInfo, defaultType, token);

        internal static async Task<JsonNode?> DeserializeAsNodeAsync(this Stream data, CancellationToken token = default)
            => await InnerDeserializeStreamAsNodeAsync(data, token);

        internal static IAsyncEnumerable<T?> DeserializeAsEnumerable<T>(this Stream data, JsonTypeInfo<T?> typeInfo, CancellationToken token = default)
            => JsonSerializer.DeserializeAsyncEnumerable(data, typeInfo, token);

        internal static async Task<List<T?>> DeserializeAsListAsync<T>(this Stream data, JsonTypeInfo<T?> typeInfo, CancellationToken token = default)
        {
            // Create List of T
            List<T?> listItem = [];

            // Enumerate in async
            await foreach (T? item in data.DeserializeAsEnumerable(typeInfo, token))
            {
                // Add an item to List<T>
                listItem.Add(item);
            }

            // Return the list
            return listItem;
        }

        private static async ValueTask<T?> InnerDeserializeStreamAsync<T>(Stream data, JsonTypeInfo<T?> typeInfo, T? defaultType, CancellationToken token) =>
        // Check if the data cannot be read, then throw
        !data.CanRead ? throw new NotSupportedException("Stream is not readable! Cannot deserialize the stream to JSON!") :
        // Try to deserialize. If it returns a null, then return the default value
            await JsonSerializer.DeserializeAsync(data, typeInfo, token) ?? defaultType;

        private static async Task<JsonNode?> InnerDeserializeStreamAsNodeAsync(Stream data, CancellationToken token) =>
        // Check if the data cannot be read, then throw
        !data.CanRead ? throw new NotSupportedException("Stream is not readable! Cannot deserialize the stream to JSON!") :
        // Try deserialize to JSON Node
            await JsonNode.ParseAsync(data, JsonNodeOptions, JsonDocumentOptions, token);

        internal static async ValueTask SerializeAsync<T>(this T? value, Stream targetStream, JsonTypeInfo<T?> typeInfo, CancellationToken token = default)
            where T : class => await InnerSerializeStreamAsync(value, targetStream, typeInfo, token);

        internal static async ValueTask SerializeAsync<T>(this T? value, Stream targetStream, JsonTypeInfo<T?> typeInfo, CancellationToken token = default)
            where T : struct => await InnerSerializeStreamAsync(value, targetStream, typeInfo, token);

        private static async ValueTask InnerSerializeStreamAsync<T>(this T? value, Stream targetStream, JsonTypeInfo<T?> typeInfo, CancellationToken token)
        {
            if (!targetStream.CanWrite)
                throw new NotSupportedException("Stream is not writeable! Cannot serialize the object into Stream!");

            if (typeInfo == null)
                throw new NotSupportedException($"Context does not contain a type info of type {typeof(T).Name}!");

            await JsonSerializer.SerializeAsync(targetStream, value, typeInfo, token);
        }
    }
}
