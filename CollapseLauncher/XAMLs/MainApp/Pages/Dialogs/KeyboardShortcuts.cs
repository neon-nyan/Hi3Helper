using CollapseLauncher.CustomControls;
using CollapseLauncher.Extension;
using CollapseLauncher.Helper.Metadata;
using CommunityToolkit.WinUI;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using static CollapseLauncher.Dialogs.SimpleDialogs;
using static Hi3Helper.Locale;
using static Hi3Helper.Logger;
using static Hi3Helper.Shared.Region.LauncherConfig;
using CollapseUIExt = CollapseLauncher.Extension.UIElementExtensions;
// ReSharper disable StringLiteralTypo
// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable PossibleNullReferenceException
// ReSharper disable AssignNullToNotNullAttribute
// ReSharper disable InconsistentNaming

namespace CollapseLauncher.Dialogs
{
    public static class KeyboardShortcuts
    {
        #region Properties
        public static event EventHandler<int> KeyboardShortcutsEvent;
        private static int _pageNum;
        private static int _oldSender;
        private static int _buttonWidth;
        public static bool CannotUseKbShortcuts { get; set; }
        #endregion

        #region Show Shortcuts ContentDialog
        public static async Task<ContentDialogResult> Dialog_ShowKbShortcuts(UIElement content, int page = 0)
        {
            _buttonWidth = int.Max(Lang._KbShortcuts.GeneralTab.Length * 5
                          + Lang._KbShortcuts.SwitchTab.Length * 5
                          + Lang._KbShortcuts.GameFolderTab.Length * 5
                          + Lang._KbShortcuts.GameManagementTab.Length * 5
                          + 5 * 50, 400);

            int swapButtonWidth = Lang._KbShortcuts.Switch_SwapBtn.Length * 5 + 2 * 50;

            StackPanel mainStack = CollapseUIExt.CreateStackPanel();
            StackPanel mainStackContent = CollapseUIExt.CreateStackPanel();

            _oldSender = page;

            // General shortcuts
            StackPanel genStack = CollapseUIExt.CreateStackPanel().WithVisibility(Visibility.Collapsed).WithWidth(_buttonWidth);
            genStack.AddElementToStackPanel(
                new TextBlock
                {
                    Text        = Lang._KbShortcuts.General_Title,
                    FontSize    = 16,
                    FontWeight  = FontWeights.Bold
                }.WithMargin(0d, 0d, 0d, 2d),
                new MenuFlyoutSeparator().WithMargin(0d, 8d),
                GenerateShortcutBlock("KbShortcutsMenu",   KbShortcutList["KbShortcutsMenu"],   Lang._KbShortcuts.General_OpenMenu, Lang._KbShortcuts.General_OpenMenu_Desc),
                GenerateShortcutBlock("HomePage",          KbShortcutList["HomePage"],          Lang._KbShortcuts.General_GoHome),
                GenerateShortcutBlock("SettingsPage",      KbShortcutList["SettingsPage"],      Lang._KbShortcuts.General_GoSettings),
                GenerateShortcutBlock("NotificationPanel", KbShortcutList["NotificationPanel"], Lang._KbShortcuts.General_OpenNotifTray),
                GenerateShortcutBlock("ReloadRegion",      KbShortcutList["ReloadRegion"],      Lang._KbShortcuts.General_ReloadRegion, Lang._KbShortcuts.General_ReloadRegion_Desc),
                new MenuFlyoutSeparator().WithMargin(0d, 10d, 0d, 8d)
                );
            _pageNum++;

            // Region/Game Shortcuts
            StackPanel changeStack = CollapseUIExt.CreateStackPanel().WithVisibility(Visibility.Collapsed).WithWidth(_buttonWidth);
            Grid changeTitleGrid = CollapseUIExt.CreateGrid()
                .WithColumns(GridLength.Auto, new GridLength(1.0d, GridUnitType.Star)).WithColumnSpacing(5d)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithHorizontalAlignment(HorizontalAlignment.Stretch);

            StackPanel changeTitleStack = changeTitleGrid.AddElementToGridColumn(CollapseUIExt.CreateStackPanel(), 0);
            changeTitleStack.AddElementToStackPanel(
                new TextBlock
                {
                    Text            = Lang._KbShortcuts.Switch_Title, FontSize = 16,
                    FontWeight      = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2)
                },
                new TextBlock
                {
                    Text            = Lang._KbShortcuts.Switch_Subtitle,
                    FontSize        = 11.5,
                    TextWrapping    = TextWrapping.Wrap,
                    MaxWidth        = _buttonWidth - swapButtonWidth
                });

