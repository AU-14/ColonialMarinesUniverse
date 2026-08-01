// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._AU14.UI;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Multi-select entity picker for the "Mass Entity Editor": rows toggle-select, a searchable parent DROPDOWN
/// narrows by ancestor prototype (abstract parents like BaseWall included), and "Select All Shown" grabs the
/// entire filtered set. The list itself is virtualized (<see cref="VirtualEntityList"/>) so it scrolls through
/// every entity in the game without building tens of thousands of sprite rows up front. Confirm fires
/// <see cref="OnEntitiesSelected"/> with every selected id.
/// </summary>
public sealed class MassEntitySelectorWindow : DefaultWindow
{
    // 🔧 TUNABLE: cap on dropdown OPTIONS shown at once (an OptionButton with thousands of items is
    // unusable - the search box narrows it). The entity list itself is uncapped.
    private const int MaxParentOptions = 150;

    private readonly IPrototypeManager _prototype;
    private readonly LineEdit _search;
    private readonly LineEdit _parentSearch;
    private readonly OptionButton _parentDropdown;
    private readonly VirtualEntityList _list;
    private readonly Label _countLabel;
    private readonly Button _confirm;

    // (id, display name, lowercased "name id" haystack) for fast filtering; built once.
    private readonly List<(string Id, string Name, string Haystack)> _all = new();

    // entity id -> every ancestor prototype id (abstract included).
    private readonly Dictionary<string, HashSet<string>> _parentsCache = new();

    // parent id -> how many selectable entities inherit from it; drives the dropdown labels/ordering.
    private readonly Dictionary<string, int> _parentCounts = new();

    // Dropdown option index -> parent id ("" = all parents). Rebuilt whenever the parent search changes.
    private readonly List<string> _parentOptionIds = new();

    private readonly HashSet<string> _selected = new();
    private string _parentFilterId = string.Empty;

    // Tiles mode: the same browser/selection flow, but over ContentTileDefinitions instead of entities.
    private readonly Button _tilesToggle;
    private readonly BoxContainer _parentRow;
    private readonly List<(string Id, string Name, string Haystack)> _allTiles = new();
    private bool _tileMode;

    public event Action<List<string>>? OnEntitiesSelected;

    /// <summary>Fired instead of <see cref="OnEntitiesSelected"/> when confirming in Tiles mode.</summary>
    public event Action<List<string>>? OnTilesSelected;

