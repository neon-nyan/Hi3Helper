// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax.Inlines;
using Microsoft.UI.Xaml.Documents;
using System;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyAutolinkInline : IAddChild
{
    public TextElement TextElement { get; }

    public MyAutolinkInline(AutolinkInline autoLinkInline)
    {
        TextElement = new Hyperlink
        {
            NavigateUri = new Uri(autoLinkInline.Url)
        };
    }


    public void AddChild(IAddChild child)
    {
        try
        {
            var text = (MyInlineText)child;
            ((Hyperlink)TextElement).Inlines.Add((Run)text.TextElement);
        }
        catch (Exception ex)
        {
            throw new Exception("Error adding child to MyAutolinkInline", ex);
        }
    }
}