            string gameMod = KbShortcutList["GameSelection"].GetFormattedModifier();
            string regionMod = KbShortcutList["RegionSelection"].GetFormattedModifier();

            Button modifierSwap =
                CollapseUIExt.CreateButtonWithIcon<Button>(
                    text:               string.Format(Lang._KbShortcuts!.Switch_SwapBtn, gameMod, regionMod),
                    iconGlyph:          "",
                    iconFontFamily:     "FontAwesomeSolid",
                    buttonStyle:        "DefaultButtonStyle",
                    iconSize:           12d,
                    textSize:           null
                    )
                .WithDataContext(new KbShortcutChangeData { KeyName = "GameSelection", PageNumber = 1 })
                .WithHorizontalAlignment(HorizontalAlignment.Right);

            modifierSwap.Click += Swap_Click;
            changeTitleGrid.AddElementToGridColumn(modifierSwap, 1);
            changeStack.AddElementToStackPanel(changeTitleGrid);

            changeStack.AddElementToStackPanel(
                new MenuFlyoutSeparator().WithMargin(0d, 8d),
                GenerateShortcutBlock("GameSelection",   KbShortcutList["GameSelection"],   Lang._KbShortcuts.Switch_ChangeGame,   string.Format(Lang._KbShortcuts.Switch_ChangeGame_Desc,   gameMod),   false),
                GenerateShortcutBlock("RegionSelection", KbShortcutList["RegionSelection"], Lang._KbShortcuts.Switch_ChangeRegion, string.Format(Lang._KbShortcuts.Switch_ChangeRegion_Desc, regionMod), false),
                new MenuFlyoutSeparator().WithMargin(0d, 10d, 0d, 8d)
                );
            _pageNum++;

            // Game folder
            StackPanel gameFolderStack = CollapseUIExt.CreateStackPanel().WithVisibility(Visibility.Collapsed).WithWidth(_buttonWidth);
            gameFolderStack.AddElementToStackPanel(
                new TextBlock { Text = Lang._KbShortcuts.GameFolder_Title, FontSize = 16, FontWeight = FontWeights.Bold }.WithMargin(0d, 0d, 0d, 2d),
                new MenuFlyoutSeparator().WithMargin(0d, 8d),
                GenerateShortcutBlock("ScreenshotFolder", KbShortcutList["ScreenshotFolder"], Lang._KbShortcuts.GameFolder_ScreenshotFolder),
                GenerateShortcutBlock("GameFolder",       KbShortcutList["GameFolder"],       Lang._KbShortcuts.GameFolder_MainFolder),
                GenerateShortcutBlock("CacheFolder",      KbShortcutList["CacheFolder"],      Lang._KbShortcuts.GameFolder_CacheFolder),
                new MenuFlyoutSeparator().WithMargin(0d, 10d, 0d, 8d)
                );
            _pageNum++;

