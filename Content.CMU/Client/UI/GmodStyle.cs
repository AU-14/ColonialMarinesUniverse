// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._AU14.UI;

/// <summary>
/// Shared "gmod menu" visual style: dark slate panels, a blue accent, and flat buttons with a hover swap and an
/// accent-tinted selected state. Applied per-instance via <see cref="ContainerButton.StyleBoxOverride"/> /
/// <see cref="PanelContainer.PanelOverride"/> so it never touches the global stylesheet or the rest of the game's
/// UI. Used by the improved construction editor and entity selector so they match the improved construction menu.
/// </summary>
public static class GmodStyle
{
    public static readonly Color PanelBg = Color.FromHex("#1E222B");
    public static readonly Color FieldBg = Color.FromHex("#15181F");
    public static readonly Color RowBg = Color.FromHex("#232833");
    public static readonly Color RowHover = Color.FromHex("#2D3340");
    public static readonly Color RowSelected = Color.FromHex("#2B3A57");
    public static readonly Color Accent = Color.FromHex("#4C8DFF");
    public static readonly Color SubtleBorder = Color.FromHex("#2A2F3A");

    /// <summary>A flat panel stylebox (subtle border) over the given background.</summary>
    public static StyleBoxFlat Panel(Color bg) => new()
    {
        BackgroundColor = bg,
        BorderColor = SubtleBorder,
        BorderThickness = new Thickness(1),
    };

    /// <summary>A flat button stylebox with comfortable padding.</summary>
    public static StyleBoxFlat ButtonBox(Color bg, Color border) => new()
    {
        BackgroundColor = bg,
        BorderColor = border,
        BorderThickness = new Thickness(1),
        Padding = new Thickness(8, 5, 8, 5),
    };

    /// <summary>
    /// Applies the modern flat look to a button: per-instance stylebox with a hover swap, plus an accent-tinted
    /// selected state for toggles. Pass custom <paramref name="baseBg"/>/<paramref name="selectedBg"/> to tint a
    /// primary action.
    /// </summary>
    public static void Modernize(ContainerButton btn, bool toggle = false, Color? baseBg = null, Color? selectedBg = null)
    {
        var normal = baseBg ?? RowBg;
        var selectedColor = selectedBg ?? RowSelected;

        void Refresh()
        {
            var selected = toggle && btn.Pressed;
            btn.StyleBoxOverride = ButtonBox(selected ? selectedColor : normal, selected ? Accent : SubtleBorder);
        }

        btn.OnMouseEntered += _ =>
        {
            if (!btn.Disabled && !(toggle && btn.Pressed))
                btn.StyleBoxOverride = ButtonBox(RowHover, SubtleBorder);
        };
        btn.OnMouseExited += _ => Refresh();
        if (toggle)
            btn.OnToggled += _ => Refresh();
        Refresh();
    }

    /// <summary>Recursively recolors every "LabelKeyText" header under <paramref name="root"/> to the accent.</summary>
    public static void RecolorKeyLabels(Control root)
    {
        foreach (var child in root.Children)
        {
            if (child is Label label && label.HasStyleClass("LabelKeyText"))
                label.FontColorOverride = Accent;
            RecolorKeyLabels(child);
        }
    }
}
