using Content.Shared.CCVar;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Map;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Doors.Components;
using Content.Shared.Maps;
using Content.Shared.Tag;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Atmos;

/// <summary>
/// Drives tile fire creep: fires periodically ignite an adjacent tile, so a burn creeps
/// outward from where it was sprayed instead of sitting still until it decays. Spread
/// never overwrites an existing fire and stops at walls, structures, and
/// <see cref="BlockTileFireComponent"/> blockers.
///
/// Depth comes from <see cref="CMUSpreadingFireComponent"/> when the prototype carries it
/// (the arsonist phoron fire fixes its own), otherwise from
/// <see cref="CCVars.CMUFireSpreadDepth"/> so every ordinary fire creeps with a
/// server-tunable range. 0 on the cvar leaves plain fires still.
/// </summary>
public sealed partial class CMUSpreadingFireSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly RMCMapSystem _rmcMap = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    private static readonly ProtoId<TagPrototype> StructureTag = "Structure";
    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    private static readonly Direction[] Directions =
    [
        Direction.North,
        Direction.East,
        Direction.South,
        Direction.West,
    ];

    private EntityQuery<BlockTileFireComponent> _blockQuery;
    private EntityQuery<DoorComponent> _doorQuery;
    private EntityQuery<TileFireComponent> _tileFireQuery;

    public override void Initialize()
    {
        _blockQuery = GetEntityQuery<BlockTileFireComponent>();
        _doorQuery = GetEntityQuery<DoorComponent>();
        _tileFireQuery = GetEntityQuery<TileFireComponent>();

        SubscribeLocalEvent<CMUSpreadingFireComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CMUSpreadingFireComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpread = _timing.CurTime + ent.Comp.SpreadEvery;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var depth = _config.GetCVar(CCVars.CMUFireSpreadDepth);
        var query = EntityQueryEnumerator<TileFireComponent>();
        while (query.MoveNext(out var uid, out var fire))
        {
            if (!TryComp(uid, out CMUSpreadingFireComponent? spread))
            {
                if (depth <= 0)
                    continue;

                spread = EnsureComp<CMUSpreadingFireComponent>(uid);
                spread.Depth = depth;
                spread.NextSpread = time + spread.SpreadEvery;
                continue;
            }

            if (time < spread.NextSpread)
                continue;

            spread.NextSpread = time + spread.SpreadEvery;

            // Children get their NextSpread set at spawn without a MapInitEvent for this
            // component, so both spawned-from-prototype and spread children meet here.
            if (spread.Depth <= 1 || fire.Id is not { } spawn)
                continue;

            var coordinates = Transform(uid).Coordinates;
            var start = _random.Next(0, Directions.Length);
            for (var i = 0; i < Directions.Length; i++)
            {
                var target = coordinates.Offset(Directions[(start + i) % Directions.Length].ToVec());
                if (!CanBurn(target))
                    continue;

                var child = Spawn(spawn, target);
                var childSpread = EnsureComp<CMUSpreadingFireComponent>(child);
                childSpread.Depth = spread.Depth - 1;
                childSpread.NextSpread = time + childSpread.SpreadEvery;
                break;
            }
        }
    }

    private bool CanBurn(EntityCoordinates target)
    {
        if (!_zLevels.TryProjectToGround(target, out target))
            return false;

        if (!_rmcMap.TryGetTileDef(target, out var tile) ||
            tile.ID == ContentTileDefinition.SpaceID)
            return false;

        var anchored = _rmcMap.GetAnchoredEntitiesEnumerator(target);
        while (anchored.MoveNext(out var ent))
        {
            if (_tileFireQuery.HasComp(ent)
                || _blockQuery.HasComp(ent))
                return false;

            if (_tag.HasAnyTag(ent, StructureTag, WallTag)
                && !_doorQuery.HasComp(ent))
                return false;
        }

        return true;
    }
}
