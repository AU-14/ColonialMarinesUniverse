using Content.Client.Eui;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Eui;
using JetBrains.Annotations;

namespace Content.Client.CMU14.ForceOnForce;

[UsedImplicitly]
public sealed class ForceOnForceHijackJoinEui : BaseEui
{
    private readonly ForceOnForceHijackJoinWindow _window = new();
    private bool _answered;

    public ForceOnForceHijackJoinEui()
    {
        _window.JoinButton.OnPressed += _ => Respond(true);
        _window.StayButton.OnPressed += _ => Respond(false);
        _window.OnClose += () => Respond(false);
    }

    private void Respond(bool join)
    {
        if (_answered)
            return;
        _answered = true;
        SendMessage(new ForceOnForceHijackJoinMessage(join));
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not ForceOnForceHijackJoinState offer)
            return;
        _window.Message.SetMessage(Loc.GetString(offer.Attacking
            ? "cmu-fof-hijack-join-attacking"
            : "cmu-fof-hijack-join-defending"));
        _window.OpenCentered();
    }

    public override void Closed()
    {
        _answered = true;
        _window.Close();
        _window.Dispose();
    }
}
