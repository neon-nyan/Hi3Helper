using CollapseLauncher.Helper.Loading;
using CollapseLauncher.Helper.Metadata;
using Hi3Helper;
using Hi3Helper.Data;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.MainEntryPoint;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

namespace CollapseLauncher.ShortcutUtils
{
    public sealed class SteamShortcut
    {
        #region Shortcut fields
        // We don't care about any other fields.
        public readonly uint AppID;
        public readonly string AppName;
        public readonly string Exe;
        public readonly string StartDir;
        public readonly string Icon;
        public readonly string LaunchOptions;
        #endregion

        private readonly string _path;
        private readonly PresetConfig _preset;

        internal SteamShortcut(string path, PresetConfig preset, bool play = false)
        {
            _path = Path.GetDirectoryName(path);
            _preset = preset;

            var translatedGameTitle =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.GameName,
                                                                        Lang._GameClientTitles)!;
            var translatedGameRegion =
                InnerLauncherConfig.GetGameTitleRegionTranslationString(preset.ZoneName,
                                                                        Lang._GameClientRegions);
            AppName = $"{translatedGameTitle} - {translatedGameRegion}";

            var stubPath = FindCollapseStubPath();
            Exe = $"\"{stubPath}\"";
            StartDir = $"{Path.GetDirectoryName(stubPath)}";

            var appNameInternal = $"{preset.GameName} - {preset.ZoneName}";
            AppID = GenerateAppId(Exe, appNameInternal);

            var gridPath = Path.Combine(_path!, "grid");
            var iconName = ShortcutCreator.GetIconName(_preset.GameType);
            Icon = Path.Combine(gridPath, iconName);

            LaunchOptions = $"open -g \"{preset.GameName}\" -r \"{preset.ZoneName}\"";
            if (play) LaunchOptions += " -p";
        }

        private static uint GenerateAppId(string exe, string appName)
        {
            // Actually the appid generation algorithm for custom apps has been changed.
            // It's now a completely random number instead of the crc32.
            // But to be able to track the target app, we still use crc32 as appid.
            var crc32 = new Crc32();
            crc32.Append(Encoding.UTF8.GetBytes(exe + appName));
            return BitConverter.ToUInt32(crc32.GetCurrentHash()) | 0x80000000;
        }

        internal async Task MoveImages(CancellationToken token)
        {
            if (!string.IsNullOrEmpty(_path)) Directory.CreateDirectory(_path);

            var gridPath = Path.Combine(_path!, "grid");
            if (!Directory.Exists(gridPath)) Directory.CreateDirectory(gridPath);

            var iconName = ShortcutCreator.GetIconName(_preset.GameType);
            var iconAssetPath = Path.Combine(Path.GetDirectoryName(AppExecutablePath)!, @"Assets\Images\GameIcon\" + iconName);

            if (!Path.Exists(Icon) && Path.Exists(iconAssetPath))
            {
                File.Copy(iconAssetPath, Icon);
                LogWriteLine($"[SteamShortcut::MoveImages] Copied icon from {iconAssetPath} to {Icon}.");
            }

            await CacheImages(token);
            if (token.IsCancellationRequested) return;

            // Game background
            CopyImageFromCache(gridPath, "_hero");

            // Game logo
            CopyImageFromCache(gridPath, "_logo");

            // Vertical banner
            // Shows when viewing all games of category or in the Home page
            CopyImageFromCache(gridPath, "p");

            // Horizontal banner
            // Appears in Big Picture mode when the game is the most recently played
            CopyImageFromCache(gridPath, "");
        }

        private void CopyImageFromCache(string gridPath, string steamSuffix)
        {
            try
            {
                File.Copy(Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix),
                          Path.Combine(gridPath, AppID + steamSuffix + ".png"), true);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task CacheImages(CancellationToken token)
        {
            Dictionary<string, SteamGameProp> assets = _preset.ZoneSteamAssets;
            if (assets == null) return;

            (string, string)[] images =
            [
                ("Hero", "_hero"),
                ("Logo", "_logo"),
                ("Banner", "p"),
                ("Preview", "")
            ];
            List<(string, string)> cacheImageList = [];

            for (var index = images.Length - 1; index >= 0; index--)
            {
                var image       = images[index];
                var asset       = assets[image.Item1];
                var steamSuffix = image.Item2;

                var cachePath = Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix);
                var hash      = MD5Hash(cachePath);
                if (!hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase))
                    cacheImageList.Add(image);
            }

            if (cacheImageList.Count == 0) return;

            LoadingMessageHelper.ShowLoadingFrame();

            for (var i = 0; i < cacheImageList.Count; i++)
            {
                var image = cacheImageList[i];
                var progressString = string.Format(Lang._Dialogs.SteamShortcutDownloadingImages, i + 1, cacheImageList.Count);
                LoadingMessageHelper.SetMessage(Lang._Dialogs.SteamShortcutTitle, progressString);
                await CacheImageFromUrl(assets[image.Item1], image.Item2, token);
            }

            LoadingMessageHelper.HideLoadingFrame();
        }

        private async Task CacheImageFromUrl(SteamGameProp asset, string steamSuffix, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;

            var cachePath = Path.Combine(AppGameImgCachedFolder, AppID + steamSuffix);

            var hash = MD5Hash(cachePath);
            if (hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase)) return;

            var cdnURL = FallbackCDNUtil.TryGetAbsoluteToRelativeCDNURL(asset.URL, "metadata/");

            for (var i = 0; i < 3; i++)
            {
                var info = new FileInfo(cachePath);
                await DownloadImage(info, cdnURL, token);

                if (token.IsCancellationRequested)
                {
                    File.Delete(cachePath);
                    return;
                }

                hash = MD5Hash(cachePath);

                if (hash.Equals(asset.MD5, StringComparison.OrdinalIgnoreCase)) return;

                File.Delete(cachePath);

                LogWriteLine($"[SteamShortcut::GetImageFromUrl] Invalid checksum for file {cachePath}! {hash} does not match {asset.MD5}.", LogType.Error);
            }

            LogWriteLine($"[SteamShortcut::GetImageFromUrl] After 3 tries, {cdnURL} could not be downloaded successfully.", LogType.Error);
        }

        private static async ValueTask DownloadImage(FileInfo fileInfo, string url, CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(4 << 10);
            try
            {
                // Try to get the remote stream and download the file
                await using BridgedNetworkStream netStream = await FallbackCDNUtil.GetHttpStreamFromResponse(url, token);
                await using FileStream outStream = fileInfo.Open(new FileStreamOptions
                {
                    Access = FileAccess.Write,
                    Mode = FileMode.Create,
                    Share = FileShare.ReadWrite
                });

                // Get the file length
                var fileLength = netStream.Length;

                // Copy (and download) the remote streams to local
                LogWriteLine($"Start downloading resource from: {url}", LogType.Default, true);
                int read;
                while ((read = await netStream.ReadAsync(buffer, token)) > 0)
                    await outStream.WriteAsync(buffer.AsMemory(0, read), token);

                LogWriteLine($"Downloading resource from: {url} has been completed and stored locally into:"
                             + $"\"{fileInfo.FullName}\" with size: {ConverterTool.SummarizeSizeSimple(fileLength)} ({fileLength} bytes)",
                             LogType.Default, true);
            }
            catch (TaskCanceledException)
            {
                // ignored
            }
            catch (Exception ex)
            {
                ErrorSender.SendException(ex, ErrorType.Connection);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
