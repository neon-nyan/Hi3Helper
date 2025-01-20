using CommunityToolkit.WinUI;
using Hi3Helper;
using Hi3Helper.CommunityToolkit.WinUI.Controls;
using Hi3Helper.SentryHelper;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Text;
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault

namespace CollapseLauncher.Extension
{
    internal enum CornerRadiusKind { Normal, Rounded }
    internal class NavigationViewItemLocaleTextProperty
    {
        public string LocaleSetName { get; set; }
        public string LocalePropertyName { get; set; }
    }

    internal static partial class UIElementExtensions
    {
#nullable enable
        /// <summary>
        /// Set the initial navigation view item's locale binding before getting set with <seealso cref="ApplyNavigationViewItemLocaleTextBindings"/>
        /// </summary>
        /// <typeparam name="T">The <seealso cref="NavigationViewItemBase"/> instance to set the initial text binding to.</typeparam>
        /// <param name="element">The <seealso cref="NavigationViewItemBase"/> instance to set the initial text binding to.</param>
        /// <param name="localeSetName">The instance name of a <seealso cref="Hi3Helper.Locale"/> members.</param>
        /// <param name="localePropertyName">Name of the locale property</param>
        /// <returns>A reference of the <typeparamref name="T"/></returns>
        internal static ref T BindNavigationViewItemText<T>(this T element, string localeSetName, string localePropertyName)
            where T : NavigationViewItemBase
        {
            NavigationViewItemLocaleTextProperty property = new NavigationViewItemLocaleTextProperty
            {
                LocaleSetName = localeSetName,
                LocalePropertyName = localePropertyName
            };

            if (element is NavigationViewItemHeader elementAsHeader)
            {
                elementAsHeader.Tag = property;
            }
            else
            {
                TextBlock textBlock = new TextBlock().WithTag(property);
                element.Content = textBlock;
            }
            return ref Unsafe.AsRef(ref element);
        }

        internal static void SetAllControlsCursorRecursive(this UIElement element, InputSystemCursor toCursor)
        {
            while (true)
            {
                // DO NOT REMOVE THIS LINE OR YOU WILL FACE THE CONSEQUENCES!
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                if (element == null)
                {
                    return;
                }

                switch (element)
                {
                    case Panel panelKind:
                    {
                        foreach (UIElement panelElements in panelKind.Children)
                        {
                            SetAllControlsCursorRecursive(panelElements, toCursor);
                        }

                        break;
                    }
                    case RadioButtons radioButtonsKind:
                    {
                        foreach (UIElement radioButtonContent in radioButtonsKind.Items.OfType<UIElement>())
                        {
                            radioButtonContent.SetCursor(toCursor);
                        }

                        break;
                    }
                    case Border borderKind:
                        SetAllControlsCursorRecursive(borderKind.Child, toCursor);
                        break;
                    case ComboBox comboBoxKind:
                        comboBoxKind.SetCursor(toCursor);
                        break;
                    case UserControl userControlKind:
                        SetAllControlsCursorRecursive(userControlKind.Content, toCursor);
                        break;
                    case ContentControl { Content: UIElement contentControlKindInner }:
                        SetAllControlsCursorRecursive(contentControlKindInner, toCursor);
                        break;
                }

                switch (element)
                {
                    case NavigationView navigationViewKind:
                    {
                        foreach (var o in navigationViewKind.FindDescendants())
                        {
                            var navigationViewElements = (UIElement)o;
                            if (navigationViewElements is NavigationViewItem)
                            {
                                navigationViewElements.SetCursor(toCursor);
                                continue;
                            }

                            SetAllControlsCursorRecursive(navigationViewElements, toCursor);
                        }

                        break;
                    }
                    case ButtonBase buttonBaseKind:
                    {
                        buttonBaseKind.SetCursor(toCursor);
                        if (buttonBaseKind is Button buttonKind && buttonKind.Flyout != null && buttonKind.Flyout is Flyout buttonKindFlyout)
                        {
                            SetAllControlsCursorRecursive(buttonKindFlyout.Content, toCursor);
                        }

                        break;
                    }
                    case ToggleSwitch:
                        element.SetCursor(toCursor);
                        break;
                }

                if (element.ContextFlyout != null && element.ContextFlyout is Flyout elementFlyoutKind)
                {
                    element = elementFlyoutKind.Content;
                    continue;
                }

                break;
            }
        }

