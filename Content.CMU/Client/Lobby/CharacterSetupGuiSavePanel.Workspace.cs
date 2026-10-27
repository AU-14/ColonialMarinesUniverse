using System.Numerics;
using Content.Client.CMU14.Lobby;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterSetupGuiSavePanel
{
    private void InitializeWorkspaceDialog()
    {
        Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetNano;
        var body = (BoxContainer) SaveButton.Parent!;
        body.Orientation = BoxContainer.LayoutOrientation.Vertical;
        body.SeparationOverride = 16;
        body.Margin = new Thickness(16);
        var description = new RichTextLabel { HorizontalExpand = true };
        description.SetMessage(Loc.GetString("cmu-workspace-save-prompt"), CmuLobbyPresentation.Text);
        var actions = new CmuAdaptiveGrid(130) { HorizontalExpand = true, HSeparationOverride = 8, VSeparationOverride = 8 };
        foreach (var (button, accent) in new[]
        {
            (SaveButton, Color.FromHex("#5CE09D")),
            (NoSaveButton, Color.FromHex("#FFB94F")),
            (CancelButton, CmuLobbyPresentation.Muted),
        })
        {
            button.Orphan();
            button.RemoveStyleClass("ButtonBig");
            button.MinHeight = 44;
            CmuLobbyPresentation.Style(button, accent, button == SaveButton);
            actions.AddChild(button);
        }
        body.AddChild(description);
        body.AddChild(actions);
        SetSize = new Vector2(470, 165);
        CrtLobbyTheme.ApplyWindow(this);
    }
}
