// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Client._AU14.UI;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client._AU14.Insurgency;

/// <summary>
///     Applies the improved-construction-menu look (<see cref="GmodStyle"/>: dark slate panels,
///     blue accent, flat modern buttons) to the programmatic INSFOR windows. Everything is done
///     with per-instance overrides, so the global stylesheet and the rest of the game stay
///     untouched, and a server without the improved construction menu merged still works - the
///     GmodStyle helper ships in the same branch as these windows.
///     Safe to call repeatedly after rebuilds: already-styled controls are skipped.
/// </summary>
public static class InsforUiStyle
{
    /// <summary>Backdrop + recursive restyle for a whole window. Call after (re)building content.</summary>
    public static void Apply(DefaultWindow window)
    {
        // DefaultWindow.Contents overlaps its children, so a panel inserted at index 0 acts as a
        // full dark backdrop behind everything the window added.
        if (window.Contents.ChildCount == 0 || window.Contents.GetChild(0).Name != "InsforBackdrop")
        {
            var backdrop = new PanelContainer
            {
                Name = "InsforBackdrop",
                PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            };
            window.Contents.AddChild(backdrop);
            backdrop.SetPositionFirst();
        }

        Restyle(window.Contents);
    }

    /// <summary>Recursively restyles buttons, text fields and headers under <paramref name="root"/>.</summary>
    public static void Restyle(Control root)
    {
        switch (root)
        {
            // CheckBox is also a ContainerButton but looks wrong with a boxy background, so it keeps
            // the stock look. StyleBoxOverride doubles as the "already styled" marker: Modernize sets
            // it immediately, so repeated Apply calls don't stack duplicate hover handlers.
            case Button button when button.StyleBoxOverride == null:
                GmodStyle.Modernize(button, button.ToggleMode);
                break;
            case OptionButton option when option.StyleBoxOverride == null:
                GmodStyle.Modernize(option);
                break;
            case LineEdit lineEdit when lineEdit.StyleBoxOverride == null:
                lineEdit.StyleBoxOverride = GmodStyle.Panel(GmodStyle.FieldBg);
                break;
            case PanelContainer panel when panel.PanelOverride == null:
                panel.PanelOverride = GmodStyle.Panel(GmodStyle.RowBg);
                break;
            case Label label when label.HasStyleClass("LabelHeading") || label.HasStyleClass("LabelHeadingBigger") || label.HasStyleClass("LabelKeyText"):
                label.FontColorOverride = GmodStyle.Accent;
                break;
        }

        foreach (var child in root.Children)
            Restyle(child);
    }
}
