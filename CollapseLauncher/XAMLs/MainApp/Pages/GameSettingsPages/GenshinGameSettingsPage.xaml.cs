using CollapseLauncher.GameSettings.Genshin;
using CollapseLauncher.Helper;
using CollapseLauncher.Helper.Animation;
using Hi3Helper;
using Hi3Helper.Shared.ClassStruct;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.Graphics.Display;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using RegistryUtils;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Globalization.NumberFormatting;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using static CollapseLauncher.Statics.GamePropertyVault;
using Brush = Microsoft.UI.Xaml.Media.Brush;
using Hi3Helper.SentryHelper;


#if !DISABLEDISCORD
using CollapseLauncher.DiscordPresence;
// ReSharper disable IdentifierTypo
// ReSharper disable CommentTypo
#endif

namespace CollapseLauncher.Pages
{
    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    public partial class GenshinGameSettingsPage
    {
        #region Properties
        private GamePresetProperty CurrentGameProperty   { get; }
        private GenshinSettings    Settings              { get => (GenshinSettings)CurrentGameProperty.GameSettings; }
        private Brush              InheritApplyTextColor { get; set; }
        private RegistryMonitor    RegistryWatcher       { get; set; }
        
        private CanvasBitmap _hdrCalibrationIcon;
        private CanvasBitmap _hdrCalibrationScene;
        private CanvasBitmap _hdrCalibrationUI;
        private bool IsHDREnabled { get; }
        private bool IsHDRSupported { get; }

        #endregion

        #region Main GSP Methods
        public GenshinGameSettingsPage()
        {
            try
            {
                CurrentGameProperty = GetCurrentGameProperty();
                DispatcherQueue?.TryEnqueue(() =>
                {
                    RegistryWatcher = new RegistryMonitor(RegistryHive.CurrentUser, Path.Combine($"Software\\{CurrentGameProperty.GameVersion.VendorTypeProp.VendorType}", CurrentGameProperty.GameVersion.GamePreset.InternalGameNameInConfig!));
                    ToggleRegistrySubscribe(true);
                });

#nullable enable
                // ReSharper disable once UnusedVariable
                DisplayAdvancedColorInfo? colorInfo = WindowUtility.CurrentWindowDisplayColorInfo;
#if SIMULATEGIHDR
                IsHDREnabled = true;
                IsHDRSupported = true;
#else
                IsHDREnabled = colorInfo?.CurrentAdvancedColorKind == DisplayAdvancedColorKind.HighDynamicRange;
                IsHDRSupported = colorInfo?.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange) ?? false;
#endif
#nullable restore

                LoadPage();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ToggleRegistrySubscribe(bool doSubscribe)
        {
            if (doSubscribe)
                RegistryWatcher.RegChanged += RegistryListener;
            else
                RegistryWatcher.RegChanged -= RegistryListener;
        }

        private void RegistryListener(object sender, EventArgs e)
        {
            LogWriteLine("[GI GSP Module] RegistryMonitor has detected registry change outside of the launcher! Reloading the page...", LogType.Warning, true);
            DispatcherQueue?.TryEnqueue(MainFrameChanger.ReloadCurrentMainFrame);
        }

        private void LoadPage()
        {
            Settings.ReloadSettings();
            InitializeComponent();

            ApplyButton.Translation = Shadow32;
            GameSettingsApplyGrid.Translation = new Vector3(0, 0, 64);
            SettingsScrollViewer.EnableImplicitAnimation(true);

            InheritApplyTextColor = ApplyText.Foreground;
        }

        private async void RegistryExportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = await Settings.ExportSettings();

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegExported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while exporting registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private async void RegistryImportClick(object sender, RoutedEventArgs e)
        {
            try
            {
                ToggleRegistrySubscribe(false);
                Exception exc = await Settings.ImportSettings();

                if (exc != null) throw exc;

                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._GameSettingsPage.SettingsRegImported;
                ApplyText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error has occurred while importing registry!\r\n{ex}", LogType.Error, true);
                ApplyText.Foreground = new SolidColorBrush(new Color { A = 255, R = 255, B = 0, G = 0 });
                ApplyText.Text = ex.Message;
                ApplyText.Visibility = Visibility.Visible;
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        private void InitializeSettings(object sender, RoutedEventArgs e)
        {
            try
            {
                BackgroundImgChanger.ToggleBackground(true);
                GameResolutionSelector.ItemsSource = ScreenResolutionsList;

                if (CurrentGameProperty.IsGameRunning)
                {
                #if !GSPBYPASSGAMERUNNING
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayGameRunningSubtitle;
                #endif
                }
                else if (GameInstallationState
                    is GameInstallStateEnum.NotInstalled
                    or GameInstallStateEnum.NeedsUpdate
                    or GameInstallStateEnum.InstalledHavePlugin
                    or GameInstallStateEnum.GameBroken)
                {
                    Overlay.Visibility = Visibility.Visible;
                    PageContent.Visibility = Visibility.Collapsed;
                    OverlayTitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledTitle;
                    OverlaySubtitle.Text = Lang._StarRailGameSettingsPage.OverlayNotInstalledSubtitle;
                }
                else
                {
                #if !DISABLEDISCORD
                    InnerLauncherConfig.AppDiscordPresence.SetActivity(ActivityType.GameSettings);
                #endif
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"FATAL ERROR!!!\r\n{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyText.Foreground = InheritApplyTextColor;
                ApplyText.Text = Lang._StarRailGameSettingsPage.SettingsApplied;
                ApplyText.Visibility = Visibility.Visible;

                ToggleRegistrySubscribe(false);
                Settings.SaveSettings();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{ex}", LogType.Error, true);
                ErrorSender.SendException(ex);
            }
            finally
            {
                ToggleRegistrySubscribe(true);
            }
        }

        public string CustomArgsValue
        {
            get => CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue;
            set
            {
                ToggleRegistrySubscribe(false);
                CurrentGameProperty.GameSettings.SettingsCustomArgument.CustomArgumentValue = value;
                ToggleRegistrySubscribe(true);
            }
        }

        public bool IsUseCustomArgs
        {
            get
            {
                bool value = CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments;
                CustomArgsTextBox.IsEnabled = value;
                return value;
            }
            set
            {
                CurrentGameProperty.GameSettings.SettingsCollapseMisc.UseCustomArguments = value;
                CustomArgsTextBox.IsEnabled = value;
            }
        }
        
        private void OnUnload(object sender, RoutedEventArgs e)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    ToggleRegistrySubscribe(false);
                    RegistryWatcher.Dispose();
                }
                catch (Exception ex)
                {
                    LogWriteLine($"[GI GSP Module] Error when disposing RegistryWatcher module!\r\n{ex}", LogType.Error, true);
                }
            });
        }
        #endregion

        #region Method - NumberBox to Slider
        // All methods in this region responsible for too much time wasted because thank you WinUI!
        // Basically these handle value linking for a slider that has numberbox attached with it. Handles the rounding, and also error fallback when the numberbox is cleared.

        private void GammaValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                // Check if NumberBox is cleared (NaN) then reuse old value
                // Why is this not the default behavior instead of throwing errors? I don't know ask Microsoft
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"Gamma value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    GammaValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder
                    {
                        Increment = 0.0000001
                    };

                    DecimalFormatter formatter = new DecimalFormatter
                    {
                        IntegerDigits  = 1,
                        FractionDigits = 5,
                        NumberRounder  = rounder
                    };
                    GammaValue.NumberFormatter = formatter;

                    GammaSlider.Value = GammaValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing Gamma NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        private void MaxLuminosityValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"MaxLuminosity value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    MaxLuminosityValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new DecimalFormatter
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
                    MaxLuminosityValue.NumberFormatter = formatter;

                    MaxLuminositySlider.Value = MaxLuminosityValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing MaxLuminosity NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        private void UiPaperWhiteValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"UiPaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    UiPaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new DecimalFormatter
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
                    UiPaperWhiteValue.NumberFormatter = formatter;

                    UiPaperWhiteSlider.Value = UiPaperWhiteValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing UiPaperWhite NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }

