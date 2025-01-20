using Hi3Helper.Shared.ClassStruct;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Windows.Foundation;
using Windows.Networking.Connectivity;
using Hi3Helper.SentryHelper;
using static CollapseLauncher.InnerLauncherConfig;
using static Hi3Helper.Locale;
using static Hi3Helper.Shared.Region.LauncherConfig;
// ReSharper disable CheckNamespace
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable IdentifierTypo

namespace CollapseLauncher
{
    #region LauncherUpdateRegion
    internal static class LauncherUpdateWatcher
    {
        private static readonly LauncherUpdateInvoker Invoker = new();
        public static           void                  GetStatus(LauncherUpdateProperty e) => Invoker!.GetStatus(e);
        
        public static bool IsMetered
        {
            get
            {
                NetworkCostType currentNetCostType = NetworkInformation.GetInternetConnectionProfile()?.GetConnectionCost()?.NetworkCostType ?? NetworkCostType.Fixed;
                return currentNetCostType is not (NetworkCostType.Unrestricted or NetworkCostType.Unknown);
            }
        }
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(AppUpdateVersionProp))]
    internal sealed partial class AppUpdateVersionPropJsonContext : JsonSerializerContext;
    
    public sealed class AppUpdateVersionProp
    {
        [JsonPropertyName("f")]
        public List<AppUpdateVersionFileProp> FileList { get; set; }

        [JsonPropertyName("forceUpdate")]
        public bool IsForceUpdate { get; set; }

        [JsonIgnore]
        public DateTime? TimeLocalTime
        {
            get => DateTimeOffset.FromUnixTimeSeconds(UnixTime).DateTime.ToLocalTime();
        }

        [JsonPropertyName("time")]
        public long UnixTime { get; set; }

        [JsonIgnore]
        public GameVersion? Version
        {
            get
            {
                return !GameVersion.TryParse(VersionString, out GameVersion? result) ? null : result;
            }
        }

        [JsonPropertyName("ver")]
        public string VersionString { get; set; }
    }

    public class AppUpdateVersionFileProp
    {
        [JsonPropertyName("p")] public string FilePath { get; set; }
        [JsonPropertyName("crc")] public string FileMD5Hash { get; set; }
        [JsonPropertyName("s")] public long FileSize { get; set; }
    }

    internal class LauncherUpdateInvoker
    {
        public static event EventHandler<LauncherUpdateProperty> UpdateEvent;
        public void GetStatus(LauncherUpdateProperty e) => UpdateEvent?.Invoke(this, e);
    }

    internal class LauncherUpdateProperty
    {
        public bool IsUpdateAvailable { get; set; }
        public GameVersion NewVersionName { get; set; }
        public bool QuitFromUpdateMenu { get; set; }
    }
    #endregion
    #region ThemeChangeRegion
    internal static class ThemeChanger
    {
        private static readonly ThemeChangerInvoker Invoker = new();
        public static void ChangeTheme(ElementTheme e)
        {
            CurrentAppTheme = e switch
            {
                ElementTheme.Light => AppThemeMode.Light,
                ElementTheme.Default => AppThemeMode.Default,
                _ => AppThemeMode.Dark
            };

            SetAppConfigValue("ThemeMode", CurrentAppTheme.ToString());
            Invoker.ChangeTheme(e);
        }
    }

    internal class ThemeChangerInvoker
    {
        public static event EventHandler<ThemeProperty> ThemeEvent;
        public void ChangeTheme(ElementTheme e) => ThemeEvent?.Invoke(this, new ThemeProperty(e));
    }

    internal class ThemeProperty
    {
        internal ThemeProperty(ElementTheme e) => Theme = e;
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public ElementTheme Theme { get; private set; }
    }
    #endregion
    #region ErrorSenderRegion
    public enum ErrorType { Unhandled, GameError, Connection, Warning, DiskCrc }

    internal static class ErrorSender
    {
        private static readonly ErrorSenderInvoker Invoker = new();
        public static           string             ExceptionContent;
        public static           ErrorType          ExceptionType;
        public static           string             ExceptionTitle;
        public static           string             ExceptionSubtitle;

        public static void SendException(Exception e, ErrorType eT = ErrorType.Unhandled, bool isSendToSentry = true)
        {
            if (isSendToSentry)
                SentryHelper.ExceptionHandler(e, eT == ErrorType.Unhandled ? 
                                                  SentryHelper.ExceptionType.UnhandledOther : SentryHelper.ExceptionType.Handled);
            Invoker.SendException(e, eT);
        } 
        public static void SendWarning(Exception e, ErrorType eT = ErrorType.Warning) =>
            Invoker.SendException(e, eT);
        public static void SendExceptionWithoutPage(Exception e, ErrorType eT = ErrorType.Unhandled)
        {
            SentryHelper.ExceptionHandler(e, eT == ErrorType.Unhandled ? SentryHelper.ExceptionType.UnhandledOther : SentryHelper.ExceptionType.Handled);
            ExceptionContent = e!.ToString();
            ExceptionType = eT;
            SetPageTitle(eT);
        }

        public static void SetPageTitle(ErrorType errorType)
        {
            var locUnhandledException = Lang!._UnhandledExceptionPage!;
            switch (errorType)
            {
                case ErrorType.Unhandled:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle1;
                    ExceptionSubtitle = locUnhandledException.UnhandledTitle1;
                    break;
                case ErrorType.Connection:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle2;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubtitle2;
                    break;
                case ErrorType.GameError:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle3;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubtitle3;
                    break;
                case ErrorType.Warning:
                    ExceptionTitle    = locUnhandledException.UnhandledTitle4;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubtitle4;
                    break;
                case ErrorType.DiskCrc:
                    ExceptionTitle    = locUnhandledException.UnhandledTitleDiskCrc;
                    ExceptionSubtitle = locUnhandledException.UnhandledSubDiskCrc;
                    break;
            }
        }
    }

    internal class ErrorSenderInvoker
    {
        public static event EventHandler<ErrorProperties> ExceptionEvent;
        public void SendException(Exception e, ErrorType eT) => ExceptionEvent?.Invoke(this, new ErrorProperties(e, eT));
    }

    internal class ErrorProperties
    {
        internal ErrorProperties(Exception e, ErrorType errorType)
        {
            Exception                    = e;
            ExceptionString              = e?.ToString() ?? string.Empty;
            ErrorSender.ExceptionContent = ExceptionString;
            ErrorSender.ExceptionType    = errorType;
            ErrorSender.SetPageTitle(errorType);
        }
        public Exception Exception { get; private set; }
        public string ExceptionString { get; }
    }
    #endregion
    #region MainFrameRegion
    internal static class MainFrameChanger
    {
        private static          Type                    _currentWindow;
        private static          Type                    _currentPage;
        private static readonly MainFrameChangerInvoker Invoker = new();
        public static           void                    GoBackWindowFrame()            => Invoker.GoBackWindowFrame();
        public static           void                    ChangeWindowFrame(Type toPage) => ChangeWindowFrame(toPage, new DrillInNavigationTransitionInfo());
        public static void ChangeWindowFrame(Type toPage, NavigationTransitionInfo transition)
        {
            _currentWindow = toPage ?? throw new NullReferenceException("Argument: toPage cannot be null!");
            Invoker.ChangeWindowFrame(toPage, transition);
        }

        public static void GoBackMainFrame() => Invoker.GoBackMainFrame();
        public static void ChangeMainFrame(Type toPage) => ChangeMainFrame(toPage, new DrillInNavigationTransitionInfo());
        public static void ChangeMainFrame(Type toPage, NavigationTransitionInfo transition)
        {
            _currentPage = toPage ?? throw new NullReferenceException("Argument: toPage cannot be null!");
            Invoker!.ChangeMainFrame(toPage, transition);
        }

        public static void ReloadCurrentWindowFrame() => ChangeWindowFrame(_currentWindow);
        public static void ReloadCurrentMainFrame() => ChangeMainFrame(_currentPage);
    }

    internal class MainFrameChangerInvoker
    {
        public static event EventHandler<MainFrameProperties> WindowFrameEvent;
        public static event EventHandler<MainFrameProperties> FrameEvent;
        public static event EventHandler WindowFrameGoBackEvent;
        public static event EventHandler FrameGoBackEvent;
        public void ChangeWindowFrame(Type e, NavigationTransitionInfo eT) => WindowFrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
        public void ChangeMainFrame(Type e, NavigationTransitionInfo eT) => FrameEvent?.Invoke(this, new MainFrameProperties(e, eT));
        public void GoBackWindowFrame() => WindowFrameGoBackEvent?.Invoke(this, null);
        public void GoBackMainFrame() => FrameGoBackEvent?.Invoke(this, null);
    }

    internal class MainFrameProperties
    {
        internal MainFrameProperties(Type frameTo, NavigationTransitionInfo transition)
        {
            FrameTo = frameTo;
            Transition = transition;
        }
        public Type FrameTo { get; private set; }
        public NavigationTransitionInfo Transition { get; private set; }
    }
    #endregion
    #region NotificationPushRegion
    internal static class NotificationSender
    {
        private static readonly NotificationInvoker Invoker = new();
        public static           void                SendNotification(NotificationInvokerProp e) => Invoker.SendNotification(e);
        public static void SendCustomNotification(int tagID, InfoBar infoBarUI) => Invoker.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Add,
            Notification = new NotificationProp
            {
                MsgId = tagID
            },
            OtherContent = infoBarUI
        });
        public static void RemoveCustomNotification(int tagID) => Invoker.SendNotification(new NotificationInvokerProp
        {
            IsCustomNotif = true,
            CustomNotifAction = NotificationCustomAction.Remove,
            Notification = new NotificationProp
            {
                MsgId = tagID
            }
        });
    }

    internal class NotificationInvoker
    {
        public static event EventHandler<NotificationInvokerProp> EventInvoker;
        public void SendNotification(NotificationInvokerProp e) => EventInvoker?.Invoke(this, e);
    }

    public enum NotificationCustomAction { Add, Remove }
    public class NotificationInvokerProp
    {
        public TypedEventHandler<InfoBar, object> CloseAction { get; set; }
        public FrameworkElement OtherContent { get; set; }
        public NotificationProp Notification { get; set; }
        public bool IsAppNotif { get; set; } = true;
        public bool IsCustomNotif { get; set; }
        public NotificationCustomAction CustomNotifAction { get; set; }

    }
    #endregion
    #region BackgroundRegion
    internal static class BackgroundImgChanger
    {
        private static readonly BackgroundImgChangerInvoker Invoker = new();
        public static void ChangeBackground(string imgPath, Action actionAfterLoaded,
            bool isCustom = true, bool isForceRecreateCache = false, bool isRequestInit = false)
        {
            Invoker!.ChangeBackground(imgPath, actionAfterLoaded, isCustom, isForceRecreateCache, isRequestInit);
        }
        public static void ToggleBackground(bool hide) => Invoker!.ToggleBackground(hide);
    }

    internal class BackgroundImgChangerInvoker
    {
        public static event EventHandler<BackgroundImgProperty> ImgEvent;
        public static event EventHandler<bool> IsImageHide;

        public void ChangeBackground(string imgPath, Action actionAfterLoaded,
                                     bool isCustom, bool isForceRecreateCache = false, bool isRequestInit = false)
        {
            ImgEvent?.Invoke(this, new BackgroundImgProperty(imgPath, isCustom, isForceRecreateCache, isRequestInit, actionAfterLoaded));
        }

        public void ToggleBackground(bool hide) => IsImageHide?.Invoke(this, hide);
    }

    internal class BackgroundImgProperty
    {
        internal BackgroundImgProperty(string imgPath, bool isCustom, bool isForceRecreateCache, bool isRequestInit, Action actionAfterLoaded)
        {
            ImgPath              = imgPath;
            IsCustom             = isCustom;
            IsForceRecreateCache = isForceRecreateCache;
            IsRequestInit        = isRequestInit;
            ActionAfterLoaded    = actionAfterLoaded;
        }

        public Action ActionAfterLoaded { get; set; }
        public bool IsRequestInit { get; set; }
        public bool IsForceRecreateCache { get; set; }
        public string ImgPath { get; private set; }
        public bool IsCustom { get; private set; }
    }
    #endregion
    #region SpawnWebView2Region
    internal static class SpawnWebView2
    {
        private static readonly SpawnWebView2Invoker Invoker = new();
        public static void SpawnWebView2Window(string url, UIElement parentUI)
        {
            if (GetAppConfigValue("UseExternalBrowser").ToBool())
            {
                if (string.IsNullOrEmpty(url)) return;
                parentUI!.DispatcherQueue!.TryEnqueue(() =>
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                });
            }
            else Invoker!.SpawnWebView2Window(url);
        }
    }

    internal class SpawnWebView2Invoker
    {
        public static event EventHandler<SpawnWebView2Property> SpawnEvent;
        public void SpawnWebView2Window(string url) => SpawnEvent?.Invoke(this, new SpawnWebView2Property(url));
    }

    internal class SpawnWebView2Property
    {
        internal SpawnWebView2Property(string url) => URL = url;

        public string URL { get; set; }
    }
    #endregion
    #region ShowLoadingPage
    internal static class ShowLoadingPage
    {
        private static readonly ShowLoadingPageInvoker Invoker = new();
        public static           void                   ShowLoading(string title, string subtitle, bool hide = false) => Invoker.ShowLoading(hide, title, subtitle);
    }

    internal class ShowLoadingPageInvoker
    {
        public static event EventHandler<ShowLoadingPageProperty> PageEvent;
        public void ShowLoading(bool hide, string title, string subtitle) => PageEvent?.Invoke(this, new ShowLoadingPageProperty(hide, title, subtitle));
    }

    internal class ShowLoadingPageProperty
    {
        internal ShowLoadingPageProperty(bool hide, string title, string subtitle)
        {
            Hide = hide;
            Title = title;
            Subtitle = subtitle;
        }
        public bool Hide { get; private set; }
        public string Title { get; private set; }
        public string Subtitle { get; private set; }
    }
    #endregion
    #region ChangeTitleDragArea
    public enum DragAreaTemplate
    {
        None,
        Full,
        Default
    }

    internal static class ChangeTitleDragArea
    {
        private static readonly ChangeTitleDragAreaInvoker Invoker = new();
        public static   void                       Change(DragAreaTemplate template) => Invoker.Change(template);
    }

    internal class ChangeTitleDragAreaInvoker
    {
        public static event EventHandler<ChangeTitleDragAreaProperty> TitleBarEvent;
        public void Change(DragAreaTemplate template) => TitleBarEvent?.Invoke(this, new ChangeTitleDragAreaProperty(template));
    }

    internal class ChangeTitleDragAreaProperty
    {
        internal ChangeTitleDragAreaProperty(DragAreaTemplate template)
        {
            Template = template;
        }

        public DragAreaTemplate Template { get; private set; }
    }
    #endregion
    #region UpdateBindings
    internal static class UpdateBindings
    {
        private static readonly UpdateBindingsInvoker Invoker = new();
        public static   void                  Update() => Invoker!.Update();
    }

    internal class UpdateBindingsInvoker
    {
        private static readonly EventArgs DummyArgs = EventArgs.Empty;
        public static event EventHandler  UpdateEvents;
        public void                       Update() => UpdateEvents?.Invoke(this, DummyArgs);
    }
    #endregion
}
