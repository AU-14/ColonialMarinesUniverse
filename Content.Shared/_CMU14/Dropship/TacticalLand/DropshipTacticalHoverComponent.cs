using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Dropship.TacticalLand;

[RegisterComponent]
public sealed partial class DropshipTacticalHoverComponent : Component
{
    [DataField]
    public EntityUid? HoverDestination;

    [DataField]
    public EntityUid? GroundMap;

    [DataField]
    public Vector2i Footprint = new(9, 17);

    [DataField]
    public int GroundMapOffset = -1;

    /// <summary>
    /// World-space velocity accumulated by gunship flight controls. Tactical
    /// hover deliberately applies no passive damping, giving it weightless momentum.
    /// </summary>
    public Vector2 GunshipLinearVelocity;

    /// <summary>
    /// Angular velocity accumulated by gunship flight controls, in degrees per second.
    /// Like linear velocity, tactical hover applies no passive damping.
    /// </summary>
    public float GunshipAngularVelocityDegrees;

    /// <summary>
    /// Unconsumed server time retained by the fixed-step flight simulation.
    /// </summary>
    public float GunshipFlightSimulationAccumulator;

    /// <summary>
    /// Static local-space centers of every occupied dropship tile. Building
    /// this once avoids enumerating the grid for every movement and alarm
    /// check while the ship is hovering.
    /// </summary>
    public readonly List<Vector2> CachedFootprintCenters = new();

    /// <summary>
    /// Occupied grid indices matching <see cref="CachedFootprintCenters"/>.
    /// Used by collision fallbacks to test local points without walking every
    /// footprint center for each candidate.
    /// </summary>
    public readonly HashSet<Vector2i> CachedFootprintTiles = new();

    /// <summary>
    /// Local-space centers of only the exterior footprint tiles, used by the
    /// proximity alarm.
    /// </summary>
    public readonly List<Vector2> CachedFootprintBoundaryCenters = new();

    /// <summary>
    /// Footprint offsets rotated into the current candidate orientation. Both
    /// rotation and translation validation can reuse these during one tick.
    /// </summary>
    public readonly List<Vector2> CachedRotatedFootprintCenters = new();
    public Angle CachedFootprintRotation;
    public bool HasCachedFootprintRotation;

    public readonly List<Vector2> CachedRotatedFootprintBoundaryCenters = new();
    public Angle CachedFootprintBoundaryRotation;
    public bool HasCachedFootprintBoundaryRotation;

    /// <summary>
    /// Reused by collision validation. Callers consume it synchronously before
    /// another footprint query is made.
    /// </summary>
    public readonly HashSet<EntityUid> CollisionBlockers = new();

    /// <summary>
    /// Direct children which belonged to the dropship when free-flight movement
    /// began. Anchored terrain entities can otherwise be adopted by the moving
    /// grid even when their fixtures are not part of the dropship obstruction
    /// mask (platform edges are the common case).
    /// </summary>
    public readonly HashSet<EntityUid> FlightGridChildren = new();
    public bool FlightGridChildrenInitialized;

    /// <summary>
    /// Exact terrain-grid poses captured immediately before a flight transform.
    /// Moving grids can temporarily adopt overlapping anchored entities; using
    /// their position after that happens makes each correction carry them along.
    /// </summary>
    public readonly Dictionary<EntityUid, DropshipTerrainAnchorPose> FlightTerrainAnchors = new();

    /// <summary>
    /// Scratch collection for fixture-overlap queries along the swept hull.
    /// Unlike snap-grid lookup, this also finds edge fixtures anchored in an
    /// adjacent tile.
    /// </summary>
    public readonly HashSet<EntityUid> FlightTerrainCandidates = new();

    /// <summary>
    /// Broadphase candidates reduced to entities with hard blocking fixtures.
    /// Reusing this set prevents fixture inspection for every sampled hull tile.
    /// </summary>
    public readonly HashSet<EntityUid> FlightHardTerrainCandidates = new();

    /// <summary>
    /// Last broadphase-query count for one collision probe. Retained for
    /// diagnostics and enforced by <see cref="GunshipSpatialQueryBudget"/>.
    /// </summary>
    public int LastFlightCollisionSpatialQueries;

    public bool HoverEffectsPoseInitialized;
    public EntityUid? HoverEffectsMap;
    public Vector2 HoverEffectsPosition;
    public Angle HoverEffectsRotation;
    public int HoverEffectsGroundOffset;
    public TimeSpan NextHoverEffectsUpdate;

    /// <summary>
    /// Pending five-second vertical movement. The dropship remains on its
    /// current level until this time is reached.
    /// </summary>
    public TimeSpan? AltitudeTransitionAt;

    public EntityUid? AltitudeTargetMap;

    public int AltitudeOffset;

    public bool AltitudeLanding;

    public EntityUid? AltitudePilot;

    [DataField]
    public EntityUid? Shadow;

    [DataField]
    public List<EntityUid> Downwashes = new();

    [DataField]
    public EntProtoId ShadowPrototype = "CMUDropshipTacticalHoverShadow";

    [DataField]
    public EntProtoId DownwashPrototype = "CMUDropshipTacticalHoverDownwash";
}

public readonly record struct DropshipTerrainAnchorPose(Vector2 Position, Angle Rotation);

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipTacticalHoverShadowComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public Vector2i Footprint = new(9, 17);

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DropshipTacticalHoverDownwashComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Dropship;

    [DataField, AutoNetworkedField]
    public Vector2 Offset;

    [DataField, AutoNetworkedField]
    public int ProjectedMapOffset = -1;
}

/// <summary>
/// Raised after a dropship leaves tactical hover so hover-only equipment can clean itself up.
/// </summary>
[ByRefEvent]
public readonly record struct DropshipTacticalHoverEndedEvent(EntityUid Dropship);
