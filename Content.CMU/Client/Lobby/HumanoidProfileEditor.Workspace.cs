using System.Linq;
using System.Numerics;
using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly WrapContainer _workspaceNavigation = new() { SeparationOverride = 8, CrossSeparationOverride = 6, HorizontalExpand = true };
    private readonly CmuAdaptiveGrid _workspaceDetails = new(200) { HSeparationOverride = 6, VSeparationOverride = 6, HorizontalExpand = true };
    private readonly CmuAdaptiveGrid _workspaceModes = new(150) { HSeparationOverride = 8, VSeparationOverride = 8 };
    private readonly BoxContainer _workspaceStorage = new() { Visible = false };
    private readonly CmuRevealPanel _workspaceSurface = new() { VerticalExpand = true, HorizontalExpand = true };
    private readonly CmuRevealPanel _choiceSurface = new() { Visible = false, MinWidth = 460, MaxWidth = 460, VerticalExpand = true };
    private readonly BoxContainer _choiceContent = new() { Orientation = LayoutOrientation.Vertical, SeparationOverride = 12, VerticalExpand = true };
    private readonly BoxContainer _loadoutDock = new() { Orientation = LayoutOrientation.Vertical, Visible = false, MinWidth = 460, MaxWidth = 460, VerticalExpand = true };
    private readonly Dictionary<string, Button> _workspaceButtons = new();
    private readonly Label _workspaceName = new() { Align = Label.AlignMode.Center, ClipText = true, HorizontalExpand = true, FontColorOverride = CmuLobbyPresentation.Text };
    private readonly Label _workspaceStatus = new();
    private readonly RichTextLabel _modeContext = new() { HorizontalExpand = true };
    private readonly Dictionary<Button, Color> _modeColors = new();
    private string _workspaceSection = string.Empty;
    private int _workspaceMode = InsurgencyTabIndex;
    private OptionButton? _choiceSource;
    private Control? _choiceOriginalParent;
    private Control? _activeSheet;
    private BoxContainer _profileTools = new();
    private BoxContainer _deploymentTools = new();
    private bool _workspaceReady;
    private readonly PanelContainer _previewStage = new();
    private readonly RichTextLabel _workspacePreviewRole = new() { HorizontalExpand = true };

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        if (!_workspaceReady)
            return base.MeasureOverride(availableSize);

        var previewWidth = Math.Clamp(availableSize.X * 0.23f, 150, 260);
        PreviewColumn.MinWidth = PreviewColumn.MaxWidth = previewWidth;
        SpriteView.Scale = new Vector2(Math.Min(9, previewWidth / 32));
        var dockOpen = _choiceSurface.Visible || _loadoutDock.Visible;
        // On narrow workspaces the detail sheet uses the stage until Done is pressed.
        EditorColumn.Visible = !dockOpen || availableSize.X >= 850;
        var dockWidth = EditorColumn.Visible ? Math.Min(460, availableSize.X * 0.45f) : availableSize.X;
        _choiceSurface.MinWidth = _choiceSurface.MaxWidth = dockWidth;
        _loadoutDock.MinWidth = _loadoutDock.MaxWidth = dockWidth;

        // Measure the dock/Bioview first so wrapping galleries receive the space they will actually use.
        var remaining = availableSize;
        var desired = Vector2.Zero;
        foreach (var child in Children)
        {
            if (!child.Visible || child == EditorColumn)
                continue;

            child.Measure(remaining);
            desired.X += child.DesiredSize.X;
            desired.Y = Math.Max(desired.Y, child.DesiredSize.Y);
            remaining.X = Math.Max(0, remaining.X - child.DesiredSize.X);
        }

        EditorColumn.Measure(remaining);
        return new Vector2(desired.X + EditorColumn.DesiredSize.X,
            Math.Max(desired.Y, EditorColumn.DesiredSize.Y));
    }

    private static void MoveControl(Control control, Control parent)
    {
        control.Orphan();
        parent.AddChild(control);
    }

    private void InitializeWorkspace()
    {
        TabContainer.TabsVisible = false;
        VerticalExpand = true;
        SeparationOverride = 0;
        EditorColumn.SeparationOverride = 12;
        EditorColumn.Margin = new Thickness(16);
        MoveControl(IdentityHeader, _workspaceStorage);
        AddChild(_workspaceStorage);
        var header = new WrapContainer { SeparationOverride = 8, CrossSeparationOverride = 6 };
        header.AddChild(_workspaceNavigation);
        foreach (var button in new[] { ResetButton, SaveButton })
        {
            MoveControl(button, header);
            CmuLobbyPresentation.Style(button);
            button.MinHeight = 44;
            button.MinWidth = 80;
        }
        EditorColumn.AddChild(header);
        header.SetPositionFirst();
        foreach (var (id, key) in new[] { ("dossier", "cmu-workspace-dossier"), ("appearance", "cmu-workspace-appearance"), ("deployment", "cmu-workspace-deployment") })
        {
            var button = CmuLobbyPresentation.Action(Loc.GetString(key));
            button.MinHeight = 44;
            button.OnPressed += _ => SelectWorkspace(id);
            _workspaceButtons[id] = button;
            _workspaceNavigation.AddChild(button);
        }
        var modesScroll = new ScrollContainer { HScrollEnabled = false, MinHeight = 184, MaxHeight = 210 };
        modesScroll.AddChild(_workspaceModes);
        EditorColumn.AddChild(modesScroll);
        EditorColumn.AddChild(_modeContext);
        EditorColumn.AddChild(_workspaceDetails);
        MoveControl(RoundPreferencePanel, _workspaceStorage);
        MoveControl(TabContainer, _workspaceSurface);
        _workspaceSurface.PanelOverride = CmuLobbyPresentation.Box(padding: 16);
        EditorColumn.AddChild(_workspaceSurface);
        EditorColumn.AddChild(_workspaceStatus);
        _choiceSurface.PanelOverride = CmuLobbyPresentation.Box(padding: 16);
        _choiceSurface.AddChild(_choiceContent);
        AddChild(_choiceSurface);
        AddChild(_loadoutDock);
        BuildPreviewStage();
        BuildAppearanceStage();
        BuildDossier();
        BuildDeploymentChoices();
        BuildDeploymentPages();
        BuildModeCards();
        CmuLobbyPresentation.UseVerticalScrolling(this);
        _requirements.Updated += RefreshWorkspaceDetails;
        _workspaceReady = true;
        SelectWorkspace("dossier");
    }

    private void BuildPreviewStage()
    {
        PreviewColumn.MinWidth = 260;
        PreviewColumn.MaxWidth = 280;
        PreviewColumn.Margin = new Thickness(0, 16, 16, 16);
        PreviewColumn.VerticalAlignment = VAlignment.Top;
        PreviewColumn.SeparationOverride = 14;
        PreviewColumn.SetPositionLast();
        var heading = CmuLobbyPresentation.Heading(Loc.GetString("cmu-workspace-bioview"));
        heading.ClipText = true;
        PreviewColumn.AddChild(heading);
        heading.SetPositionFirst();
        SpriteView.Scale = new Vector2(9);
        SpriteView.MinHeight = 320;
        SpriteView.MaxHeight = 320;
        SpriteView.VerticalExpand = false;
        _previewStage.PanelOverride = CmuLobbyPresentation.Box(padding: 8);
        MoveControl(SpriteView, _previewStage);
        PreviewColumn.AddChild(_previewStage);
        _previewStage.SetPositionInParent(1);
        PreviewColumn.AddChild(_workspaceName);
        _workspaceName.SetPositionInParent(2);
        PreviewJobLabel.MinWidth = 0;
        PreviewJobLabel.MaxWidth = 260;
        PreviewJobLabel.HorizontalAlignment = HAlignment.Stretch;
        PreviewJobLabel.Align = Label.AlignMode.Center;
        PreviewJobLabel.FontColorOverride = CmuLobbyPresentation.Text;
        PreviewJobLabel.Visible = false;
        PreviewColumn.AddChild(_workspacePreviewRole);
        _workspacePreviewRole.SetPositionInParent(PreviewJobLabel.GetPositionInParent());
        ShowClothes.Parent!.Visible = false;
        var clothes = new BoxContainer { SeparationOverride = 8 };
        clothes.AddChild(new Label { Text = Loc.GetString("humanoid-profile-editor-clothing"), ClipText = true, HorizontalExpand = true });
        MoveControl(ShowClothes, clothes);
        CmuLobbyPresentation.Style(ShowClothes);
        PreviewColumn.AddChild(clothes);
        var shortcuts = new CmuAdaptiveGrid(116) { HorizontalExpand = true };
        foreach (var (id, key) in new[] { ("hair", "cmu-workspace-hair"), ("face", "cmu-workspace-facial-hair"), ("body", "cmu-workspace-body"), ("markings", "humanoid-profile-editor-markings-tab") })
        {
            var button = CmuLobbyPresentation.Action(Loc.GetString(key));
            button.MinWidth = 116;
            button.OnPressed += _ => { SelectWorkspace("appearance"); SelectAppearancePage(id); };
            shortcuts.AddChild(button);
        }
        PreviewColumn.AddChild(shortcuts);
    }

    private void BuildDeploymentChoices()
    {
        var choices = new CmuAdaptiveGrid(190) { HorizontalExpand = true, Margin = new Thickness(0, 8) };
        foreach (var (source, key) in new[] { (FoFSideButton, "cmu-fof-preferred-side"), (FoFFallbackButton, "cmu-fof-fallback-label") })
        {
            source.Parent!.Visible = false;
            choices.AddChild(new CmuFactButton(Loc.GetString(key), source, OpenWorkspaceChoice));
        }
        TabContainer.GetChild(ForceOnForceTabIndex).AddChild(choices);
        choices.SetPositionInParent(1);
    }

    private void BuildModeCards()
    {
        foreach (var (index, faction, code) in new[] { (InsurgencyTabIndex, "opfor", "INS"), (ColonyFallTabIndex, "colonists", "CF"), (DistressSignalTabIndex, "other", "DS"), (ForceOnForceTabIndex, "govfor", "FOF") })
        {
            var accent = CmuLobbyPresentation.FactionColor(faction);
            var button = CmuLobbyPresentation.Action(string.Empty, accent);
            _modeColors[button] = accent;
            button.MinHeight = 184;
            button.ToolTip = TabContainer.GetActualTabTitle(index);
            var column = new BoxContainer { Orientation = LayoutOrientation.Vertical, MouseFilter = MouseFilterMode.Ignore, SeparationOverride = 6 };
            column.AddChild(new CmuFactionArtwork { Faction = faction, MinHeight = 125, MaxHeight = 125, HorizontalExpand = true, MouseFilter = MouseFilterMode.Ignore });
            column.AddChild(new Label { Text = code + " / " + TabContainer.GetActualTabTitle(index), ClipText = true, FontColorOverride = CmuLobbyPresentation.Text });
            button.AddChild(column);
            CmuLobbyPresentation.Style(button, accent, index == _workspaceMode);
            button.OnPressed += _ =>
            {
                if (_workspaceMode == index) return;
                CloseWorkspaceChoice();
                CloseWorkspaceLoadout();
                _workspaceMode = index;
                TabContainer.CurrentTab = index;
                RefreshWorkspaceDetails();
                foreach (var sibling in _workspaceModes.Children.OfType<Button>())
                    CmuLobbyPresentation.Style(sibling, _modeColors[sibling], sibling == button);
            };
            _workspaceModes.AddChild(button);
        }
    }

    private void SelectWorkspace(string section)
    {
        if (_workspaceSection == section) return;
        CloseWorkspaceChoice();
        CloseWorkspaceLoadout();
        _workspaceSection = section;
        _workspaceModes.Visible = section == "deployment";
        _workspaceModes.Parent!.Visible = _workspaceModes.Visible;
        _modeContext.Visible = section == "deployment";
        TabContainer.CurrentTab = section switch { "dossier" => CharacterDescriptionTabIndex, "appearance" => _appearancePage switch { "regulation" => RegulationAppearanceTabIndex, "markings" => MarkingsTabIndex, _ => 0 }, _ => _workspaceMode };
        foreach (var (id, button) in _workspaceButtons)
            CmuLobbyPresentation.Style(button, selected: id == section);
        RefreshWorkspaceDetails();
        _workspaceSurface.Enter();
    }

    private void RefreshWorkspaceDetails()
    {
        if (_workspaceSection.Length == 0) return;
        _workspaceDetails.RemoveAllChildren();
        if (_workspaceSection == "deployment")
        {
            var nested = _workspaceMode switch { InsurgencyTabIndex => InsurgencyTabs, ColonyFallTabIndex => ColonyFallTabs, DistressSignalTabIndex => DistressSignalTabs, _ => ForceOnForceTabs };
            AddSectionNavigation(nested, Enumerable.Range(0, nested.ChildCount));
            _modeContext.SetMessage(Loc.GetString("cmu-workspace-mode-context"), CmuLobbyPresentation.Text);
            _workspaceSurface.PanelOverride = CmuLobbyPresentation.Box(WorkspaceModeColor(), padding: 12);
        }
        else
        {
            _workspaceSurface.PanelOverride = CmuLobbyPresentation.Box(padding: 12);
            if (_workspaceSection == "appearance")
                BuildAppearanceNavigation();
            else
                AddSectionNavigation(TabContainer, new[] { CharacterDescriptionTabIndex, TraitsTabIndex, NamedItemsTabIndex }.Concat(Enumerable.Range(NamedItemsTabIndex + 1, Math.Max(0, TabContainer.ChildCount - NamedItemsTabIndex - 1))));
        }
    }

    private Color WorkspaceModeColor() => CmuLobbyPresentation.FactionColor(_workspaceMode switch
    {
        InsurgencyTabIndex => "opfor",
        ColonyFallTabIndex => "colonists",
        ForceOnForceTabIndex => "govfor",
        _ => "other",
    });

    private void AddSectionNavigation(TabContainer tabs, IEnumerable<int> indices)
    {
        tabs.TabsVisible = false;
        foreach (var index in indices)
        {
            if (!tabs.GetTabVisible(index)) continue;
            var button = CmuLobbyPresentation.Action(tabs.GetActualTabTitle(index));
            button.Label.ClipText = true;
            var accent = _workspaceSection == "deployment" ? WorkspaceModeColor() : (Color?) null;
            CmuLobbyPresentation.Style(button, accent, tabs.CurrentTab == index);
            button.OnPressed += _ =>
            {
                if (tabs.CurrentTab == index) return;
                CloseWorkspaceChoice();
                tabs.CurrentTab = index;
                foreach (var sibling in _workspaceDetails.Children.OfType<Button>())
                    CmuLobbyPresentation.Style(sibling, accent, sibling == button);
            };
            _workspaceDetails.AddChild(button);
        }
        CrtLobbyTheme.Apply(_workspaceDetails);
    }

    private void PrepareWorkspaceSheet(string title)
    {
        // Keep the mounted surface visible when replacing content: no entrance replay or form reflow.
        CloseWorkspaceChoice(false);
        CloseWorkspaceLoadout();
        PreviewColumn.Visible = false;
        var header = new BoxContainer { SeparationOverride = 8 };
        header.AddChild(new Label { Text = title, ClipText = true, HorizontalExpand = true, FontColorOverride = CmuLobbyPresentation.Text });
        var close = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-done"));
        close.HorizontalExpand = false;
        close.OnPressed += _ => CloseWorkspaceChoice();
        header.AddChild(close);
        _choiceContent.AddChild(header);
        _choiceSurface.Reveal();
    }

    private void OpenWorkspaceSheet(Control content, string title)
    {
        if (_activeSheet == content) { CloseWorkspaceChoice(); return; }
        PrepareWorkspaceSheet(title);
        _activeSheet = content;
        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        MoveControl(content, scroll);
        _choiceContent.AddChild(scroll);
        CmuLobbyPresentation.UseVerticalScrolling(_choiceContent);
        CrtLobbyTheme.Apply(_choiceContent);
    }

    private void OpenWorkspaceChoice(OptionButton source, string title)
    {
        if (_choiceSource == source) { CloseWorkspaceChoice(); return; }
        PrepareWorkspaceSheet(title);
        _choiceSource = source;
        _choiceOriginalParent = source.OptionsScroll.Parent;
        source.Filterable = source.ItemCount > 8;
        // Keep native filtering, disabled states and callbacks; options fill the responsive sheet.
        var options = source.OptionsScroll.GetChild(0).GetChild(1);
        foreach (var option in options.Children.OfType<Button>())
        {
            option.MinHeight = 48;
            option.MinWidth = 0;
            option.HorizontalExpand = true;
            option.Label.ClipText = true;
            option.ToolTip = option.Text;
            CmuLobbyPresentation.Style(option, selected: option.Pressed);
        }
        source.OptionsScroll.ReturnMeasure = false;
        source.OptionsScroll.HScrollEnabled = false;
        source.OptionsScroll.HorizontalExpand = true;
        source.OptionsScroll.VerticalExpand = true;
        source.OptionsScroll.MaxHeight = float.PositiveInfinity;
        MoveControl(source.OptionsScroll, _choiceContent);
        source.OnItemSelected += WorkspaceChoiceSelected;
        CmuLobbyPresentation.UseVerticalScrolling(_choiceContent);
        CrtLobbyTheme.Apply(_choiceContent);
    }

    private void WorkspaceChoiceSelected(OptionButton.ItemSelectedEventArgs _) => CloseWorkspaceChoice();

    private void CloseWorkspaceChoice(bool hide = true)
    {
        if (_choiceSource != null)
        {
            _choiceSource.OnItemSelected -= WorkspaceChoiceSelected;
            _choiceSource.OptionsScroll.ReturnMeasure = true;
            _choiceSource.OptionsScroll.MaxHeight = 300;
            if (_choiceOriginalParent != null)
                MoveControl(_choiceSource.OptionsScroll, _choiceOriginalParent);
        }
        if (_activeSheet != null)
            MoveControl(_activeSheet, _workspaceStorage);
        _choiceSource = null;
        _choiceOriginalParent = null;
        _activeSheet = null;
        _choiceContent.RemoveAllChildren();
        if (hide)
        {
            _choiceSurface.Visible = false;
            PreviewColumn.Visible = !_loadoutDock.Visible;
        }
    }

    private void UpdateWorkspaceStatus()
    {
        if (!_workspaceReady) return;
        foreach (var refresh in _refreshRecordSummaries)
            refresh();
        _workspaceName.Text = Profile?.Name;
        _workspaceStatus.Text = Loc.GetString(IsDirty ? "cmu-workspace-unsaved" : "cmu-workspace-saved");
        _workspaceStatus.FontColorOverride = IsDirty ? Color.FromHex("#D3AF70") : CmuLobbyPresentation.Muted;
    }
}
