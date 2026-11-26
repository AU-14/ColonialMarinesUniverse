using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.CMU14.Administration;
using Content.Shared.Administration;
using Content.Shared.CMU14.Administration;

namespace Content.Client.Construction.UI;

public sealed partial class GmodConstructionMenu
{
    private readonly ToolPermissionClientSystem _toolPermissions =
        IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ToolPermissionClientSystem>();

    private void RefreshToolPermissions()
    {
        _isAdmin = IoCManager.Resolve<IClientAdminManager>().HasFlag(AdminFlags.Host);
        _canEditMenu = _toolPermissions.CanUse(AU14ToolPermissions.Construction);
        ConstructionItemsEditorButton.Visible = _canEditMenu;
        MassEntityEditorButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.Mass);
        TilesEditorButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.Tiles);
        LatheEditorButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.Lathe);
        SpawnlistDeleteButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.SpawnlistDelete);
        ZLevelTogglesButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.ZLevelToggles);
        ZSyncListsButton.Visible = _toolPermissions.CanUse(AU14ToolPermissions.ZSync);
        ToolPermissionsButton.Visible = _isAdmin;
        AdminToolsHeader.Visible = _isAdmin || AU14ToolPermissions.AllTools.Any(tool => _toolPermissions.CanUse(tool.Id));
        RecipeChangeButton.Visible = _canEditMenu && _currentTargetEntityId != null;
        RecipeRemoveButton.Visible = _canEditMenu && _currentRecipeId != null;
        SavedBuildFolderButton.Visible = _isAdmin && _currentSavedBuild != null;
    }

    private void ConfigureToolsToggle()
    {
        Modernize(TabTools, toggle: true);
        TabTools.OnToggled += args =>
        {
            var widthChange = args.Pressed ? ToolsPanel.MinWidth : -ToolsPanel.MinWidth;
            ToolsPanel.Visible = args.Pressed;
            MinSize += new Vector2(widthChange, 0);
            SetSize = new Vector2(Size.X + widthChange, Size.Y);
        };
    }

    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var mode = base.GetDragModeFor(relativeMousePos);
        if (mode != DragMode.None)
            return mode;

        var header = relativeMousePos + GlobalPosition - TopBarPanel.GlobalPosition;
        return header.X >= 0 && header.Y >= 0 && header.X < TopBarPanel.Width && header.Y < TopBarPanel.Height
            ? DragMode.Move : DragMode.None;
    }
}
