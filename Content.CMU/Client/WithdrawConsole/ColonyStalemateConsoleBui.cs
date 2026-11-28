using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared.CMU14.WithdrawConsole;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.WithdrawConsole;

public sealed class ColonyStalemateConsoleBui(EntityUid owner, Enum key) : BoundUserInterface(owner, key)
{
    private ConfirmButton? _vote;

    protected override void Open()
    {
        base.Open();
        var window = this.CreateWindow<DefaultWindow>();
        window.Title = Loc.GetString("cmu-colony-stalemate-title");
        window.MinWidth = 360;

        var kit = new CrtConsoleKit();
        kit.Root.AddChild(CrtConsoleKit.Wrapping(Loc.GetString("cmu-colony-stalemate-help"), CrtTerminalPalette.Text));
        _vote = new ConfirmButton { Text = Loc.GetString("cmu-colony-stalemate-propose"), Disabled = true };
        CmuButtonStyles.Apply(_vote, CmuButtonStyles.Variant.Neutral);
        _vote.OnPressed += _ => SendPredictedMessage(new WithdrawConsoleToggleStalemateMsg());
        kit.Root.AddChild(_vote);
        window.Contents.AddChild(kit.Tube);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_vote == null || state is not WithdrawConsoleBuiState console)
            return;

        _vote.Disabled = !console.IsUnlocked || console.RoundEndTriggered;
        _vote.Text = Loc.GetString(console.StalemateToggled ? "withdraw-console-ui-stalemate-cancel" : "cmu-colony-stalemate-propose");
    }
}
