using CollapseLauncher.GameSettings.Zenless;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Hi3Helper.SentryHelper;
// ReSharper disable NonReadonlyMemberInGetHashCode
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

#nullable enable
namespace CollapseLauncher.GameSettings.Base
{
    public enum JsonEnumStoreType
    {
        AsNumber,
        AsString,
        AsNumberString
    }

    internal static class MagicNodeBaseValuesExt
    {
        // ReSharper disable once UnusedMember.Local
        private static readonly JsonSerializerOptions JsonSerializerOpts = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        private static JsonObject EnsureCreatedObject(this JsonNode? node, string keyName)
        {
            // If the node is empty, then create a new instance of it
            node ??= new JsonObject();

            // Return
            return node.EnsureCreatedInner<JsonObject>(keyName);
        }

        private static JsonArray EnsureCreatedArray(this JsonNode? node, string keyName)
        {
            // If the node is empty, then create a new instance of it
            node ??= new JsonArray();

            // Return
            return node.EnsureCreatedInner<JsonArray>(keyName);
        }

        private static T EnsureCreatedInner<T>(this JsonNode? node, string keyName)
            where T : JsonNode
        {
            // SANITATION: Avoid creation of JsonNode directly
            if (typeof(T) == typeof(JsonNode))
                throw new InvalidOperationException("You cannot initialize the parent JsonNode type. Only JsonObject or JsonArray is accepted!");

            // Try get if the type is an array or object
            bool isTryCreateArray = typeof(T) == typeof(JsonArray);

            // Set parent node as object
            JsonObject? parentNodeObj = node?.AsObject();

            // If the value node does not exist, then create and add a new one
            if (!(parentNodeObj?.TryGetPropertyValue(keyName, out JsonNode? valueNode) ?? false))
            {
                // Otherwise, create a new empty one.
                JsonNodeOptions options = new JsonNodeOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                JsonNode jsonValueNode = isTryCreateArray ?
                    new JsonArray(options) :
                    new JsonObject(options);
                valueNode = jsonValueNode;
                parentNodeObj?.Add(new KeyValuePair<string, JsonNode?>(keyName, jsonValueNode));
            }

            // If the value node keeps returning null, SCREW IT!!!
            if (valueNode == null)
                throw new TypeInitializationException(
                    nameof(T),
                    new NullReferenceException(
                        $"Failed to create the type of {nameof(T)} in the parent node as it is a null!"
                        ));

            // Return object node
            return (T)valueNode;
        }

        public static string? GetNodeValue(this JsonNode? node, string keyName, string? defaultValue)
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // If node is null, return the default value
            if (jsonObject == null) return defaultValue;

            // Try to get node as struct value
            if (!jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNodeValue) || jsonNodeValue == null)
            {
                return defaultValue;
            }

