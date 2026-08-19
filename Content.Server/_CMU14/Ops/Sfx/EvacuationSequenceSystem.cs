using System.Linq;
using Content.Shared._CMU14.Ops.Sfx;
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

    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructSequence = "SelfDestructSequence";
    private static readonly ProtoId<ScriptedSoundSequencePrototype> SelfDestructEngineSequence = "SelfDestructEngineSequence";
    private static readonly EntProtoId SelfDestructWarhead = "CMUSelfDestructWarheadExplosion";
    private static readonly EntProtoId ScatterWarhead = "CMUSelfDestructScatterExplosion";

    private static readonly int[] VolleyDelays = [15, 27, 37];
    private const int WarheadsPerVolley = 3;
    private const float EdgePickChance = 0.6f;
    private const float EdgeMargin = 8f;

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
        if (TryGetShipTiles(ev.Map, out var tiles, out _))
            _orbitalCannon.SpawnExplosion(SelfDestructWarhead, _random.Pick(tiles));

        for (var i = 0; i < VolleyDelays.Length; i++)
        {
            var map = ev.Map;
            var delay = VolleyDelays[i];
            Timer.Spawn(TimeSpan.FromSeconds(delay), () => ScatterVolley(map));
        }
    }

    private bool TryGetShipTiles(EntityUid map, out List<EntityCoordinates> tiles, out List<EntityCoordinates> edgeTiles)
    {
        tiles = new List<EntityCoordinates>();
        edgeTiles = new List<EntityCoordinates>();
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

        var bounds = target.Comp.LocalAABB;
        foreach (var tile in _map.GetAllTiles(target, target.Comp))
        {
            var coords = new EntityCoordinates(target, tile.GridIndices);
            tiles.Add(coords);

            var p = tile.GridIndices;
            if (bounds.Width > EdgeMargin * 2 && bounds.Height > EdgeMargin * 2
                && (bounds.Left + EdgeMargin > p.X || p.X > bounds.Right - EdgeMargin
                || bounds.Bottom + EdgeMargin > p.Y || p.Y > bounds.Top - EdgeMargin))
            {
                edgeTiles.Add(coords);
            }
        }

        return tiles.Count > 0;
    }

    private void ScatterVolley(EntityUid map)
    {
        if (!TryGetShipTiles(map, out var tiles, out var edgeTiles))
            return;

        for (var i = 0; i < WarheadsPerVolley; i++)
        {
            var pool = edgeTiles.Count > 0 && _random.Prob(EdgePickChance) ? edgeTiles : tiles;
            _orbitalCannon.SpawnExplosion(ScatterWarhead, _random.Pick(pool));
        }
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