        internal static void ApplyNavigationViewItemLocaleTextBindings(this NavigationView navViewControl)
        {
            foreach (NavigationViewItemBase navItem in navViewControl
                .FindDescendants()
                .OfType<NavigationViewItemBase>())
            {
                string? localeValue = null;
                if (navItem.Content is TextBlock { Tag: NavigationViewItemLocaleTextProperty localeProperty } navItemTextBlock)
                {
                    navItemTextBlock.BindProperty(
                        TextBlock.TextProperty,
                        Locale.Lang,
                        $"{localeProperty.LocaleSetName}.{localeProperty.LocalePropertyName}");
                    localeValue = navItemTextBlock.GetValue(TextBlock.TextProperty) as string;
                }
                else if (navItem is NavigationViewItemHeader { Tag: NavigationViewItemLocaleTextProperty localePropertyOnHeader } navItemAsHeader)
                {
                    navItemAsHeader.BindProperty(
                        ContentControl.ContentProperty,
                        Locale.Lang,
                        $"{localePropertyOnHeader.LocaleSetName}.{localePropertyOnHeader.LocalePropertyName}");
                    localeValue = navItemAsHeader.GetValue(ContentControl.ContentProperty) as string;
                }

                if (!string.IsNullOrEmpty(localeValue))
                {
                    ToolTipService.SetToolTip(navItem, localeValue);
                }
            }

            navViewControl.UpdateLayout();
        }

        internal static ref T BindProperty<T>(this T element, DependencyProperty dependencyProperty, object objectToBind, string propertyName, IValueConverter? converter = null, BindingMode bindingMode = BindingMode.OneWay)
            where T : FrameworkElement
        {
            // Create a new binding instance
            Binding binding = new Binding
            {
                Source = objectToBind,
                Mode = bindingMode,
                Path = new PropertyPath(propertyName),
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
            };

            // If the converter is assigned, then add the converter
            if (converter != null)
            {
                binding.Converter = converter;
            }

            // Set binding to the element
            element.SetBinding(dependencyProperty, binding);

            return ref Unsafe.AsRef(ref element);
        }
#nullable restore

        internal static TButtonBase CreateButtonWithIcon<TButtonBase>(string text = null, string iconGlyph = null, string iconFontFamily = "FontAwesome",
            string buttonStyle = "DefaultButtonStyle", double iconSize = 16d, double? textSize = null, CornerRadius? cornerRadius = null, FontWeight? textWeight = null)
            where TButtonBase : ButtonBase, new()
        {
            Grid contentPanel = CreateIconTextGrid(text, iconGlyph, iconFontFamily, iconSize, textSize, textWeight);
            TButtonBase buttonReturn = new TButtonBase();

            buttonReturn.CornerRadius = cornerRadius ?? AttachRoundedKindCornerRadius(buttonReturn);
            buttonReturn.Content      = contentPanel;
            buttonReturn.Style        = GetApplicationResource<Style>(buttonStyle);
            return buttonReturn;
        }

