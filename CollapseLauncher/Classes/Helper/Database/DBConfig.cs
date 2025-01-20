using Hi3Helper.Data;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CollapseLauncher.Helper.Database
{
    public static class DbConfig
    {
        #region Properties

        private static readonly string ConfigFolder = LauncherConfig.AppDataFolder;

        private const string ConfigFileName = "dbConfig.ini";
        private const string DbSectionName   = "database";

        private static readonly string ConfigPath = Path.Combine(ConfigFolder, ConfigFileName);

        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static IniFile _config = new();
        #endregion

        public static void Init()
        {
            EnsureConfigExist();
            Load();
            if (!_config.ContainsKey(DbSectionName))
                _config.Add(DbSectionName, DbSettingsTemplate);

            DefaultChecker();
        }

        private static void EnsureConfigExist()
        {
            if (File.Exists(ConfigPath)) return;

            var f = File.Create(ConfigPath);
            f.Close();
        }

        private static void DefaultChecker()
        {
            foreach (KeyValuePair<string, IniValue> entry in DbSettingsTemplate.Where(bool (entry) => !_config[DbSectionName].ContainsKey(entry.Key)
                                                                                                      || string.IsNullOrEmpty(_config[DbSectionName][entry.Key])))
            {
                SetValue(entry.Key, entry.Value);
            }
        }

        private static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                _config.Load(ConfigPath);
            }
        }

        private static void Save() => _config.Save(ConfigPath);
        
        public static IniValue GetConfig(string key) => _config[DbSectionName][key];

        private static void SetValue(string key, IniValue value) => _config[DbSectionName]![key] = value;

        public static void SetAndSaveValue(string key, IniValue value)
        {
            SetValue(key, value);
            Save();
        }

        #region Template
        private static readonly Dictionary<string, IniValue> DbSettingsTemplate = new()
        {
            { "enabled", false },
            { "url", "" },
            { "token", "" },
            { "userGuid", "" }
        };
        #endregion

        public static bool DbEnabled
        {
            get => GetConfig("enabled");
            set => SetAndSaveValue("enabled", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string DbUrl
        {
            get => GetConfig("url");
            set => SetAndSaveValue("url", value);
        }
        
        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string DbToken
        {
            get => GetConfig("token");
            set => SetAndSaveValue("token", value);
        }

        [DebuggerHidden]
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public static string UserGuid
        {
            get
            {
                var  c = GetConfig("userGuid");

                if (!string.IsNullOrEmpty(c)) return c; // Return early if config is set

                c = Guid.CreateVersion7().ToString();
                SetAndSaveValue("userGuid", c);

                return c;
            }
            set => SetAndSaveValue("userGuid", value);
        }
    }
}