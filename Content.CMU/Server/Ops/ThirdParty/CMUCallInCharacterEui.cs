using Content.Server.EUI;
using Content.Shared.CMU14.Ops.ThirdParty;
using Content.Shared.Eui;

namespace Content.Server.CMU14.Ops.ThirdParty;

public sealed class CMUCallInCharacterEui(CMUCallInCharacterState state, TimeSpan deadline) : BaseEui
{
    public readonly CMUCallInCharacterState Offer = state;
    public readonly TimeSpan Deadline = deadline;
    public int? SelectedSlot { get; private set; }
    public bool Finished { get; private set; }

    // EuiManager also calls Closed directly when the session disconnects.
    public override void Closed() => Finished = true;

    public override EuiStateBase GetNewState() => Offer;

    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is CMUCallInCharacterChoice choice)
        {
            if (choice.Slot is { } slot && Offer.Characters.ContainsKey(slot))
                SelectedSlot = slot;
            Close();
            return;
        }

        base.HandleMessage(msg);
    }
}