        internal static Grid CreateIconTextGrid(string text = null, string iconGlyph = null, string iconFontFamily = "FontAwesome",
            double iconSize = 16d, double? textSize = null, FontWeight? textWeight = null)
        {
            bool isHasIcon = !string.IsNullOrEmpty(iconGlyph);
            bool isHasText = !string.IsNullOrEmpty(text);

            if (!isHasIcon && !isHasText)
                throw new NullReferenceException("[UIElementExtensions::CreateIconTextGrid()] At least \"text\" or \"iconGlyph\" must be set!");

            Grid contentPanel = CreateGrid()
                .WithColumns(GridLength.Auto, new GridLength(1, GridUnitType.Star))
                .WithColumnSpacing(8)
                .WithPadding(isHasText ? 4d : 0d, 0d);

            textWeight ??= FontWeights.SemiBold;

            if (isHasIcon) _ = contentPanel.AddElementToGridColumn(new FontIcon
            {
                Glyph = iconGlyph,
                FontSize = iconSize,
                FontFamily = iconFontFamily switch
                {
                    "FontAwesome" => FontCollections.FontAwesomeRegular,
                    "FontAwesomeSolid" => FontCollections.FontAwesomeSolid,
                    "FontAwesomeBrand" => FontCollections.FontAwesomeBrand,
                    _ => GetApplicationResource<FontFamily>(iconFontFamily)
                }
            }, 0, !isHasText ? 2 : 0)
                    .WithMargin(isHasText ? 0d : -5d, isHasText ? 1d : 0d, isHasText ? 0d : -5d, 0d)
                    .WithVerticalAlignment(VerticalAlignment.Center);

            if (!isHasText)
            {
                return contentPanel;
            }

            TextBlock textBlock = contentPanel.AddElementToGridColumn(new TextBlock
            {
                Text       = text,
                FontWeight = textWeight.Value
            }, isHasIcon ? 1 : 0, isHasIcon ? 0 : 2).WithVerticalAlignment(VerticalAlignment.Center);

            if (textSize != null) textBlock.FontSize = textSize.Value;

            return contentPanel;
        }

        internal static Grid       CreateGrid()                                                     => new();
        internal static StackPanel CreateStackPanel(Orientation orientation = Orientation.Vertical) => new() { Orientation = orientation };

        internal static void AddElementToStackPanel(this Panel stackPanel, params FrameworkElement[] elements)
            => AddElementToStackPanel(stackPanel, elements.AsEnumerable());
        internal static void AddElementToStackPanel(this Panel stackPanel, IEnumerable<FrameworkElement> elements)
        {
            foreach (FrameworkElement element in elements)
                stackPanel.Children.Add(element);
        }
        internal static ref TElement AddElementToStackPanel<TElement>(this Panel stackPanel, TElement element)
            where TElement : FrameworkElement
        {
            stackPanel.Children.Add(element);
            return ref Unsafe.AsRef(ref element);
        }

