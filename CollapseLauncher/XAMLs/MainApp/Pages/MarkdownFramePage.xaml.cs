using CollapseLauncher.Helper;
using CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock;
using Hi3Helper;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;

#nullable enable
namespace CollapseLauncher
{
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class MarkdownFramePage : Page
    {
        /// <summary>
        /// Parameters for the MarkdownFramePage.
        /// Use either MarkdownUri, MarkdownText, or MarkdownUriCdn.
        /// </summary>
        public class MarkdownFramePageParams
        {
            /// <summary>
            /// URL of the markdown you want to load
            /// </summary>
            public string? MarkdownUri    { get; set; }

            /// <summary>
            /// (Optional) URL of the web page you want to open by pressing the "Open in External Browser" button.
            /// </summary>
            public string? WebUri         { get; set; }

            /// <summary>
            /// Raw markdown you want to show
            /// </summary>
            public string? MarkdownText   { get; set; }

            /// <summary>
            /// Relative path of Collapse's CDN you want to use for the markdown
            /// </summary>
            public string? MarkdownUriCdn { get; set; }

            /// <summary>
            /// (Optional) Title of the overlay
            /// </summary>
            public string? Title          { get; set; }
        }

        private string? _webUri;
        private          HttpClient?        _client;
        private readonly MarkdownConfig     _markdownConfig = new();
        
        internal static  MarkdownFramePage? Current { get; set; }

        public MarkdownFramePage()
        {
            InitializeComponent();
            MarkdownContainer.Text = $"{Locale.Lang._FileCleanupPage.LoadingTitle}...";
            Current                = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Let the overlay navigated before the markdown is loaded to prevent UI thread stuck
            base.OnNavigatedTo(e);

            if (e is { Parameter: MarkdownFramePageParams parameters })
            {
                Initialize(parameters);
            }
        }

        private void Initialize(MarkdownFramePageParams parameters)
        {
            if (parameters is { MarkdownText: null } and { MarkdownUri: null } and { MarkdownUriCdn: null })
                throw new
                    NullReferenceException("[MarkdownFramePage] Either MarkdownUri, MarkdownUriCdn or MarkdownText needs to be filled!");

            if ((parameters is { MarkdownText  : not null } ? 1 : 0) +
                (parameters is { MarkdownUri   : not null } ? 1 : 0) +
                (parameters is { MarkdownUriCdn: not null } ? 1 : 0) >= 2)
                throw new
                    InvalidDataException("[MarkdownFramePage] Multiple markdown sources were assigned! Only assign one of the three possible sources!");

            if (parameters is { WebUri: not null })
            {
                MarkdownOpenExternalBtn.Visibility = Visibility.Visible;
                _webUri                            = parameters.WebUri;
            }

            if (parameters is { Title: not null })
            {
                MarkdownFrameTitle.Text       = parameters.Title;
                MarkdownFrameTitle.Visibility = Visibility.Visible;
            }

            SpawnMarkdownPanel(parameters.MarkdownUri,
                               parameters.WebUri,
                               parameters.MarkdownText,
                               parameters.MarkdownUriCdn);
        }

        private async void SpawnMarkdownPanel(string? markdownUri,
                                              string? webUri         = null,
                                              string? markdownText   = null,
                                              string? markdownUriCdn = null)
        {
            try
            {
                MarkdownPanel.Visibility  =  Visibility.Visible;
                MarkdownPanel.Translation += Shadow32;

                ChangeTitleDragArea.Change(DragAreaTemplate.Full);

                if (markdownText != null)
                {
                    MarkdownContainer.Text = markdownText;
                }
                else if (markdownUri != null)
                {
                    _client             = new HttpClientBuilder()
                                            .UseLauncherConfig()
                                            .SetUserAgent(GetAppConfigValue("UserAgent").ToString())
                                            .Create();
                    _client.BaseAddress = new Uri(markdownUri);
                    using HttpResponseMessage response = await _client.GetAsync(markdownUri);

                    LogWriteLine($"[MarkdownFramePage] Loading Markdown from URL {markdownUri}\r\n" +
                                 $"{response.EnsureSuccessStatusCode()}",
                                 LogType.Scheme);

                    MarkdownContainer.Text = await response.Content.ReadAsStringAsync();
                }
                else if (markdownUriCdn != null)
                {
                    await using BridgedNetworkStream netStream =
                        await FallbackCDNUtil.TryGetCDNFallbackStream(markdownUriCdn,
                                                                      new CancellationToken(),
                                                                      true);
                    var buffer = new byte[netStream.Length];
                    await netStream.ReadExactlyAsync(buffer);

                    MarkdownContainer.Text = Encoding.UTF8.GetString(buffer);
                }
            }
            catch (Exception ex)
            {
                if (webUri != null)
                {
                    LogWriteLine($"Error while initialize Markdown. Open it to browser instead!\r\n{ex}",
                                 LogType.Error,
                                 true);
                    OpenWebUri();
                }
                else
                {
                    LogWriteLine($"Error while initializing MarkdownFramePage\r\n{ex}",
                                 LogType.Error,
                                 true);
                    var exFormat = new Exception("Error while initializing MarkdownFramePage",
                                                 ex);
                    ErrorSender.SendException(exFormat);
                }
            }
        }

        private void MarkdownOpenExternalBtn_Click(object          sender,
                                                   RoutedEventArgs e) =>
            OpenWebUri();

        private void OpenWebUri()
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName        = _webUri
                }
            }.Start();
        }

        private void MarkdownFramePage_OnUnloaded(object sender, RoutedEventArgs e)
        {
            ChangeTitleDragArea.Change(DragAreaTemplate.Default);
            Current = null;
        }
    }
}