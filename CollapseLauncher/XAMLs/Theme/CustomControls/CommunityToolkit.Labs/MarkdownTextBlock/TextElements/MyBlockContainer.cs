// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyBlockContainer : IAddChild
{
    private readonly MyFlowDocument _flowDocument;
    private readonly Paragraph      _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyBlockContainer()
    {
        InlineUIContainer _inlineUIContainer = new InlineUIContainer();
        _flowDocument = new MyFlowDocument();
        _inlineUIContainer.Child = _flowDocument.RichTextBlock;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(_inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