        internal static void AddGridColumns(this Grid grid, params GridLength[] columnWidths)
        {
            if (columnWidths.Length == 0)
                throw new IndexOutOfRangeException("\"columnWidth\" cannot be empty!");

            foreach (var t in columnWidths)
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = t
                });
        }

        internal static void AddGridColumns(this Grid grid, int count, GridLength? columnWidth = null)
        {
            for (; count > 0; count--) grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = columnWidth ?? GridLength.Auto
            });
        }

        internal static void AddGridRows(this Grid grid, int count, GridLength? columnHeight = null)
        {
            for (; count > 0; count--) grid.RowDefinitions.Add(new RowDefinition
            {
                Height = columnHeight ?? GridLength.Auto
            });
        }

        internal static ref TElement AddElementToGridRowColumn<TElement>(this Grid grid, TElement element, int rowIndex = 0, int columnIndex = 0, int rowSpan = 0, int columnSpan = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridRowPosition(element, rowIndex, rowSpan);
            SetElementGridColumnPosition(element, columnIndex, columnSpan);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement AddElementToGridRow<TElement>(this Grid grid, TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridRowPosition(element, index, span);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement AddElementToGridColumn<TElement>(this Grid grid, TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            grid.Children.Add(element);
            SetElementGridColumnPosition(element, index, span);
            return ref Unsafe.AsRef(ref element);
        }

        internal static void ClearChildren<TElement>(this TElement element)
            where TElement : Panel
        {
            if (element == null) return;
            element.Children.Clear();
        }

        internal static void SetElementGridRowPosition<TElement>(TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            Grid.SetRow(element, index);
            if (span > 0) Grid.SetRowSpan(element, span);
        }
        internal static void SetElementGridColumnPosition<TElement>(TElement element, int index, int span = 0)
            where TElement : FrameworkElement
        {
            Grid.SetColumn(element, index);
            if (span > 0) Grid.SetColumnSpan(element, span);
        }

        internal static ref TextBlock AddTextBlockNewLine(this TextBlock textBlock, int count = 1)
        {
            while (count-- > 0) { textBlock.Inlines.Add(new LineBreak()); }
            return ref Unsafe.AsRef(ref textBlock);
        }

        internal static ref TextBlock AddTextBlockLine(this TextBlock textBlock, string message, bool appendSpaceAtEnd, FontWeight? weight = null, double size = 14d)
        {
            message += ' ';
            return ref textBlock.AddTextBlockLine(message, weight, size);
        }

        internal static ref TextBlock AddTextBlockLine(this TextBlock textBlock, string message, FontWeight? weight = null, double size = 14d)
        {
            weight ??= FontWeights.Normal;
            textBlock.Inlines.Add(new Run { Text = message, FontWeight = weight.Value, FontSize = size });
            return ref Unsafe.AsRef(ref textBlock);
        }

        internal static TReturnType GetApplicationResource<TReturnType>(string resourceKey)
        {
            if (!Application.Current.Resources.TryGetValue(resourceKey, out object resourceObj))
                throw new KeyNotFoundException($"Application resource with key: {resourceKey} does not exist!");

            if (resourceObj is not TReturnType resource)
                throw new InvalidCastException($"Object type for resource \"{resourceKey}\" is not valid! Trying to get type: {typeof(TReturnType).Name}, but the object type is: {resourceObj.GetType().Name}");

            return resource;
        }

        internal static void SetApplicationResource(string resourceKey, object value)
        {
            if (!Application.Current.Resources.ContainsKey(resourceKey))
                throw new KeyNotFoundException($"Application resource with key: {resourceKey} does not exist!");

            Application.Current.Resources[resourceKey] = value;
        }

        internal static CornerRadius GetElementCornerRadius(FrameworkElement element, CornerRadiusKind kind = CornerRadiusKind.Normal)
        {
            switch (kind)
            {
                default:
                    return new CornerRadius(8);
                case CornerRadiusKind.Rounded:
                    double radiusSize = element.ActualHeight / 2;
                    return new CornerRadius(radiusSize);
            }
        }

        internal static CornerRadius AttachRoundedKindCornerRadius(FrameworkElement element)
        {
            CornerRadius initialRadius = GetElementCornerRadius(element, CornerRadiusKind.Rounded);
            element.SizeChanged += (_, _) => InnerSetCornerRadius(element, GetElementCornerRadius(element, CornerRadiusKind.Rounded));
            return initialRadius;
        }

        internal static void FindAndSetTextBlockWrapping(this UIElement element, TextWrapping wrap = TextWrapping.Wrap, HorizontalAlignment posAlign = HorizontalAlignment.Center, TextAlignment textAlign = TextAlignment.Center, bool recursiveAssignment = false, bool isParentAButton = false)
        {
            while (true)
            {
                if (element is TextBlock textBlock)
                {
                    textBlock.TextWrapping = wrap;
                    if (isParentAButton)
                    {
                        textBlock.HorizontalAlignment     = posAlign;
                        textBlock.HorizontalTextAlignment = textAlign;
                    }
                }

                if (!recursiveAssignment) return;

                switch (element)
                {
                    case ButtonBase { Content: UIElement buttonContent }:
                        buttonContent.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, true, true);
                        break;
                    case ButtonBase button:
                    {
                        if (button.Content is string buttonString) button.Content = new TextBlock { Text = buttonString, TextWrapping = wrap, HorizontalAlignment = HorizontalAlignment.Center };
                        break;
                    }
                    case Panel panel:
                    {
                        foreach (UIElement childrenElement in panel.Children!) childrenElement.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, true, isParentAButton);
                        break;
                    }
                    case ScrollViewer { Content: UIElement elementInner }:
                        elementInner.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, true, isParentAButton);
                        break;
                }

                switch (element)
                {
                    case ContentControl { Content: UIElement contentControlInner } contentControl and (SettingsCard or Expander):
                    {
                        contentControlInner.FindAndSetTextBlockWrapping(wrap, posAlign, textAlign, true, isParentAButton);

                        if (contentControl is Expander { Header: UIElement expanderHeader })
                        {
                            element             = expanderHeader;
                            continue;
                        }

                        break;
                    }
                    case InfoBar { Content: UIElement infoBarInner }:
                        element             = infoBarInner;
                        continue;
                }

                break;
            }
        }

        internal static ref TElement WithWidthAndHeight<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement
        {
            SetWidth(element, uniform);
            SetHeight(element, uniform);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMinWidthAndMinHeight<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement
        {
            SetMinWidth(element, uniform);
            SetMinHeight(element, uniform);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithWidth<TElement>(this TElement element, double width)
            where TElement : FrameworkElement
        {
            SetWidth(element, width);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMinWidth<TElement>(this TElement element, double width)
            where TElement : FrameworkElement
        {
            SetMinWidth(element, width);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithHeight<TElement>(this TElement element, double height)
            where TElement : FrameworkElement
        {
            SetHeight(element, height);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMinHeight<TElement>(this TElement element, double height)
            where TElement : FrameworkElement
        {
            SetMinHeight(element, height);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TGrid WithRowSpacing<TGrid>(this TGrid grid, double rowSpacing)
            where TGrid : Grid
        {
            SetRowSpacing(grid, rowSpacing);
            return ref Unsafe.AsRef(ref grid);
        }
        internal static ref TGrid WithColumnSpacing<TGrid>(this TGrid grid, double columnSpacing)
            where TGrid : Grid
        {
            SetColumnSpacing(grid, columnSpacing);
            return ref Unsafe.AsRef(ref grid);
        }
        internal static ref TGrid WithColumns<TGrid>(this TGrid grid, params GridLength[] columns)
            where TGrid : Grid
        {
            SetGridSlices(grid, columns, true);
            return ref Unsafe.AsRef(ref grid);
        }
        internal static ref TGrid WithRows<TGrid>(this TGrid grid, params GridLength[] rows)
            where TGrid : Grid
        {
            SetGridSlices(grid, rows, false);
            return ref Unsafe.AsRef(ref grid);
        }

        internal static ref TElement WithCornerRadius<TElement>(this TElement element, double uniform, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement
        {
            SetCornerRadius(element, uniform, kind);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithCornerRadius<TElement>(this TElement element, double horizontal, double vertical, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement
        {
            SetCornerRadius(element, horizontal, vertical, kind);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithCornerRadius<TElement>(this TElement element, double left, double top, double right, double bottom, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement
        {
            SetCornerRadius(element, left, top, right, bottom, kind);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithVisibility<TElement>(this TElement element, Visibility visibility)
            where TElement : FrameworkElement
        {
            SetVisibility(element, visibility);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithTag<TElement>(this TElement element, object tag)
            where TElement : FrameworkElement
        {
            SetTag(element, tag);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithDataContext<TElement>(this TElement element, object dataContext)
            where TElement : FrameworkElement
        {
            SetDataContext(element, dataContext);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithBackground<TElement>(this TElement element, Brush brush)
            where TElement : FrameworkElement
        {
            SetBackground(element, brush);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithForeground<TElement>(this TElement element, Brush brush)
            where TElement : FrameworkElement
        {
            SetForeground(element, brush);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithOpacity<TElement>(this TElement element, double opacity)
            where TElement : FrameworkElement
        {
            SetOpacity(element, opacity);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithStretch<TElement>(this TElement element, Stretch stretch)
            where TElement : FrameworkElement
        {
            SetStretch(element, stretch);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithPadding<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement
        {
            SetPadding(element, uniform);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithPadding<TElement>(this TElement element, double horizontal, double vertical)
            where TElement : FrameworkElement
        {
            SetPadding(element, horizontal, vertical);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithPadding<TElement>(this TElement element, double left, double top, double right, double bottom)
            where TElement : FrameworkElement
        {
            SetPadding(element, left, top, right, bottom);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithPadding<TElement>(this TElement element, Thickness thickness)
            where TElement : FrameworkElement
        {
            SetPadding(element, thickness);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TElement WithMargin<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement
        {
            SetMargin(element, uniform);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMargin<TElement>(this TElement element, double horizontal, double vertical)
            where TElement : FrameworkElement
        {
            SetMargin(element, horizontal, vertical);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMargin<TElement>(this TElement element, double left, double top, double right, double bottom)
            where TElement : FrameworkElement
        {
            SetMargin(element, left, top, right, bottom);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithMargin<TElement>(this TElement element, Thickness thickness)
            where TElement : FrameworkElement
        {
            SetMargin(element, thickness);
            return ref Unsafe.AsRef(ref element);
        }

        internal static ref TButton WithFlyout<TButton>(this TButton button, FlyoutBase flyout)
            where TButton : Button
        {
            SetButtonFlyout(button, flyout);
            return ref Unsafe.AsRef(ref button);
        }

        internal static ref TElement WithHorizontalAlignment<TElement>(this TElement element, HorizontalAlignment alignment)
            where TElement : FrameworkElement
        {
            SetHorizontalAlignment(element, alignment);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithHorizontalContentAlignment<TElement>(this TElement element, HorizontalAlignment alignment)
            where TElement : Control
        {
            SetHorizontalContentAlignment(element, alignment);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithVerticalAlignment<TElement>(this TElement element, VerticalAlignment alignment)
            where TElement : FrameworkElement
        {
            SetVerticalAlignment(element, alignment);
            return ref Unsafe.AsRef(ref element);
        }
        internal static ref TElement WithVerticalContentAlignment<TElement>(this TElement element, VerticalAlignment alignment)
            where TElement : Control
        {
            SetVerticalContentAlignment(element, alignment);
            return ref Unsafe.AsRef(ref element);
        }

        internal static void SetGridSlices<TGrid>(this TGrid grid, GridLength[] gridSlices, bool isColumn)
            where TGrid : Grid
        {
            foreach (var t in gridSlices)
            {
                if (isColumn)
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = t });
                else
                    grid.RowDefinitions.Add(new RowDefinition { Height = t });
            }
        }

        internal static void SetVisibility<TElement>(this TElement element, Visibility visibility)
            where TElement : UIElement => element.Visibility = visibility;
        internal static void SetTag<TElement>(this TElement element, object tag)
            where TElement : FrameworkElement => element.Tag = tag;
        internal static void SetDataContext<TElement>(this TElement element, object dataContext)
            where TElement : FrameworkElement => element.DataContext = dataContext;

        internal static void SetCornerRadius<TElement>(this TElement element, double uniform, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement => SetCornerRadius(element, uniform, uniform, uniform, uniform, kind);
        internal static void SetCornerRadius<TElement>(this TElement element, double horizontal, double vertical, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement => SetCornerRadius(element, horizontal, vertical, horizontal, vertical, kind);
        internal static void SetCornerRadius<TElement>(this TElement element, double left, double top, double right, double bottom, CornerRadiusKind kind = CornerRadiusKind.Normal)
            where TElement : FrameworkElement
        {
            CornerRadius cornerRadius =
            kind == CornerRadiusKind.Normal ?
                new CornerRadius(left, top, right, bottom) :
                AttachRoundedKindCornerRadius(element);

            InnerSetCornerRadius(element, cornerRadius);
        }

        internal static void SetRowSpacing<TGrid>(this TGrid element, double rowSpacing)
            where TGrid : Grid => element.RowSpacing = rowSpacing;
        internal static void SetColumnSpacing<TGrid>(this TGrid element, double columnSpacing)
            where TGrid : Grid => element.ColumnSpacing = columnSpacing;

        internal static void SetMinWidth<TElement>(this TElement element, double width)
            where TElement : FrameworkElement => element.MinWidth = width;
        internal static void SetMinHeight<TElement>(this TElement element, double height)
            where TElement : FrameworkElement => element.MinHeight = height;
        internal static void SetWidth<TElement>(this TElement element, double width)
            where TElement : FrameworkElement => element.Width = width;
        internal static void SetHeight<TElement>(this TElement element, double height)
            where TElement : FrameworkElement => element.Height = height;

        internal static void SetPadding<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement => SetPadding(element, uniform, uniform, uniform, uniform);
        internal static void SetPadding<TElement>(this TElement element, double horizontal, double vertical)
            where TElement : FrameworkElement => SetPadding(element, horizontal, vertical, horizontal, vertical);
        internal static void SetPadding<TElement>(this TElement element, double left, double top, double right, double bottom)
            where TElement : FrameworkElement => element.SetPadding(new Thickness(left, top, right, bottom));
        internal static void SetPadding<TElement>(this TElement element, Thickness thickness)
            where TElement : FrameworkElement
        {
            if (element == null) return;

            switch (element)
            {
                case Control control:
                    control.Padding = thickness;
                    break;
                case Border border:
                    border.Padding = thickness;
                    break;
                case Grid grid:
                    grid.Padding = thickness;
                    break;
                case StackPanel stackPanel:
                    stackPanel.Padding = thickness;
                    break;
                case TextBlock textBlock:
                    textBlock.Padding = thickness;
                    break;
            }
        }

        internal static void SetBackground<TElement>(this TElement element, Brush brush)
            where TElement : FrameworkElement
        {
            if (element == null) return;

            switch (element)
            {
                case Control control:
                    control.Background = brush;
                    break;
                case Border border:
                    border.Background = brush;
                    break;
                case Grid grid:
                    grid.Background = brush;
                    break;
                case StackPanel stackPanel:
                    stackPanel.Background = brush;
                    break;
            }
        }

        internal static void SetForeground<TElement>(this TElement element, Brush brush)
            where TElement : FrameworkElement
        {
            if (element == null) return;

            switch (element)
            {
                case Control control:
                    control.Foreground = brush;
                    break;
                case TextBlock textBlock:
                    textBlock.Foreground = brush;
                    break;
            }
        }

        internal static void SetOpacity<TElement>(this TElement element, double opacity)
            where TElement : FrameworkElement => element.Opacity = opacity;

        internal static void SetStretch<TElement>(this TElement element, Stretch stretch)
            where TElement : FrameworkElement
        {
            switch (element)
            {
                case Image image:
                    image.Stretch = stretch;
                    break;
                case MediaPlayerElement mediaPlayer:
                    mediaPlayer.Stretch = stretch;
                    break;
            }
        }

        internal static void SetMargin<TElement>(this TElement element, double uniform)
            where TElement : FrameworkElement => SetMargin(element, uniform, uniform, uniform, uniform);
        internal static void SetMargin<TElement>(this TElement element, double horizontal, double vertical)
            where TElement : FrameworkElement => SetMargin(element, horizontal, vertical, horizontal, vertical);
        internal static void SetMargin<TElement>(this TElement element, double left, double top, double right, double bottom)
            where TElement : FrameworkElement => element.SetMargin(new Thickness(left, top, right, bottom));
        internal static void SetMargin<TElement>(this TElement element, Thickness thickness)
            where TElement : FrameworkElement => element.Margin = thickness;

        internal static void SetButtonFlyout<TButton>(this TButton button, FlyoutBase flyout)
            where TButton : Button => button.Flyout = flyout;

        internal static void SetHorizontalContentAlignment<TElement>(this TElement element, HorizontalAlignment alignment)
            where TElement : Control => element.HorizontalContentAlignment = alignment;
        internal static void SetVerticalContentAlignment<TElement>(this TElement element, VerticalAlignment alignment)
            where TElement : Control => element.VerticalContentAlignment = alignment;

        internal static void SetHorizontalAlignment<TElement>(this TElement element, HorizontalAlignment alignment)
            where TElement : FrameworkElement => element.HorizontalAlignment = alignment;
        internal static void SetVerticalAlignment<TElement>(this TElement element, VerticalAlignment alignment)
            where TElement : FrameworkElement => element.VerticalAlignment = alignment;

        private static void InnerSetCornerRadius<TElement>(TElement element, CornerRadius cornerRadius)
            where TElement : FrameworkElement
        {
            if (element == null) return;

            switch (element)
            {
                case Control control:
                    control.CornerRadius = cornerRadius;
                    break;
                case StackPanel stackPanel:
                    stackPanel.CornerRadius = cornerRadius;
                    break;
                case Grid grid:
                    grid.CornerRadius = cornerRadius;
                    break;
            }
        }

        internal static void ApplyDropShadow(this FrameworkElement element, Color? shadowColor = null,
            double blurRadius = 10, double opacity = 0.25, bool isMasked = true, Vector3? offset = null)
        {
            var shadowPanel = element.FindDescendant("ShadowGrid");
            if (shadowPanel == null)
            {
                shadowPanel = CreateGrid()
                    .WithHorizontalAlignment(HorizontalAlignment.Stretch)
                    .WithVerticalAlignment(VerticalAlignment.Stretch);
                shadowPanel.Name = "ShadowGrid";
            }

            // If the element happened to have the parent null (in case of
            // the element is initialized but not added to a Panel yet), then
            // attach the shadow while the element is already loaded.
            if (element.Parent is null) element.Loaded += (_, _) =>
            {
                if (element.DispatcherQueue.HasThreadAccess)
                    AssignShadowAttachment(element, isMasked);
                else
                    element.DispatcherQueue.TryEnqueue(() => AssignShadowAttachment(element, isMasked));
            };
            // However if the element has been added to a parent
            // Panel, then assign it right away.
            else AssignShadowAttachment(element, isMasked);
            return;

            void AssignShadowAttachment(FrameworkElement thisElement, bool innerMasked)
            {
                switch (thisElement)
                {
                    case IconElement iconElement:
                        AttachShadow(iconElement, true, offset);
                        break;
                    case Image imageElement:
                        AttachShadow(imageElement, true, offset);
                        break;
                    case ImageEx.ImageEx imageExElement:
                        AttachShadow(imageExElement, true, offset);
                        break;
                    default:
                        AttachShadow(element, innerMasked, offset);
                        break;
                }
            }

            void AttachShadow(FrameworkElement thisElement, bool innerMask, Vector3? thisOffset)
            {
                FrameworkElement xamlRoot = thisElement.Parent as FrameworkElement ?? thisElement.FindDescendant<Grid>();

                if (xamlRoot is Border borderParent)
                    xamlRoot = borderParent.Child as Grid ?? borderParent.Child.FindAscendant<Grid>();

                if (xamlRoot is not Panel panel)
                {
                    return;
                }

                try
                {
                    panel.Children.Add(shadowPanel);
                    Canvas.SetZIndex(shadowPanel, -1);
                    if (shadowPanel is not Panel)
                        throw new NotSupportedException("The ShadowGrid must be at least a Grid or StackPanel or any \"Panel\" elements");

                    if (xamlRoot == null || xamlRoot is not Panel)
                        throw new NullReferenceException("The element must be inside of a Grid or StackPanel or any \"Panel\" elements");

                    thisElement.ApplyDropShadow(shadowPanel, shadowColor, blurRadius, opacity, innerMask, thisOffset);
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledXaml);
                    Logger.LogWriteLine($"Failed while attaching shadow to an element\r\n{ex}", LogType.Warning, true);
                }
            }
        }

        internal static void ApplyDropShadow(this FrameworkElement from, FrameworkElement to, Color? shadowColor = null,
            double blurRadius = 10, double opacity = 0.25, bool isMasked = false, Vector3? offset = null)
        {
            offset ??= Vector3.Zero;
            // ReSharper disable ConstantConditionalAccessQualifier
            string passedValue = $"{offset?.X},{offset?.Y},{offset?.Z}";
            // ReSharper restore ConstantConditionalAccessQualifier

            AttachedDropShadow shadow = new AttachedDropShadow
            {
                Color = shadowColor ?? Colors.Black,
                BlurRadius = blurRadius,
                Opacity = opacity,
                CastTo = to,
                IsMasked = isMasked,
                Offset = passedValue
            };
            Effects.SetShadow(from, shadow);
        }
    }
}
