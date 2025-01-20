using Hi3Helper.EncTool.Parser.Sleepy.JsonConverters;
using System.Text.Json.Serialization;
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    internal class ZenlessResManifestAsset
    {
        [JsonPropertyName("remoteName")]
        public string FileRelativePath { get; set; }

        [JsonPropertyName("md5")] // "mD5" they said. BROO, IT'S A F**KING XXH64 HASH!!!!
        [JsonConverter(typeof(NumberStringToXxh64HashBytesConverter))] // AND THEY STORED IT AS A NUMBER IN A STRING WTFF??????
        public byte[] Xxh64Hash { get; set; } // classic

        [JsonPropertyName("fileSize")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public long FileSize { get; set; }

        [JsonPropertyName("isPatch")]
        public bool IsPersistentFile { get; set; }

        [JsonPropertyName("tags")]
        public int[] Tags { get; set; }
    }
}
