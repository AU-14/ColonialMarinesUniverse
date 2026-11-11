using System.Numerics;
using Content.Shared.Atmos;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Raised for in-grid tiles about to couple to the map atmosphere
/// (isSpace floor tiles such as Lattice). Truly missing tiles (NoGridTile,
/// including punched holes) never raise this and stay space. A subscriber
/// may mark the tile sealed:
/// it then holds a normal mutable mixture instead of becoming a vacuum sink.
/// <see cref="Seed"/> optionally supplies the initial mixture as a clone
/// sized for the tile; when null the engine falls back to standard tile air.
/// </summary>
[ByRefEvent] // raised by-ref so handlers can write back Sealed/Seed
public sealed class CMUZOpeningSealCheckEvent
{
    public readonly EntityUid Grid;
    public readonly Vector2i Indices;

    public bool Sealed;
    public GasMixture? Seed;

    public CMUZOpeningSealCheckEvent(EntityUid grid, Vector2i indices)
    {
        Grid = grid;
        Indices = indices;
    }
}
