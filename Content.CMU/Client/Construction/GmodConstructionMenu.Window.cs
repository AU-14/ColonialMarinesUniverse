// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Client.Administration.Managers;
using Content.Client.CMU14.Administration;
using Content.Client.CMU14.Construction.CustomConstruction;
using Content.Shared.Administration;
using Content.Shared.CMU14.Administration;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Construction;

public sealed partial class GmodConstructionMenu
{
    private readonly MenuBar.Menu _staffMenu = new();
    private readonly MenuBar.MenuButton _changeRecipe = new();
    private readonly MenuBar.MenuButton _removeRecipe = new();
    private string? _targetEntity;
    private string? _recipeId;

    private readonly ToolPermissionClientSystem ToolPermissions = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ToolPermissionClientSystem>();

    private void ConfigureAdminTools()
    {
        _staffMenu.Title = Loc.GetString("gmod-construction-menu-admin-tools");
        AdminTools.Menus.Add(_staffMenu);
        var editor = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CustomConstructionEditorClientSystem>();
        _changeRecipe.Text = Loc.GetString("gmod-construction-menu-change-recipe");
        _changeRecipe.OnPressed = () => { if (_targetEntity is { } id) editor.RequestChangeRecipe(id); };
        _removeRecipe.Text = Loc.GetString("gmod-construction-menu-remove-item");
        _removeRecipe.OnPressed = () => { if (_recipeId is { } id) editor.HideRecipe(id); };
        UpdateRecipeActions();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        ToolPermissions.GrantsChanged += RefreshAdminTools;
        RefreshAdminTools();
    }

    protected override void ExitedTree()
    {
        ToolPermissions.GrantsChanged -= RefreshAdminTools;
        base.ExitedTree();
    }

    private void UpdateRecipeActions()
    {
        _changeRecipe.Disabled = _targetEntity == null;
        _removeRecipe.Disabled = _recipeId == null;
    }

    private void RefreshAdminTools()
    {
        var permissions = ToolPermissions;
        var editor = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<CustomConstructionEditorClientSystem>();
        var entries = _staffMenu.Entries;
        entries.Clear();
        void Add(string key, Action open) => entries.Add(new MenuBar.MenuButton { Text = Loc.GetString(key), OnPressed = open });

        if (permissions.CanUse(AU14ToolPermissions.Construction))
        {
            Add("gmod-construction-menu-items-editor", editor.OpenItemsEditor);
            entries.Add(_changeRecipe);
            entries.Add(_removeRecipe);
        }
        if (permissions.CanUse(AU14ToolPermissions.Mass))
            Add("gmod-construction-menu-mass-editor", editor.OpenMassEditor);
        if (permissions.CanUse(AU14ToolPermissions.Tiles))
            Add("gmod-construction-menu-tiles-editor", editor.OpenTilesEditor);
        if (permissions.CanUse(AU14ToolPermissions.Lathe))
            Add("gmod-construction-menu-lathe-editor", editor.OpenLatheEditor);
        if (permissions.CanUse(AU14ToolPermissions.SpawnlistDelete))
            Add("gmod-construction-menu-spawnlist-delete", editor.OpenSpawnlistDelete);
        if (permissions.CanUse(AU14ToolPermissions.ZLevelToggles))
            Add("gmod-construction-menu-zlevel-toggles", editor.OpenZLevelToggles);
        if (permissions.CanUse(AU14ToolPermissions.ZSync))
            Add("gmod-construction-menu-zsync-lists", editor.OpenZSyncLists);
        if (IoCManager.Resolve<IClientAdminManager>().HasFlag(AdminFlags.Host))
            Add("gmod-construction-menu-tool-permissions", permissions.OpenManager);
        AdminTools.Visible = entries.Count != 0;
    }
}
