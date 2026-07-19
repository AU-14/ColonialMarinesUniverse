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
/// multi-select removal and a move-to-the-other-list action. All edits apply to the current SCOPE: Global
/// (every map) or a multi-selected set of planet maps, picked through the expandable scope panel; a label
/// on the right panel always confirms which scope is being edited. Fires <see cref="OnModify"/> for every
/// change; the server replies with fresh lists which repopulate the right panel in place.
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
    private readonly Button _moveToOpposite;
    private readonly List<string> _listParentOptionIds = new();
    private readonly List<string> _shownListIds = new();
    private readonly HashSet<string> _listSelected = new();
    private string _listParentFilterId = string.Empty;
    private bool _viewingBlacklist;

    // Scope picker: Global (all maps) or a multi-selection of planet maps.
    private readonly Button _scopeButton;
    private readonly BoxContainer _scopePanel;
    private readonly BoxContainer _scopeMapList;
    private readonly Label _scopeInfo;
    private readonly Dictionary<string, CheckBox> _scopeChecks = new();
    private readonly HashSet<string> _scopeMaps = new();
    private bool _scopeGlobal = true;
    private bool _syncingScopeChecks;

    private Dictionary<string, List<string>> _whitelists = new();
    private Dictionary<string, List<string>> _blacklists = new();
    private List<ZSyncMapOption> _maps = new();

    private DefaultWindow? _conflictWindow;

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
        addWhite.OnPressed += _ => SubmitAdd(new List<string>(_browserSelected), blacklist: false, clearBrowser: true);
        addBlack.OnPressed += _ => SubmitAdd(new List<string>(_browserSelected), blacklist: true, clearBrowser: true);
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

        // Scope button sits above the right-hand search bars; pressing it expands the scope panel to the right.
        _scopeButton = new Button { HorizontalExpand = true, ToggleMode = true };
        GmodStyle.Modernize(_scopeButton);
        _scopeInfo = new Label { HorizontalExpand = true };

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
        _moveToOpposite = new Button();
        foreach (var b in new[] { listSelectAll, listClear, removeSelected, _moveToOpposite })
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
                MapIds = ScopeMapIds(),
            });
            _listSelected.Clear();
        };
        // Move the selected entries from the viewed list onto the opposite one (add = server removes them
        // from the source list automatically).
        _moveToOpposite.OnPressed += _ => SubmitAdd(new List<string>(_listSelected), blacklist: !_viewingBlacklist, clearBrowser: false);
        _listCount = new Label { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8, 0, 0, 0) };
        right.AddChild(new Label { Text = Loc.GetString("au-zsync-lists-header"), StyleClasses = { "LabelKeyText" } });
        right.AddChild(_scopeButton);
        right.AddChild(_scopeInfo);
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
        right.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { _moveToOpposite },
        });

        // ---------------- Scope panel (expands to the right of the lists panel) ----------------
        var scopeGlobalButton = new Button { Text = Loc.GetString("au-zsync-scope-global-button"), HorizontalExpand = true };
        GmodStyle.Modernize(scopeGlobalButton);
        scopeGlobalButton.OnPressed += _ =>
        {
            _scopeGlobal = true;
            _scopeMaps.Clear();
            foreach (var map in _maps)
                _scopeMaps.Add(map.Id);
            SyncScopeChecks();
            OnScopeChanged();
        };

        _scopeMapList = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        var scopeScroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scopeScroll.AddChild(_scopeMapList);

        _scopePanel = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            MinWidth = 220,
            Visible = false,
            Margin = new Thickness(8, 0, 0, 0),
        };
        _scopePanel.AddChild(new Label { Text = Loc.GetString("au-zsync-scope-header"), StyleClasses = { "LabelKeyText" } });
        _scopePanel.AddChild(scopeGlobalButton);
        _scopePanel.AddChild(scopeScroll);

        _scopeButton.OnToggled += args => _scopePanel.Visible = args.Pressed;

        var split = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(8) };
        split.AddChild(left);
        split.AddChild(right);
        split.AddChild(_scopePanel);

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
        UpdateScopeUi();
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
        _whitelists = ev.Whitelists;
        _blacklists = ev.Blacklists;
        _maps = ev.Maps;
        _listSelected.Clear();
        RebuildScopeChecks();
        RefreshListPanel();
    }

    // ---------------- Scope handling ----------------

    /// <summary>MapIds payload for modify events: empty = the global scope.</summary>
    private List<string> ScopeMapIds()
    {
        return _scopeGlobal ? new List<string>() : _scopeMaps.ToList();
    }

    /// <summary>The scope keys whose entries the right panel shows ("" = global).</summary>
    private List<string> ViewScopes()
    {
        return _scopeGlobal ? new List<string> { string.Empty } : _scopeMaps.ToList();
    }

    private void RebuildScopeChecks()
    {
        _scopeMapList.RemoveAllChildren();
        _scopeChecks.Clear();

        foreach (var map in _maps)
        {
            var check = new CheckBox { Text = map.Name, ToolTip = map.Id, HorizontalExpand = true };
            check.OnToggled += args =>
            {
                if (_syncingScopeChecks)
                    return;

                // Any manual map toggle leaves Global mode; the checked set becomes the scope.
                _scopeGlobal = false;
                if (args.Pressed) _scopeMaps.Add(map.Id);
                else _scopeMaps.Remove(map.Id);

                // Nothing selected falls back to Global so edits always have a target.
                if (_scopeMaps.Count == 0)
                {
                    _scopeGlobal = true;
                    foreach (var m in _maps)
                        _scopeMaps.Add(m.Id);
                    SyncScopeChecks();
                }

                OnScopeChanged();
            };
            _scopeChecks[map.Id] = check;
            _scopeMapList.AddChild(check);
        }

        // Default state (and after a Global press): every map checkmarked = Global.
        if (_scopeGlobal)
        {
            _scopeMaps.Clear();
            foreach (var map in _maps)
                _scopeMaps.Add(map.Id);
        }
        else
        {
            _scopeMaps.RemoveWhere(id => _maps.All(m => m.Id != id));
            if (_scopeMaps.Count == 0)
                _scopeGlobal = true;
        }

        SyncScopeChecks();
        UpdateScopeUi();
    }

    private void SyncScopeChecks()
    {
        _syncingScopeChecks = true;
        foreach (var (id, check) in _scopeChecks)
            check.Pressed = _scopeGlobal || _scopeMaps.Contains(id);
        _syncingScopeChecks = false;
    }

    private void OnScopeChanged()
    {
        _listSelected.Clear();
        UpdateScopeUi();
        RefreshListPanel();
    }

    /// <summary>Visual confirmation of what is being edited: the scope button text, its tooltip, and the
    /// info label under it always name the current scope.</summary>
    private void UpdateScopeUi()
    {
        string scopeText;
        if (_scopeGlobal)
        {
            scopeText = Loc.GetString("au-zsync-scope-global");
        }
        else
        {
            var names = _maps.Where(m => _scopeMaps.Contains(m.Id)).Select(m => m.Name).ToList();
            scopeText = names.Count <= 3
                ? string.Join(", ", names)
                : Loc.GetString("au-zsync-scope-maps", ("count", names.Count));
            _scopeInfo.ToolTip = string.Join(", ", names);
        }

        _scopeButton.Text = Loc.GetString("au-zsync-scope-button", ("scope", scopeText)) + " ▸";
        _scopeButton.ToolTip = _scopeGlobal
            ? Loc.GetString("au-zsync-scope-global")
            : string.Join(", ", _maps.Where(m => _scopeMaps.Contains(m.Id)).Select(m => m.Name));
        _scopeInfo.Text = Loc.GetString("au-zsync-scope-info", ("scope", scopeText));
        if (_scopeGlobal)
            _scopeInfo.ToolTip = Loc.GetString("au-zsync-scope-global");
    }

    // ---------------- Adding (with cross-list conflict confirmation) ----------------

    /// <summary>Union of a scope-keyed list dictionary across the currently viewed scopes.</summary>
    private HashSet<string> UnionForScopes(Dictionary<string, List<string>> lists)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var scope in ViewScopes())
        {
            if (lists.TryGetValue(scope, out var entries))
                result.UnionWith(entries);
        }

        return result;
    }

    private void SubmitAdd(List<string> ids, bool blacklist, bool clearBrowser)
    {
        if (ids.Count == 0)
            return;

        // Entries already on the OPPOSITE list (in the target scopes) get a confirmation: Confirm moves
        // them over, Ignore adds only the non-conflicting ones.
        var opposite = UnionForScopes(blacklist ? _whitelists : _blacklists);
        var conflicts = ids.Where(opposite.Contains).ToList();

        if (conflicts.Count == 0)
        {
            SendAdd(ids, blacklist, clearBrowser);
            return;
        }

        OpenConflictWindow(ids, conflicts, blacklist, clearBrowser);
    }

    private void SendAdd(List<string> ids, bool blacklist, bool clearBrowser)
    {
        if (ids.Count == 0)
            return;

        OnModify?.Invoke(new ModifyZBorderSyncEvent
        {
            ProtoIds = ids,
            Blacklist = blacklist,
            Add = true,
            MapIds = ScopeMapIds(),
        });

        if (clearBrowser)
        {
            _browserSelected.Clear();
            _browser.RefreshRows();
        }

        _listSelected.Clear();
        UpdateCounts();
    }

    private void OpenConflictWindow(List<string> ids, List<string> conflicts, bool blacklist, bool clearBrowser)
    {
        _conflictWindow?.Close();

        var win = new DefaultWindow
        {
            Title = Loc.GetString("au-zsync-conflict-title"),
            MinSize = new Vector2(420, 320),
        };

        var body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, Margin = new Thickness(6) };
        var oppositeName = Loc.GetString(blacklist ? "au-zsync-whitelist" : "au-zsync-blacklist");
        body.AddChild(new RichTextLabel { Text = Loc.GetString("au-zsync-conflict-text", ("list", oppositeName)) });

        var listBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
        foreach (var id in conflicts)
        {
            var name = _prototype.TryIndex<EntityPrototype>(id, out var proto) && !string.IsNullOrEmpty(proto.Name)
                ? $"{proto.Name} ({id})"
                : id;
            listBox.AddChild(new Label { Text = "- " + name });
        }

        var scroll = new ScrollContainer { HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(listBox);
        body.AddChild(scroll);

        var confirm = new Button { Text = Loc.GetString("au-zsync-confirm"), HorizontalExpand = true };
        var ignore = new Button { Text = Loc.GetString("au-zsync-ignore"), HorizontalExpand = true };
        GmodStyle.Modernize(confirm);
        GmodStyle.Modernize(ignore);
        confirm.OnPressed += _ =>
        {
            SendAdd(ids, blacklist, clearBrowser);
            win.Close();
        };
        ignore.OnPressed += _ =>
        {
            SendAdd(ids.Where(id => !conflicts.Contains(id)).ToList(), blacklist, clearBrowser);
            win.Close();
        };

        body.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 6, 0, 0),
            Children = { confirm, ignore },
        });

        win.Contents.AddChild(body);
        _conflictWindow = win;
        win.OpenCentered();
    }

    // ---------------- Browser / list panel ----------------

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
        var source = UnionForScopes(_viewingBlacklist ? _blacklists : _whitelists)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _moveToOpposite.Text = Loc.GetString(_viewingBlacklist ? "au-zsync-move-to-whitelist" : "au-zsync-move-to-blacklist");

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

    public override void Close()
    {
        _conflictWindow?.Close();
        base.Close();
    }
}
