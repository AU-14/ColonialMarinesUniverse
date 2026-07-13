// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._AU14.Construction.CustomConstruction;
using Content.Client._AU14.UI;
using Content.Shared._AU14.ZLevelBuilding;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.ZLevelBuilding;

/// <summary>
/// The "Z-Sync Lists" tool: controls which wall prototypes get mirrored across z-levels as map borders.
/// LEFT half is the Mass-Entity-Editor-style browser (search + abstract-parent dropdown + virtualized
/// multi-select + Select All Shown) with "Add to Whitelist" / "Add to Blacklist" actions. RIGHT half shows
/// the current lists (whitelist = reflected, blacklist = overrides), filterable by parent or all, with
/// multi-select removal. Fires <see cref="OnModify"/> for every change; the server replies with fresh
/// lists which repopulate the right panel in place.
/// </summary>
public sealed class ZBorderSyncWindow : DefaultWindow
{
    // 🔧 TUNABLE: max dropdown options shown at once (search narrows them).
    private const int MaxParentOptions = 150;

    public event Action<ModifyZBorderSyncEvent>? OnModify;
    public event Action<bool>? OnPickFromWorld;

    private readonly IPrototypeManager _prototype;
    private readonly EntityParentIndex _index;

    // Left: browser.
    private readonly LineEdit _search;
    private readonly LineEdit _parentSearch;
    private readonly OptionButton _parentDropdown;
    private readonly VirtualEntityList _browser;
    private readonly Label _browserCount;
    private readonly List<string> _parentOptionIds = new();
    private readonly HashSet<string> _browserSelected = new();
    private string _parentFilterId = string.Empty;

    // Right: current lists.
    private readonly OptionButton _listPicker;
    private readonly LineEdit _listParentSearch;
    private readonly OptionButton _listParentDropdown;
    private readonly VirtualEntityList _listView;
    private readonly Label _listCount;
    private readonly List<string> _listParentOptionIds = new();
    private readonly List<string> _shownListIds = new();
    private readonly HashSet<string> _listSelected = new();
    private string _listParentFilterId = string.Empty;
    private bool _viewingBlacklist;

    private List<string> _whitelist = new();
    private List<string> _blacklist = new();

    public ZBorderSyncWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();
        _index = EntityParentIndex.Build(_prototype);

        Title = Loc.GetString("au-zsync-title");
        MinSize = new Vector2(1040, 700);

        // ---------------- Left: browser ----------------
        _search = new LineEdit { PlaceHolder = Loc.GetString("construction-selector-search"), HorizontalExpand = true };
        _parentSearch = new LineEdit { PlaceHolder = Loc.GetString("construction-mass-selector-parent-search"), HorizontalExpand = true };
        _parentDropdown = new OptionButton { HorizontalExpand = true };

        _browser = new VirtualEntityList
        {
            ToggleMode = true,
            IsSelected = id => _browserSelected.Contains(id),
        };
        _browser.OnRowToggled += (id, pressed) =>
        {
            if (pressed) _browserSelected.Add(id);
            else _browserSelected.Remove(id);
            UpdateCounts();
        };

