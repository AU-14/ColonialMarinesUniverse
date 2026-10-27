using System.Linq;
using Content.Client.CMU14.Lobby;
using Content.Shared.Humanoid;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly BoxContainer _workspaceHair = WorkspaceColumn();
    private readonly BoxContainer _workspaceFace = WorkspaceColumn();
    private readonly Dictionary<string, Control> _appearancePages = new();
    private string _appearancePage = "hair";
    private static readonly (string Id, string Key)[] AppearanceSections =
    {
        ("body", "cmu-workspace-body"), ("hair", "cmu-workspace-hair"),
        ("face", "cmu-workspace-facial-hair"),
        ("regulation", "humanoid-profile-editor-regulation-appearance-tab"),
        ("markings", "humanoid-profile-editor-markings-tab"),
    };

    private void BuildAppearanceStage()
    {
        var page = TabContainer.GetChild(0);
        MoveControl(page.GetChild(0), _workspaceStorage);
        var body = WorkspaceColumn();
        body.MaxWidth = 680;
        body.AddChild(CmuLobbyPresentation.Heading(Loc.GetString("cmu-workspace-body")));
        var facts = new CmuAdaptiveGrid(180) { HorizontalExpand = true };
        facts.AddChild(new CmuFactButton(Loc.GetString("humanoid-profile-editor-sex-label"), SexButton, OpenWorkspaceChoice));
        facts.AddChild(new CmuFactButton(Loc.GetString("humanoid-profile-editor-build-label"), BuildButton, OpenWorkspaceChoice));
        body.AddChild(facts);
        MoveControl(SkinToneNameLabel.Parent!, body);
        MoveControl(CharacterSkinSlider, body);
        MoveControl(CharacterSkinColorPicker, body);
        body.AddChild(CmuLobbyPresentation.Heading(Loc.GetString("cmu-workspace-eyes")));
        MoveControl(EyeColorNameLabel.Parent!, body);
        MoveControl(CharacterEyeColorPicker, body);
        MoveControl(SyntheticContainer, body);
        var bodyScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, HorizontalExpand = true };
        bodyScroll.AddChild(body);
        _appearancePages["body"] = bodyScroll;
        _workspaceHair.VerticalExpand = true;
        _workspaceFace.VerticalExpand = true;
        _appearancePages["hair"] = _workspaceHair;
        _appearancePages["face"] = _workspaceFace;
        foreach (var content in _appearancePages.Values)
        {
            content.Visible = content == _workspaceHair;
            page.AddChild(content);
        }

        _deploymentTools = WorkspaceColumn();
        _deploymentTools.AddChild(new CmuFactButton(Loc.GetString("humanoid-profile-editor-job-fallback-label"), PreferenceUnavailableButton, OpenWorkspaceChoice));
        _deploymentTools.AddChild(WorkspaceField("humanoid-profile-editor-enable-playtime-perks", PlaytimePerksButton));
        _deploymentTools.AddChild(WorkspaceField("humanoid-profile-editor-xeno-prefix", XenoPrefix));
        XenoPrefix.ToolTip = Loc.GetString("humanoid-profile-editor-xeno-prefix-tooltip");
        _deploymentTools.AddChild(WorkspaceField("humanoid-profile-editor-xeno-postfix", XenoPostfix));
        XenoPostfix.ToolTip = Loc.GetString("humanoid-profile-editor-xeno-postfix-tooltip");
        var securityCard = FoxtrotSquadOptInButton.Parent!.Parent!.Parent!;
        var securityRow = (BoxContainer) FoxtrotSquadOptInButton.Parent;
        securityRow.Orientation = LayoutOrientation.Vertical;
        CmuLobbyPresentation.Style(FoxtrotSquadOptInButton, CmuLobbyPresentation.FactionColor("govfor"));
        FoxtrotSquadOptInButton.MinWidth = 0;
        FoxtrotSquadOptInButton.MinHeight = 38;
        FoxtrotSquadOptInButton.HorizontalExpand = true;
        MoveControl(securityCard, _deploymentTools);
        _workspaceStorage.AddChild(_deploymentTools);

        // Keep both restricted regulation pickers and their native validation, but give each a full gallery.
        var regulation = TabContainer.GetChild(RegulationAppearanceTabIndex);
        MoveControl(regulation.GetChild(0), _workspaceStorage);
        MoveControl(RegulationAppearanceInfo, regulation);
        var navigation = new BoxContainer { SeparationOverride = 8, Margin = new Thickness(0, 8) };
        regulation.AddChild(navigation);
        var pages = new List<Control>();
        foreach (var picker in new[] { RegulationHairStylePicker, RegulationFacialHairPicker })
        {
            picker.EnableWorkspaceGallery(() =>
                (Profile?.Sex == Sex.Female, Profile?.Appearance.SkinColor ?? Color.White,
                    Profile?.Appearance.EyeColor ?? Color.Black));
            // UpdateData owns the picker's species/sex visibility; navigation owns its wrapper.
            var wrapper = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                VerticalExpand = true,
                Visible = picker == RegulationHairStylePicker,
            };
            MoveControl(picker, wrapper);
            regulation.AddChild(wrapper);
            pages.Add(wrapper);
            var button = CmuLobbyPresentation.Action(Loc.GetString(picker == RegulationHairStylePicker ? "cmu-workspace-hair" : "cmu-workspace-facial-hair"));
            CmuLobbyPresentation.Style(button, selected: wrapper.Visible);
            button.OnPressed += _ =>
            {
                foreach (var section in pages) section.Visible = section == wrapper;
                foreach (var item in navigation.Children.OfType<Button>()) CmuLobbyPresentation.Style(item, selected: item == button);
            };
            navigation.AddChild(button);
        }
        Markings.EnableWorkspaceGallery();
    }

    private void BuildAppearanceNavigation()
    {
        foreach (var (id, key) in AppearanceSections)
        {
            var button = CmuLobbyPresentation.Action(Loc.GetString(key));
            CmuLobbyPresentation.Style(button, selected: _appearancePage == id);
            button.OnPressed += _ => SelectAppearancePage(id);
            _workspaceDetails.AddChild(button);
        }
    }

    private void SelectAppearancePage(string id)
    {
        CloseWorkspaceChoice();
        CloseWorkspaceLoadout();
        _appearancePage = id;
        TabContainer.CurrentTab = id switch { "regulation" => RegulationAppearanceTabIndex, "markings" => MarkingsTabIndex, _ => 0 };
        foreach (var (key, page) in _appearancePages) page.Visible = key == id;
        RefreshWorkspaceDetails();
    }
}
