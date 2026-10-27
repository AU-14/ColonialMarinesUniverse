using Content.Client.CMU14.Lobby;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<Action> _refreshRecordSummaries = new();
    private static BoxContainer WorkspaceColumn() => new()
    {
        Orientation = LayoutOrientation.Vertical,
        SeparationOverride = 12,
        HorizontalExpand = true,
    };

    private static Control WorkspaceField(string key, Control input)
    {
        var field = WorkspaceColumn();
        field.AddChild(new Label { Text = Loc.GetString(key), FontColorOverride = CmuLobbyPresentation.Muted });
        input.MinWidth = 0;
        input.MaxWidth = float.PositiveInfinity;
        input.MinHeight = 38;
        input.Margin = new Thickness(0);
        input.HorizontalAlignment = HAlignment.Stretch;
        input.HorizontalExpand = true;
        if (input is Button button)
            CmuLobbyPresentation.Style(button);
        if (input is TextEdit textEdit)
            field.AddChild(CmuLobbyPresentation.TextArea(textEdit));
        else
            MoveControl(input, field);
        return field;
    }

    private void BuildDossier()
    {
        var scroll = (ScrollContainer) TabContainer.GetChild(CharacterDescriptionTabIndex).GetChild(0);
        MoveControl(scroll.GetChild(0), _workspaceStorage);
        var page = WorkspaceColumn();
        scroll.AddChild(page);
        TabContainer.SetTabVisible(CharacterDescriptionTabIndex, true);
        page.AddChild(CmuLobbyPresentation.Heading(Loc.GetString("cmu-workspace-personal-file")));
        var identity = new CmuAdaptiveGrid(250) { HorizontalExpand = true };
        identity.AddChild(WorkspaceField("cmu-profile-editor-first-name-label", FirstNameEdit));
        identity.AddChild(WorkspaceField("cmu-profile-editor-last-name-label", LastNameEdit));
        identity.AddChild(WorkspaceField("cmu-profile-editor-nickname-label", NicknameEdit));
        identity.AddChild(WorkspaceField("humanoid-profile-editor-age-label", AgeEdit));
        page.AddChild(identity);
        MoveControl(NameRequiredLabel, page);
        WarningLabel.MaxWidth = float.PositiveInfinity;
        WarningLabel.HorizontalAlignment = HAlignment.Stretch;
        WarningLabel.HorizontalExpand = true;
        WarningLabel.VerticalExpand = false;
        MoveControl(WarningLabel, page);

        var facts = new CmuAdaptiveGrid(240) { HorizontalExpand = true, Visible = _allowCharacterDescription };
        foreach (var (source, key) in new[]
        {
            (PronounsButton, "humanoid-profile-editor-pronouns-label"),
            (OriginButton, "humanoid-profile-editor-origin-label"),
            (AllegianceButton, "humanoid-profile-editor-allegiance-label"),
            (ReligionButton, "cmu-humanoid-profile-editor-religion-label"),
            (BloodTypeButton, "cmu-humanoid-profile-editor-blood-type-label"),
        })
            facts.AddChild(new CmuFactButton(Loc.GetString(key), source, OpenWorkspaceChoice) { MinWidth = 170, MinHeight = 76 });
        var measurements = WorkspaceColumn();
        measurements.AddChild(WorkspaceField("cmu-workspace-height-feet", HeightFeetEdit));
        measurements.AddChild(WorkspaceField("cmu-workspace-height-inches", HeightInchesEdit));
        measurements.AddChild(WorkspaceField("humanoid-profile-editor-weight-label", WeightEdit));
        _workspaceStorage.AddChild(measurements);
        var measure = CmuLobbyPresentation.Card(Loc.GetString("cmu-workspace-measurements"), Loc.GetString("cmu-workspace-edit-details"));
        measure.MinHeight = 76;
        measure.OnPressed += _ => OpenWorkspaceSheet(measurements, Loc.GetString("cmu-workspace-measurements"));
        facts.AddChild(measure);
        page.AddChild(facts);

        var recordCards = new CmuAdaptiveGrid(350) { HorizontalExpand = true, Visible = _allowCharacterDescription };
        foreach (var (editor, title, caption, color) in new[]
        {
            (FullDescriptionEdit, "humanoid-profile-editor-full-description-label", "cmu-workspace-description-caption", "#93ADC9"),
            (MedicalRecordEdit, "humanoid-profile-editor-medical-record-label", "cmu-workspace-medical-caption", "#93BBAE"),
            (CriminalRecordEdit, "humanoid-profile-editor-criminal-record-label", "cmu-workspace-criminal-caption", "#C5AA80"),
            (GeneralRecordEdit, "humanoid-profile-editor-general-record-label", "cmu-workspace-general-caption", "#B4A0C5"),
        })
        {
            var content = WorkspaceColumn();
            if (editor == FullDescriptionEdit)
            {
                content.AddChild(WorkspaceField("humanoid-profile-editor-short-examine-label", ShortExamineEdit));
                content.AddChild(WorkspaceField("humanoid-profile-editor-hide-meta-label", HideMetaInformationButton));
            }
            editor.MinHeight = 420;
            content.AddChild(new Label { Text = Loc.GetString(caption), FontColorOverride = CmuLobbyPresentation.Muted, ClipText = true });
            editor.Placeholder = new Rope.Leaf(Loc.GetString("cmu-workspace-record-placeholder"));
            content.AddChild(CmuLobbyPresentation.TextArea(editor));
            _workspaceStorage.AddChild(content);
            var card = CmuLobbyPresentation.Card(Loc.GetString(title), Loc.GetString(caption), Color.FromHex(color));
            card.OnPressed += _ => OpenWorkspaceSheet(content, Loc.GetString(title));
            var summary = new Label { ClipText = true, FontColorOverride = CmuLobbyPresentation.Text };
            card.GetChild(card.ChildCount - 1).AddChild(summary);
            card.MinHeight = 124;
            Rope.Node? previousText = null;
            void RefreshSummary()
            {
                if (ReferenceEquals(previousText, editor.TextRope)) return;
                previousText = editor.TextRope;
                var text = Rope.Collapse(editor.TextRope).Replace('\n', ' ');
                summary.Text = string.IsNullOrWhiteSpace(text) ? Loc.GetString("cmu-workspace-record-empty") : text;
                card.ToolTip = Loc.GetString(title) + "\n" + Loc.GetString(caption);
            }
            editor.OnTextChanged += _ => RefreshSummary();
            _refreshRecordSummaries.Add(RefreshSummary);
            RefreshSummary();
            recordCards.AddChild(card);
        }
        page.AddChild(recordCards);
        var gear = WorkspaceColumn();
        gear.AddChild(WorkspaceField("cmu-humanoid-profile-editor-armor-flavor-label", ArmorFlavorTextEdit));
        gear.AddChild(WorkspaceField("cmu-humanoid-profile-editor-helmet-flavor-label", HelmetFlavorTextEdit));
        _workspaceStorage.AddChild(gear);
        var extras = new CmuAdaptiveGrid(200) { HSeparationOverride = 8, VSeparationOverride = 8 };
        var gearButton = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-equipment-identity"));
        gearButton.Visible = _allowCharacterDescription;
        gearButton.OnPressed += _ => OpenWorkspaceSheet(gear, Loc.GetString("cmu-workspace-equipment-identity"));
        extras.AddChild(gearButton);
        var tools = CmuLobbyPresentation.Action(Loc.GetString("cmu-workspace-profile-tools"));
        tools.OnPressed += _ => OpenWorkspaceSheet(_profileTools, Loc.GetString("cmu-workspace-profile-tools"));
        extras.AddChild(tools);
        page.AddChild(extras);

        var deployment = new PanelContainer { PanelOverride = CmuLobbyPresentation.Box(CmuLobbyPresentation.FactionColor("govfor")) };
        var deploymentContent = WorkspaceColumn();
        deploymentContent.AddChild(CmuLobbyPresentation.Heading(Loc.GetString("cmu-workspace-deployment-settings")));
        MoveControl(_deploymentTools, deploymentContent);
        deployment.AddChild(deploymentContent);
        page.AddChild(deployment);

        _profileTools = WorkspaceColumn();
        MoveControl(IdentityHeader.GetChild(1), _profileTools);
        foreach (var button in new[] { ImportButton, ExportButton, ExportImageButton, OpenImagesButton })
        {
            MoveControl(button, _profileTools);
            CmuLobbyPresentation.Style(button);
            button.MinHeight = 44;
        }
        _workspaceStorage.AddChild(_profileTools);
    }
}
