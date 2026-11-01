using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

// server -> client, tells the lobby whether a hunt is on this round and if this player readied up for it.
// StrandedOpen is a free stranded slot, which can happen with no hunt at all
[Serializable, NetSerializable]
public sealed class YautjaHuntLobbyStatusEvent(bool huntActive, bool huntReady, bool ancientSlotAllowed, bool huntWhitelisted, bool strandedOpen) : EntityEventArgs
{
    public readonly bool HuntWhitelisted = huntWhitelisted;
    public readonly bool HuntActive = huntActive;
    public readonly bool HuntReady = huntReady;
    public readonly bool AncientSlotAllowed = ancientSlotAllowed;
    public readonly bool StrandedOpen = strandedOpen;
}
