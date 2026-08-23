using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Ops.Sfx;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._CMU14.ZLevels.Ordnance;
using Content.Shared._RMC14.Evacuation;
using Content.Shared._RMC14.OrbitalCannon;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Ops.Sfx;

public sealed partial class EvacuationSequenceSystem : EntitySystem
{
    [Dependency] private OrbitalCannonSystem _orbitalCannon = default!;
    [Dependency] private ScriptedSoundSystem _scriptedSound = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CMUSharedZLevelsSystem _zLevels = default!;

    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructSequence = "SelfDestructSequence";
    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructEngineSequence = "SelfDestructEngineSequence";
    private static readonly EntProtoId SelfDestructWarhead = "CMUSelfDestructWarheadExplosion";
    private static readonly EntProtoId ScatterWarhead = "CMUSelfDestructScatterExplosion";

    private static readonly int[] VolleyDelays = [37, 52, 65];
    private const int WarheadsPerVolley = 3;
    private const int MainWarheadSplits = 2;

    public override void Initialize()
    {
        SubscribeLocalEvent<EvacuationEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<EvacuationDisabledEvent>(OnDisabled);
        SubscribeLocalEvent<ShipSelfDestructEvent>(OnSelfDestruct);
        Subs.CVar(_cfg, CCVars.EnableEvacSfx, OnCVarChanged);
    }

    private void OnEnabled(ref EvacuationEnabledEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.EnableEvacSfx)) return;

        if (TryComp<EvacuationProgressComponent>(ev.Map, out var progress) && progress.DropShipCrashed)
            return;

        if (!_scriptedSound.TryGetActiveSequence(SelfDestructSequence, ev.Map, out _))
            _scriptedSound.StartSequence(SelfDestructSequence, ev.Map);

        if (!_scriptedSound.TryGetActiveSequence(SelfDestructEngineSequence, ev.Map, out _))
            _scriptedSound.StartSequence(SelfDestructEngineSequence, ev.Map);
    }

    private void OnDisabled(ref EvacuationDisabledEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.EnableEvacSfx)) return;

        if (_scriptedSound.TryGetActiveSequence(SelfDestructSequence, ev.Map, out var seq))
            _scriptedSound.StopSequence(seq);

        if (_scriptedSound.TryGetActiveSequence(SelfDestructEngineSequence, ev.Map, out var engineSeq))
            _scriptedSound.StopSequence(engineSeq);
    }

    private void OnSelfDestruct(ref ShipSelfDestructEvent ev)
    {
        if (TryGetShipWorldTiles(ev.Map, out var tiles))
        {
            foreach (var pos in PickSpread(tiles, MainWarheadSplits))
                _orbitalCannon.SpawnExplosion(SelfDestructWarhead, new EntityCoordinates(ev.Map, pos), CMUTopDownOrdnanceKind.Scuttle);
        }

        for (var i = 0; i < VolleyDelays.Length; i++)
        {
            var map = ev.Map;
            var delay = VolleyDelays[i];
            Timer.Spawn(TimeSpan.FromSeconds(delay), () => ScatterVolley(map));
        }
    }

    private bool TryGetShipWorldTiles(EntityUid map, out List<Vector2> tiles)
    {
        tiles = new List<Vector2>();
        if (TerminatingOrDeleted(map))
            return false;

        var seen = new HashSet<Vector2i>();
        foreach (var networkMap in _zLevels.GetAllNetworkMaps(map))
        {
            if (TerminatingOrDeleted(networkMap))
                continue;

            Entity<MapGridComponent>? ship = null;
            var bestArea = 0f;
            foreach (var grid in _map.GetAllGrids(Transform(networkMap).MapID))
            {
                var aabb = grid.Comp.LocalAABB;
                var area = aabb.Width * aabb.Height;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                ship = grid;
            }

            if (ship is not { } target)
                continue;

            foreach (var tile in _map.GetAllTiles(target, target.Comp))
            {
                var world = _map.GridTileToWorldPos(target, target.Comp, tile.GridIndices);
                if (seen.Add(new Vector2i((int) world.X, (int) world.Y)))
                    tiles.Add(world);
            }
        }

        return tiles.Count > 0;
    }

    private List<Vector2> PickSpread(List<Vector2> tiles, int count)
    {
        var picked = new List<Vector2>(count);
        for (var i = 0; i < count && tiles.Count > 0; i++)
        {
            if (picked.Count == 0)
            {
                picked.Add(_random.Pick(tiles));
                continue;
            }

            var best = tiles[0];
            var bestScore = -1f;
            foreach (var tile in tiles)
            {
                var score = float.MaxValue;
                foreach (var p in picked)
                {
                    var d = (tile - p).LengthSquared();
                    if (d < score)
                        score = d;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = tile;
                }
            }

            picked.Add(best);
        }

        return picked;
    }

    private void ScatterVolley(EntityUid map)
    {
        if (!TryGetShipWorldTiles(map, out var tiles))
            return;

        foreach (var pos in PickSpread(tiles, WarheadsPerVolley))
            _orbitalCannon.SpawnExplosion(ScatterWarhead, new EntityCoordinates(map, pos), CMUTopDownOrdnanceKind.Scuttle);
    }

    private void OnCVarChanged(bool enabled)
    {
        if (enabled) return;
        foreach (var (uid, comp) in _scriptedSound.GetActiveSequences().ToList())
        {
            if (comp.SequenceId == SelfDestructSequence || comp.SequenceId == SelfDestructEngineSequence)
                _scriptedSound.StopSequence(uid);
        }
    }
}