    public MassEntitySelectorWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("construction-mass-selector-title");
        MinSize = new Vector2(560, 680);

        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("construction-selector-search"),
            HorizontalExpand = true,
        };
        _parentSearch = new LineEdit
        {
            PlaceHolder = Loc.GetString("construction-mass-selector-parent-search"),
            HorizontalExpand = true,
        };
        _parentDropdown = new OptionButton { HorizontalExpand = true };
        _parentRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { _parentSearch, _parentDropdown },
        };

        // Entities <-> Tiles mode switch. Tiles reuse the exact same virtualized browser + multi-select flow.
        _tilesToggle = new Button
        {
            Text = Loc.GetString("construction-mass-selector-tiles"),
            ToggleMode = true,
        };
        GmodStyle.Modernize(_tilesToggle);

        _list = new VirtualEntityList
        {
            ToggleMode = true,
            IsSelected = id => _selected.Contains(id),
        };
        _tilesToggle.OnToggled += args =>
        {
            _tileMode = args.Pressed;
            _selected.Clear();
            _parentRow.Visible = !_tileMode; // tiles have no prototype parents to filter by
            if (_tileMode && _allTiles.Count == 0)
                BuildTileIndex();
            _list.IconFactory = _tileMode ? MakeTileIcon : null;
            Refresh();
        };
        _list.OnRowToggled += (id, pressed) =>
        {
            if (pressed)
                _selected.Add(id);
            else
                _selected.Remove(id);
            UpdateCount();
        };

        var selectAll = new Button { Text = Loc.GetString("construction-mass-selector-select-all") };
        var clear = new Button { Text = Loc.GetString("construction-mass-selector-clear") };
        _countLabel = new Label { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        _confirm = new Button { Text = Loc.GetString("construction-mass-selector-confirm") };

        GmodStyle.Modernize(selectAll);
        GmodStyle.Modernize(clear);
        GmodStyle.Modernize(_confirm);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 4, 0, 0),
            Children = { _countLabel, selectAll, clear, _confirm },
        };

        var searchRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Children = { _search, _tilesToggle },
        };

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(searchRow);
        root.AddChild(_parentRow);
        root.AddChild(_list);
        root.AddChild(buttons);

        var panel = new PanelContainer
        {
            PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(root);
        Contents.AddChild(panel);

        BuildIndex();
        PopulateParentDropdown(string.Empty);
        Refresh();

        _search.OnTextChanged += _ => Refresh();
        _parentSearch.OnTextChanged += args => PopulateParentDropdown(args.Text);
        _parentDropdown.OnItemSelected += args =>
        {
            _parentDropdown.SelectId(args.Id);
            _parentFilterId = args.Id >= 0 && args.Id < _parentOptionIds.Count
                ? _parentOptionIds[args.Id]
                : string.Empty;
            Refresh();
        };
        selectAll.OnPressed += _ =>
        {
            // The WHOLE filtered set - this is how "add everything under BaseWall" works: pick the parent
            // in the dropdown, hit Select All Shown, confirm.
            foreach (var entry in CurrentIndex())
            {
                if (Matches(entry))
                    _selected.Add(entry.Id);
            }
            _list.RefreshRows();
            UpdateCount();
        };
        clear.OnPressed += _ =>
        {
            _selected.Clear();
            _list.RefreshRows();
            UpdateCount();
        };
        _confirm.OnPressed += _ =>
        {
            if (_selected.Count == 0)
                return;

            var ids = new List<string>(_selected);
            if (_tileMode)
                OnTilesSelected?.Invoke(ids);
            else
                OnEntitiesSelected?.Invoke(ids);
            Close();
        };
    }

    /// <summary>The index the current mode browses: entity prototypes or tile definitions.</summary>
    private List<(string Id, string Name, string Haystack)> CurrentIndex() => _tileMode ? _allTiles : _all;

    private void BuildTileIndex()
    {
        foreach (var tile in _prototype.EnumeratePrototypes<ContentTileDefinition>())
        {
            if (tile.Abstract || tile.ID == ContentTileDefinition.SpaceID)
                continue;

            _allTiles.Add((tile.ID, tile.ID, tile.ID.ToLowerInvariant()));
        }

        _allTiles.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>Tile-mode row icon: the tile's texture (same preview technique as the Tiles Editor).</summary>
    private Control MakeTileIcon(string id)
    {
        var preview = new TextureRect
        {
            MinSize = new Vector2(32, 32),
            SetSize = new Vector2(32, 32),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VAlignment.Center,
        };
        if (_prototype.TryIndex<ContentTileDefinition>(id, out var def) && def.Sprite is { } sprite)
        {
            try { preview.Texture = IoCManager.Resolve<IResourceCache>().GetResource<TextureResource>(sprite).Texture; }
            catch { /* missing texture - just show the label */ }
        }
        return preview;
    }

    private void BuildIndex()
    {
        foreach (var proto in _prototype.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract || proto.HideSpawnMenu)
                continue;

            var name = string.IsNullOrEmpty(proto.Name) ? proto.ID : proto.Name;
            _all.Add((proto.ID, name, $"{name} {proto.ID}".ToLowerInvariant()));
        }

        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));

        // Ancestor chains (abstract prototypes included) for the parent dropdown/filter. EnumerateAllParents
        // is the abstract-inclusive API; a per-entity failure must never kill the window, so it's guarded.
        foreach (var (id, _, _) in _all)
        {
            var parents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var (parentId, _) in _prototype.EnumerateAllParents<EntityPrototype>(id, includeSelf: false))
                {
                    parents.Add(parentId);
                }
            }
            catch (Exception e)
            {
                Logger.GetSawmill("au14.massedit").Warning($"EnumerateAllParents failed for {id}: {e.Message}");
            }
            _parentsCache[id] = parents;
        }

        // Fallback: if the enumeration produced nothing at all (engine internals unavailable client-side),
        // rebuild the ancestor graph straight from the raw prototype YAML mappings ("parent:" nodes), which
        // exist for abstract prototypes too.
        if (_parentsCache.Values.All(p => p.Count == 0))
            BuildParentIndexFromMappings();

        foreach (var parents in _parentsCache.Values)
        {
            foreach (var parentId in parents)
            {
                _parentCounts[parentId] = _parentCounts.GetValueOrDefault(parentId) + 1;
            }
        }
    }

    /// <summary>Walk "parent:" nodes in the raw prototype mappings to build the full ancestor set per entity.</summary>
    private void BuildParentIndexFromMappings()
    {
        var directCache = new Dictionary<string, List<string>>();

        List<string> GetDirect(string id)
        {
            if (directCache.TryGetValue(id, out var cached))
                return cached;

            var list = new List<string>();
            if (_prototype.TryGetMapping(typeof(EntityPrototype), id, out var mapping) &&
                mapping.TryGet("parent", out var parentNode))
            {
                switch (parentNode)
                {
                    case ValueDataNode value when !string.IsNullOrWhiteSpace(value.Value):
                        list.Add(value.Value);
                        break;
                    case SequenceDataNode sequence:
                        foreach (var child in sequence)
                        {
                            if (child is ValueDataNode v && !string.IsNullOrWhiteSpace(v.Value))
                                list.Add(v.Value);
                        }
                        break;
                }
            }

            directCache[id] = list;
            return list;
        }

        foreach (var (id, _, _) in _all)
        {
            var parents = _parentsCache[id];
            var queue = new Queue<string>(GetDirect(id));
            while (queue.TryDequeue(out var parentId))
            {
                if (!parents.Add(parentId))
                    continue;

                foreach (var grand in GetDirect(parentId))
                    queue.Enqueue(grand);
            }
        }
    }

    /// <summary>Rebuild the parent dropdown from the parent search text. "All parents" is always option 0.</summary>
    private void PopulateParentDropdown(string filter)
    {
        _parentDropdown.Clear();
        _parentOptionIds.Clear();

        _parentDropdown.AddItem(Loc.GetString("construction-mass-selector-parent-all"), 0);
        _parentOptionIds.Add(string.Empty);

        var needle = filter.Trim();
        var index = 1;
        foreach (var (parentId, count) in _parentCounts
                     .Where(kv => needle.Length == 0 || kv.Key.Contains(needle, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(kv => kv.Value)
                     .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(MaxParentOptions))
        {
            _parentDropdown.AddItem($"{parentId} ({count})", index++);
            _parentOptionIds.Add(parentId);
        }

        // Keep the active filter selected if it survived the re-filter; otherwise fall back to "All parents".
        var current = _parentOptionIds.IndexOf(_parentFilterId);
        if (current < 0)
        {
            _parentFilterId = string.Empty;
            current = 0;
            Refresh();
        }
        _parentDropdown.Select(current);
    }

    private bool Matches((string Id, string Name, string Haystack) entry)
    {
        var needle = _search.Text.Trim().ToLowerInvariant();
        if (needle.Length > 0 && !entry.Haystack.Contains(needle))
            return false;

        if (_tileMode)
            return true; // no parent filtering for tiles

        if (_parentFilterId.Length > 0 &&
            (!_parentsCache.TryGetValue(entry.Id, out var parents) || !parents.Contains(_parentFilterId)))
        {
            return false;
        }

        return true;
    }

    private void Refresh()
    {
        var items = new List<(string Id, string Name)>();
        foreach (var entry in CurrentIndex())
        {
            if (Matches(entry))
                items.Add((entry.Id, entry.Name));
        }

        _list.SetItems(items);
        UpdateCount();
    }

    private void UpdateCount()
    {
        _countLabel.Text = Loc.GetString("construction-mass-selector-count", ("count", _selected.Count));
        _confirm.Disabled = _selected.Count == 0;
    }
}
