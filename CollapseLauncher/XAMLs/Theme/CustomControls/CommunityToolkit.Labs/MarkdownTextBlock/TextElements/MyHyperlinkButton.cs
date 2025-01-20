// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyHyperlinkButton : IAddChild
{
    private          HyperlinkButton?  _hyperLinkButton;
    private readonly InlineUIContainer _inlineUIContainer = new();
    private          MyFlowDocument?   _flowDoc;
    private readonly LinkInline?       _linkInline;
    private readonly HtmlNode?         _htmlNode;

    public bool IsHtml => _htmlNode != null;

    public TextElement TextElement
    {
        get => _inlineUIContainer;
    }

    public MyHyperlinkButton(LinkInline linkInline, string? baseUrl)
    {
        var url = linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() : linkInline.Url;
        _linkInline = linkInline;
        Init(url, baseUrl);
    }

    public MyHyperlinkButton(HtmlNode htmlNode, string? baseUrl)
    {
        _htmlNode = htmlNode;
        var url = htmlNode.GetAttributeValue("href", "#");
        Init(url, baseUrl);
    }

    private void Init(string? url, string? baseUrl)
    {
        _hyperLinkButton = new HyperlinkButton
        {
            NavigateUri = Extensions.GetUri(url, baseUrl),
            Padding     = new Thickness(0),
            Margin      = new Thickness(0)
        };
        if (IsHtml && _htmlNode != null)
        {
            _flowDoc = new MyFlowDocument();
        }
        else if (_linkInline != null)
        {
            _flowDoc = new MyFlowDocument();
        }
        _inlineUIContainer.Child = _hyperLinkButton;
        _hyperLinkButton.Content = _flowDoc?.RichTextBlock;
    }

    public void AddChild(IAddChild child)
    {
        _flowDoc?.AddChild(child);
    }
}