        private void ScenePaperWhiteValue_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            try
            {
                if (double.IsNaN(args.NewValue))
                {
                    LogWriteLine($"ScenePaperWhite value from NumberBox is invalid, resetting it to last value: {args.OldValue}", LogType.Warning);
                    ScenePaperWhiteValue.Value = args.OldValue;
                }
                else
                {
                    IncrementNumberRounder rounder = new IncrementNumberRounder
                    {
                        Increment = 0.1
                    };

                    DecimalFormatter formatter = new DecimalFormatter
                    {
                        IntegerDigits  = 3,
                        FractionDigits = 1,
                        NumberRounder  = rounder
                    };
                    ScenePaperWhiteValue.NumberFormatter = formatter;

                    ScenePaperWhiteSlider.Value = ScenePaperWhiteValue.Value;
                }
            }
            catch (Exception ex)
            {
                LogWriteLine($"Error when processing ScenePaperWhite NumberBox!\r\n{ex}", LogType.Error, true);
            }
        }
        #endregion

        #region Method - Slider to Numberbox
        // Methods to link all slider value to numberbox, for those with one.

        private void GammaSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            GammaValue.Value = Math.Round(e.NewValue, 5);
        }

        private void MaxLuminositySlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            var value = Math.Round(e.NewValue, 1);
            MaxLuminosityValue.Value = value;
            MaxLuminosity            = value;
            DrawHDRCalibrationImage1();
        }

        private void UiPaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            var value = Math.Round(e.NewValue, 1);
            UiPaperWhiteValue.Value = value;
            UiPaperWhite            = value;
            DrawHDRCalibrationImage2();
        }

        private void ScenePaperWhiteSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (e.OldValue == 0) return;
            var value = Math.Round(e.NewValue, 1);
            ScenePaperWhiteValue.Value = value;
            ScenePaperWhite            = value;
            DrawHDRCalibrationImage2();
        }
        #endregion

        #region Method - HDR Calibration Panels
        private static async Task<StorageFile> GetAppFileAsync(Uri uri)
        {
            StorageFile file;
            try
            {
                file = await StorageFile.GetFileFromApplicationUriAsync(uri);
            }
            catch (ArgumentException)
            {
                file = await StorageFile.GetFileFromPathAsync(Path.GetFullPath("." + uri.LocalPath));
            }
            return file;
        }

        private void DrawHDRCalibrationImage1()
        {
            CanvasSwapChainPanel panel     = HDRCalibrationPanel1;
            CanvasSwapChain      swapChain = panel.SwapChain;

            if (swapChain == null) return;
            float w      = (float)panel.Width;
            float h      = (float)panel.Height;
            float bgGain = (float)MaxLuminosity / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                CanvasSolidColorBrush white = CanvasSolidColorBrush.CreateHdr(swapChain, new Vector4(125, 125, 125, 1));
                ds.FillRectangle(0, 0, w, h, white);

                LinearTransferEffect bg = new LinearTransferEffect
                {
                    Source = _hdrCalibrationIcon,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect((w - 0.6 * h) / 2, h * 0.2, h * 0.6, h * 0.6), _hdrCalibrationIcon.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel1_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CanvasSwapChainPanel panel     = sender as CanvasSwapChainPanel;
                CanvasDevice         device    = CanvasDevice.GetSharedDevice();
                float                w         = (float)panel.Width;
                float                h         = (float)panel.Height;
                float                dpi       = 96 * (float)XamlRoot.RasterizationScale;
                CanvasSwapChain      swapChain = new CanvasSwapChain(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
                panel.SwapChain = swapChain;

                // Unpacked app failed to open ms-appx uri, so we need to read it manually :(
                StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/Sign.png"));
                using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
                {
                    _hdrCalibrationIcon = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                MaxLuminositySlider.Value = MaxLuminosity;
                DrawHDRCalibrationImage1();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private static float GammaCorrection(float val, float max) => val * MathF.Pow(val / max, 2.2f);

        private void DrawHDRCalibrationImage2()
        {
            CanvasSwapChainPanel panel     = HDRCalibrationPanel2;
            CanvasSwapChain      swapChain = panel.SwapChain;
            
            if (swapChain == null) return;
            float w      = (float)panel.Width;
            float h      = (float)panel.Height;
            float bgGain = (float)ScenePaperWhite / 80;
            float uiGain = (GammaCorrection(((float)UiPaperWhite - (float)ScenePaperWhite + 350) * 2, 1600) + 50) / 80;

            using (CanvasDrawingSession ds = swapChain.CreateDrawingSession(Colors.White))
            {
                LinearTransferEffect bg = new LinearTransferEffect
                {
                    Source = _hdrCalibrationScene,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = bgGain,
                    GreenSlope = bgGain,
                    BlueSlope = bgGain
                };
                ds.DrawImage(bg, new Rect(0, 0, w, h), _hdrCalibrationScene.Bounds);

                LinearTransferEffect ui = new LinearTransferEffect
                {
                    Source = _hdrCalibrationUI,
                    BufferPrecision = CanvasBufferPrecision.Precision16Float,
                    RedSlope = uiGain,
                    GreenSlope = uiGain,
                    BlueSlope = uiGain
                };
                ds.DrawImage(ui, new Rect(0, 0, w, h), _hdrCalibrationUI.Bounds);
            }
            swapChain.Present();
        }

        private async void SwapChainPanel2_OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                CanvasSwapChainPanel panel     = sender as CanvasSwapChainPanel;
                CanvasDevice         device    = CanvasDevice.GetSharedDevice();
                float                w         = (float)panel.Width;
                float                h         = (float)panel.Height;
                float                dpi       = 96 * (float)XamlRoot.RasterizationScale;
                CanvasSwapChain      swapChain = new CanvasSwapChain(device, w, h, dpi, DirectXPixelFormat.R16G16B16A16Float, 2, CanvasAlphaMode.Premultiplied);
                panel.SwapChain = swapChain;

                StorageFile bgFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/Scene.jxr"));
                using (IRandomAccessStream stream = await bgFile.OpenReadAsync())
                {
                    _hdrCalibrationScene = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                StorageFile uiFile = await GetAppFileAsync(new Uri("ms-appx:///Assets/Images/GenshinHDRCalibration/UI.jxr"));
                using (IRandomAccessStream stream = await uiFile.OpenReadAsync())
                {
                    _hdrCalibrationUI = await CanvasBitmap.LoadAsync(swapChain, stream, dpi);
                }

                ScenePaperWhiteSlider.Value = ScenePaperWhite;
                UiPaperWhiteSlider.Value    = UiPaperWhite;
                DrawHDRCalibrationImage2();
            }
            catch (Exception ex)
            {
                await SentryHelper.ExceptionHandlerAsync(ex);
            }
        }

        private void HDRExpander_OnExpanding(Expander sender, ExpanderExpandingEventArgs args)
        {
            sender.IsExpanded = IsHDREnabled;
        }
        #endregion
    }
}
