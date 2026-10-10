using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Private cabin fixtures use their mapped anchors when there is no ordinary tile wall.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMU3DVehicleCabinComponent : Component;
