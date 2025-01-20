using Hi3Helper.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using WinRT;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
#if !APPLYUPDATE
using static Hi3Helper.Shared.Region.LauncherConfig;
#else
using ApplyUpdate;
using Avalonia.Platform;
using System.Linq;
using System.Reflection;
using static ApplyUpdate.Statics;
#endif

namespace Hi3Helper
{
    public struct LangMetadata
    {
        public LangMetadata(string filePath, int index)
        {
            this.LangFilePath = filePath;
            this.LangIndex = index;
            this.LangIsLoaded = false;

            ReadOnlySpan<char> langRelativePath = filePath.AsSpan().Slice(AppLangFolder!.Length + 1);

            try
            {
                _ = LoadLangBase(filePath);
                LogWriteLine($"Locale file: {langRelativePath} loaded as {this.LangName} by {this.LangAuthor}", LogType.Scheme, true);
            }
            catch (Exception ex)
            {
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while parsing locale file: {langRelativePath}. Ignoring!\r\n{ex}", LogType.Warning, true);
            }
        }

#if APPLYUPDATE
        public LangMetadata(Uri fileUri, int index)
        {
            this.LangFilePath = fileUri.AbsoluteUri;
            this.LangIndex = index;
            this.LangIsLoaded = false;

            try
            {
                _ = LoadLang(fileUri);
                LogWriteLine($"Locale file: {fileUri.AbsoluteUri} loaded as {this.LangName} by {this.LangAuthor}", LogType.Scheme, true);
            }
            catch (Exception e)
            {
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
                LogWriteLine($"Failed while parsing locale file: {fileUri.AbsoluteUri}. Ignoring!\r\n{e}", LogType.Warning, true);
            }
        }
#endif

        public LocalizationParams LoadLang()
        {
#if APPLYUPDATE
            return LoadLang(new Uri(LangFilePath));
#else
            using (Stream s = new FileStream(this.LangFilePath!, FileMode.Open, FileAccess.Read))
            {
                return LoadLang(s);
            }
#endif
        }

        public LocalizationParamsBase LoadLangBase(string langPath)
        {
            using (Stream s = new FileStream(langPath!, FileMode.Open, FileAccess.Read))
            {
                return LoadLangBase(s);
            }
        }

#if APPLYUPDATE
        public LocalizationParamsBase LoadLangBase(Uri langUri)
        {
            using (Stream s = AssetLoader.Open(langUri))
            {
                return LoadLangBase(s);
            }
        }
#endif

        public LocalizationParams LoadLang(Stream langStream)
        {
            LocalizationParams _langData = JsonSerializer.Deserialize(langStream!, CoreLibraryFieldsJsonContext.Default.LocalizationParams);
            this.LangAuthor = _langData!.Author;
            this.LangID = _langData.LanguageID.ToLower();
            this.LangName = _langData.LanguageName;
            this.LangIsLoaded = true;

            return _langData;
        }

        public LocalizationParamsBase LoadLangBase(Stream langStream)
        {
            LocalizationParamsBase _langData = JsonSerializer.Deserialize(langStream!, CoreLibraryFieldsJsonContext.Default.LocalizationParamsBase);
            this.LangAuthor = _langData!.Author;
            this.LangID = _langData.LanguageID.ToLower();
            this.LangName = _langData.LanguageName;
            this.LangIsLoaded = true;

            return _langData;
        }

        public int LangIndex;
        public string LangFilePath;
        public string LangAuthor;
        public string LangID;
        public string LangName;
        public bool LangIsLoaded;
    }

    [GeneratedBindableCustomProperty]
    public sealed partial class Locale
    {
        public const string FallbackLangID = "en-us";

#if APPLYUPDATE
        public static IEnumerable<Uri> GetLocaleUri()
        {
            Assembly thisAssembly = Assembly.GetExecutingAssembly();
            AssetLoader.SetDefaultAssembly(thisAssembly);
            foreach (Uri asset in AssetLoader
                .GetAssets(new Uri($"avares://ApplyUpdate-Core/Assets/Locale"), null)
                .Where(x => x.AbsoluteUri
                    .EndsWith(".json", StringComparison.InvariantCultureIgnoreCase)))
                yield return asset;
        }
#endif

