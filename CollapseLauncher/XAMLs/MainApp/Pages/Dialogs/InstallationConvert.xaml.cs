using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Metadata;
using CollapseLauncher.Statics;
using Hi3Helper;
using Hi3Helper.Data;
using Hi3Helper.Http;
using Hi3Helper.Http.Legacy;
using Hi3Helper.Win32.FileDialogCOM;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Data.ConverterTool;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher.Dialogs
{
    public partial class InstallationConvert : Page
    {
        private          string                   _sourceDataIntegrityURL;
        private          string                   _gameVersion;
        private          bool                     _isAlreadyConverted;
        private          PresetConfig             _sourceProfile;
        private          PresetConfig             _targetProfile;
        private          GameConversionManagement _converter;
        private          IniFile                  _sourceIniFile;
        private readonly CancellationTokenSource  _tokenSource = new();
        private          GamePresetProperty       CurrentGameProperty { get; }
        private readonly Stopwatch                _currentStopwatch = Stopwatch.StartNew();

        public InstallationConvert()
        {
            try
            {
                CurrentGameProperty = GamePropertyVault.GetCurrentGameProperty();
                InitializeComponent();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        public async void StartConversionProcess()
        {
            try
            {
                bool isAskContinue = true;
                while (isAskContinue)
                {
                    (_sourceProfile, _targetProfile) = await AskConversionDestination();
                    if (IsSourceGameExist(_sourceProfile))
                        isAskContinue = false;
                    else
                    {
                        await new ContentDialogCollapse(ContentDialogTheme.Error)
                        {
                            Title = Lang._InstallConvert.SelectDialogTitle,
                            Content = Lang._InstallConvert.SelectDialogSubtitleNotInstalled,
                            CloseButtonText = null,
                            PrimaryButtonText = Lang._Misc.Okay,
                            SecondaryButtonText = null,
                            DefaultButton = ContentDialogButton.Primary,
                            Background = UIElementExtensions.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                            XamlRoot = Content.XamlRoot
                        }.QueueAndSpawnDialog();
                    }
                }

                DoSetProfileDataLocation();
                await DoDownloadRecipe();
                await DoPrepareIngredients();

                CancelBtn.IsEnabled = false;
                await DoConversion();

                _isAlreadyConverted = true;
                CancelBtn.IsEnabled = true;
                await DoVerification();

                ApplyConfiguration();

                await new ContentDialogCollapse(ContentDialogTheme.Success)
                {
                    Title = Lang._InstallConvert.ConvertSuccessTitle,
                    Content = new TextBlock
                    {
                        Text = string.Format(Lang._InstallConvert.ConvertSuccessSubtitle, _sourceProfile.ZoneName, _targetProfile.ZoneName),
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText = null,
                    PrimaryButtonText = Lang._Misc.OkayBackToMenu,
                    SecondaryButtonText = null,
                    DefaultButton = ContentDialogButton.Primary,
                    Background = UIElementExtensions.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                    XamlRoot = Content.XamlRoot
                }.QueueAndSpawnDialog();

                OperationCancelled();
            }
            catch (TaskCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}");
                OperationCancelled();
            }
            catch (OperationCanceledException)
            {
                LogWriteLine($"Conversion process is cancelled for Game {CurrentGameProperty.GameVersion.GamePreset.ZoneFullname}");
                OperationCancelled();
            }
            catch (Exception ex)
            {
                RevertConversion();
                LogWriteLine($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(new Exception($"Conversion process has failed! But don't worry, the file have been reverted :D\r\n{ex}", ex));
            }
            finally
            {
                _converter?.Dispose();
            }
        }

        private void DoSetProfileDataLocation()
        {
            _sourceProfile.ActualGameDataLocation = NormalizePath(_sourceIniFile["launcher"]["game_install_path"].ToString());
            _targetProfile.ActualGameDataLocation = Path.Combine(Path.GetDirectoryName(_sourceProfile.ActualGameDataLocation) ?? "", $"{_targetProfile.GameDirectoryName}_ConvertedTo-{_targetProfile.ProfileName}");

            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 0;

                Step1.Opacity = 1f;
                Step1ProgressRing.IsIndeterminate = false;
                Step1ProgressRing.Value = 100;
                Step1ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private async Task<string> FetchDataIntegrityURL(PresetConfig profile)
        {
            // Initialize new proxy-aware HttpClient
            using HttpClient client = new HttpClientBuilder()
                .UseLauncherConfig()
                .SetAllowedDecompression(DecompressionMethods.None)
                .Create();

            // Use the new DownloadClient instance
            DownloadClient downloadClient = DownloadClient.CreateInstance(client);
            Dictionary<string, string> repoList;

            try
            {
                FallbackCDNUtil.DownloadProgress += Step2ProgressEvents;
                using MemoryStream s           = new MemoryStream();
                string             repoListURL = string.Format(AppGameRepoIndexURLPrefix, profile.ProfileName);
                await FallbackCDNUtil.DownloadCDNFallbackContent(downloadClient, s, repoListURL, _tokenSource.Token);
                s.Position = 0;
                repoList  = await s.DeserializeAsync(CoreLibraryJsonContext.Default.DictionaryStringString, token: _tokenSource.Token);
            }
            finally
            {
                FallbackCDNUtil.DownloadProgress -= Step2ProgressEvents;
            }

            RegionResourceProp entry = await FallbackCDNUtil.DownloadAsJSONType(profile.LauncherResourceURL, RegionResourcePropJsonContext.Default.RegionResourceProp, _tokenSource.Token);
            _gameVersion = entry.data?.game?.latest?.version;

            return repoList[_gameVersion ?? throw new InvalidOperationException()];
        }

        internal bool IsSourceGameExist(PresetConfig profile)
        {
            string iniPath = Path.Combine(AppGameFolder, profile.ProfileName ?? "", "config.ini");
            if (!File.Exists(iniPath))
                return false;

            _sourceIniFile = IniFile.LoadFrom(iniPath);
            try
            {
                var gamePath = NormalizePath(_sourceIniFile["launcher"]["game_install_path"].ToString());
                if (!Directory.Exists(gamePath))
                    return false;

                // Concat the vendor app info file and return if it doesn't exist
                string infoVendorPath = Path.Combine(gamePath, $"{Path.GetFileNameWithoutExtension(profile.GameExecutableName)}_Data\\app.info");
                if (!File.Exists(infoVendorPath)) return false;

                // If does, then process the file
                string[] infoEntries = File.ReadAllLines(infoVendorPath);
                if (infoEntries.Length < 2) return false;

                // Try parse the vendor name and internal game name. If parsing fail, then return false
                if (!Enum.TryParse(infoEntries[0], out GameVendorType vendorType)) return false;
                if (!(vendorType == _sourceProfile.VendorType && infoEntries[1] == _sourceProfile.InternalGameNameInConfig)) return false;

                // Try load the Version INI file
                string sourceIniVersionPath = Path.Combine(gamePath, "config.ini");
                if (!File.Exists(sourceIniVersionPath)) return false;
                IniFile sourceIniVersionFile = IniFile.LoadFrom(sourceIniVersionPath);

                // Check if the version value exist and matches
                if (!(sourceIniVersionFile.ContainsKey("General") && sourceIniVersionFile["General"].ContainsKey("game_version"))) return false;
                string localVersionString = sourceIniVersionFile["General"]["game_version"].ToString();
                if (string.IsNullOrEmpty(localVersionString)) return false;
                GameVersion localVersion = new GameVersion(localVersionString);
                GameVersion? remoteVersion = CurrentGameProperty.GameVersion.GetGameVersionApi();
                if (!localVersion.IsMatch(remoteVersion)) return false;

                var execPath = Path.Combine(gamePath, profile.GameExecutableName ?? "");
                if (!File.Exists(execPath))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        internal async Task<(PresetConfig, PresetConfig)> AskConversionDestination()
        {
            (ContentDialogResult result, ComboBox sourceGame, ComboBox targetGame) = await Dialog_SelectGameConvertRecipe(Content);
            PresetConfig sourceRet = null;
            PresetConfig targetRet = null;

            if (sourceGame.SelectedItem == null || targetGame.SelectedItem == null)
                throw new OperationCanceledException();

            string sourceGameRegionString = InnerLauncherConfig.GetComboBoxGameRegionValue(sourceGame.SelectedItem);
            string targetGameRegionString = InnerLauncherConfig.GetComboBoxGameRegionValue(targetGame.SelectedItem);

            switch (result)
            {
                case ContentDialogResult.Secondary:
                    if (LauncherMetadataHelper.CurrentMetadataConfigGameName != null)
                    {
                        sourceRet = LauncherMetadataHelper
                                   .LauncherMetadataConfig![LauncherMetadataHelper.CurrentMetadataConfigGameName].Values
                                   .Where(x => x.ZoneName == sourceGameRegionString)!.First();
                        targetRet = LauncherMetadataHelper
                                   .LauncherMetadataConfig![LauncherMetadataHelper.CurrentMetadataConfigGameName].Values
                                   .Where(x => x.ZoneName == targetGameRegionString)!.First();
                    }

                    break;
                case ContentDialogResult.Primary:
                    throw new OperationCanceledException();
                case ContentDialogResult.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return (sourceRet, targetRet);
        }

        public static List<string> GetConvertibleNameList(string zoneName)
        {
            List<string> outList = [];
            if (LauncherMetadataHelper.CurrentMetadataConfigGameName == null)
            {
                return outList;
            }

            List<string> gameTargetProfileName = LauncherMetadataHelper.LauncherMetadataConfig![LauncherMetadataHelper.CurrentMetadataConfigGameName].Values
                                                                       .Where(x => x.ZoneName == zoneName)
                                                                       .Select(x => x.ConvertibleTo)
                                                                       .First()!;

            for (var index = gameTargetProfileName.Count - 1; index >= 0; index--)
            {
                var entry = gameTargetProfileName[index];
                outList.Add(LauncherMetadataHelper
                           .LauncherMetadataConfig[LauncherMetadataHelper.CurrentMetadataConfigGameName].Values
                           .Where(x => x.ZoneName == entry)
                           .Select(x => x.ZoneName)
                           .First());
            }

            return outList;
        }

        private async Task DoDownloadRecipe()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 1;

                Step2.Opacity = 1f;
                Step2ProgressRing.IsIndeterminate = false;
                Step2ProgressRing.Value = 0;
                Step2ProgressStatus.Text = Lang._InstallConvert.Step2Subtitle;
            });

            _sourceDataIntegrityURL = await FetchDataIntegrityURL(_sourceProfile);

            bool isChosen = false;
            string cPath = null;
            while (!isChosen)
            {
                string fileName =
                    $"Cookbook_{_sourceProfile.ProfileName}_{_targetProfile.ProfileName}_{_gameVersion}_*_crc32.diff";
                switch (await Dialog_LocateDownloadedConvertRecipe(Content, fileName))
                {
                    case ContentDialogResult.Primary:
                        cPath = await FileDialogNative.GetFilePicker(
                            new Dictionary<string, string> { { string.Format(Lang._InstallConvert.CookbookFileBrowserFileTypeCategory, _sourceProfile.ProfileName, _targetProfile.ProfileName), fileName } });
                        isChosen = !string.IsNullOrEmpty(cPath);
                        break;
                    case ContentDialogResult.None:
                        throw new OperationCanceledException();
                }
            }

            _converter = new GameConversionManagement(_sourceProfile, _targetProfile, _sourceDataIntegrityURL, _gameVersion, cPath, _tokenSource.Token);

            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 1;

                Step2.Opacity = 1f;
                Step2ProgressRing.IsIndeterminate = false;
                Step2ProgressRing.Value = 100;
                Step2ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step2ProgressEvents(object sender, DownloadEvent e)
        {
            double speed = e.SizeDownloaded / _currentStopwatch.Elapsed.TotalSeconds;
            DispatcherQueue?.TryEnqueue(() =>
            {
                Step2ProgressStatus.Text = $"{e.ProgressPercentage}% - {string.Format(Lang._Misc.SpeedPerSec, SummarizeSizeSimple(speed))}";
                Step2ProgressRing.Value = e.ProgressPercentage;
            });
        }

        private async Task DoPrepareIngredients()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 2;

                Step3.Opacity = 1f;
                Step3ProgressRing.IsIndeterminate = false;
                Step3ProgressRing.Value = 0;
                Step3ProgressStatus.Text = Lang._InstallConvert.Step3Subtitle;
            });

            _converter.ProgressChanged += Step3ProgressEvents;
            await _converter.StartPreparation();
            _converter.ProgressChanged -= Step3ProgressEvents;

            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 2;

                Step3.Opacity = 1f;
                Step3ProgressRing.IsIndeterminate = false;
                Step3ProgressRing.Value = 100;
                Step3ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step3ProgressEvents(object sender, ConvertProgress e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                Step3ProgressRing.Value = e.Percentage;
                Step3ProgressTitle.Text = e.ProgressStatus;
                Step3ProgressStatus.Text = e.ProgressDetail;
            });
        }

        private async Task DoConversion()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 0;
                Step4ProgressStatus.Text = Lang._InstallConvert.Step4Subtitle;
            });

            _converter.ProgressChanged += Step4ProgressEvents;
            await _converter.StartConversion();
            _converter.ProgressChanged -= Step4ProgressEvents;

            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 3;

                Step4.Opacity = 1f;
                Step4ProgressRing.IsIndeterminate = false;
                Step4ProgressRing.Value = 100;
                Step4ProgressStatus.Text = Lang._Misc.Completed;
            });
        }

        private void Step4ProgressEvents(object sender, ConvertProgress e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                Step4ProgressRing.Value = e.Percentage;
                Step4ProgressStatus.Text = e.ProgressDetail;
            });
        }

        private async Task DoVerification()
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 4;

                Step5.Opacity = 1f;
                Step5ProgressRing.IsIndeterminate = false;
                Step5ProgressRing.Value = 0;
                Step5ProgressStatus.Text = Lang._InstallConvert.Step5Subtitle;
            });

            _converter.ProgressChanged += Step5ProgressEvents;
            await _converter.PostConversionVerify();
            _converter.ProgressChanged -= Step5ProgressEvents;

            DispatcherQueue?.TryEnqueue(() =>
            {
                ProgressSlider.Value = 4;

                Step5.Opacity = 1f;
                Step5ProgressRing.IsIndeterminate = false;
                Step5ProgressRing.Value = 100;
                Step5ProgressStatus.Text = "Completed!";
            });
        }

        private void Step5ProgressEvents(object sender, ConvertProgress e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                Step5ProgressRing.Value = e.Percentage;
                Step5ProgressStatus.Text = e.ProgressDetail;
            });
        }

        public void ApplyConfiguration()
        {
            // CurrentGameProperty._GameVersion.GamePreset = TargetProfile;
            CurrentGameProperty.GameVersion.Reinitialize();
            CurrentGameProperty.GameVersion.UpdateGamePath(_targetProfile.ActualGameDataLocation);

            string gameCategory = GetAppConfigValue("GameCategory").ToString();
            LauncherMetadataHelper.SetPreviousGameRegion(gameCategory, _targetProfile.ZoneName);
            LoadAppConfig();
        }

        private void OperationCancelled()
        {
            RevertConversion();
            MainFrameChanger.ChangeWindowFrame(typeof(MainPage));
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            StartConversionProcess();
        }

        private async void CancelConversion(object sender, RoutedEventArgs e)
        {
            try
            {
                var contentText = _isAlreadyConverted ? string.Format(Lang._InstallConvert.CancelMsgSubtitle2, _targetProfile.ZoneName) : Lang._InstallConvert.CancelMsgSubtitle1;

                ContentDialog dialog = new ContentDialogCollapse(ContentDialogTheme.Warning)
                {
                    Title = Lang._InstallConvert.CancelMsgTitle,
                    Content = new TextBlock
                    {
                        Text         = contentText,
                        TextWrapping = TextWrapping.Wrap
                    },
                    CloseButtonText     = null,
                    PrimaryButtonText   = Lang._Misc.Yes,
                    SecondaryButtonText = Lang._Misc.No,
                    DefaultButton       = ContentDialogButton.Secondary,
                    Background          = UIElementExtensions.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                    XamlRoot            = Content.XamlRoot
                };

                if (await dialog.QueueAndSpawnDialog() == ContentDialogResult.Primary)
                {
                    await _tokenSource.CancelAsync();
                }
            }
            catch
            {
                // ignored
            }
        }

        private void RevertConversion()
        {
            if (_sourceProfile is null || _targetProfile is null) return;

            string origPath = _sourceProfile.ActualGameDataLocation;
            string ignoredPath = _targetProfile.ActualGameDataLocation + "_Ingredients";

            if (Directory.Exists(_targetProfile.ActualGameDataLocation))
            {
                // Do force config apply if the file has been actually converted.
                ApplyConfiguration();
                return;
            }

            if (!Directory.Exists(ignoredPath)) return;

            int    dirLength = ignoredPath.Length + 1;
            foreach (string filePath in Directory.EnumerateFiles(ignoredPath, "*", SearchOption.AllDirectories))
            {
                ReadOnlySpan<char> relativePath   = filePath.AsSpan()[dirLength..];
                var                destFilePath   = Path.Combine(origPath ?? "", relativePath.ToString());
                var                destFolderPath = Path.GetDirectoryName(destFilePath);

                if (!Directory.Exists(destFolderPath))
                    Directory.CreateDirectory(destFolderPath ?? "");

                try
                {
                    LogWriteLine($"Moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"", LogType.Default, true);
                    File.Move(filePath, destFilePath, true);
                }
                catch (Exception ex)
                {
                    LogWriteLine($"Error while moving \"{relativePath.ToString()}\" to \"{destFolderPath}\"\r\nException: {ex}", LogType.Error, true);
                }
            }

            Directory.Delete(ignoredPath, true);
        }
    }
}