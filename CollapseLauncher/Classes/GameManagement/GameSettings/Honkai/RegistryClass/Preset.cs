using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Interfaces;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using static CollapseLauncher.GameSettings.Base.SettingsBase;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.GameSettings
{
    internal class PresetConst
    {
        public const string DefaultPresetName = "Custom";
    }

    internal class Preset<T1, TObjectType> where T1 : IGameSettingsValue<T1>
    {
#nullable enable
        #region Fields
        private          string                  CurrentPresetName = PresetConst.DefaultPresetName;
        private readonly Dictionary<string, T1>? _Presets;
        #endregion

        #region Properties
        /// <summary>
        /// The preset of the given settings
        /// </summary>
        public Dictionary<string, T1>? Presets
        {
            get
            {
                return _Presets;
            }
            init
            {
                _Presets?.Clear();
                _Presets = value;
#nullable disable
                _Presets?.Add(PresetConst.DefaultPresetName, default);
#nullable enable
            }
        }

        /// <summary>
        /// The preset keys of the given settings
        /// </summary>
        public IList<string>? PresetKeys { get; init; }
        #endregion

        #region Methods
        public Preset(string presetJSONPath, JsonTypeInfo<Dictionary<string, T1>?> jsonType)
        {
            using FileStream fs = new FileStream(presetJSONPath, FileMode.Open, FileAccess.Read);
            Presets    = fs.Deserialize(jsonType);
            PresetKeys = GetPresetKeys();
        }

        /// <summary>
        /// Load the instance of the preset
        /// </summary>
        /// <param name="gameType">The type of the game</param>
        /// <param name="jsonType">JSON type information for deserialization</param>
        /// <returns>The instance of preset</returns>
        public static Preset<T1, TObjectType> LoadPreset(GameNameType gameType, JsonTypeInfo<Dictionary<string, T1>?> jsonType)
        {
            string presetPath = Path.Combine(AppExecutableDir, $@"Assets\Presets\{gameType}\", $"{typeof(T1).Name}.json");
            return new Preset<T1, TObjectType>(presetPath, jsonType);
        }

        /// <param name="key">The key of the preset</param>
        /// <returns>Returns a boolean to check whether the key exists. Otherwise, false if key doesn't exist or preset is <c>null</c></returns>
        public bool IsPresetKeyExist(string key) => Presets?.ContainsKey(key) ?? false;

        /// <param name="key">The key of the preset</param>
        /// <returns>Returns a value of the preset by its key</returns>
        /// <exception cref="KeyNotFoundException">When the key doesn't exist or preset is null</exception>
        public T1? GetPresetFromKey(string key)
        {
            T1? result = default;
            bool isResultOk = Presets?.TryGetValue(key, out result) ?? false;

            if (!isResultOk)
            {
                throw new KeyNotFoundException($"Preset key: {key} for {typeof(T1).Name} doesn't exist!");
            }

            return result;
        }

        /// <returns>Returns a <c>List-string</c> of the preset</returns>
        /// <exception cref="NullReferenceException"></exception>
        private List<string> GetPresetKeys()
        {
            if (Presets == null)
            {
                throw new NullReferenceException("Preset is null!");
            }

            return Presets.Keys.ToList();
        }
        
        /// <summary>
        /// Set the preset name based on equality of the given value with the preset. If it doesn't match, it will be set to <c>DefaultPresetName</c>
        /// </summary>
        /// <param name="value">The value to be compared with the preset</param>
        /// <exception cref="NullReferenceException">If <code>RegistryRoot</code> is null</exception>
        public void SetPresetKey(T1? value)
        {
            if (value != null)
            {
                KeyValuePair<string, T1>? foundPreset = Presets?.Where(x => x.Value.Equals(value)).FirstOrDefault();

                if (foundPreset.HasValue)
                {
                    CurrentPresetName = foundPreset.Value.Key;
                }
                return;
            }
            CurrentPresetName = PresetConst.DefaultPresetName;
        }

        /// <returns>Get the current preset key name. If it doesn't match, then return the <c>DefaultPresetName</c></returns>
        /// <exception cref="NullReferenceException">If <code>RegistryRoot</code> is null</exception>
        public string GetPresetKey()
        {
            string presetRegistryName = $"Preset_{typeof(T1).Name}";
            if (RegistryRoot == null) throw new NullReferenceException($"Cannot load preset name of {typeof(T1).Name} RegistryKey is unexpectedly not initialized!");

            string? value = (string?)RegistryRoot.GetValue(presetRegistryName, null);

            if (value != null)
            {
                KeyValuePair<string, T1>? foundPreset = Presets?.Where(x => x.Key == value).FirstOrDefault();

                if (foundPreset.HasValue)
                {
                    return foundPreset.Value.Key;
                }
            }

            return PresetConst.DefaultPresetName;
        }

        /// <summary>
        /// Save changes of the current preset name
        /// </summary>
        /// <exception cref="NullReferenceException"></exception>
        public void SaveChanges()
        {
            string presetRegistryName = $"Preset_{typeof(T1).Name}";
            if (RegistryRoot == null) throw new NullReferenceException($"Cannot save preset name of {typeof(T1).Name} since RegistryKey is unexpectedly not initialized!");

            RegistryRoot.SetValue(presetRegistryName, CurrentPresetName, RegistryValueKind.String);
        }
        #endregion
    }
}