            return jsonNodeValue.AsValue().GetValue<string>();
        }

        public static TJsonNodeType GetAsJsonNode<TJsonNodeType>(this JsonNode? node, string keyName)
            where TJsonNodeType : JsonNode
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // Try to get the JsonNode from the parent and if not null, return.
            if (jsonObject != null &&
                jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNode) &&
                jsonNode != null)
                return (TJsonNodeType)jsonNode;

            // Try to get the JsonNode member type to create
            Type jsonNodeType = typeof(TJsonNodeType);
            JsonNode jsonReturn;

            // If the type to get is an array, then return
            if (jsonNodeType == typeof(JsonArray))
                jsonReturn = node.EnsureCreatedArray(keyName);
            // Otherwise, ensure that an empty object is created in the parent node
            else
                jsonReturn = node.EnsureCreatedObject(keyName);

            // and then return it.
            return (TJsonNodeType)jsonReturn;
        }

        public static TValue GetNodeValue<TValue>(this JsonNode? node, string keyName, TValue defaultValue)
            where TValue : struct
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // Try to get node as struct value
            if (jsonObject == null ||
                !jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNodeValue) ||
                jsonNodeValue == null)
            {
                return defaultValue;
            }

            // If the value is not a boolean or the JsonValueKind is not a number, then return the value
            if (typeof(TValue) != typeof(bool) || jsonNodeValue.GetValueKind() != JsonValueKind.Number)
            {
                return jsonNodeValue.AsValue().GetValue<TValue>();
            }

            // Otherwise, get the default value
            // Assuming 0 is false, and any non-zero number is true
            int numValue  = jsonNodeValue.AsValue().GetValue<int>();
            bool boolValue = numValue != 0;
            return (TValue)(object)boolValue; // Cast bool to TValue
        }

        public static TEnum GetNodeValueEnum<TEnum>(this JsonNode? node, string keyName, TEnum defaultValue)
            where TEnum : struct
        {
            // Get node as object
            JsonObject? jsonObject = node?.AsObject();

            // If node is null, return the default value
            if (jsonObject == null) return defaultValue;

            // Try to get node as struct value
            if (!jsonObject.TryGetPropertyValue(keyName, out JsonNode? jsonNodeValue) || jsonNodeValue == null)
            {
                return defaultValue;
            }

            // Get the JsonValue representative from the node and get the kind/type
            JsonValue     enumValueRaw     = jsonNodeValue.AsValue();
            JsonValueKind enumValueRawKind = enumValueRaw.GetValueKind();

            // Decide the return value
            switch (enumValueRawKind)
            {
                case JsonValueKind.Number: // If it's a number
                    int enumAsInt = (int)enumValueRaw; // Cast JsonValue as int
                    return EnumFromInt(enumAsInt); // Cast and return it as an enum
                case JsonValueKind.String: // If it's a string
                    string? enumAsString = (string?)enumValueRaw; // Cast JsonValue as string

                    if (Enum.TryParse(enumAsString, true, out TEnum enumParsedFromString)) // Try parse as a named member
                        return enumParsedFromString; // If successful, return the returned value

                    // If the string is actually a number as a string, then try parse it as int
                    if (int.TryParse(enumAsString, null, out int enumAsIntFromString))
                        return EnumFromInt(enumAsIntFromString); // Cast and return it as an enum

                    // Throw if all the attempts were failed
                    throw new InvalidDataException($"String value: {enumAsString} at key: {keyName} is not a valid member of enum: {nameof(TEnum)}");
            }

            // Otherwise, return the default value instead
            return defaultValue;

            TEnum EnumFromInt(int value) => Unsafe.As<int, TEnum>(ref value); // Unsafe casting from int to TEnum
        }

        public static void SetAsJsonNode(this JsonNode? node, string keyName, JsonNode? jsonNode)
        {
            // If node is null, return and ignore
            if (node == null) return;

            // Get node as object
            JsonObject jsonObject = node.AsObject();

            // Set the value of the JsonNode
            SetValueOfJsonNode(node, keyName, jsonNode, jsonObject);
        }

        public static void SetNodeValue<TValue>(this JsonNode? node, string keyName, TValue value, JsonSerializerContext? context = null)
        {
            // If node is null, return and ignore
            if (node == null) return;

            // Get node as object
            JsonObject jsonObject = node.AsObject();

            // Create an instance of the JSON node value
            JsonValue? jsonValue = CreateJsonValue(value, context);

            // Set the value of the JsonNode
            SetValueOfJsonNode(node, keyName, jsonValue, jsonObject);
        }

        private static void SetValueOfJsonNode(JsonNode node, string keyName, JsonNode? jsonNode, JsonObject jsonObject)
        {
            // If the node has object, then assign the new value
            if (jsonObject.ContainsKey(keyName))
                node[keyName] = jsonNode;
            // Otherwise, add it
            else
                jsonObject.Add(new KeyValuePair<string, JsonNode?>(keyName, jsonNode));
        }

        public static void SetNodeValueEnum<TEnum>(this JsonNode? node, string keyName, TEnum value, JsonEnumStoreType enumStoreType = JsonEnumStoreType.AsNumber)
            where TEnum : struct, Enum
        {
            // If node is null, return and ignore
            if (node == null) return;

            // Get node as object
            JsonObject jsonObject = node.AsObject();

            // Create an instance of the JSON node value
            JsonValue? jsonValue = enumStoreType switch
            {
                JsonEnumStoreType.AsNumber => AsEnumNumber(value),
                JsonEnumStoreType.AsString => AsEnumString(value),
                JsonEnumStoreType.AsNumberString => AsEnumNumberString(value),
                _ => throw new NotSupportedException($"Enum store type: {enumStoreType} is not supported!")
            };

            // If the node has object, then assign the new value
            if (jsonObject.ContainsKey(keyName))
                node[keyName] = jsonValue;
            // Otherwise, add it
            else
                jsonObject.Add(new KeyValuePair<string, JsonNode?>(keyName, jsonValue));
            return;

            JsonValue AsEnumNumber(TEnum v)
            {
                int enumAsNumber = Unsafe.As<TEnum, int>(ref v);
                return JsonValue.Create(enumAsNumber);
            }

            JsonValue? AsEnumString(TEnum v)
            {
                string? enumName = Enum.GetName(v);
                return JsonValue.Create(enumName);
            }

            JsonValue AsEnumNumberString(TEnum v)
            {
                int enumAsNumber = Unsafe.As<TEnum, int>(ref v);
                string enumAsNumberString = $"{enumAsNumber}";
                return JsonValue.Create(enumAsNumberString);
            }
        }

        private static JsonValue? CreateJson<TDynamic>(TDynamic dynamicValue, JsonSerializerContext context)
        {
            JsonTypeInfo jsonTypeInfo = context.GetTypeInfo(typeof(TDynamic))
                ?? throw new NotSupportedException($"Context does not include a JsonTypeInfo<T> of type {nameof(TDynamic)}");
            JsonTypeInfo<TDynamic> jsonTypeInfoT = (JsonTypeInfo<TDynamic>)jsonTypeInfo;
            return JsonValue.Create(dynamicValue, jsonTypeInfoT);
        }

        private static JsonValue? CreateJsonValue<TValue>(TValue value, JsonSerializerContext? context)
            => value switch
        {
            bool vBool => JsonValue.Create(vBool),
            byte vByte => JsonValue.Create(vByte),
            sbyte vSbyte => JsonValue.Create(vSbyte),
            short vShort => JsonValue.Create(vShort),
            char vChar => JsonValue.Create(vChar),
            int vInt => JsonValue.Create(vInt),
            uint vUint => JsonValue.Create(vUint),
            long vLong => JsonValue.Create(vLong),
            ulong vUlong => JsonValue.Create(vUlong),
            float vFloat => JsonValue.Create(vFloat),
            double vDouble => JsonValue.Create(vDouble),
            decimal vDecimal => JsonValue.Create(vDecimal),
            string vString => JsonValue.Create(vString),
            DateTime vDatetime => JsonValue.Create(vDatetime),
            DateTimeOffset vDatetimeOffset => JsonValue.Create(vDatetimeOffset),
            Guid vGuid => JsonValue.Create(vGuid),
            JsonElement vJsonElement => JsonValue.Create(vJsonElement),
            _ => CreateJson(value, context ?? throw new NotSupportedException("You cannot pass a null context while setting a non-struct value to JsonValue"))
        };
    }

    internal class MagicNodeBaseValues<T> : IGameSettingsValueMagic<T>
        where T : MagicNodeBaseValues<T>, new()
    {
        [JsonIgnore]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public byte[] Magic { get; protected set; }

        [JsonIgnore]
        protected SettingsGameVersionManager GameVersionManager { get; set; }

        [JsonIgnore]
        public JsonNode? SettingsJsonNode { get; protected set; }

        [JsonIgnore]
        public JsonTypeInfo<T?> TypeInfo { get; protected set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


        [Obsolete("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!", true)]
        public static T Load() => throw new NotSupportedException("Loading settings with Load() is not supported for IGameSettingsValueMagic<T> member. Use LoadWithMagic() instead!");

        public static T LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager, JsonTypeInfo<T?> typeInfo)
        {
            if (magic == null || magic.Length == 0)
                throw new NullReferenceException("Magic cannot be an empty array!");

            try
            {
                string filePath = versionManager.ConfigFilePath;

                if (!File.Exists(filePath)) throw new FileNotFoundException("MagicNodeBaseValues config file not found!");
                string raw = Sleepy.ReadString(filePath, magic);

#if DEBUG
                Logger.LogWriteLine($"RAW MagicNodeBaseValues Settings: {filePath}\r\n" +
                             $"{raw}", LogType.Debug, true);
#endif
                JsonNode? node = raw.DeserializeAsJsonNode();
                T data = new T();
                data.InjectNodeAndMagic(node, magic, versionManager, typeInfo);
                return data;
            }
            catch (Exception ex)
            {
                if (ex is not FileNotFoundException)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Failed to parse MagicNodeBaseValues settings\r\n{ex}", LogType.Error, true);
                }
                else
                {
                    Logger.LogWriteLine("Magic file is not found, returning default values!");
                }
                return DefaultValue(magic, versionManager, typeInfo);
            }
        }

        private static T DefaultValue(byte[] magic, SettingsGameVersionManager versionManager, JsonTypeInfo<T?> typeInfo)
        {
            // Generate dummy data
            T data = new T();

            // Generate raw JSON string
            string rawJson = data.Serialize(typeInfo, false);

            // Deserialize it back to JSON Node and inject
            // the node and magic
            JsonNode? defaultJsonNode = rawJson.DeserializeAsJsonNode();
            data.InjectNodeAndMagic(defaultJsonNode, magic, versionManager, typeInfo);

            // Return
            return data;
        }

        public void Save()
        {
            // Get the file and dir path
            string filePath = GameVersionManager.ConfigFilePath;
            string? fileDirPath = Path.GetDirectoryName(filePath);

            // Create the dir if not exist
            if (string.IsNullOrEmpty(fileDirPath) && !Directory.Exists(fileDirPath))
                Directory.CreateDirectory(fileDirPath!);

            // Write into the file
            string jsonString = SettingsJsonNode.SerializeJsonNode(TypeInfo, false);
            Sleepy.WriteString(filePath, jsonString, Magic);
        }

        public override bool Equals(object? obj)
        {
            if (obj is T other)
                return Equals(other);

            return false;
        }

        public override int GetHashCode() => SettingsJsonNode?.GetHashCode() ?? 0;

        public bool Equals(T? other) => JsonNode.DeepEquals(SettingsJsonNode, other?.SettingsJsonNode);

        protected virtual void InjectNodeAndMagic(JsonNode? jsonNode, byte[] magic, SettingsGameVersionManager versionManager, JsonTypeInfo<T?> typeInfo)
        {
            SettingsJsonNode = jsonNode;
            GameVersionManager = versionManager;
            Magic = magic;
            TypeInfo = typeInfo;
        }
    }
}
