// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.WorldMarket;

// What the windows of the ambassador consoles are made of: an old amber terminal. A dark brown screen with a double amber frame that has notches at the corners, faint lines of the scan, small
// pixel letters, buttons that light up when the cursor is over them, and red ones for what must be done with care. The helpers have the names of the ones of the console of the colony
// administration (ConsoleKit), so that a window can be moved from the one to the other by changing what it uses.
internal static class TermPalette
{
    public static readonly Color Bg = Color.FromHex("#140e02");
    public static readonly Color Panel = Color.FromHex("#1a1203");
    public static readonly Color Card = Color.FromHex("#241a05");
    public static readonly Color Dark = Color.FromHex("#0e0901");
    public static readonly Color Border = Color.FromHex("#8a6200");
    public static readonly Color Line = Color.FromHex("#e5a600");
    public static readonly Color Gold = Color.FromHex("#ffd23f");
    public static readonly Color Text = Color.FromHex("#f0b400");
    public static readonly Color Muted = Color.FromHex("#a87a00");
    public static readonly Color Good = Color.FromHex("#c9e24a");
    public static readonly Color Bad = Color.FromHex("#ff5a3c");

    public static Color Dim(Color color, float factor) => new(color.R * factor, color.G * factor, color.B * factor, color.A);
}

// The small pixel font of the terminal (the same for every size that is asked for).
internal static class TermFont
{
    private static readonly Dictionary<int, Font> Cache = new();

    public static Font Get(int size = 12)
    {
        if (Cache.TryGetValue(size, out var known))
            return known;

        var resource = IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/Cozette/CozetteVector.ttf");
        return Cache[size] = new VectorFont(resource, size);
    }
}

// A panel of the terminal: the dark ground, the amber line, and (for the big one) a second thin line inside it, the notches at the corners and the lines of the scan.
internal sealed class TermPanel : PanelContainer
{
    private readonly bool _inner;
    private readonly bool _scan;

    public TermPanel(bool inner = false, bool scanlines = false, Color? edge = null)
    {
        _inner = inner;
        _scan = scanlines;

        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = inner ? TermPalette.Card : TermPalette.Panel,
            BorderColor = edge ?? (inner ? TermPalette.Border : TermPalette.Line),
            BorderThickness = new Thickness(inner ? 1 : 2),
        };
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (!_inner)
        {
            handle.DrawRect(new UIBox2(5f, 5f, size.X - 5f, size.Y - 5f), TermPalette.Border.WithAlpha(0.7f), filled: false);

            // The notches at the corners.
            foreach (var corner in new[] { new Vector2(0f, 0f), new Vector2(size.X - 9f, 0f), new Vector2(0f, size.Y - 9f), new Vector2(size.X - 9f, size.Y - 9f) })
                handle.DrawRect(UIBox2.FromDimensions(corner, new Vector2(9f, 9f)), TermPalette.Line);
        }

        if (!_scan)
            return;

        for (var y = 3f; y < size.Y; y += 4f)
            handle.DrawLine(new Vector2(2f, y), new Vector2(size.X - 2f, y), new Color(0f, 0f, 0f, 0.13f));
    }
}

internal enum TermVariant : byte
{
    Normal,
    Danger,
}

// A button of the terminal: a dark box with an amber line and amber letters; lit when the cursor is over it, full of amber when it is on (a toggle), dim when it cannot be pressed; the red
// one is filled with red.
internal sealed class TermButton : PanelContainer
{
    private readonly Label _label;
    private bool _hover;
    private bool _down;
    private bool _disabled;
    private bool _pressed;
    private TermVariant _variant;

    public bool ToggleMode { get; set; }

    // Pressed (the button itself is given).
    public event Action<TermButton>? OnPressed;

    // A toggle was switched (to what).
    public event Action<bool>? OnToggled;

    public TermButton()
    {
        MouseFilter = MouseFilterMode.Stop;
        _label = new Label
        {
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(10f, 4f, 10f, 4f),
            FontOverride = TermFont.Get(),
        };

        AddChild(_label);
        Paint();
    }

    public string Text
    {
        get => _label.Text ?? string.Empty;
        set => _label.Text = value;
    }

    public bool Disabled
    {
        get => _disabled;
        set
        {
            _disabled = value;
            Paint();
        }
    }

