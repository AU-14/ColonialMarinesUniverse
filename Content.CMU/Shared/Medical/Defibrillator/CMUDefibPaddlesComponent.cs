using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Defibrillator;

/// <summary>
/// A defibrillator whose paddles sit docked on the unit and only come out while it's charged for a shock.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUDefibPaddlesComponent : Component;

[Serializable, NetSerializable]
public enum CMUDefibVisuals : byte
{
    PaddlesOut,
}
