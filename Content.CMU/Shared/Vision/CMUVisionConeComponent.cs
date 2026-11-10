using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Vision;

/// <summary>
/// Overrides the vision cone of the entity that has it. Entities without this component use the defaults below.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVisionConeComponent : Component
{
    /// <summary>
    /// Total width of the cone in degrees. 360 sees everything around.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Angle = 120f;

    /// <summary>
    /// Mobs closer than this many tiles are always seen, regardless of facing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Radius = 1.5f;
}
