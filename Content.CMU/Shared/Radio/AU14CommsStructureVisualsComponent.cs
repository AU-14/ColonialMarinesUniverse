using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Radio;

// swaps a comms structure's sprite to its damaged state once it has taken enough
// damage, well before the destruction threshold
[RegisterComponent]
public sealed partial class AU14CommsStructureVisualsComponent : Component
{
    [DataField]
    public FixedPoint2 DamagedAt = 250;
}

[Serializable, NetSerializable]
public enum AU14CommsStructureVisuals : byte
{
    Damaged,
}
