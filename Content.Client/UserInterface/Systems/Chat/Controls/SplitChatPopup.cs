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
    private readonly Button _stackedButton;
    private readonly Button _sideBySideButton;

    public event Action<string?>? OnTabSelected;

    /// <summary>
    ///     Raised with true for side-by-side panes, false for stacked.
    /// </summary>
    public event Action<bool>? OnDirectionSelected;

    public SplitChatPopup()
    {
        // Style class rather than a PanelOverride so the popup follows the CRT palette instead of
        // keeping whatever colour was baked in when it was built.
        var panel = new PanelContainer();
        panel.AddStyleClass(StyleNano.CrtUiEnabled
            ? StyleNano.StyleClassCrtChatPopup
            : StyleNano.StyleClassChatSubPanel);
        AddChild(panel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 5,
            MinWidth = 180
        };
        panel.AddChild(root);

        root.AddChild(CreateHeading(Loc.GetString("hud-chatbox-split-direction")));

        var directions = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2
        };
        root.AddChild(directions);

        _stackedButton = CreateItemButton(Loc.GetString("hud-chatbox-split-direction-stacked"));
        _stackedButton.OnPressed += _ => OnDirectionSelected?.Invoke(false);
        directions.AddChild(_stackedButton);

        _sideBySideButton = CreateItemButton(Loc.GetString("hud-chatbox-split-direction-side"));
        _sideBySideButton.OnPressed += _ => OnDirectionSelected?.Invoke(true);
        directions.AddChild(_sideBySideButton);

        root.AddChild(CreateHeading(Loc.GetString("hud-chatbox-split-picker")));

        _tabs = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4
        };
        root.AddChild(_tabs);
    }

    private static Label CreateHeading(string text)
    {
        var label = new Label { Text = text };
        if (StyleNano.CrtUiEnabled)
            label.AddStyleClass(StyleNano.StyleClassCrtHeading);

        return label;
    }

    private static Button CreateItemButton(string text)
    {
        var button = new Button
        {
            Text = text,
            ToggleMode = true,
            HorizontalExpand = true,
            MinHeight = 26,
            StyleClasses = { StyleNano.StyleClassChatChannelSelectorButton }
        };

        // Chat is skipped by CrtLobbyTheme, so every CRT class in here is set by hand.
        if (StyleNano.CrtUiEnabled)
            button.AddStyleClass(StyleNano.StyleClassCrtButton);

        return button;
    }

    /// <summary>
    ///     Reflects which way the panes are currently split.
    /// </summary>
    public void SetDirection(bool horizontal)
    {
        _stackedButton.Pressed = !horizontal;
        _sideBySideButton.Pressed = horizontal;
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
            var button = CreateItemButton(tab.Title);
            button.Pressed = splitEnabled && string.Equals(tab.Id, activeTabId, StringComparison.OrdinalIgnoreCase);
            button.OnPressed += _ =>
            {
                Close();
                OnTabSelected?.Invoke(capturedId);
            };
            _tabs.AddChild(button);
        }

        if (!splitEnabled)
            return;

        var closeButton = CreateItemButton(Loc.GetString("hud-chatbox-split-close"));
        closeButton.ToggleMode = false;
        closeButton.OnPressed += _ =>
        {
            Close();
            OnTabSelected?.Invoke(null);
        };
        _tabs.AddChild(closeButton);
    }
}
