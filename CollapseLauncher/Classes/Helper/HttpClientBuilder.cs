using CollapseLauncher.Helper.Update;
using Hi3Helper.Shared.Region;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.InteropServices;
// ReSharper disable StringLiteralTypo

#nullable enable
namespace CollapseLauncher.Helper
{
    public class HttpClientBuilder : HttpClientBuilder<SocketsHttpHandler>;

    public class HttpClientBuilder<THandler> where THandler : HttpMessageHandler, new()
    {
        private const int MaxConnectionsDefault = 32;
        private const double HttpTimeoutDefault = 90; // in Seconds

        private bool IsUseProxy              { get; set; } = true;
        private bool IsUseSystemProxy        { get; set; } = true;
        private bool IsAllowHttpRedirections { get; set; }
        private bool IsAllowHttpCookies      { get; set; }
        private bool IsAllowUntrustedCert    { get; set; }

        private int                         MaxConnections            { get; set; } = MaxConnectionsDefault;
        private DecompressionMethods        DecompressionMethod       { get; set; } = DecompressionMethods.All;
        private WebProxy?                   ExternalProxy             { get; set; }
        private Version                     HttpProtocolVersion       { get; set; } = HttpVersion.Version30;
        private string?                     HttpUserAgent             { get; set; } = GetDefaultUserAgent();
        private string?                     HttpAuthHeader            { get; set; }
        private HttpVersionPolicy           HttpProtocolVersionPolicy { get; set; } = HttpVersionPolicy.RequestVersionOrLower;
        private TimeSpan                    HttpTimeout               { get; set; } = TimeSpan.FromSeconds(HttpTimeoutDefault);
        private Uri?                        HttpBaseUri               { get; set; }
        private Dictionary<string, string?> HttpHeaders               { get; } = new();

        public HttpClientBuilder<THandler> UseProxy(bool isUseSystemProxy = true)
        {
            IsUseProxy = true;
            IsUseSystemProxy = isUseSystemProxy;
            return this;
        }

        private static string GetDefaultUserAgent()
        {
            Version operatingSystemVer = Environment.OSVersion.Version;

            return $"Mozilla/5.0 (Windows NT {operatingSystemVer}; Win64; x64) "
                + $"{RuntimeInformation.FrameworkDescription.Replace(' ', '/')} (KHTML, like Gecko) "
                + $"Collapse/{LauncherUpdateHelper.LauncherCurrentVersionString}-{(LauncherConfig.IsPreview ? "Preview" : "Stable")} "
                + $"WinAppSDK/{LauncherConfig.WindowsAppSdkVersion}";
        }

        public HttpClientBuilder<THandler> UseExternalProxy(string host, string? username = null, string? password = null)
        {
            // Try to create the Uri
            if (Uri.TryCreate(host, UriKind.Absolute, out Uri? hostUri))
            {
                return UseExternalProxy(hostUri, username, password);
            }

            IsUseProxy       = false;
            IsUseSystemProxy = false;
            ExternalProxy    = null;
            return this;

        }

        public HttpClientBuilder<THandler> UseExternalProxy(Uri hostUri, string? username = null, string? password = null)
        {
            IsUseSystemProxy = false;

            // Initialize the proxy host
            ExternalProxy =
                !string.IsNullOrEmpty(username)
             && !string.IsNullOrEmpty(password) ?
                  new WebProxy(hostUri, true, null, new NetworkCredential(username, password))
                : new WebProxy(hostUri, true);

            return this;
        }

