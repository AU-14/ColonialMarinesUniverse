using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Item.HoldUp;

/// <summary>
///     Sent by the client when the player shift-middle-clicks one of their hand slots,
///     asking to emote holding up whatever is in that hand.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUHoldUpItemEvent(string handName) : EntityEventArgs
{
    public readonly string HandName = handName;
}