            // Game management
            StackPanel gameManageStack = CollapseUIExt.CreateStackPanel().WithVisibility(Visibility.Collapsed).WithWidth(_buttonWidth);
            gameManageStack.AddElementToStackPanel(
                new TextBlock { Text = Lang._KbShortcuts.GameManagement_Title, FontSize = 16, FontWeight = FontWeights.Bold }.WithMargin(0d, 0d, 0d, 2d),
                new TextBlock { Text = Lang._KbShortcuts.GameManagement_Subtitle, FontSize = 11.5, TextWrapping = TextWrapping.Wrap },
                new MenuFlyoutSeparator().WithMargin(0d, 8d),
                GenerateShortcutBlock("ForceCloseGame",   KbShortcutList["ForceCloseGame"],   Lang._KbShortcuts.GameManagement_ForceCloseGame, Lang._KbShortcuts.GameManagement_ForceCloseGame_Desc),
                GenerateShortcutBlock("RepairPage",       KbShortcutList["RepairPage"],       Lang._KbShortcuts.GameManagement_GoRepair),
                GenerateShortcutBlock("GameSettingsPage", KbShortcutList["GameSettingsPage"], Lang._KbShortcuts.GameManagement_GoSettings),
                GenerateShortcutBlock("CachesPage",       KbShortcutList["CachesPage"],       Lang._KbShortcuts.GameManagement_GoCaches),
                new MenuFlyoutSeparator().WithMargin(0d, 10d, 0d, 8d)
                );
            _pageNum = 0;

            StackPanel buttonStack = CollapseUIExt.CreateStackPanel(Orientation.Horizontal)
                .WithMargin(0d, 10d, 0d, 0d).WithHorizontalAlignment(HorizontalAlignment.Center);

            Button genButton         = CollapseUIExt.CreateButtonWithIcon<Button>(text: Lang._KbShortcuts!.GeneralTab)          .WithDataContext(0);
            Button changeButton      = CollapseUIExt.CreateButtonWithIcon<Button>(text: Lang._KbShortcuts!.SwitchTab)           .WithDataContext(1);
            Button gameFolderButton  = CollapseUIExt.CreateButtonWithIcon<Button>(text: Lang._KbShortcuts!.GameFolderTab)       .WithDataContext(2);
            Button gameManagerButton = CollapseUIExt.CreateButtonWithIcon<Button>(text: Lang._KbShortcuts!.GameManagementTab)   .WithDataContext(3);

            List<StackPanel> stacks  = [genStack, changeStack, gameFolderStack, gameManageStack];
            List<Button>     buttons = [genButton, changeButton, gameFolderButton, gameManagerButton];

            foreach (Button button in buttons)
            {
                button.Click += (o, _) => { ChangeMenuVisibility((int)((Button)o).DataContext, stacks, buttons); };
                button.SetMargin(3d, 0d);
                button.SetCornerRadius(15d);
                buttonStack.AddElementToStackPanel(button);
            }

            mainStack.AddElementToStackPanel(mainStackContent, buttonStack);
            mainStackContent.AddElementToStackPanel(stacks);

            ChangeMenuVisibility(page, stacks, buttons);

            return await SpawnDialog(
                    Lang._KbShortcuts.DialogTitle,
                    mainStack,
                    content,
                    Lang._Misc.Close
                );
        }