        public static void InitializeLocale()
        {
            int i = 0;
#if APPLYUPDATE
            foreach (Uri langPath in GetLocaleUri())
            {
                LangMetadata Metadata = new LangMetadata(langPath, i);
#else
            TrySafeRenameOldEnFallback();
            foreach (string langPath in Directory.EnumerateFiles(AppLangFolder!, "*.json", SearchOption.AllDirectories))
            {
                LangMetadata Metadata = new LangMetadata(langPath, i);
#endif
                if (Metadata.LangIsLoaded)
                {
                    LanguageNames!.Add(Metadata.LangID.ToLower(), Metadata);
                    LanguageIDIndex!.Add(Metadata.LangID);
                    i++;
                }
            }

            if (!LanguageNames!.ContainsKey(FallbackLangID))
            {
                throw new LocalizationNotFoundException($"Fallback locale file with ID: \"{FallbackLangID}\" doesn't exist!");
            }
        }

#if !APPLYUPDATE
        private static void TrySafeRenameOldEnFallback()
        {
            string possibleOldPath = Path.Combine(AppLangFolder!, "en.json");
            string possibleNewPath = Path.Combine(AppLangFolder!, "en-us.json");

            if (File.Exists(possibleOldPath) && File.Exists(possibleNewPath)) File.Delete(possibleOldPath);
            if (File.Exists(possibleOldPath) && !File.Exists(possibleNewPath)) File.Move(possibleOldPath, possibleNewPath);
        }
#endif

        public static void LoadLocale(string langID)
        {
            try
            {
                LangFallback = LanguageNames![FallbackLangID].LoadLang();
                langID = langID!.ToLower();
                if (!LanguageNames.ContainsKey(langID))
                {
                    Lang = LanguageNames[FallbackLangID].LoadLang();
                    LogWriteLine($"Locale file with ID: {langID} doesn't exist! Fallback locale will be loaded instead.", LogType.Warning, true);
                    return;
                }

                Lang = LanguageNames[langID].LoadLang();
                TryLoadSizePrefix(Lang);
            }
            catch (Exception ex)
            {
                SentryHelper.SentryHelper.ExceptionHandler(ex, SentryHelper.SentryHelper.ExceptionType.UnhandledOther);
                throw new LocalizationInnerException($"Failed while loading locale with ID {langID}!", ex);
            }
            finally
            {
                LangFallback = null;
            }
        }

        private static void TryLoadSizePrefix(LocalizationParams langData)
        {
            if (string.IsNullOrEmpty(langData._Misc.SizePrefixes1000U))
            {
                LogWriteLine($"Locale file with ID: {langData.LanguageID} doesn't have size prefix value! The size prefix will not be changed!.", LogType.Warning, true);
                return;
            }

            ReadOnlySpan<char> speedPrefixSpan = langData._Misc.SizePrefixes1000U.AsSpan();
            Span<Range> spanRange = stackalloc Range[32];
            int rangesCount = speedPrefixSpan.Split(spanRange, '|', StringSplitOptions.RemoveEmptyEntries);

            if (rangesCount != 9)
            {
                LogWriteLine($"Locale file with ID: {langData.LanguageID} doesn't have a correct size prefix values! Make sure that the prefix value consists 9 values with '|' as its delimiter!", LogType.Warning, true);
                return;
            }

            string[] sizeSurfixes = new string[rangesCount];

            try
            {
                for (int i = 0; i < rangesCount; i++)
                    sizeSurfixes[i] = speedPrefixSpan[spanRange[i]].ToString();
            }
            catch (Exception ex)
            {
                SentryHelper.SentryHelper.ExceptionHandler(ex);
                LogWriteLine($"An error has occurred while parsing size prefix value for locale file with ID: {langData.LanguageID}!\r\n{ex}", LogType.Warning, true);
                return;
            }

            ConverterTool.SizeSuffixes = sizeSurfixes;
        }

        public static Dictionary<string, LangMetadata> LanguageNames = new Dictionary<string, LangMetadata>();
        public static List<string> LanguageIDIndex = new List<string>();
        public static LocalizationParams Lang;
#nullable enable
        public static LocalizationParams? LangFallback;

        [GeneratedBindableCustomProperty]
        public partial class LocalizationParamsBase
        {
            public string LanguageName { get; set; } = "";
            public string LanguageID { get; set; } = "";
            public string Author { get; set; } = "Unknown";
        }

        [GeneratedBindableCustomProperty]
        public sealed partial class LocalizationParams : LocalizationParamsBase;
    }

    public class LocalizationNotFoundException : Exception
    {
        public LocalizationNotFoundException(string message)
            : base(message) { }
    }

    public class LocalizationInnerException : Exception
    {
        public LocalizationInnerException(string message, Exception ex)
            : base(message, ex) { }
    }
}