        public HttpClientBuilder<THandler> UseLauncherConfig(int maxConnections = MaxConnectionsDefault)
        {
            bool lIsUseProxy = LauncherConfig.GetAppConfigValue("IsUseProxy").ToBool();
            bool lIsAllowHttpRedirections = LauncherConfig.GetAppConfigValue("IsAllowHttpRedirections").ToBool();
            bool lIsAllowHttpCookies = LauncherConfig.GetAppConfigValue("IsAllowHttpCookies").ToBool();
            bool lIsAllowUntrustedCert = LauncherConfig.GetAppConfigValue("IsAllowUntrustedCert").ToBool();

            string? lHttpProxyUrl = LauncherConfig.GetAppConfigValue("HttpProxyUrl").ToString();
            string? lHttpProxyUsername = LauncherConfig.GetAppConfigValue("HttpProxyUsername").ToString();
            string? lHttpProxyPassword = LauncherConfig.GetAppConfigValue("HttpProxyPassword").ToString();

            double lHttpClientTimeout = LauncherConfig.GetAppConfigValue("HttpClientTimeout").ToDouble();

            bool isHttpProxyUrlValid = Uri.TryCreate(lHttpProxyUrl, UriKind.Absolute, out Uri? lProxyUri);

            UseProxy();

            if (lIsUseProxy && isHttpProxyUrlValid && lProxyUri != null)
                UseExternalProxy(lProxyUri, lHttpProxyUsername, lHttpProxyPassword);

            AllowUntrustedCert(lIsAllowUntrustedCert);
            AllowCookies(lIsAllowHttpCookies);
            AllowRedirections(lIsAllowHttpRedirections);

            SetTimeout(lHttpClientTimeout);
            SetMaxConnection(maxConnections);

            return this;
        }

        public HttpClientBuilder<THandler> SetMaxConnection(int maxConnections = MaxConnectionsDefault)
        {
            if (maxConnections < 2)
                maxConnections = 2;

            MaxConnections = maxConnections;
            return this;
        }

        public HttpClientBuilder<THandler> SetAllowedDecompression(DecompressionMethods decompressionMethods = DecompressionMethods.All)
        {
            DecompressionMethod = decompressionMethods;
            return this;
        }

        public HttpClientBuilder<THandler> AllowRedirections(bool allowRedirections = true)
        {
            IsAllowHttpRedirections = allowRedirections;
            return this;
        }

        public HttpClientBuilder<THandler> SetAuthHeader(string authHeader)
        {
            if (!string.IsNullOrEmpty(authHeader)) HttpAuthHeader = authHeader;
            return this;
        }

        public HttpClientBuilder<THandler> AllowCookies(bool allowCookies = true)
        {
            IsAllowHttpCookies = allowCookies;
            return this;
        }

        public HttpClientBuilder<THandler> AllowUntrustedCert(bool allowUntrustedCert = false)
        {
            IsAllowUntrustedCert = allowUntrustedCert;
            return this;
        }

        public HttpClientBuilder<THandler> SetHttpVersion(Version? version = null, HttpVersionPolicy versionPolicy = HttpVersionPolicy.RequestVersionOrLower)
        {
            if (version != null)
                HttpProtocolVersion = version;

            HttpProtocolVersionPolicy = versionPolicy;
            return this;
        }

        public HttpClientBuilder<THandler> SetTimeout(double fromSeconds = HttpTimeoutDefault)
        {
            if (double.IsNaN(fromSeconds) || double.IsInfinity(fromSeconds))
                fromSeconds = HttpTimeoutDefault;

            return SetTimeout(TimeSpan.FromSeconds(fromSeconds));
        }

        public HttpClientBuilder<THandler> SetTimeout(TimeSpan? timeout = null)
        {
            timeout     ??= TimeSpan.FromSeconds(HttpTimeoutDefault);
            HttpTimeout =   timeout.Value;
            return this;
        }

        public HttpClientBuilder<THandler> SetUserAgent(string? userAgent = null)
        {
            HttpUserAgent = userAgent;
            return this;
        }

        public HttpClientBuilder<THandler> SetBaseUrl(string baseUrl)
        {
            Uri baseUri = new Uri(baseUrl);
            return SetBaseUrl(baseUri);
        }

        public HttpClientBuilder<THandler> SetBaseUrl(Uri baseUrl)
        {
            HttpBaseUri = baseUrl;
            return this;
        }

