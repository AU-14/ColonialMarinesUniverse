using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.CMU14.ZLevels.Core;

/// <summary>A real tile aperture. Grid and Tile identify it; Center and Distance are query results.</summary>
public readonly record struct CMUZOpeningPortal(EntityUid Grid, Vector2i Tile, Vector2 Center, float Distance);
