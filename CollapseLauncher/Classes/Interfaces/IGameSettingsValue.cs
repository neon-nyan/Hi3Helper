using CollapseLauncher.GameSettings.Base;
using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

namespace CollapseLauncher.Interfaces
{
    internal interface IGameSettingsValue<out T>
    {
        static abstract T Load();
        void Save();
    }

    internal interface IGameSettingsValueMagic<T> : IGameSettingsValue<T>
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        byte[] Magic { get; }

#nullable enable
        static abstract T LoadWithMagic(byte[] magic, SettingsGameVersionManager versionManager, JsonTypeInfo<T?> typeInfo);
    }
}
