using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using System;

namespace Content.Shared.CMU14.Hygiene;

[RegisterComponent, NetworkedComponent]
public sealed partial class CMUFourUseSoapComponent : Component
{
    [DataField] public int UsesRemaining = 4;
    [DataField] public int BitesTowardsUse;
    [DataField] public FixedPoint2 FloorPickupAmount = FixedPoint2.New(120);
}
[Serializable, NetSerializable]
public enum CMUSoapVisuals : byte { Uses, Layer }
