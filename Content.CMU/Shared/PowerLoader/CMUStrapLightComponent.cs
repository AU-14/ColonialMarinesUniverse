using System.Numerics;

namespace Content.Shared.CMU14.PowerLoader;

/// <summary>
/// Turns the entity's point light on while something is strapped to it, like the power loader's beacon.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUStrapLightSystem))]
public sealed partial class CMUStrapLightComponent : Component
{
    /// <summary>
    /// Where the light sits on the sprite for each on-screen direction, in tiles from the entity's center.
    /// Applied on the client so the light stays on the drawn beacon however the entity is rotated.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> Offsets = new();
}
