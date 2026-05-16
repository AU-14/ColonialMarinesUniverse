using System;
using System.Collections.Generic;
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
                Modulate = active ? Color.FromHex("#9fd0b3") : Color.FromHex("#737987")
            };
            button.OnPressed += _ =>
            {
                Close();
                OnTabSelected?.Invoke(capturedId);
            };
            _tabs.AddChild(button);
        }
    }
}
