using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja;

// server -> client, tells the lobby whether a hunt is on this round and if this player readied up for it
[Serializable, NetSerializable]
public sealed class YautjaHuntLobbyStatusEvent(bool huntActive, bool huntReady, bool ancientSlotAllowed) : EntityEventArgs
{
    public readonly bool HuntActive = huntActive;
    public readonly bool HuntReady = huntReady;
    public readonly bool AncientSlotAllowed = ancientSlotAllowed;
}
