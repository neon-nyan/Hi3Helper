// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Extensions.Tables;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyTableCell : IAddChild
{
    private readonly TableCell      _tableCell;
    private readonly Paragraph      _paragraph = new();
    private readonly MyFlowDocument _flowDocument;
    private readonly int            _columnIndex;
    private readonly int            _rowIndex;
    private readonly Grid           _container;

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public Grid Container
    {
        get => _container;
    }

    public int ColumnSpan
    {
        get => _tableCell.ColumnSpan;
    }

    public int RowSpan
    {
        get => _tableCell.RowSpan;
    }

    public int ColumnIndex
    {
        get => _columnIndex;
    }

    public int RowIndex
    {
        get => _rowIndex;
    }

    public MyTableCell(TableCell tableCell, TextAlignment textAlignment, bool isHeader, int columnIndex, int rowIndex)
    {
        _tableCell = tableCell;
        _columnIndex = columnIndex;
        _rowIndex = rowIndex;
        _container = new Grid();

        _flowDocument                                       = new MyFlowDocument
        {
            RichTextBlock =
            {
                TextWrapping            = TextWrapping.Wrap,
                TextAlignment           = textAlignment,
                HorizontalTextAlignment = textAlignment,
                HorizontalAlignment     = textAlignment switch
                                          {
                                              TextAlignment.Left => HorizontalAlignment.Left,
                                              TextAlignment.Center => HorizontalAlignment.Center,
                                              TextAlignment.Right => HorizontalAlignment.Right,
                                              _ => HorizontalAlignment.Left
                                          }
            }
        };

        _container.Padding = new Thickness(4);
        if (isHeader)
        {
            _flowDocument.RichTextBlock.FontWeight = FontWeights.Bold;
        }
        _flowDocument.RichTextBlock.HorizontalAlignment = textAlignment switch
        {
            TextAlignment.Left => HorizontalAlignment.Left,
            TextAlignment.Center => HorizontalAlignment.Center,
            TextAlignment.Right => HorizontalAlignment.Right,
            _ => HorizontalAlignment.Left
        };
        _container.Children.Add(_flowDocument.RichTextBlock);
    }

    public void AddChild(IAddChild child)
    {
        _flowDocument.AddChild(child);
    }
}
