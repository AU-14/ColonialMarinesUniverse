using Robust.Shared.Map;

namespace Content.Server._CMU14.ZLevels.Core;

/// <summary>
/// Server-local tile cache used to avoid repeating fall activation probes while an entity remains on one tile.
/// </summary>
[RegisterComponent, Access(typeof(CMUZLevelsSystem))]
public sealed partial class CMUZPhysicsActivationCacheComponent : Component
{
    public EntityUid Map = EntityUid.Invalid;
    public Vector2i Tile;
}