        private static Grid GenerateShortcutBlock(string keyName, KbShortcut shortcut, string description, string example = null, bool enableSwapButton = true)
        {
            Grid shortcutGrid = CollapseUIExt.CreateGrid()
                .WithColumns(GridLength.Auto, new GridLength(1d, GridUnitType.Star)).WithColumnSpacing(5)
                .WithMargin(0d, 8d)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithHorizontalAlignment(HorizontalAlignment.Stretch);

            StackPanel shortcutDesc = CollapseUIExt.CreateStackPanel()
                .WithMargin(0d, 1d, 5d, 1d)
                .WithHorizontalAlignment(HorizontalAlignment.Left)
                .WithVerticalAlignment(VerticalAlignment.Center);

            int maxLen = enableSwapButton ? _buttonWidth - 180 : _buttonWidth - 150;

            if (example != null)
            {
                shortcutDesc.AddElementToStackPanel(
                    new TextBlock
                    {
                        Text         = description, FontSize = 14,
                        TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen
                    }.WithMargin(5d, 2d, 0d, 1d),
                    new TextBlock
                    {
                        Text         = example, FontSize = 11, Margin = new Thickness(5, 1, 0, 2),
                        TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen
                    }.WithMargin(5d, 1d, 0d, 2d));
            }
            else
            {
                shortcutDesc.AddElementToStackPanel(
                    new TextBlock
                    {
                        Text         = description, FontSize = 14, Margin = new Thickness(5, 1, 5, 1),
                        TextWrapping = TextWrapping.Wrap, MaxWidth = maxLen
                    });
            }

            shortcutGrid.AddElementToGridColumn(shortcutDesc, 0);

            StackPanel shortcutButtons = CollapseUIExt.CreateStackPanel(Orientation.Horizontal)
                .WithHorizontalAlignment(HorizontalAlignment.Right);

            if (enableSwapButton)
            {
                Button shortcutSwap =
                    CollapseUIExt.CreateButtonWithIcon<Button>(
                        iconGlyph:      "",
                        iconSize:       12d,
                        iconFontFamily: "FontAwesomeSolid",
                        cornerRadius:   new CornerRadius(5d)
                    )
                    .WithMargin(0d, 0d, 5d, 0d)
                    .WithDataContext(new KbShortcutChangeData
                    {
                        Description = description,
                        PageNumber = _pageNum,
                        KeyName = keyName,
                        Shortcut = shortcut
                    });
                shortcutButtons.AddElementToStackPanel(shortcutSwap);
                shortcutSwap.Click += Swap_Click;
            }

            if (shortcut.Modifier != VirtualKeyModifiers.None)
            {
                shortcutButtons.AddElementToStackPanel(
                    CreateKeyBoardButton(shortcut.GetFormattedModifier()),
                    new TextBlock
                        {
                        Text = "+",
                        FontWeight = FontWeights.Bold,
                        FontSize = 20
                    }.WithVerticalAlignment(VerticalAlignment.Center)
                    .WithHorizontalAlignment(HorizontalAlignment.Right));
            }

            shortcutButtons.AddElementToStackPanel(CreateKeyBoardButton(shortcut.GetKey(keyName)));
            shortcutGrid.AddElementToGridColumn(shortcutButtons, 1);

            return shortcutGrid;
        }

        private static Border CreateKeyBoardButton(string key)
        {
            Border keyBoxBorder = new Border
            {
                Height = 42,
                Width = 42,
                Margin = new Thickness(5, 0, 5, 0),
                CornerRadius = new CornerRadius(5),
                Background = CollapseUIExt.GetApplicationResource<Brush>("SystemFillColorAttentionBrush")
            };

            ThemeShadow ts = new ThemeShadow();
            ts.Receivers.Add(keyBoxBorder);
            keyBoxBorder.Translation += Shadow48;
            keyBoxBorder.Shadow = ts;

            TextBlock keyBox = new TextBlock
            {
                Text = key,
                Foreground = new SolidColorBrush(Application.Current.RequestedTheme == ApplicationTheme.Dark ? Colors.Black : Colors.White),
                FontWeight = FontWeights.Medium,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            keyBoxBorder.Child = keyBox;

            return keyBoxBorder;
        }

        private static async void ChangeMenuVisibility<T1, T2>(int sender, List<T1> stacks, List<T2> buttons)
            where T1 : FrameworkElement
            where T2 : FrameworkElement
        {
            try
            {
                (buttons[_oldSender] as Button).Style = CollapseUIExt.GetApplicationResource<Style>("DefaultButtonStyle");
                FrameworkElement oldStack = stacks[_oldSender];
                (buttons[sender] as Button).Style = CollapseUIExt.GetApplicationResource<Style>("AccentButtonStyle");
                FrameworkElement newStack = stacks[sender];

                if (sender == _oldSender)
                {
                    oldStack.Visibility = Visibility.Collapsed;
                    newStack.Visibility = Visibility.Visible;
                    return;
                }

                _oldSender = sender;

                Storyboard storyboard = new Storyboard();
                DoubleAnimation opacityAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(0.2))
                };

                Storyboard.SetTarget(opacityAnimation, oldStack);
                Storyboard.SetTargetProperty(opacityAnimation, "Opacity");
                storyboard.Children.Add(opacityAnimation);

                storyboard.Begin();

                await Task.Delay(200);
                newStack.Visibility = Visibility.Visible;
                oldStack.Visibility = Visibility.Collapsed;

                Storyboard storyboard2 = new Storyboard();
                DoubleAnimation opacityAnimation2 = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = new Duration(TimeSpan.FromSeconds(0.2))
                };

