// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyQuote : IAddChild
{
    private readonly Paragraph      _paragraph;
    private readonly MyFlowDocument _flowDocument;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyQuote()
    {
        _paragraph = new Paragraph();

        _flowDocument = new MyFlowDocument();
        var inlineUIContainer = new InlineUIContainer();

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });

        var bar = new Grid
        {
            Width      = 4,
            Background = new SolidColorBrush(Colors.Gray)
        };
        bar.SetValue(Grid.ColumnProperty, 0);
        bar.VerticalAlignment = VerticalAlignment.Stretch;
        bar.Margin = new Thickness(0, 0, 4, 0);
        grid.Children.Add(bar);

        var rightGrid = new Grid
        {
            Padding = new Thickness(4)
        };
        rightGrid.Children.Add(_flowDocument.RichTextBlock);

        rightGrid.SetValue(Grid.ColumnProperty, 1);
        grid.Children.Add(rightGrid);
        grid.Margin = new Thickness(0, 2, 0, 2);

        inlineUIContainer.Child = grid;

        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
