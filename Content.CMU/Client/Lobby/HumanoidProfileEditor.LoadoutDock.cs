using Content.Client.CMU14.Lobby;
using Content.Client.Lobby.UI.Loadouts;
using Content.Shared.CCVar;
using Content.Shared.Clothing;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly CmuRevealPanel _loadoutSurface = new() { HorizontalExpand = true, VerticalExpand = true };
    private string? _workspaceLoadoutJob;

    private void OpenWorkspaceLoadout(JobPrototype job, RoleLoadout loadout, RoleLoadoutPrototype prototype)
    {
        var collection = IoCManager.Instance;
        if (collection == null || _playerManager.LocalSession is not { } session || Profile == null)
            return;
        if (_workspaceLoadoutJob == job.ID && _loadoutDock.Visible)
            return;
        CloseWorkspaceChoice();
        var entering = !_loadoutDock.Visible;
        SpriteView.Orphan();
        _loadoutSurface.ReleaseChildren();
        if (_loadoutSurface.Parent == null) _loadoutDock.AddChild(_loadoutSurface);
        _loadoutSurface.PanelOverride = CmuLobbyPresentation.Box(padding: 16);
        _workspaceLoadoutJob = job.ID;
        _loadoutDock.Visible = true;
        PreviewColumn.Visible = false;
        JobOverride = job;
        var key = LoadoutSystem.GetJobPrototype(job.ID);
        var column = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 10, VerticalExpand = true };
        var heading = new BoxContainer();
        heading.AddChild(CmuLobbyPresentation.Heading(job.LocalizedName));
        var close = CmuLobbyPresentation.Action("×");
        close.HorizontalExpand = false;
        close.ToolTip = Loc.GetString("cmu-workspace-close-loadout");
        close.OnPressed += _ => CloseWorkspaceLoadout();
        heading.AddChild(close);
        column.AddChild(heading);

        // The same live model remains visible in the dock; only role content is rebuilt on a role switch.
        SpriteView.Orphan();
        column.AddChild(SpriteView);
        SpriteView.VerticalExpand = false;
        SpriteView.MinHeight = 250;
        SpriteView.MaxHeight = 250;
        column.AddChild(new Label { Text = Profile.Name, HorizontalAlignment = HAlignment.Center, FontColorOverride = CmuLobbyPresentation.Text });
        if (prototype.CanCustomizeName)
        {
            column.AddChild(new Label { Text = Loc.GetString(prototype.NameDataset == null ? "loadout-name-edit-label" : "loadout-name-edit-label-dataset") });
            var name = new LineEdit { Text = loadout.EntityName ?? string.Empty, HorizontalExpand = true };
            var max = _cfgManager.GetCVar(CCVars.MaxLoadoutNameLength);
            name.IsValid = text => text.Length <= max;
            name.OnTextChanged += args =>
            {
                loadout.EntityName = args.Text;
                Profile = Profile?.WithLoadout(key, loadout);
                SetDirty();
            };
            column.AddChild(name);
        }
        var tabs = new CmuAdaptiveGrid(135) { HorizontalExpand = true };
        var tabScroll = new ScrollContainer { HScrollEnabled = false, MinHeight = 90, MaxHeight = 130 };
        tabScroll.AddChild(tabs);
        column.AddChild(tabScroll);
        var content = new BoxContainer { Orientation = LayoutOrientation.Vertical, VerticalExpand = true };
        var scroll = new ScrollContainer { HScrollEnabled = false, HorizontalExpand = true, VerticalExpand = true };
        scroll.AddChild(content);
        column.AddChild(scroll);
        var groups = new List<LoadoutGroupContainer>();
        foreach (var id in prototype.Groups)
        {
            if (!_prototypeManager.Resolve(id, out var group) || group.Hidden) continue;
            var control = new LoadoutGroupContainer(Profile, loadout, group, session, collection) { Visible = groups.Count == 0 };
            control.EnableCmuGallery();
            CmuLobbyPresentation.UseVerticalScrolling(control);
            groups.Add(control);
            content.AddChild(control);
            var button = CmuLobbyPresentation.Action(Loc.GetString(group.Name));
            button.MinWidth = 125;
            button.Label.ClipText = true;
            button.OnPressed += _ =>
            {
                foreach (var other in groups) other.Visible = other == control;
                foreach (var other in tabs.Children)
                    if (other is Button item) CmuLobbyPresentation.Style(item, selected: item == button);
            };
            tabs.AddChild(button);
            CmuLobbyPresentation.Style(button, selected: control.Visible);
            control.OnLoadoutPressed += selected =>
            {
                loadout.AddLoadout(id, selected, _prototypeManager);
                Changed();
            };
            control.OnLoadoutUnpressed += selected =>
            {
                loadout.RemoveLoadout(id, selected, _prototypeManager);
                Changed();
            };
        }
        var defaults = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-role-defaults"));
        defaults.OnPressed += _ =>
        {
            loadout.EntityName = null;
            loadout.SetDefault(Profile, session, _prototypeManager, force: true);
            Changed();
            _workspaceLoadoutJob = null;
            OpenWorkspaceLoadout(job, loadout, prototype);
        };
        column.AddChild(defaults);
        void Changed()
        {
            Profile = Profile?.WithLoadout(key, loadout);
            if (Profile == null) return;
            foreach (var group in groups) group.RefreshLoadouts(Profile, loadout, session, collection);
            ReloadPreview();
            SetDirty();
        }
        _loadoutSurface.AddChild(column);
        CmuLobbyPresentation.UseVerticalScrolling(column);
        CrtLobbyTheme.Apply(column);
        if (entering) _loadoutSurface.Enter();
        ReloadPreview(false);
    }

    private void CloseWorkspaceLoadout()
    {
        if (!_loadoutDock.Visible) return;
        // Return the persistent preview before releasing the old group's local preview entities.
        SpriteView.Orphan();
        _previewStage.AddChild(SpriteView);
        SpriteView.VerticalExpand = false;
        SpriteView.MinHeight = 320;
        SpriteView.MaxHeight = 320;
        _loadoutSurface.ReleaseChildren();
        _loadoutDock.Visible = false;
        PreviewColumn.Visible = true;
        _workspaceLoadoutJob = null;
        JobOverride = null;
        ReloadPreview(false);
    }
}
