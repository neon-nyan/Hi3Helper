// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Helpers;
using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Text;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyCodeBlock : IAddChild
{
    private readonly Paragraph _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyCodeBlock(CodeBlock codeBlock, MarkdownConfig config)
    {
        MarkdownConfig _config = config;
        _paragraph = new Paragraph();
        var container = new InlineUIContainer();
        var border    = new Border
        {
            Background   = (Brush)Application.Current.Resources["ExpanderHeaderBackground"],
            Padding      = _config.Themes.Padding,
            Margin       = _config.Themes.InternalMargin,
            CornerRadius = _config.Themes.CornerRadius
        };
        var richTextBlock = new RichTextBlock();

        if (codeBlock is FencedCodeBlock fencedCodeBlock)
        {
            //#if !WINAPPSDK
            //            var formatter = new ColorCode.RichTextBlockFormatter(Extensions.GetOneDarkProStyle());
            //#else
            //            var formatter = new ColorCode.RichTextBlockFormatter(Extensions.GetOneDarkProStyle());
            //#endif
            var stringBuilder = new StringBuilder();

            // go through all the lines backwards and only add the lines to a stack if we have encountered the first non-empty line
            StringLine[]  lines                        = fencedCodeBlock.Lines.Lines;
            Stack<string> stack                        = new();
            var           encounteredFirstNonEmptyLine = false;
            if (lines.Length != 0)
            {
                for (var i = lines.Length - 1; i >= 0; i--)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line.ToString()) && !encounteredFirstNonEmptyLine)
                    {
                        continue;
                    }

                    encounteredFirstNonEmptyLine = true;
                    stack.Push(line.ToString());
                }

                // append all the lines in the stack to the string builder
                while (stack.Count > 0)
                {
                    stringBuilder.AppendLine(stack.Pop());
                }
            }

            //formatter.FormatRichTextBlock(stringBuilder.ToString(), fencedCodeBlock.ToLanguage(), richTextBlock);
        }
        else
        {
            foreach (var line in codeBlock.Lines.Lines)
            {
                var paragraph = new Paragraph();
                var lineString = line.ToString();
                if (!string.IsNullOrWhiteSpace(lineString))
                {
                    paragraph.Inlines.Add(new Run { Text = lineString });
                }
                richTextBlock.Blocks.Add(paragraph);
            }
        }
        border.Child = richTextBlock;
        container.Child = border;
        _paragraph.Inlines.Add(container);
    }

    public void AddChild(IAddChild child) { }
}
