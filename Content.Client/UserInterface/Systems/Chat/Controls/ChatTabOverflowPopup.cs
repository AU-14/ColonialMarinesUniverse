using System;
using System.Collections.Generic;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatTabOverflowPopup : Popup
{
    private readonly BoxContainer _tabs;

    public event Action<string>? OnTabSelected;

    public ChatTabOverflowPopup()
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#07090B"),
                BorderColor = Color.FromHex("#263039"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            }
        };
        AddChild(panel);

        _tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            MinWidth = 170
        };
        panel.AddChild(_tabs);
    }

    public void ConfigureTabs(IReadOnlyList<ChatTabSettings> tabs, string activeTabId)
    {
        while (_tabs.ChildCount > 0)
        {
            _tabs.RemoveChild(0);
        }

        foreach (var tab in tabs)
        {
            var capturedId = tab.Id;
            var active = string.Equals(tab.Id, activeTabId, StringComparison.OrdinalIgnoreCase);
            var button = new Button
            {
                Text = tab.Title,
                ToggleMode = true,
                Pressed = active,
                HorizontalExpand = true,
                MinHeight = 28,
                StyleClasses = { StyleNano.StyleClassChatChannelSelectorButton },
            };

            // Same split as the tab strip: under CRT the fill carries the state and Modulate is
            // left alone, because it multiplies the stylebox as well as the label. These rows are
            // plain Buttons rather than ChatTabButtons, so the tab classes are added by hand.
            if (StyleNano.CrtUiEnabled)
            {
                button.AddStyleClass(StyleNano.StyleClassCrtChatTab);
                if (active)
                    button.AddStyleClass(StyleNano.StyleClassCrtChatTabSelected);

                button.Label.FontColorOverride = active
                    ? CrtTerminalPalette.TextBright
                    : CrtTerminalPalette.TextDim;
            }
            else
            {
                button.Modulate = active ? Color.White : Color.FromHex("#737987");
            }

            button.OnPressed += _ =>
            {
                Close();
                OnTabSelected?.Invoke(capturedId);
            };
            _tabs.AddChild(button);
        }
    }
}
