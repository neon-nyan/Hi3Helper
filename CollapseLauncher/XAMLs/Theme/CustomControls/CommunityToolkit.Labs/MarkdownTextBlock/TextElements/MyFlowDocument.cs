// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Block = Microsoft.UI.Xaml.Documents.Block;
using Inline = Microsoft.UI.Xaml.Documents.Inline;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

public class MyFlowDocument : IAddChild
{
    private RichTextBlock _richTextBlock = new();

    // useless property
    public TextElement TextElement { get; set; } = new Run();
    //

    public RichTextBlock RichTextBlock
    {
        get => _richTextBlock;
        set => _richTextBlock = value;
    }

    public void AddChild(IAddChild child)
    {
        TextElement element = child.TextElement;
        if (element != null)
        {
            if (element is Block block)
            {
                _richTextBlock.Blocks.Add(block);
            }
            else if (element is Inline inline)
            {
                var paragraph = new Paragraph();
                paragraph.Inlines.Add(inline);
                _richTextBlock.Blocks.Add(paragraph);
            }
        }
    }
}
