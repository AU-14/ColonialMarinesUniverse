using Content.Client.CMU14.Lobby;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGui
{
    private ScrollContainer? _workspaceCharacters;
    private Button? _workspaceCharacterSwitch;

    private void InitializeWorkspaceHeader()
    {
        var header = CloseButton.Parent!;
        var main = header.Parent!;
        var records = new CmuAdaptiveGrid(260)
        {
            HSeparationOverride = 6,
            VSeparationOverride = 6,
            HorizontalExpand = true,
            Visible = false,
            Margin = new Thickness(12, 4, 12, 10),
        };
        foreach (var button in new Button[] { CommendationsReceivedButton, CommendationsGivenButton, StatsButton, AdminRemarksButton, RulesButton })
        {
            button.Orphan();
            button.HorizontalExpand = true;
            button.HorizontalAlignment = HAlignment.Stretch;
            CmuLobbyPresentation.Style(button);
            records.AddChild(button);
        }
        header.AddChild(new Control { HorizontalExpand = true });
        var service = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-service-record"));
        service.HorizontalExpand = false;
        service.OnPressed += _ => records.Visible = !records.Visible;
        header.AddChild(service);
        CloseButton.SetPositionLast();
        CmuLobbyPresentation.Style(CloseButton);
        main.AddChild(records);
        records.SetPositionInParent(1);
        _createNewCharacterButton.Text = Loc.GetString("cmu-workspace-new-character");
        _createNewCharacterButton.MinHeight = 48;
        CmuLobbyPresentation.Style(_createNewCharacterButton);
        // A compact switcher leaves the full workspace available to the preview and editor.
        _workspaceCharacters = (ScrollContainer) Characters.Parent!;
        var body = (BoxContainer) _workspaceCharacters.Parent!;
        body.Orientation = BoxContainer.LayoutOrientation.Vertical;
        // The editor is now below the toolbar, so it must claim the remaining vertical space.
        CharEditor.VerticalExpand = true;
        BodyDividerPanel.Visible = false;
        _workspaceCharacters.Visible = false;
        _workspaceCharacters.MinWidth = 0;
        _workspaceCharacters.MaxWidth = float.PositiveInfinity;
        _workspaceCharacters.MinHeight = 190;
        _workspaceCharacters.MaxHeight = 190;
        _workspaceCharacters.HScrollEnabled = false;
        _workspaceCharacters.VScrollEnabled = true;
        Characters.HSeparationOverride = 10;
        Characters.VSeparationOverride = 10;
        var toolbar = new CmuAdaptiveGrid(320)
        {
            HSeparationOverride = 12,
            VSeparationOverride = 8,
            HorizontalExpand = true,
            MaxWidth = 840,
            Margin = new Thickness(16, 12),
        };
        _workspaceCharacterSwitch = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-switch-character"));
        _workspaceCharacterSwitch.Label.ClipText = true;
        _workspaceCharacterSwitch.Label.Orphan();
        var identity = new BoxContainer { SeparationOverride = 12, MouseFilter = MouseFilterMode.Ignore };
        var editor = (HumanoidProfileEditor) CharEditor.GetChild(0);
        identity.AddChild(new CmuCharacterHeadshot(() => editor.Profile));
        identity.AddChild(_workspaceCharacterSwitch.Label);
        _workspaceCharacterSwitch.AddChild(identity);
        _createNewCharacterButton.HorizontalExpand = true;
        _createNewCharacterButton.MinHeight = 74;
        _workspaceCharacterSwitch.OnPressed += _ => _workspaceCharacters.Visible = !_workspaceCharacters.Visible;
        toolbar.AddChild(_workspaceCharacterSwitch);
        toolbar.AddChild(_createNewCharacterButton);
        body.AddChild(toolbar);
        toolbar.SetPositionFirst();
        CrtLobbyTheme.Apply(header);
        CrtLobbyTheme.Apply(toolbar);
    }

    private void RefreshWorkspaceCharacterTitle()
    {
        if (_workspaceCharacterSwitch == null) return;
        _workspaceCharacterSwitch.Text = _preferencesManager.Preferences?.SelectedCharacter.Name + "  /  " + Loc.GetString("cmu-workspace-switch-character");
        _workspaceCharacterSwitch.ToolTip = _workspaceCharacterSwitch.Text;
    }
}
