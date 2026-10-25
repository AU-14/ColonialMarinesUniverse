// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.CMU14.Crafting;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.WorldMarket;

// What the console of the colony administration is made of: the same dark panel with the gold edge, the cards and the pills of the crafting window and its games (their palette and their
// drawing), a tab that is a pill, the graph of an index and the bar of a quota.
internal static class ConsoleKit
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
            FontColorOverride = color ?? CraftPalette.Text,
            HorizontalExpand = expand,
            HorizontalAlignment = align,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static Label Muted(string text, bool expand = false) => Text(text, CraftPalette.Muted, expand);

    public static Label Gold(string text, bool expand = false) => Text(text, CraftPalette.Gold, expand);

    // A card: a rounded-looking dark box with a thin edge, and what it holds inside with a margin.
    public static PanelContainer Card(Control content, Color? edge = null, int margin = 8)
    {
        var inner = Column(4);
        inner.Margin = new Thickness(margin);
        inner.AddChild(content);

        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CraftPalette.Card,
                BorderColor = edge ?? CraftPalette.Border,
                BorderThickness = new Thickness(1),
            },
            Children = { inner },
        };
    }

    public static Control Rule()
    {
        return new PanelContainer { PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#ffffff18") }, MinHeight = 1, HorizontalExpand = true };
    }

    public static Button Action(string text, Action onPressed, bool enabled = true, int minWidth = 0)
    {
        var button = new Button { Text = text, Disabled = !enabled, StyleClasses = { "OpenBoth" }, MinWidth = minWidth };
        button.OnPressed += _ => onPressed();
        return button;
    }
}

// A tab: a pill that is lit when it is the open one.
internal sealed class TabPill : PanelContainer
{
    private readonly Label _label;
    private bool _active;

    public event Action? OnPicked;

    public TabPill(string text)
    {
        MouseFilter = MouseFilterMode.Stop;
        _label = new Label { Text = text, Margin = new Thickness(10, 3, 10, 3), VerticalAlignment = VAlignment.Center };
        AddChild(_label);
        Paint();
    }

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Paint();
        }
    }

    private void Paint()
    {
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = _active ? CraftPalette.Gold.WithAlpha(0.22f) : CraftPalette.Dark,
            BorderColor = _active ? CraftPalette.Gold : CraftPalette.Border,
            BorderThickness = new Thickness(1),
        };

        _label.FontColorOverride = _active ? CraftPalette.Gold : CraftPalette.Muted;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        OnPicked?.Invoke();
        args.Handle();
    }
}

// The graph of an index: a line of its last values over the line of the usual price, in the colour of the index.
internal sealed class SparkLine : Control
{
    private readonly List<float> _values;
    private readonly Color _color;

    public SparkLine(List<float> values, Color color)
    {
        _values = values;
        _color = color;
        MinSize = new Vector2(120, 38);
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = PixelSize.X;
        var height = PixelSize.Y;
        handle.DrawRect(new UIBox2(0, 0, width, height), CraftPalette.Inset);

        if (_values.Count < 2)
            return;

        var min = 40f;
        var max = 220f;
        foreach (var value in _values)
        {
            min = Math.Min(min, value - 4f);
            max = Math.Max(max, value + 4f);
        }

        // The usual price (100) is a faint line across the graph.
        float Y(float value) => height - 3f - (value - min) / (max - min) * (height - 6f);
        handle.DrawLine(new Vector2(0, Y(100f)), new Vector2(width, Y(100f)), CraftPalette.Border.WithAlpha(0.6f));

        var points = new Vector2[_values.Count];
        for (var i = 0; i < _values.Count; i++)
            points[i] = new Vector2(3f + i * (width - 6f) / (_values.Count - 1), Y(_values[i]));

        MiniDraw.Stroke(handle, points, points.Length, 2f, _color);
        handle.DrawCircle(points[^1], 3f, _color);
    }
}

// A bar that fills: the progress of a quota or of an order, the standing with an organization, the heat that the law has on the colony.
internal sealed class FillBar : Control
{
    private float _fraction;
    private Color _color;

    public FillBar(float fraction, Color color, int height = 10)
    {
        _fraction = fraction;
        _color = color;
        MinSize = new Vector2(80, height);
        HorizontalExpand = true;
        VerticalAlignment = VAlignment.Center;
    }

    public void Set(float fraction, Color color)
    {
        _fraction = fraction;
        _color = color;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var box = new UIBox2(0, 0, PixelSize.X, PixelSize.Y);
        MiniDraw.Pill(handle, box, CraftPalette.Inset);

        var fill = Math.Clamp(_fraction, 0f, 1f);
        if (fill > 0.001f)
            MiniDraw.Pill(handle, new UIBox2(1, 1, Math.Max(PixelSize.Y - 1, (PixelSize.X - 1) * fill), PixelSize.Y - 1), _color);
    }
}