    public bool Pressed
    {
        get => _pressed;
        set
        {
            _pressed = value;
            Paint();
        }
    }

    public TermVariant Variant
    {
        get => _variant;
        set
        {
            _variant = value;
            Paint();
        }
    }

    private void Paint()
    {
        Color ground;
        Color edge;
        Color letters;

        if (_disabled)
        {
            ground = TermPalette.Dark;
            edge = TermPalette.Dim(TermPalette.Border, 0.5f);
            letters = TermPalette.Dim(TermPalette.Muted, 0.6f);
        }
        else if (_variant == TermVariant.Danger)
        {
            ground = _hover ? Color.FromHex("#ff6a4a") : Color.FromHex("#d8341c");
            edge = Color.FromHex("#ff9a7a");
            letters = Color.FromHex("#2a0600");
        }
        else if (_pressed)
        {
            ground = TermPalette.Text;
            edge = TermPalette.Gold;
            letters = TermPalette.Dark;
        }
        else if (_hover)
        {
            ground = TermPalette.Card;
            edge = TermPalette.Gold;
            letters = TermPalette.Gold;
        }
        else
        {
            ground = TermPalette.Dark;
            edge = TermPalette.Border;
            letters = TermPalette.Text;
        }

        PanelOverride = new StyleBoxFlat { BackgroundColor = ground, BorderColor = edge, BorderThickness = new Thickness(1) };
        _label.FontColorOverride = letters;
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hover = true;
        Paint();
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hover = false;
        _down = false;
        Paint();
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (_disabled || args.Function != EngineKeyFunctions.UIClick)
            return;

        _down = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || !_down)
            return;

        _down = false;
        args.Handle();

        if (_disabled || !_hover)
            return;

        if (ToggleMode)
        {
            _pressed = !_pressed;
            OnToggled?.Invoke(_pressed);
        }

        Paint();
        OnPressed?.Invoke(this);
    }
}

internal static class TermKit
{
    public static string Time(int seconds)
    {
        seconds = Math.Max(0, seconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    public static string Money(float amount) => $"${(long) Math.Round(amount):N0}";

    public static BoxContainer Column(int separation = 4) => new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation, HorizontalExpand = true };

    public static BoxContainer Row(int separation = 8) => new() { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };

    public static Label Text(string text, Color? color = null, bool expand = false, Control.HAlignment align = Control.HAlignment.Left)
    {
        return new Label
        {
            Text = text,
            FontOverride = TermFont.Get(),
            FontColorOverride = color ?? TermPalette.Text,
            HorizontalExpand = expand,
            HorizontalAlignment = align,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static Label Muted(string text, bool expand = false) => Text(text, TermPalette.Muted, expand);

    public static Label Gold(string text, bool expand = false) => Text(text, TermPalette.Gold, expand);

    // A card: a box with a thin amber line, and what it holds inside with a margin.
    public static TermPanel Card(Control content, Color? edge = null, int margin = 8)
    {
        var inner = Column(4);
        inner.Margin = new Thickness(margin);
        inner.AddChild(content);

        var panel = new TermPanel(inner: true, edge: edge) { HorizontalExpand = true };
        panel.AddChild(inner);
        return panel;
    }

    // A cell of a header: a small box with a word in it, as on the head of a screen of the terminal.
    public static Control Cell(string text, Color? color = null)
    {
        var panel = new TermPanel(inner: true);
        var label = Text(text, color ?? TermPalette.Gold);
        label.Margin = new Thickness(10f, 2f, 10f, 2f);
        panel.AddChild(label);
        return panel;
    }

    public static Control Rule()
    {
        return new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = TermPalette.Border.WithAlpha(0.5f) }, MinHeight = 1, HorizontalExpand = true };
    }

    public static TermButton Action(string text, Action onPressed, bool enabled = true, int minWidth = 0)
    {
        var button = new TermButton { Text = text, Disabled = !enabled, MinWidth = minWidth };
        button.OnPressed += _ => onPressed();
        return button;
    }

    // A red button, for what must be done with care.
    public static TermButton Danger(string text, Action onPressed, bool enabled = true, int minWidth = 0)
    {
        var button = Action(text, onPressed, enabled, minWidth);
        button.Variant = TermVariant.Danger;
        return button;
    }
}
