// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using System.Linq;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements.Html;

// block
internal class MyDetails : IAddChild
{
    private readonly MyFlowDocument _flowDocument;
    private readonly Paragraph      _paragraph;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyDetails(HtmlNode details)
    {
        HtmlNode _htmlNode = details;

        var header = _htmlNode.ChildNodes
            .FirstOrDefault(
                x => x.Name == "summary" ||
                x.Name == "header");

        InlineUIContainer _inlineUIContainer = new InlineUIContainer();
        Expander          _expander          = new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _flowDocument                                   = new MyFlowDocument
        {
            RichTextBlock =
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            }
        };
        _expander.Content                               = _flowDocument.RichTextBlock;
        var headerBlock = new TextBlock
        {
            Text                = header?.InnerText,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        _expander.Header = headerBlock;
        _inlineUIContainer.Child = _expander;
        _paragraph = new Paragraph();
        _paragraph.Inlines.Add(_inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
