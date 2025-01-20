// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyInlineText : IAddChild
{
    private readonly Run _run;

    public TextElement TextElement
    {
        get => _run;
    }

    public MyInlineText(string text)
    {
        _run = new Run
        {
            Text = text
        };
    }

    public void AddChild(IAddChild child) { }
}
