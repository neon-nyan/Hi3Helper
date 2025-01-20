// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Extensions.Tables;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System.Linq;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyTable : IAddChild
{
    private readonly Paragraph        _paragraph;
    private readonly MyTableUIElement _tableElement;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyTable(Table table)
    {
        _paragraph = new Paragraph();
        var column = table.FirstOrDefault() is not TableRow row ? 0 : row.Count;

        _tableElement = new MyTableUIElement
        (
            column,
            table.Count,
            1,
            new SolidColorBrush(Colors.Gray)
        );

        var inlineUIContainer = new InlineUIContainer
        {
            Child = _tableElement
        };
        _paragraph.Inlines.Add(inlineUIContainer);
    }

    public void AddChild(IAddChild child)
    {
        if (child is MyTableCell cellChild)
        {
            var cell = cellChild.Container;

            Grid.SetColumn(cell, cellChild.ColumnIndex);
            Grid.SetRow(cell, cellChild.RowIndex);
            Grid.SetColumnSpan(cell, cellChild.ColumnSpan);
            Grid.SetRowSpan(cell, cellChild.RowSpan);

            _tableElement.Children.Add(cell);
        }
    }
}
