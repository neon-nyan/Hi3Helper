using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Hi3Helper.SentryHelper;

namespace CollapseLauncher
{
    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(CommunityToolsProperty))]
    internal sealed partial class CommunityToolsPropertyJsonContext : JsonSerializerContext;

    public class CommunityToolsProperty
    {
        public Dictionary<GameNameType, List<CommunityToolsEntry>> OfficialToolsDictionary { get; set; }
        public Dictionary<GameNameType, List<CommunityToolsEntry>> CommunityToolsDictionary { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public ObservableCollection<CommunityToolsEntry> OfficialToolsList = [];
        [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
        public ObservableCollection<CommunityToolsEntry> CommunityToolsList = [];

        public void Clear()
        {
            OfficialToolsList.Clear();
            CommunityToolsList.Clear();
        }

        public static async Task<CommunityToolsProperty> LoadCommunityTools(Stream fileStream)
        {
            try
            {
                CommunityToolsProperty communityToolkitProperty = await fileStream.DeserializeAsync(CommunityToolsPropertyJsonContext.Default.CommunityToolsProperty);
                ResolveCommunityToolkitFontAwesomeGlyph(communityToolkitProperty?.OfficialToolsDictionary);
                ResolveCommunityToolkitFontAwesomeGlyph(communityToolkitProperty?.CommunityToolsDictionary);
                return communityToolkitProperty;
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Error while parsing Community Tools list from the {(fileStream is FileStream fs ? $"FileStream: {fs.Name}" : "Stream")}\r\n{ex}", LogType.Error, true);
                return new CommunityToolsProperty();
            }
        }

        private static void ResolveCommunityToolkitFontAwesomeGlyph(Dictionary<GameNameType, List<CommunityToolsEntry>> dictionary)
        {
            // Get font paths
            string fontAwesomeSolidPath = FontCollections.FontAwesomeSolid.Source;
            string fontAwesomeRegularPath = FontCollections.FontAwesomeRegular.Source;
            string fontAwesomeBrandPath = FontCollections.FontAwesomeBrand.Source;

            // Enumerate key pairs
            foreach (KeyValuePair<GameNameType, List<CommunityToolsEntry>> keyPair in dictionary)
            {
                // Skip if value list is null or empty
                if ((keyPair.Value?.Count ?? 0) == 0)
                    continue;

                // Enumerate list
                foreach (CommunityToolsEntry entry in keyPair.Value)
                {
                    // Get the last index of font namespace. If none was found, then skip
                    int lastIndexOfNamespace = entry.IconFontFamily.LastIndexOf("#", StringComparison.Ordinal);
                    if (lastIndexOfNamespace == -1)
                        continue;

                    // Get the font path as its base only
                    ReadOnlySpan<char> currentEntryFontPath = entry.IconFontFamily.AsSpan(0, lastIndexOfNamespace).TrimEnd('/');

                    // Check if the path has Solid font-family.
                    if (fontAwesomeSolidPath.AsSpan().StartsWith(currentEntryFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.IconFontFamily = fontAwesomeSolidPath;
                        continue;
                    }

                    // Check if the path has Regular font-family
                    if (fontAwesomeRegularPath.AsSpan().StartsWith(currentEntryFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.IconFontFamily = fontAwesomeRegularPath;
                        continue;
                    }

                    // Check if the path has Brands font-family
                    if (fontAwesomeBrandPath.AsSpan().StartsWith(currentEntryFontPath, StringComparison.OrdinalIgnoreCase))
                    {
                        entry.IconFontFamily = fontAwesomeBrandPath;
                    }
                }
            }
        }
    }

    public class CommunityToolsEntry
    {
        public string IconFontFamily { get; set; }
        public string IconGlyph { get; set; }
        public string Text { get; set; }
        public string URL { get; set; }
        public List<string> Profiles { get; set; }
    }
}
