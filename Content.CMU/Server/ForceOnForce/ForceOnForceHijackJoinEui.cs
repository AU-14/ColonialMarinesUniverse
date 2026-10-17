using Content.Server.EUI;
using Content.Shared.CMU14.ForceOnForce;
using Content.Shared.Eui;

namespace Content.Server.CMU14.ForceOnForce;

public sealed class ForceOnForceHijackJoinEui(ForceOnForceHijackJoinSystem system, bool attacking) : BaseEui
{
    public override void Opened() => StateDirty();

    public override EuiStateBase GetNewState() => new ForceOnForceHijackJoinState(attacking);

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is ForceOnForceHijackJoinMessage choice)
        {
            system.Respond(this, choice.Join);
            Close();
            return;
        }

        base.HandleMessage(msg);
    }

    public override void Closed() => system.Forget(this);
}
