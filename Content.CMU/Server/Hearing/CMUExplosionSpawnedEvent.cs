using Robust.Shared.Map;

namespace Content.Server.CMU14.Hearing;

/// <summary>
///     Raised broadcast when an explosion goes off. Radius is the explosion's size in tiles.
/// </summary>
[ByRefEvent]
public readonly record struct CMUExplosionSpawnedEvent(MapCoordinates Epicenter, int Radius);