        var selectAll = new Button { Text = Loc.GetString("construction-mass-selector-select-all") };
        var clear = new Button { Text = Loc.GetString("construction-mass-selector-clear") };
        var addWhite = new Button { Text = Loc.GetString("au-zsync-add-whitelist") };
        var addBlack = new Button { Text = Loc.GetString("au-zsync-add-blacklist") };
        var pickWhite = new Button { Text = Loc.GetString("au-zsync-pick-whitelist") };
        var pickBlack = new Button { Text = Loc.GetString("au-zsync-pick-blacklist") };
        _browserCount = new Label { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        foreach (var b in new[] { selectAll, clear, addWhite, addBlack, pickWhite, pickBlack })
            GmodStyle.Modernize(b);

        selectAll.OnPressed += _ =>
        {
            foreach (var entry in _index.All)
            {
                if (BrowserMatches(entry))
                    _browserSelected.Add(entry.Id);
            }
            _browser.RefreshRows();
            UpdateCounts();
        };
        clear.OnPressed += _ =>
        {
            _browserSelected.Clear();
            _browser.RefreshRows();
            UpdateCounts();
        };
        addWhite.OnPressed += _ => SubmitAdd(blacklist: false);
        addBlack.OnPressed += _ => SubmitAdd(blacklist: true);
        pickWhite.OnPressed += _ => OnPickFromWorld?.Invoke(false);
        pickBlack.OnPressed += _ => OnPickFromWorld?.Invoke(true);

        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true };
        left.AddChild(new Label { Text = Loc.GetString("au-zsync-browser-header"), StyleClasses = { "LabelKeyText" } });
        left.AddChild(_search);
        left.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { _parentSearch, _parentDropdown },
        });
        left.AddChild(_browser);
        left.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { _browserCount, selectAll, clear, addWhite, addBlack },
        });
        left.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { pickWhite, pickBlack },
        });

        // ---------------- Right: current lists ----------------
        _listPicker = new OptionButton { HorizontalExpand = true };
        _listPicker.AddItem(Loc.GetString("au-zsync-whitelist"), 0);
        _listPicker.AddItem(Loc.GetString("au-zsync-blacklist"), 1);
        _listPicker.Select(0);
        _listPicker.OnItemSelected += a =>
        {
            _listPicker.Select(a.Id);
            _viewingBlacklist = a.Id == 1;
            _listSelected.Clear();
            RefreshListPanel();
        };

        _listParentSearch = new LineEdit { PlaceHolder = Loc.GetString("construction-mass-selector-parent-search"), HorizontalExpand = true };
        _listParentDropdown = new OptionButton { HorizontalExpand = true };

        _listView = new VirtualEntityList
        {
            ToggleMode = true,
            IsSelected = id => _listSelected.Contains(id),
        };
        _listView.OnRowToggled += (id, pressed) =>
        {
            if (pressed) _listSelected.Add(id);
            else _listSelected.Remove(id);
            UpdateCounts();
        };

        var listSelectAll = new Button { Text = Loc.GetString("construction-mass-selector-select-all") };
        var listClear = new Button { Text = Loc.GetString("construction-mass-selector-clear") };
        var removeSelected = new Button { Text = Loc.GetString("au-zsync-remove-selected") };
        foreach (var b in new[] { listSelectAll, listClear, removeSelected })
            GmodStyle.Modernize(b);
        listSelectAll.OnPressed += _ =>
        {
            foreach (var id in _shownListIds)
                _listSelected.Add(id);

            _listView.RefreshRows();
            UpdateCounts();
        };
        listClear.OnPressed += _ =>
        {
            _listSelected.Clear();
            _listView.RefreshRows();
            UpdateCounts();
        };
        removeSelected.OnPressed += _ =>
        {
            if (_listSelected.Count == 0)
                return;

            OnModify?.Invoke(new ModifyZBorderSyncEvent
            {
                ProtoIds = new List<string>(_listSelected),
                Blacklist = _viewingBlacklist,
                Add = false,
            });
            _listSelected.Clear();
        };
        _listCount = new Label { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        right.AddChild(new Label { Text = Loc.GetString("au-zsync-lists-header"), StyleClasses = { "LabelKeyText" } });
        right.AddChild(_listPicker);
        right.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { _listParentSearch, _listParentDropdown },
        });
        right.AddChild(_listView);
        right.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { _listCount, listSelectAll, listClear, removeSelected },
        });

        var split = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8) };
        split.AddChild(left);
        split.AddChild(right);

        var panel = new PanelContainer
        {
            PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(split);
        Contents.AddChild(panel);

        PopulateParentDropdown(_parentDropdown, _parentOptionIds, string.Empty, _index.ParentCounts,
            () => _parentFilterId, id => { _parentFilterId = id; RefreshBrowser(); });
        RefreshBrowser();
        RefreshListPanel();

        _search.OnTextChanged += _ => RefreshBrowser();
        _parentSearch.OnTextChanged += args => PopulateParentDropdown(_parentDropdown, _parentOptionIds, args.Text,
            _index.ParentCounts, () => _parentFilterId, id => { _parentFilterId = id; RefreshBrowser(); });
        _listParentSearch.OnTextChanged += _ => RefreshListPanel();

        // Dropdown selections are wired ONCE here (Populate* only rebuilds the option items).
        _parentDropdown.OnItemSelected += args =>
        {
            _parentDropdown.Select(args.Id);
            _parentFilterId = args.Id >= 0 && args.Id < _parentOptionIds.Count ? _parentOptionIds[args.Id] : string.Empty;
            RefreshBrowser();
        };
        _listParentDropdown.OnItemSelected += args =>
        {
            _listParentDropdown.Select(args.Id);
            _listParentFilterId = args.Id >= 0 && args.Id < _listParentOptionIds.Count ? _listParentOptionIds[args.Id] : string.Empty;
            RefreshListPanel();
        };
    }

    /// <summary>Called by the client system whenever the server sends fresh lists.</summary>
    public void Populate(OpenZBorderSyncEvent ev)
    {
        _whitelist = ev.Whitelist;
        _blacklist = ev.Blacklist;
        _listSelected.Clear();
        RefreshListPanel();
    }

    private void SubmitAdd(bool blacklist)
    {
        if (_browserSelected.Count == 0)
            return;

        OnModify?.Invoke(new ModifyZBorderSyncEvent
        {
            ProtoIds = new List<string>(_browserSelected),
            Blacklist = blacklist,
            Add = true,
        });
        _browserSelected.Clear();
        _browser.RefreshRows();
        UpdateCounts();
    }

    private bool BrowserMatches((string Id, string Name, string Haystack) entry)
    {
        var needle = _search.Text.Trim().ToLowerInvariant();
        if (needle.Length > 0 && !entry.Haystack.Contains(needle))
            return false;

        return _parentFilterId.Length == 0 || _index.HasAncestor(entry.Id, _parentFilterId);
    }

    private void RefreshBrowser()
    {
        var items = new List<(string Id, string Name)>();
        foreach (var entry in _index.All)
        {
            if (BrowserMatches(entry))
                items.Add((entry.Id, entry.Name));
        }
        _browser.SetItems(items);
        UpdateCounts();
    }

    /// <summary>Rebuilds the right panel: parent dropdown scoped to parents present in the shown list.</summary>
    private void RefreshListPanel()
    {
        var source = _viewingBlacklist ? _blacklist : _whitelist;

        // Parent counts restricted to the listed entities, so the dropdown only offers relevant parents.
        var counts = new Dictionary<string, int>();
        foreach (var id in source)
        {
            if (!_index.Parents.TryGetValue(id, out var parents))
                continue;
            foreach (var p in parents)
                counts[p] = counts.GetValueOrDefault(p) + 1;
        }

        PopulateParentDropdown(_listParentDropdown, _listParentOptionIds, _listParentSearch.Text, counts,
            () => _listParentFilterId, id => { _listParentFilterId = id; RefreshListPanel(); });

        var items = new List<(string Id, string Name)>();
        _shownListIds.Clear();
        foreach (var id in source)
        {
            if (_listParentFilterId.Length > 0 && !_index.HasAncestor(id, _listParentFilterId))
                continue;

            var name = _prototype.TryIndex<EntityPrototype>(id, out var proto) && !string.IsNullOrEmpty(proto.Name)
                ? proto.Name
                : id;
            items.Add((id, name));
            _shownListIds.Add(id);
        }
        _listView.SetItems(items);
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        _browserCount.Text = Loc.GetString("construction-mass-selector-count", ("count", _browserSelected.Count));
        _listCount.Text = Loc.GetString("construction-mass-selector-count", ("count", _listSelected.Count));
    }

    private void PopulateParentDropdown(OptionButton dropdown, List<string> optionIds, string filter,
        Dictionary<string, int> counts, Func<string> getCurrent, Action<string> setCurrent)
    {
        dropdown.Clear();
        optionIds.Clear();

        dropdown.AddItem(Loc.GetString("construction-mass-selector-parent-all"), 0);
        optionIds.Add(string.Empty);

        var needle = filter.Trim();
        var index = 1;
        foreach (var (parentId, count) in counts
                     .Where(kv => needle.Length == 0 || kv.Key.Contains(needle, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(kv => kv.Value)
                     .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(MaxParentOptions))
        {
            dropdown.AddItem($"{parentId} ({count})", index++);
            optionIds.Add(parentId);
        }

        var current = optionIds.IndexOf(getCurrent());
        if (current < 0)
        {
            setCurrent(string.Empty);
            current = 0;
        }
        dropdown.Select(current);
    }
}
