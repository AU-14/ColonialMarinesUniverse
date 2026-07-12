// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._AU14.UI;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Entity-Spawn-Panel-style picker reshaped for SELECTION: a search box over every spawnable entity
/// prototype and a scrollable list of rows showing each entity's SPRITE next to its name + id. Clicking a
/// row picks it. The list is virtualized (<see cref="VirtualEntityList"/>) so it scrolls through the entire
/// prototype set without lag. Reused by the construction editor (choose a custom material/tool entity) and
/// by the in-menu "Construction Items Editor" utility. Fires <see cref="OnEntitySelected"/>.
/// </summary>
public sealed class EntitySelectorWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototype;
    private readonly LineEdit _search;
    private readonly VirtualEntityList _list;

    // (id, display name, lowercased "name id" haystack) for fast filtering; built once.
    private readonly List<(string Id, string Name, string Haystack)> _all = new();

    public event Action<string>? OnEntitySelected;

    public EntitySelectorWindow()
    {
        _prototype = IoCManager.Resolve<IPrototypeManager>();

        Title = Loc.GetString("construction-selector-title");
        MinSize = new Vector2(460, 560);

        _search = new LineEdit
        {
            PlaceHolder = Loc.GetString("construction-selector-search"),
            HorizontalExpand = true,
        };
        _list = new VirtualEntityList();
        _list.OnRowToggled += (id, _) =>
        {
            OnEntitySelected?.Invoke(id);
            Close();
        };

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(_search);
        root.AddChild(_list);

        // Dark gmod-style panel behind the picker so it matches the construction menu / editor.
        var panel = new PanelContainer
        {
            PanelOverride = GmodStyle.Panel(GmodStyle.PanelBg),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        panel.AddChild(root);
        Contents.AddChild(panel);

        BuildIndex();
        Refresh(string.Empty);

        _search.OnTextChanged += args => Refresh(args.Text);
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
    }

    private void Refresh(string filter)
    {
        var needle = filter.Trim().ToLowerInvariant();
        var items = new List<(string Id, string Name)>();
        foreach (var entry in _all)
        {
            if (needle.Length > 0 && !entry.Haystack.Contains(needle))
                continue;

            items.Add((entry.Id, entry.Name));
        }

        _list.SetItems(items);
    }
}
