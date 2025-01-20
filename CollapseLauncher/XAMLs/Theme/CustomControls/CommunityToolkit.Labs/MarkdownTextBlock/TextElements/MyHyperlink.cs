// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Hi3Helper;
using HtmlAgilityPack;
using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
using System;
using Inline = Microsoft.UI.Xaml.Documents.Inline;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyHyperlink : IAddChild
{
    private readonly Hyperlink _hyperlink;

    public TextElement TextElement
    {
        get => _hyperlink;
    }

    public MyHyperlink(LinkInline linkInline, string? baseUrl)
    {
        var url = linkInline.GetDynamicUrl != null ? linkInline.GetDynamicUrl() : linkInline.Url;
        _hyperlink = new Hyperlink
        {
            NavigateUri = Extensions.GetUri(url, baseUrl)
        };
    }

    public MyHyperlink(HtmlNode htmlNode, string? baseUrl)
    {
        var url = htmlNode.GetAttributeValue("href", "#");
        _hyperlink = new Hyperlink
        {
            NavigateUri = Extensions.GetUri(url, baseUrl)
        };
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            try
            {
                _hyperlink.Inlines.Add(inlineChild);
                // TODO: Add support for click handler
            }
            catch (Exception ex)
            {
                Logger.LogWriteLine($"[MyHyperlink::AddChild()] Failed while adding inlines\r\n{ex}", LogType.Error, true);
            }
        }
    }
}