                Storyboard.SetTarget(opacityAnimation2, newStack);
                Storyboard.SetTargetProperty(opacityAnimation2, "Opacity");
                storyboard2.Children.Add(opacityAnimation2);

                storyboard2.Begin();
            }
            catch (Exception e)
            {
                LogWriteLine(e.ToString());
            }
        }
        #endregion

        #region Change Key Combinations
        private static async Task Dialog_SwitchKey(UIElement content, KbShortcutChangeData data)
        {
            StackPanel mainSwitchKeyContent = CollapseUIExt.CreateStackPanel();
            mainSwitchKeyContent.MinWidth = 350d;
            mainSwitchKeyContent.MaxWidth = 600d;

            StackPanel helpStack = CollapseUIExt.CreateStackPanel();
            helpStack.MaxWidth = 360d;
            Flyout helpFlyout = new Flyout
                                {
                                    Content = helpStack,
                                    Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                                };
            helpStack.AddElementToStackPanel(
                new TextBlock
                {
                    Text = Lang._KbShortcuts.ChangeShortcut_Help1,
                    TextWrapping = TextWrapping.Wrap
                }.WithMargin(0d, 2d, 0d, 4d),
                new TextBlock
                {
                    Text = Lang._KbShortcuts.ChangeShortcut_Help2
                }.WithMargin(5d, 4d, 0d, 4d),
                new TextBlock
                {
                    Text = Lang._KbShortcuts.ChangeShortcut_Help3
                }.WithMargin(5d, 4d, 0d, 8d),
                new TextBlock
                {
                    Text = Lang._KbShortcuts.ChangeShortcut_Help4,
                    TextWrapping = TextWrapping.Wrap
                }.WithMargin(0d, 4d, 0d, 0d));

            StackPanel introPanel = CollapseUIExt.CreateStackPanel(Orientation.Horizontal)
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithMargin(0d, -7d, 0d, 0d);
            introPanel.AddElementToStackPanel(new TextBlock
            {
                Text = Lang._KbShortcuts.ChangeShortcut_Text,
                TextWrapping = TextWrapping.Wrap
            }.WithMargin(0d, 0d, 0d, 2d).WithHorizontalAlignment(HorizontalAlignment.Center));

            Button helpButton =
                CollapseUIExt.CreateButtonWithIcon<Button>(
                    iconGlyph: "info",
                    iconFontFamily: "FontAwesomeSolid",
                    iconSize: 10,
                    cornerRadius: new CornerRadius(5)
                )
                .WithMargin(8, -2, 0, 2)
                .WithFlyout(helpFlyout);

            introPanel.AddElementToStackPanel(helpButton);
            mainSwitchKeyContent.AddElementToStackPanel(
                introPanel,
                new TextBlock
                {
                    Text = data.Description,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeights.Bold
                }.WithMargin(5d, 6d, 0d, 10d).WithHorizontalAlignment(HorizontalAlignment.Center));

            StackPanel keysPanel = CollapseUIExt.CreateStackPanel(Orientation.Horizontal)
                .WithHorizontalAlignment(HorizontalAlignment.Center)
                .WithVerticalAlignment(VerticalAlignment.Center)
                .WithMargin(0d, 8d, 0d, -5d);

            keysPanel.AddElementToStackPanel(CreateKeyBoardButton(data.Shortcut.GetFormattedModifier()));
            keysPanel.AddElementToStackPanel(new TextBlock
                                             {
                                                 Text                = "+", FontSize = 20, FontWeight = FontWeights.Bold,
                                                 HorizontalAlignment = HorizontalAlignment.Center,
                                                 VerticalAlignment   = VerticalAlignment.Center
                                             });
            
            keysPanel.AddElementToStackPanel(CreateKeyBoardButton(data.Shortcut.GetKey(data.KeyName)));
            keysPanel.AddElementToStackPanel(new FontIcon
            {
                                                 Glyph      = "arrow-right", FontSize = 15,
                                                 FontFamily = CollapseUIExt.GetApplicationResource<FontFamily>("FontAwesomeSolid"),
                                                 Margin     = new Thickness(10, 0, 10, 0)
                                             });
            
            Border newKey1 = CreateKeyBoardButton("?");
            keysPanel.AddElementToStackPanel(newKey1);
            keysPanel.AddElementToStackPanel(new TextBlock
            {
                                                 Text                = "+", FontSize = 20, FontWeight = FontWeights.Bold,
                                                 HorizontalAlignment = HorizontalAlignment.Center,
                                                 VerticalAlignment   = VerticalAlignment.Center
                                             });
            
            Border newKey2 = CreateKeyBoardButton("?");
            keysPanel.AddElementToStackPanel(newKey2);
            mainSwitchKeyContent.AddElementToStackPanel(keysPanel);

            ContentDialogCollapse result = new ContentDialogCollapse(ContentDialogTheme.Informational)
            {
                Title = Lang._KbShortcuts.ChangeShortcut_Title,
                Content = mainSwitchKeyContent,
                CloseButtonText = Lang._Misc.Cancel,
                PrimaryButtonText = Lang._Misc.Change,
                SecondaryButtonText = null,
                IsPrimaryButtonEnabled = false,
                DefaultButton = ContentDialogButton.Primary,
                Background = CollapseUIExt.GetApplicationResource<Brush>("DialogAcrylicBrush"),
                XamlRoot = content.XamlRoot
            };

            int keyCount = 0;
            TextBlock text1 = newKey1.Child as TextBlock;
            TextBlock text2 = newKey2.Child as TextBlock;
            KbShortcut newShortcut = new KbShortcut();

            result.KeyDown += (_, s) =>
            {
                VirtualKey inputKey = s.Key;

                result.IsPrimaryButtonEnabled = false;
                keyCount = 1;
                text2.Text = "?";
                switch (inputKey)
                {
                    case VirtualKey.Control:
                        newShortcut.Modifier = VirtualKeyModifiers.Control;
                        break;
                    case VirtualKey.Shift:
                        newShortcut.Modifier = VirtualKeyModifiers.Shift;
                        break;
                    case VirtualKey.Menu:
                        newShortcut.Modifier = VirtualKeyModifiers.Menu;
                        break;
                    default:
                        keyCount = 0;
                        break;
                }
                text1.Text = keyCount != 0 ? newShortcut.GetFormattedModifier() : "?";
            };

            result.KeyUp += (_, s) =>
            {
                int keyValue = (int)s.Key;

                if (keyCount < 1 || keyValue is not ((>= 0x41 and <= 0x5A) or 9)) // Virtual-Key codes for Letters and Tab
                {
                    return;
                }

                newShortcut.Key = s.Key;
                text2.Text      = s.Key.ToString();

                result.IsPrimaryButtonEnabled = ValidKeyCombination(newShortcut);
            };

            CannotUseKbShortcuts = true;
            if (await result.QueueAndSpawnDialog() == ContentDialogResult.Primary)
                ChangeShortcut(data.KeyName, newShortcut);
            CannotUseKbShortcuts = false;
        }

        private static async void Swap_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                KbShortcutChangeData data = (KbShortcutChangeData)(sender as Button).DataContext;

                if (data is { KeyName: "GameSelection" or "RegionSelection" })
                {
                    SwapModifiers();
                    (sender as Button).FindParent<ContentDialogCollapse>().Hide();
                    await Task.Delay(200);
                    await Dialog_ShowKbShortcuts(sender as UIElement, 1);
                }
                else
                {
                    try
                    {
                        (sender as Button).FindParent<ContentDialogCollapse>().Hide();
                        await Task.Delay(200);
                        await Dialog_SwitchKey(sender as UIElement, data);
                        await Dialog_ShowKbShortcuts(sender as UIElement, data.PageNumber);
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine(ex.ToString());
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        private static void ChangeShortcut(string key, KbShortcut newShortcut)
        {
            if (!KbShortcutList.TryGetValue(key, out KbShortcut oldShortcut))
            {
                oldShortcut = new KbShortcut { Key = VirtualKey.None, Modifier = VirtualKeyModifiers.None };
            }

            KbShortcutList[key] = newShortcut;

            LogWriteLine($"[KeyboardShortcuts::ChangeKeybind] Swapped {oldShortcut} with {newShortcut} for the key {key}.");
            SaveKbShortcuts();
            KeyboardShortcutsEvent(null, 1);
        }

        // Swaps the modifiers for the shortcuts related to changing game/region.
        private static void SwapModifiers()
        {
            (KbShortcutList["GameSelection"], KbShortcutList["RegionSelection"]) = 
                (KbShortcutList["RegionSelection"], KbShortcutList["GameSelection"]);

            SaveKbShortcuts();
            KeyboardShortcutsEvent(null, 1);
        }

        public static void ResetKeyboardShortcuts()
        {
            KbShortcutList = null;
            SaveKbShortcuts();
            LoadKbShortcuts();
        }

        private static bool ValidKeyCombination(KbShortcut shortcut)
        {
            return !KbShortcutList.Any(x => x.Value.Key == shortcut.Key && x.Value.Modifier == shortcut.Modifier)
                   && !ForbiddenShortcutList.Any(x => x.Key == shortcut.Key && x.Modifier == shortcut.Modifier);
        }
        #endregion

        #region Default/Forbidden Keyboard Shortcuts
        public static readonly Dictionary<string, KbShortcut> DefaultShortcutList = new()
        {
            { "GameSelection", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.None } },
            { "RegionSelection", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.None } },

            { "KbShortcutsMenu", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.Tab } },
            { "HomePage", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.H } },
            { "SettingsPage", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.S } },
            { "NotificationPanel", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.Q } },

            { "ScreenshotFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.X } },
            { "GameFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.F } },
            { "CacheFolder", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.G } },
            { "ForceCloseGame", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.E } },

            { "RepairPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.R } },
            { "GameSettingsPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.S } },
            { "CachesPage", new KbShortcut { Modifier = VirtualKeyModifiers.Shift, Key = VirtualKey.C } },

            { "ReloadRegion", new KbShortcut { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.R } }
        };

        private static readonly List<KbShortcut> ForbiddenShortcutList =
        [
            new() { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.A },
            new() { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.X },
            new() { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.C },
            new() { Modifier = VirtualKeyModifiers.Control, Key = VirtualKey.V }
        ];
        #endregion

        #region Custom Keyboard Shortcuts
        internal static Dictionary<string, KbShortcut> KbShortcutList = new();

        public static void LoadKbShortcuts()
        {
            bool saveAfterLoad = false;
            string keyListStr = GetAppConfigValue("KbShortcutList").ToString();
            LogWriteLine("[KeyboardShortcuts] The following configuration is gonna be loaded from the .ini file:\n\r\t" + keyListStr);

            Dictionary<string, KbShortcut> resultList = new();

            if (keyListStr == null)
            {
                foreach (KeyValuePair<string, KbShortcut> entry in DefaultShortcutList)
                {
                    resultList.Add(entry.Key, entry.Value);
                }

                KbShortcutList = resultList;
                return;
            }

            if (!keyListStr.Contains(':'))
            {
                saveAfterLoad = true;
                LogWriteLine($"[KeyboardShortcuts::LoadKbShortcuts] Detected old KbShortcutList! Converting...\n\r\tOld Value:\"{keyListStr}\"",
                    writeToLog: true);
                var keyList = DefaultShortcutList.Keys;
                int index = 0;
                foreach (string combination in keyListStr.Split('|'))
                {
                    resultList.Add(keyList.ElementAt(index), KbShortcut.FromOldKeyList(combination.Split(",")));
                    index++;
                }
            }
            else
            {
                foreach (string combination in keyListStr.Split('|'))
                {
                    string[] values = combination.Split(":");
                    resultList.Add(values[0], KbShortcut.FromCode(values[1]));
                }
            }

            IEnumerable<string> missingKeys    = DefaultShortcutList.Keys.Except(resultList.Keys);
            IEnumerable<string> deprecatedKeys = resultList.Keys.Except(DefaultShortcutList.Keys);

            // ReSharper disable PossibleMultipleEnumeration
            saveAfterLoad = saveAfterLoad || missingKeys.Any() || deprecatedKeys.Any();

            foreach (string key in missingKeys)
            {
                resultList.Add(key, DefaultShortcutList[key]);
            }

            foreach (string key in deprecatedKeys)
            {
                resultList.Remove(key);
            }

            KbShortcutList = resultList;

            if (saveAfterLoad)
                SaveKbShortcuts();
        }

        public static void SaveKbShortcuts()
        {
            KbShortcutList ??= DefaultShortcutList;
            string res = "";
            foreach (KeyValuePair<string, KbShortcut> entry in KbShortcutList)
            {
                res += $"{entry.Key}:{entry.Value.ToCode()}|";
            }
            res = res.Remove(res.Length - 1);
            LogWriteLine("[KeyboardShortcuts::SaveKbShortcuts] The following configuration was saved:\n\r\t" + res);
            SetAndSaveConfigValue("KbShortcutList", res);
        }
        #endregion

        #region KbShortcut class
        public class KbShortcut
        {
            public VirtualKeyModifiers Modifier { get; set; } = VirtualKeyModifiers.None;
            public VirtualKey Key { get; set; } = VirtualKey.None;

            public string GetFormattedModifier()
            {
                return Modifier switch
                {
                    VirtualKeyModifiers.Control => Lang._KbShortcuts.Keyboard_Control,
                    VirtualKeyModifiers.Menu => Lang._KbShortcuts.Keyboard_Menu,
                    VirtualKeyModifiers.Shift => Lang._KbShortcuts.Keyboard_Shift,
                    _ => Modifier.ToString()
                };
            }

            public string GetKey(string dictionaryKey = "")
            {
                return dictionaryKey switch
                {
                    "GameSelection" => $"1 - {LauncherMetadataHelper.CurrentGameNameCount}",
                    "RegionSelection" => $"1 - {LauncherMetadataHelper.CurrentGameRegionMaxCount}",
                    _ => Key.ToString()
                };
            }

            public string ToCode()
            {
                return (int)Modifier + "," + (int)Key;
            }

            public static KbShortcut FromCode(string code)
            {
                string[] split = code.Split(',');
                return new KbShortcut { Modifier = (VirtualKeyModifiers)int.Parse(split[0]), Key = (VirtualKey)int.Parse(split[1]) };
            }

            public static KbShortcut FromOldKeyList(string[] strings)
            {
                if (strings.Length != 2)
                    return null;

                VirtualKeyModifiers mod = strings[0] switch
                {
                    "Ctrl" => VirtualKeyModifiers.Control,
                    "Shift" => VirtualKeyModifiers.Shift,
                    "Alt" => VirtualKeyModifiers.Menu,
                    _ => Enum.Parse<VirtualKeyModifiers>(strings[0])
                };

                if (strings[1] is "1 - 3" or "1 - 6")
                    return new KbShortcut { Modifier = mod, Key = VirtualKey.None };

                VirtualKey key = Enum.Parse<VirtualKey>(strings[1]);
                return new KbShortcut { Modifier = mod, Key = key };
            }

            public override string ToString()
            {
                return GetFormattedModifier() + "," + GetKey();
            }
        }
        #endregion

        #region Change Keyboard Shortcut struct
        private class KbShortcutChangeData
        {
            public string KeyName { get; init; }
            public KbShortcut Shortcut { get; init; }
            public string Description { get; init; }
            public int PageNumber { get; init; }
        }
        #endregion
    }
}