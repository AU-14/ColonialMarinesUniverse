// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Virtualized entity list for the construction-menu pickers: the scrollbar spans EVERY item, but only the
/// rows actually on screen exist as controls (each row carries a live EntityPrototypeView, and spinning up
/// tens of thousands of those froze the client). Same technique as the F5 entity spawn panel - it reuses the
/// engine's <see cref="PrototypeListContainer"/> to fake the full list height and offset the visible slice.
/// </summary>
public sealed class VirtualEntityList : Control
{
    /// <summary>Rows toggle-select (mass editor) instead of firing once per click (single pickers).</summary>
    public bool ToggleMode;

    /// <summary>Supplies the selected-state for a row as it scrolls into view (toggle mode only).</summary>
    public Func<string, bool>? IsSelected;

    /// <summary>(entity id, pressed). In non-toggle mode pressed is always true.</summary>
    public event Action<string, bool>? OnRowToggled;

    /// <summary>Builds the 32x32 icon control for an item id. Defaults to an entity sprite view; the mass
    /// editor's tile mode swaps in a tile-texture factory instead.</summary>
    public Func<string, Control>? IconFactory;

    private static readonly Color SelectedColor = Color.FromHex("#ffd77a"); // amber highlight

    private readonly ScrollContainer _scroll;
    private readonly PrototypeListContainer _list;

    private IReadOnlyList<(string Id, string Name)> _items = Array.Empty<(string, string)>();
    private (int Start, int End) _visible = (0, -1);

    public VirtualEntityList()
    {
        HorizontalExpand = true;
        VerticalExpand = true;

        _list = new PrototypeListContainer { HorizontalExpand = true };
        _scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
        };
        _scroll.AddChild(_list);
        AddChild(_scroll);

        _scroll.OnScrolled += UpdateVisible;
        OnResized += UpdateVisible;
    }

    /// <summary>Replace the full item set (already filtered/sorted by the caller) and jump back to the top.</summary>
    public void SetItems(IReadOnlyList<(string Id, string Name)> items)
    {
        _items = items;
        _scroll.SetScrollValue(Vector2.Zero);
        RefreshRows();
    }

    /// <summary>Rebuild the visible slice (e.g. after an external Select All changed row selection state).</summary>
    public void RefreshRows()
    {
        _visible = (0, -1);
        _list.RemoveAllChildren();
        UpdateVisible();
    }

    private void UpdateVisible()
    {
        _list.TotalItemCount = _items.Count;

        if (_items.Count == 0)
        {
            _list.RemoveAllChildren();
            _visible = (0, -1);
            return;
        }

        // Need one live row to know the real row height (font/UI-scale dependent).
        if (_list.ChildCount == 0)
        {
            _list.ItemOffset = 0;
            _list.AddChild(MakeRow(_items[0].Id, _items[0].Name));
            _visible = (0, 0);
        }

        var probe = _list.GetChild(0);
        probe.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        var rowHeight = Math.Max(probe.DesiredSize.Y, 1f) + PrototypeListContainer.Separation;

        // Which slice of the full list is inside the scroll viewport right now?
        var scrolled = Math.Max(-_list.Position.Y, 0);
        var start = (int)(scrolled / rowHeight);
        var end = start + (int)(Math.Max(_scroll.Height, 0) / rowHeight) + 1;
        end = Math.Min(end, _items.Count - 1);
        start = Math.Clamp(start, 0, end);

        if ((start, end) == _visible)
            return;

        // Small slice (a screenful), so rebuilding it outright is cheap and far simpler than incremental
        // add/remove bookkeeping.
        _visible = (start, end);
        _list.RemoveAllChildren();
        _list.ItemOffset = start;
        for (var i = start; i <= end; i++)
        {
            _list.AddChild(MakeRow(_items[i].Id, _items[i].Name));
        }
    }

    private Control MakeRow(string id, string name)
    {
        Control view;
        if (IconFactory != null)
        {
            view = IconFactory(id);
        }
        else
        {
            var protoView = new EntityPrototypeView
            {
                SetSize = new Vector2(32, 32),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VAlignment.Center,
            };
            protoView.SetPrototype(id);
            view = protoView;
        }

        var selected = ToggleMode && (IsSelected?.Invoke(id) ?? false);
        var row = new ContainerButton
        {
            HorizontalExpand = true,
            ToggleMode = ToggleMode,
            Pressed = selected,
        };
        var label = new Label
        {
            Text = $"{name}  [{id}]",
            VerticalAlignment = VAlignment.Center,
            ClipText = true,
            HorizontalExpand = true,
        };
        if (selected)
            label.FontColorOverride = SelectedColor;

        row.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 2, 4, 2),
            HorizontalExpand = true,
            Children = { view, label },
        });
        GmodStyle.Modernize(row);

        if (ToggleMode)
        {
            row.OnToggled += args =>
            {
                label.FontColorOverride = args.Pressed ? SelectedColor : null;
                OnRowToggled?.Invoke(id, args.Pressed);
            };
        }
        else
        {
            row.OnPressed += _ => OnRowToggled?.Invoke(id, true);
        }

        return row;
    }
}
