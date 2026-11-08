using System.Numerics;
using Content.Server._RMC14.NPC.HTN;
using Content.Server._RMC14.Spawners;
using Content.Server.CMU14.Dropship.MultiDeck;
using Content.Server.CMU14.Fob;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.CMU14.Dropship;

/// <summary>Marks a gauss turret orbital-dropped onto the first Distress Signal landing zone.</summary>
[RegisterComponent]
public sealed partial class CMULandingZoneTurretComponent : Component;

/// <summary>
/// The first friendly Distress Signal landing orbital-drops a capped battery of gauss turrets around the LZ.
/// They hold the LZ for 15 minutes after the landing.
/// </summary>
public sealed partial class CMULandingZoneTurretDropSystem : EntitySystem
{
    [Dependency] private SharedDropshipSystem _dropships = default!;
    [Dependency] private GunIFFSystem _gunIFF = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MultiDeckDropshipSystem _multiDeck = default!;
    [Dependency] private SharedRMCOrbitalDeployerSystem _orbital = default!;
    [Dependency] private SharedSentryTargetingSystem _sentry = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public static readonly EntProtoId TurretPrototype = "RMCTurretGaussSpaceborne";
    public const int MaxTurrets = 6;
    public static readonly TimeSpan TurretLifetime = TimeSpan.FromMinutes(15);

    private const float ExistingTurretRange = 20f;
    private const float HullPadding = 2f;
    private const float MaxPlacementDistance = 18f;
    private const float MinTurretSpacing = 3f;
    private const float SkyFallDuration = 5f;

    private const CollisionGroup BlockMask = CollisionGroup.Impassable |
                                             CollisionGroup.MidImpassable |
                                             CollisionGroup.HighImpassable;

    private bool _dropped;

    public override void Initialize()
    {
        // Runs after the FOB pilot so legacy turrets it purges are not counted against the cap.
        SubscribeLocalEvent<DropshipLandedOnPlanetEvent>(OnLanded, after: [typeof(CMUFobMapSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _dropped = false);
    }

    private void OnLanded(ref DropshipLandedOnPlanetEvent args)
    {
        if (_dropped || _ticker.CurrentPreset?.ID != "DistressSignal")
            return;

        var ship = args.Dropship;
        if (ship.Comp.Crashed || ship.Comp.HijackLandAt != null || ship.Comp.IsHumanHijack ||
            ship.Comp.Destination is not { } destination || TerminatingOrDeleted(destination))
        {
            return;
        }

        var destinationXform = Transform(destination);
        if (destinationXform.GridUid is not { } groundGrid ||
            !TryComp(groundGrid, out MapGridComponent? grid) ||
            destinationXform.MapUid is not { } groundMap ||
            Transform(ship).MapUid is not { } shipMap ||
            _multiDeck.GetGroundMap(ship, shipMap) != groundMap)
        {
            return;
        }

        _dropped = true;

        var center = _transform.GetWorldPosition(destinationXform);
        var count = MaxTurrets - CountExistingTurrets(groundMap, center);
        if (count <= 0)
            return;

        var hull = TryComp(ship, out MapGridComponent? shipGrid)
            ? _transform.GetWorldMatrix(ship).TransformBox(shipGrid.LocalAABB).Enlarged(HullPadding)
            : new Box2(center, center);

        _dropships.TryGetGridFaction(ship, out var faction);
        var marineFaction = GetMarineFaction();

        var mapId = Transform(groundMap).MapID;
        foreach (var position in FindPlacements((groundGrid, grid), center, hull, count))
        {
            var turret = Spawn(TurretPrototype, MapCoordinates.Nullspace);

            // Purged by the FOB pilot and given an unrelated landing timer; this battery has its own lifetime.
            RemComp<CMUFobLegacyDefenseComponent>(turret);
            RemComp<TimedDespawnOnLandingComponent>(turret);
            RemComp<WakeNPCOnDropshipLandingComponent>(turret);
            EnsureComp<CMULandingZoneTurretComponent>(turret);
            EnsureComp<TimedDespawnComponent>(turret).Lifetime = (float) TurretLifetime.TotalSeconds;

            _gunIFF.SetUserFaction(turret, marineFaction);
            if (faction != null)
                _sentry.TryApplyDefaultFaction(turret, faction);

            // The supply-drop sequence shows the incoming-airdrop marker until the pod lands and opens.
            _orbital.DoOrbitalDeploy(turret, new MapCoordinates(position, mapId), SkyFallDuration);
        }
    }

    private int CountExistingTurrets(EntityUid map, Vector2 center)
    {
        var existing = 0;
        var query = EntityQueryEnumerator<SentryTargetingComponent, MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var meta, out var xform))
        {
            if (xform.MapUid != map ||
                meta.EntityPrototype?.ID != TurretPrototype.Id ||
                EntityManager.IsQueuedForDeletion(uid) ||
                Vector2.Distance(_transform.GetWorldPosition(xform), center) > ExistingTurretRange)
            {
                continue;
            }

            existing++;
        }

        return existing;
    }

    private EntProtoId<IFFFactionComponent> GetMarineFaction()
    {
        var rules = EntityQueryEnumerator<CMDistressSignalRuleComponent, ActiveGameRuleComponent>();
        return rules.MoveNext(out var rule, out _) ? rule.MarineFaction : "FactionMarine";
    }

    private List<Vector2> FindPlacements(Entity<MapGridComponent> grid, Vector2 center, Box2 hull, int count)
    {
        var placements = new List<Vector2>(count);
        var used = new HashSet<Vector2i>();

        // March outward along evenly spread bearings and take the first open tile clear of the hull.
        for (var i = 0; i < count; i++)
        {
            var direction = Angle.FromDegrees(45 + 360.0 * i / count).ToVec();
            for (var distance = 1f; distance <= MaxPlacementDistance; distance += 0.5f)
            {
                var point = center + direction * distance;
                if (hull.Contains(point))
                    continue;

                var tile = _map.WorldToTile(grid, grid.Comp, point);
                if (!used.Add(tile))
                    continue;

                if (!_map.TryGetTileRef(grid, grid.Comp, tile, out var tileRef) ||
                    tileRef.Tile.IsEmpty ||
                    _turf.IsTileBlocked(tileRef, BlockMask))
                {
                    continue;
                }

                var tileCenter = _transform.ToMapCoordinates(
                    new EntityCoordinates(grid, _map.TileCenterToVector(grid, grid.Comp, tile))).Position;
                if (placements.Exists(other => Vector2.Distance(other, tileCenter) < MinTurretSpacing))
                    continue;

                placements.Add(tileCenter);
                break;
            }
        }

        return placements;
    }
}
