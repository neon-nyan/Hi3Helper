using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Database;
using CollapseLauncher.Interfaces;
using Hi3Helper;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;
using static Hi3Helper.Logger;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static CollapseLauncher.InnerLauncherConfig;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher.GamePlaytime
{
    internal sealed partial class Playtime : IGamePlaytime
    {
        #region Properties
        public event EventHandler<CollapsePlaytime> PlaytimeUpdated;
#nullable enable
        public CollapsePlaytime CollapsePlaytime { get; private set; }

        private static readonly HashSet<int>      ActiveSessions = []; 
        private                 RegistryKey?      _registryRoot;
        private readonly        IGameVersionCheck _gameVersionManager;

        private readonly CancellationTokenSourceWrapper _token = new();
        #endregion

        public Playtime(IGameVersionCheck gameVersionManager, IGameSettings gameSettings)
        {
            string        registryPath = Path.Combine($"Software\\{gameVersionManager.VendorTypeProp.VendorType}", gameVersionManager.GamePreset.InternalGameNameInConfig!);
            _registryRoot = Registry.CurrentUser.OpenSubKey(registryPath, true);

            _registryRoot ??= Registry.CurrentUser.CreateSubKey(registryPath, true, RegistryOptions.None);

            _gameVersionManager = gameVersionManager;

            CollapsePlaytime = CollapsePlaytime.Load(_registryRoot,
                                              _gameVersionManager.GamePreset.HashID,
                                              _gameVersionManager,
                                              gameSettings);
            
            
            if (DbHandler.IsEnabled && gameSettings.AsIGameSettingsUniversal().SettingsCollapseMisc.IsSyncPlaytimeToDatabase)
                _ = CheckDb();
        }
#nullable disable

        public async Task CheckDb(bool redirectThrow = false)
        {
            var needUpdate = await CollapsePlaytime.DbSync(redirectThrow);
            if (needUpdate is not { IsUpdated: true, PlaytimeData: not null }) return;

            CollapsePlaytime = needUpdate.PlaytimeData;
            PlaytimeUpdated?.Invoke(this, CollapsePlaytime);
        }

        public void Update(TimeSpan timeSpan, bool forceUpdateDb = false)
        {
            TimeSpan oldTimeSpan = CollapsePlaytime.TotalPlaytime;

            CollapsePlaytime.Update(timeSpan, true, true);
            PlaytimeUpdated?.Invoke(this, CollapsePlaytime);

            LogWriteLine($"Playtime counter changed to {TimeSpanToString(timeSpan)}. (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public void Reset()
        {
            TimeSpan oldTimeSpan = CollapsePlaytime.TotalPlaytime;

            CollapsePlaytime.Reset();
            PlaytimeUpdated?.Invoke(this, CollapsePlaytime);

            LogWriteLine($"Playtime counter was reset! (Previous value: {TimeSpanToString(oldTimeSpan)})", writeToLog: true);
        }

        public async void StartSession(Process proc, DateTime? begin = null)
        {
            int hashId = _gameVersionManager.GamePreset.HashID;

            // If a playtime HashSet has already tracked, then return (do not track the playtime more than once)
            // Otherwise, add it to track list
            if (!ActiveSessions.Add(hashId)) return;

            begin ??= DateTime.Now;

            TimeSpan initialTimeSpan = CollapsePlaytime.TotalPlaytime;

            CollapsePlaytime.LastPlayed  = begin;
            CollapsePlaytime.LastSession = TimeSpan.Zero;
            CollapsePlaytime.Save();
            PlaytimeUpdated?.Invoke(this, CollapsePlaytime);

#if DEBUG
            LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - Started session at {begin.Value.ToLongTimeString()}.");
#endif
            int elapsedSeconds = 0;

            using (var inGameTimer = new Timer())
            {
                inGameTimer.Interval = 60000;
                inGameTimer.Elapsed += (_, _) => 
                {
                    elapsedSeconds += 60;
                    DateTime now = DateTime.Now;

                    CollapsePlaytime.AddMinute();
                    PlaytimeUpdated?.Invoke(this, CollapsePlaytime);
#if DEBUG
                    LogWriteLine($"{_gameVersionManager.GamePreset.ProfileName} - {elapsedSeconds}s elapsed. ({now.ToLongTimeString()})");
#endif
                };

                inGameTimer.Start();
                await proc.WaitForExitAsync(_token.Token);
                inGameTimer.Stop();
            }

            DateTime end = DateTime.Now;
            double totalElapsedSeconds = (end - begin.Value).TotalSeconds;
            if (totalElapsedSeconds < 0)
            {
                LogWriteLine($"[HomePage::StartPlaytimeCounter] Date difference cannot be lower than 0. ({totalElapsedSeconds}s)", LogType.Error);
                Dialog_InvalidPlaytime(m_mainPage?.Content, elapsedSeconds);
                totalElapsedSeconds = elapsedSeconds;
            }

            TimeSpan totalTimeSpan = TimeSpan.FromSeconds(totalElapsedSeconds);
            LogWriteLine($"Added {totalElapsedSeconds}s [{totalTimeSpan.Hours}h {totalTimeSpan.Minutes}m {totalTimeSpan.Seconds}s] " +
                         $"to {_gameVersionManager.GamePreset.ProfileName} playtime.", LogType.Default, true);
            
            CollapsePlaytime.Update(initialTimeSpan.Add(totalTimeSpan), false, true);
            PlaytimeUpdated?.Invoke(this, CollapsePlaytime);

            ActiveSessions.Remove(hashId);
        }

        private static string TimeSpanToString(TimeSpan timeSpan) => $"{timeSpan.Days * 24 + timeSpan.Hours}h {timeSpan.Minutes}m";

        public void Dispose()
        {
            _token.Cancel();
            CollapsePlaytime.Save(true);
            CollapsePlaytime.LastDbUpdate = DateTime.MinValue;
            _registryRoot           = null;
        }
    }
}
