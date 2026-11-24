using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Camera;

/// <summary>
/// Marks a camera as having broadcasting-dependent visuals.
/// </summary>
[RegisterComponent]
public sealed partial class CMUBroadcastVisualsComponent : Component;

[Serializable, NetSerializable]
public enum CMUBroadcastVisuals : byte
{
    Broadcasting,
}
