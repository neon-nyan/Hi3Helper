using CollapseLauncher.GameSettings.Base;
using CollapseLauncher.GameSettings.Zenless.Context;
using CollapseLauncher.GameVersioning;
using CollapseLauncher.Interfaces;
using System;
using System.Diagnostics;

// ReSharper disable CheckNamespace

namespace CollapseLauncher.GameSettings.Zenless
{
    internal class ZenlessSettings : SettingsBase
    {
        #region Variables
#nullable enable
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[]? _magicReDo;
#nullable restore
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private byte[] MagicReDo
        {
            get
            {
                if (_magicReDo != null) return _magicReDo;
                _magicReDo = (GameVersionManager as GameTypeZenlessVersion)?.GamePreset
                                                                             .GetGameDataTemplate("ImSleepin", [1, 0, 0, 0
                                                                             ]);
                if (_magicReDo == null || _magicReDo.Length == 0)
                    throw new NullReferenceException("MagicReDo value for ZZZ settings is empty!");
                return _magicReDo;
            }
        }
        #endregion

        #region Properties
        public GeneralData GeneralData { get; set; }
        #endregion

        public ZenlessSettings(IGameVersionCheck GameVersionManager) : base(GameVersionManager)
        {
            // Initialize magic
            _ = MagicReDo;

            InitializeSettings();
        }

        public sealed override void InitializeSettings()
        {
            const string ZZZSettingsConfigFile = @"{0}_Data\Persistent\LocalStorage\{1}";

            base.InitializeSettings();

            SettingsScreen = ScreenManager.Load();
            GeneralData = GeneralData.LoadWithMagic(
                MagicReDo,
                SettingsGameVersionManager.Create(GameVersionManager, ZZZSettingsConfigFile, "GENERAL_DATA.bin"),
                ZenlessSettingsJsonContext.Default.GeneralData);
        }

        public override void ReloadSettings()
        {
            GeneralData.Dispose();
            InitializeSettings();
        }

        public override void SaveSettings()
        {
            base.SaveSettings();
            SettingsScreen.Save();
            GeneralData.Save();
        }

        public override IGameSettingsUniversal AsIGameSettingsUniversal() => this;
    }
}