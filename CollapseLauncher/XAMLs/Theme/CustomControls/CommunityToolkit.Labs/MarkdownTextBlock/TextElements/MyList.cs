// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig.Syntax;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using RomanNumerals;
using System.Globalization;

namespace CommunityToolkit.Labs.WinUI.Labs.MarkdownTextBlock.TextElements;

internal class MyList : IAddChild
{
    private readonly Paragraph  _paragraph;
    private readonly StackPanel _stackPanel;
    private readonly BulletType _bulletType;
    private readonly bool       _isOrdered;
    private          int        _index = 1;
    private const    string     Dot   = "• ";

    public TextElement TextElement
    {
        get => _paragraph;
    }

    public MyList(ListBlock listBlock)
    {
        _paragraph = new Paragraph();
        InlineUIContainer container = new InlineUIContainer();
        _stackPanel = new StackPanel();

        if (listBlock.IsOrdered)
        {
            _isOrdered = true;
            _bulletType = ToBulletType(listBlock.BulletType);

            if (listBlock.OrderedStart != null && listBlock.DefaultOrderedStart != listBlock.OrderedStart)
            {
                int startIndex = int.Parse(listBlock.OrderedStart, NumberFormatInfo.InvariantInfo);
                _index = startIndex;
            }
        }

        _stackPanel.Orientation = Orientation.Vertical;
        container.Child = _stackPanel;
        _paragraph.Inlines.Add(container);
    }

    public void AddChild(IAddChild child)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        string bullet;
        if (_isOrdered)
        {
            bullet = _bulletType switch
            {
                BulletType.Number => $"{_index}. ",
                BulletType.LowerAlpha => $"{_index.ToAlphabetical()}. ",
                BulletType.UpperAlpha => $"{_index.ToAlphabetical().ToUpper()}. ",
                BulletType.LowerRoman => $"{_index.ToRomanNumerals().ToLower()} ",
                BulletType.UpperRoman => $"{_index.ToRomanNumerals().ToUpper()} ",
                BulletType.Circle => Dot,
                _ => Dot
            };
            _index++;
        }
        else
        {
            bullet = Dot;
        }
        var textBlock = new TextBlock
        {
            Text = bullet
        };
        textBlock.SetValue(Grid.ColumnProperty, 0);
        textBlock.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(textBlock);
        var flowDoc = new MyFlowDocument();
        flowDoc.AddChild(child);

        flowDoc.RichTextBlock.SetValue(Grid.ColumnProperty, 1);
        flowDoc.RichTextBlock.Padding = new Thickness(8, 0, 0, 0);
        flowDoc.RichTextBlock.VerticalAlignment = VerticalAlignment.Top;
        grid.Children.Add(flowDoc.RichTextBlock);

        _stackPanel.Children.Add(grid);
    }

    private BulletType ToBulletType(char bullet)
    {
        // Gets or sets the type of the bullet (e.g: '1', 'a', 'A', 'i', 'I').
        return bullet switch
               {
                   '1' => BulletType.Number,
                   'a' => BulletType.LowerAlpha,
                   'A' => BulletType.UpperAlpha,
                   'i' => BulletType.LowerRoman,
                   'I' => BulletType.UpperRoman,
                   _ => BulletType.Circle
               };
    }
}

internal enum BulletType
{
    Circle,
    Number,
    LowerAlpha,
    UpperAlpha,
    LowerRoman,
    UpperRoman
}
