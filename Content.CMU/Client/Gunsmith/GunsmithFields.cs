// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Globalization;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Gunsmith;

/// <summary>A labelled box for a number: the player types it, point or comma, and the window parses it.</summary>
public sealed class NumberField : BoxContainer
{
    private readonly LineEdit _edit;
    private double _value;

    public event Action<double>? Changed;

    public NumberField(string label, double value, string unit = "")
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(new Label { Text = label, HorizontalExpand = true, MinWidth = 190 });
        _edit = new LineEdit { MinWidth = 90, Text = Format(value) };
        AddChild(_edit);
        AddChild(new Label { Text = unit, MinWidth = 36 });

        _value = value;
        _edit.OnTextChanged += args =>
        {
            if (TryParse(args.Text, out var parsed))
            {
                _value = parsed;
                _edit.ModulateSelfOverride = null;
                Changed?.Invoke(parsed);
            }
            else
            {
                _edit.ModulateSelfOverride = Color.FromHex("#ff8080");
            }
        };
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = value;
            _edit.Text = Format(value);
            _edit.ModulateSelfOverride = null;
        }
    }

    /// <summary>The value without telling the listeners (used when the window loads a design into its fields).</summary>
    public void Set(double value) => Value = value;

    public static string Format(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static bool TryParse(string text, out double value) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
}

/// <summary>A labelled list to choose from.</summary>
public sealed class ChoiceField : BoxContainer
{
    private readonly OptionButton _button = new() { MinWidth = 190, HorizontalExpand = true };
    private readonly List<string> _ids = new();

    public event Action<string>? Chosen;

    public ChoiceField(string label)
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        SeparationOverride = 6;

        AddChild(new Label { Text = label, HorizontalExpand = true, MinWidth = 190 });
        AddChild(_button);

        _button.OnItemSelected += args =>
        {
            _button.SelectId(args.Id);
            if (args.Id >= 0 && args.Id < _ids.Count)
                Chosen?.Invoke(_ids[args.Id]);
        };
    }

    public void Add(string id, string text)
    {
        _ids.Add(id);
        _button.AddItem(text, _ids.Count - 1);
    }

    public void Clear()
    {
        _ids.Clear();
        _button.Clear();
    }

    public string Selected
    {
        get => _button.SelectedId >= 0 && _button.SelectedId < _ids.Count ? _ids[_button.SelectedId] : string.Empty;
        set
        {
            var index = _ids.IndexOf(value);
            if (index >= 0)
                _button.SelectId(index);
        }
    }
}

/// <summary>A labelled tick box.</summary>
public sealed class ToggleField : BoxContainer
{
    private readonly CheckBox _box = new();

    public event Action<bool>? Toggled;

    public ToggleField(string label)
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        _box.Text = label;
        AddChild(_box);
        _box.OnToggled += args => Toggled?.Invoke(args.Pressed);
    }

    public bool Value
    {
        get => _box.Pressed;
        set => _box.Pressed = value;
    }
}

/// <summary>
/// A list of presets with a box to find one in: a drop-down of thirty calibers does not fit the screen, a list that scrolls does.
/// </summary>
public sealed class PresetPicker : BoxContainer
{
    private readonly LineEdit _filter = new() { HorizontalExpand = true, PlaceHolder = Loc.GetString("gunsmith-ui-preset-filter") };
    private readonly ItemList _list = new() { MinSize = new System.Numerics.Vector2(0, 150), HorizontalExpand = true };
    private readonly List<(string Id, string Text)> _all = new();
    private readonly List<string> _shown = new();

    public event Action<string>? Chosen;

    public PresetPicker(string label)
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 3;

        AddChild(new Label { Text = label });
        AddChild(_filter);
        AddChild(_list);

        _filter.OnTextChanged += _ => Rebuild();
        _list.OnItemSelected += args =>
        {
            if (args.ItemIndex >= 0 && args.ItemIndex < _shown.Count)
                Chosen?.Invoke(_shown[args.ItemIndex]);
        };
    }

    public void Add(string id, string text)
    {
        _all.Add((id, text));
        Rebuild();
    }

    public void Clear()
    {
        _all.Clear();
        Rebuild();
    }

    private void Rebuild()
    {
        _list.Clear();
        _shown.Clear();
        var filter = _filter.Text.Trim();
        foreach (var (id, text) in _all)
        {
            if (filter.Length > 0 && !text.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                continue;

            _shown.Add(id);
            _list.AddItem(text);
        }
    }
}
