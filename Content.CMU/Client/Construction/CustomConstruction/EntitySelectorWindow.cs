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
using Robust.Shared.Prototypes;

namespace Content.Client._AU14.Construction.CustomConstruction;

/// <summary>
/// Entity-Spawn-Panel-style picker reshaped for SELECTION: a search box over every spawnable entity
/// prototype and a scrollable list of rows showing each entity's SPRITE next to its name + id. Clicking a
/// row picks it. Reused by the construction editor (choose a custom material/tool entity) and by the
/// in-menu "Construction Items Editor" utility (choose an item to add). Fires <see cref="OnEntitySelected"/>.
/// </summary>
public sealed class EntitySelectorWindow : DefaultWindow
{
    // Cap rendered rows so a blank search doesn't try to spin up a sprite view for the entire prototype set.
    private const int MaxRows = 200;

    private readonly IPrototypeManager _prototype;
    private readonly LineEdit _search;
    private readonly BoxContainer _rows;

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
        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };

        var scroll = new ScrollContainer { VerticalExpand = true, HorizontalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_rows);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8) };
        root.AddChild(_search);
        root.AddChild(scroll);

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
        _rows.RemoveAllChildren();

        var needle = filter.Trim().ToLowerInvariant();
        var count = 0;
        foreach (var entry in _all)
        {
            if (needle.Length > 0 && !entry.Haystack.Contains(needle))
                continue;

            _rows.AddChild(MakeRow(entry.Id, entry.Name));

            if (++count >= MaxRows)
                break;
        }
    }

    private Control MakeRow(string id, string name)
    {
        var view = new EntityPrototypeView
        {
            SetSize = new Vector2(32, 32),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VAlignment.Center,
        };
        view.SetPrototype(id);

        var row = new ContainerButton { HorizontalExpand = true, Margin = new Thickness(0, 0, 0, 2) };
        row.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(4, 2, 4, 2),
            Children =
            {
                view,
                new Label { Text = $"{name}  [{id}]", VerticalAlignment = VAlignment.Center },
            },
        });
        GmodStyle.Modernize(row);
        row.OnPressed += _ =>
        {
            OnEntitySelected?.Invoke(id);
            Close();
        };
        return row;
    }
}
