// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using CollapseLauncher.Helper;
using Hi3Helper;
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Globalization;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyImage : IAddChild
{
    private readonly InlineUIContainer _container = new();
    private          Image             _image     = new();
    private          Grid?             _imageGrid;
    private readonly Uri?              _uri;
    private readonly IImageProvider?   _imageProvider;
    private readonly ISVGRenderer      _svgRenderer;
    private readonly double            _precedentWidth;
    private readonly double            _precedentHeight;
    private          bool              _loaded;

    public TextElement TextElement
    {
        get => _container;
    }

    public MyImage(LinkInline linkInline, Uri uri, MarkdownConfig config)
    {
        _uri = uri;
        _imageProvider = config.ImageProvider;
        _svgRenderer = config.SVGRenderer == null ? new DefaultSVGRenderer() : config.SVGRenderer;
        Init();
        var size = Extensions.GetMarkdownImageSize(linkInline);
        if (size.Width != 0)
        {
            _precedentWidth = size.Width;
        }
        if (size.Height != 0)
        {
            _precedentHeight = size.Height;
        }
    }

    public MyImage(HtmlNode htmlNode, MarkdownConfig? config)
    {
        Uri.TryCreate(htmlNode.GetAttributeValue("src", "#"), UriKind.RelativeOrAbsolute, out _uri);
        _imageProvider = config?.ImageProvider;
        _svgRenderer = config?.SVGRenderer == null ? new DefaultSVGRenderer() : config.SVGRenderer;
        Init();
        int.TryParse(
            htmlNode.GetAttributeValue("width", "0"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var width
        );
        int.TryParse(
            htmlNode.GetAttributeValue("height", "0"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var height
        );
        if (width > 0)
        {
            _precedentWidth = width;
        }
        if (height > 0)
        {
            _precedentHeight = height;
        }
    }

    private void Init()
    {
        _image.Loaded += LoadImage;
        _imageGrid = new Grid { CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8) };
        _imageGrid.Children.Add(_image);
        _container.Child = _imageGrid;
    }

    private async void LoadImage(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        try
        {
            if (_imageProvider != null && _imageProvider.ShouldUseThisProvider(_uri?.AbsoluteUri))
            {
                _image = await _imageProvider.GetImage(_uri?.AbsoluteUri);
                _container.Child = _image;
            }
            else
            {
                HttpClient client = new HttpClientBuilder()
                    .UseLauncherConfig()
                    .Create();

                // Download data from URL
                HttpResponseMessage response = await client.GetAsync(_uri);

                // Get the Content-Type header
                string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

                if (contentType == "image/svg+xml")
                {
                    string svgString = await response.Content.ReadAsStringAsync();
                    var resImage = await _svgRenderer.SvgToImage(svgString);
                    if (resImage != null)
                    {
                        _image = resImage;
                        _container.Child = _image;
                    }
                }
                else
                {
                    byte[] data = await response.Content.ReadAsByteArrayAsync();
                    // Create a BitmapImage for other supported formats
                    BitmapImage bitmap = new BitmapImage();
                    using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
                    {
                        // Write the data to the stream
                        await stream.WriteAsync(data.AsBuffer());
                        stream.Seek(0);

                        // Set the source of the BitmapImage
                        await bitmap.SetSourceAsync(stream);
                    }

                    _image.Source = bitmap;
                    // _image.Width = bitmap.PixelWidth == 0 ? bitmap.DecodePixelWidth : bitmap.PixelWidth;
                    // _image.Height = bitmap.PixelHeight == 0 ? bitmap.DecodePixelHeight : bitmap.PixelHeight;
                }

                _loaded = true;
            }

            if (_precedentWidth != 0)
            {
                _image.Width = _precedentWidth;
            }
            if (_precedentHeight != 0)
            {
                _image.Height = _precedentHeight;
            }
        }
        catch (Exception ex)
        {
            Logger.LogWriteLine($"[MyImage::LoadImage()] Failed while loading image from {_uri}\r\n{ex}", LogType.Error, true);
        }
    }

    public void AddChild(IAddChild child) { }
}
