using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Engine airtightness only covers the tile an entity is anchored on, so multi-tile doors
/// leak through every tile they span but do not sit on. Spawns an invisible airtight entity
/// on each offset tile to seal it. See <see cref="CMUWideAirtightSystem"/>.
/// </summary>
[RegisterComponent]
public sealed partial class CMUWideAirtightComponent : Component
{
    /// <summary>Tiles to seal besides the door's own, in door-local space, rotated with it.</summary>
    [DataField]
    public List<Vector2i> Offsets = new();

    [DataField]
    public EntProtoId Companion = "CMUAirtightSeal";

    /// <summary>Spawned seals, tracked so they die with the door instead of lingering on the grid.</summary>
    public List<EntityUid> Seals = new();
}
