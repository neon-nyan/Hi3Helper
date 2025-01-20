// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using FontWeight = Windows.UI.Text.FontWeight;
using FontWeights = Microsoft.UI.Text.FontWeights;
// ReSharper disable PartialTypeWithSinglePart

namespace CommunityToolkit.WinUI.Controls.MarkdownTextBlockRns;

public sealed partial class MarkdownThemes : DependencyObject
{
    internal static MarkdownThemes Default { get; } = new();

    public Thickness Padding { get; set; } = new(8);

    public Thickness InternalMargin { get; set; } = new(16);

    public CornerRadius CornerRadius { get; set; } = new(8);

    public double H1FontSize { get; set; } = 22;

    public double H2FontSize { get; set; } = 20;

    public double H3FontSize { get; set; } = 18;

    public double H4FontSize { get; set; } = 16;

    public double H5FontSize { get; set; } = 14;

    public double H6FontSize { get; set; } = 12;

    public Brush HeadingForeground { get; set; } = (Brush)Application.Current.Resources["AccentColor"];

    public FontWeight H1FontWeight { get; set; } = FontWeights.Bold;

    public FontWeight H2FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H3FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H4FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H5FontWeight { get; set; } = FontWeights.Normal;

    public FontWeight H6FontWeight { get; set; } = FontWeights.Normal;

    public AcrylicBrush InlineCodeBackground { get; set; } = (AcrylicBrush)Application.Current.Resources["AccentAcrylicInAppFillColorDefaultBrush"];

    public Brush InlineCodeBorderBrush { get; set; } = new SolidColorBrush(Colors.Gray);

    public Thickness InlineCodeBorderThickness { get; set; } = new(1);

    public CornerRadius InlineCodeCornerRadius { get; set; } = new(2);

    public Thickness InlineCodePadding { get; set; } = new(4, 0, 4, 0);

    public double InlineCodeFontSize { get; set; } = 12;
    public FontFamily InlineCodeFontFamily { get; set; } = new("Consolas");

    public FontWeight InlineCodeFontWeight { get; set; } = FontWeights.SemiBold;
}
