using System;
using System.Collections.Generic;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class SplitChatPopup : Popup
{
    private readonly BoxContainer _tabs;

    public event Action<string?>? OnTabSelected;

    public SplitChatPopup()
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

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            MinWidth = 180
        };
        panel.AddChild(root);

        root.AddChild(new Label
        {
            Text = Loc.GetString("hud-chatbox-split-picker"),
            Modulate = Color.FromHex("#D6DCE0")
        });

        _tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4
        };
        root.AddChild(_tabs);
    }

    public void ConfigureTabs(IReadOnlyList<ChatTabSettings> tabs, string activeTabId, bool splitEnabled)
    {
        while (_tabs.ChildCount > 0)
        {
            _tabs.RemoveChild(0);
        }

        foreach (var tab in tabs)
        {
            var capturedId = tab.Id;
            var button = new Button
            {
                Text = tab.Title,
                ToggleMode = true,
                Pressed = splitEnabled && string.Equals(tab.Id, activeTabId, StringComparison.OrdinalIgnoreCase),
                HorizontalExpand = true,
                MinHeight = 28,
                StyleClasses = { StyleNano.StyleClassChatChannelSelectorButton },
                Modulate = splitEnabled && string.Equals(tab.Id, activeTabId, StringComparison.OrdinalIgnoreCase)
                    ? Color.FromHex("#9fd0b3")
                    : Color.FromHex("#737987")
            };
            button.OnPressed += _ =>
            {
                Close();
                OnTabSelected?.Invoke(capturedId);
            };
            _tabs.AddChild(button);
        }

        if (!splitEnabled)
            return;

        var closeButton = new Button
        {
            Text = Loc.GetString("hud-chatbox-split-close"),
            HorizontalExpand = true,
            MinHeight = 28,
            StyleClasses = { StyleNano.StyleClassChatChannelSelectorButton },
            Modulate = Color.FromHex("#ff8787")
        };
        closeButton.OnPressed += _ =>
        {
            Close();
            OnTabSelected?.Invoke(null);
        };
        _tabs.AddChild(closeButton);
    }
}
