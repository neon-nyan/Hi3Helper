// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements.Html;

internal class MyInline : IAddChild
{
    private readonly Paragraph         _paragraph;
    private readonly InlineUIContainer _inlineUIContainer;

    public TextElement TextElement
    {
        get => _inlineUIContainer;
    }

    public MyInline()
    {
        _paragraph = new Paragraph();
        _inlineUIContainer = new InlineUIContainer();
        RichTextBlock _richTextBlock = new RichTextBlock();
        _richTextBlock.Blocks.Add(_paragraph);

        _richTextBlock.HorizontalAlignment = HorizontalAlignment.Stretch;
        _inlineUIContainer.Child = _richTextBlock;
    }

    public void AddChild(IAddChild child)
    {
        if (child.TextElement is Inline inlineChild)
        {
            _paragraph.Inlines.Add(inlineChild);
        }
        // we shouldn't support rendering block in inline
        // but if we want to support it, we can do it like this:
        //else if (child.TextElement is Block blockChild)
        //{
        //    _richTextBlock.Blocks.Add(blockChild);
        //    // if we add a new block to an inline container,
        //    // if the next child is inline, it needs to be added after the block
        //    // so we add a new paragraph. This way the next time
        //    // AddChild is called, it's added to the new paragraph
        //    _paragraph = new Paragraph();
        //    _richTextBlock.Blocks.Add(_paragraph);
        //}
    }
}
