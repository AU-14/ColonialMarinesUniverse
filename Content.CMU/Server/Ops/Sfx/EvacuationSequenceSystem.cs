using System.Linq;
using Content.Shared._CMU14.Ops.Sfx;
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
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;

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
        if (TryGetShipTiles(ev.Map, out var tiles))
        {
            foreach (var tile in PickSpread(tiles, MainWarheadSplits))
                _orbitalCannon.SpawnExplosion(SelfDestructWarhead, tile, CMUTopDownOrdnanceKind.Scuttle);
        }

        for (var i = 0; i < VolleyDelays.Length; i++)
        {
            var map = ev.Map;
            var delay = VolleyDelays[i];
            Timer.Spawn(TimeSpan.FromSeconds(delay), () => ScatterVolley(map));
        }
    }

    private bool TryGetShipTiles(EntityUid map, out List<EntityCoordinates> tiles)
    {
        tiles = new List<EntityCoordinates>();
        if (TerminatingOrDeleted(map))
            return false;

        var mapId = Transform(map).MapID;
        Entity<MapGridComponent>? ship = null;
        var bestArea = 0f;
        foreach (var grid in _map.GetAllGrids(mapId))
        {
            var aabb = grid.Comp.LocalAABB;
            var area = aabb.Width * aabb.Height;
            if (area <= bestArea)
                continue;

            bestArea = area;
            ship = grid;
        }

        if (ship is not { } target)
            return false;

        foreach (var tile in _map.GetAllTiles(target, target.Comp))
            tiles.Add(new EntityCoordinates(target, tile.GridIndices));

        return tiles.Count > 0;
    }

    private List<EntityCoordinates> PickSpread(List<EntityCoordinates> tiles, int count)
    {
        var picked = new List<EntityCoordinates>(count);
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
                    var d = (tile.Position - p.Position).LengthSquared();
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
        if (!TryGetShipTiles(map, out var tiles))
            return;

        foreach (var tile in PickSpread(tiles, WarheadsPerVolley))
            _orbitalCannon.SpawnExplosion(ScatterWarhead, tile, CMUTopDownOrdnanceKind.Scuttle);
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