        public HttpClientBuilder<THandler> AddHeader(string key, string? value)
        {
            // Throw if the key is null or empty
            ArgumentException.ThrowIfNullOrEmpty(key, nameof(key));

            // Try check if the key is user-agent. If the user-agent has already
            // been set, then override the value from HttpUserAgent property
            if (key.Equals("User-Agent", StringComparison.OrdinalIgnoreCase))
            {
                HttpUserAgent = null;
            }

            // If the key already exist, then override the previous one.
            // Otherwise, add the new key-value pair
            // ReSharper disable once RedundantDictionaryContainsKeyBeforeAdding
            if (HttpHeaders != null && HttpHeaders.ContainsKey(key))
            {
                HttpHeaders[key] = value;
            }
            else
            {
                HttpHeaders?.Add(key, value);
            }

            // Return the instance of the builder
            return this;
        }

        public HttpClient Create()
        {
            // Create the instance of the handler
            THandler handler = new();

            // Set the features of each handler
            if (typeof(THandler) == typeof(HttpClientHandler))
            {
                // Cast as HttpClientHandler
                if (handler is not HttpClientHandler httpClientHandler)
                    throw new InvalidCastException("Cannot cast handler as HttpClientHandler");

                // Set the properties
                httpClientHandler.UseProxy = IsUseProxy || IsUseSystemProxy;
                httpClientHandler.MaxConnectionsPerServer = MaxConnections;
                httpClientHandler.AllowAutoRedirect = IsAllowHttpRedirections;
                httpClientHandler.UseCookies = IsAllowHttpCookies;
                httpClientHandler.AutomaticDecompression = DecompressionMethod;
                httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;

                // Toggle for allowing untrusted cert
                if (IsAllowUntrustedCert)
                    httpClientHandler.ServerCertificateCustomValidationCallback = delegate { return true; };

                // Set if the external proxy is set
                if (!IsUseSystemProxy && ExternalProxy != null)
                    httpClientHandler.Proxy = ExternalProxy;
            }
            else if (typeof(THandler) == typeof(SocketsHttpHandler))
            {
                // Cast as SocketsHttpHandler
                if (handler is not SocketsHttpHandler socketsHttpHandler)
                    throw new InvalidCastException("Cannot cast handler as SocketsHttpHandler");

                // Set the properties
                socketsHttpHandler.UseProxy = IsUseProxy || IsUseSystemProxy;
                socketsHttpHandler.MaxConnectionsPerServer = MaxConnections;
                socketsHttpHandler.AllowAutoRedirect = IsAllowHttpRedirections;
                socketsHttpHandler.UseCookies = IsAllowHttpCookies;
                socketsHttpHandler.AutomaticDecompression = DecompressionMethod;
                socketsHttpHandler.EnableMultipleHttp2Connections = true;
                socketsHttpHandler.EnableMultipleHttp3Connections = true;

                // Toggle for allowing untrusted cert
                if (IsAllowUntrustedCert)
                {
                    SslClientAuthenticationOptions sslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = delegate { return true; }
                    };
                    socketsHttpHandler.SslOptions = sslOptions;
                }

                // Set if the external proxy is set
                if (!IsUseSystemProxy && ExternalProxy != null)
                    socketsHttpHandler.Proxy = ExternalProxy;
            }
            else
            {
                throw new InvalidOperationException("Generic must be a member of HttpMessageHandler!");
            }

            // Create the HttpClient instance
            HttpClient client = new HttpClient(handler, false)
            {
                Timeout = HttpTimeout,
                DefaultRequestVersion = HttpProtocolVersion,
                DefaultVersionPolicy = HttpProtocolVersionPolicy,
                BaseAddress = HttpBaseUri,
                MaxResponseContentBufferSize = int.MaxValue
            };

            // Set User-agent
            if (!string.IsNullOrEmpty(HttpUserAgent))
                client.DefaultRequestHeaders.Add("User-Agent", HttpUserAgent);
            
            // Add Http Auth Header
            if (!string.IsNullOrEmpty(HttpAuthHeader))
                client.DefaultRequestHeaders.Add("Authorization", HttpAuthHeader);

            // Add other headers
            foreach (KeyValuePair<string, string?> header in HttpHeaders)
            {
                _ = client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            return client;
        }
    }
}