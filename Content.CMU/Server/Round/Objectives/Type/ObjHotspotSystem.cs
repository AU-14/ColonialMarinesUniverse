using System.Linq;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjHotspotSystem : ObjectiveSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedTacticalMapSystem _tacMap = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-hotspot");
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveActivatedEvent args)
    {
        if (TryComp(uid, out TransformComponent? xform))
        {
            var halfHeight = comp.HalfHeight == 0 ? comp.HalfWidth : comp.HalfHeight;
            _tacMap.DrawTacticalMapRectangle(Color.FromHex(comp.ZoneColorHex), _transform.GetWorldPosition(xform), comp.HalfWidth, halfHeight);
        }
    }

    private void OnReset(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CurrentController = string.Empty;
        comp.TicksScored = 0;
        Dirty(uid, comp);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<HotspotObjectiveComponent, CMUObjectiveComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var objComp, out var xform))
        {
            if (!objComp.Active)
                continue;

            comp.TickAccumulator += frameTime;
            if (comp.TickAccumulator < comp.TickSeconds)
                continue;

            comp.TickAccumulator = 0;
            Tick(uid, comp, objComp, xform);
        }
    }

    private void Tick(EntityUid uid, HotspotObjectiveComponent comp, CMUObjectiveComponent objComp, TransformComponent xform)
    {
        var mapId = ObjCtrl.GetPlanetMapId();
        if (mapId is not { } planet || planet == MapId.Nullspace)
            return;

        var planetMaps = _zLevels.GetAllNetworkMapIds(planet);
        var center = _transform.GetWorldPosition(xform);
        var halfWidth = comp.HalfWidth;
        var halfHeight = comp.HalfHeight == 0 ? comp.HalfWidth : comp.HalfHeight;

        var competing = objComp.Factions.Select(f => f.ToLowerInvariant()).ToHashSet();

        var presence = new Dictionary<string, int>();
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var factions, out var mobState, out var xformComp))
        {
            if (mobState.CurrentState != MobState.Alive || !planetMaps.Contains(xformComp.MapID))
                continue;

            var pos = _transform.GetWorldPosition(xformComp);
            if (pos.X < center.X - halfWidth || pos.X > center.X + halfWidth
                || pos.Y < center.Y - halfHeight || pos.Y > center.Y + halfHeight)
                continue;

            foreach (var faction in factions.Factions)
            {
                var key = faction.ToString().ToLowerInvariant() switch
                {
                    "auweyu" => "weyu",
                    var id => id,
                };
                if (!competing.Contains(key))
                    continue;

                presence.TryAdd(key, 0);
                presence[key]++;
                break;
            }
        }

        if (presence.Count == 0)
            return;

        var strongest = presence.MaxBy(kvp => kvp.Value);
        var best = strongest.Value;
        if (presence.Values.Count(v => v == best) > 1)
            return; // contested, nobody scores

        var controller = strongest.Key;
        if (comp.CurrentController != controller)
        {
            comp.CurrentController = controller;
            Dirty(uid, comp);
        }

        var points = comp.PointsPerTick;
        if (comp.NormalizeByTeamSize)
        {
            var alive = CountAliveByFaction(competing);
            alive.TryGetValue(controller, out var own);
            var enemies = 0;
            foreach (var (faction, count) in alive)
            {
                if (faction != controller)
                    enemies += count;
            }

            own = Math.Max(1, own);
            var scale = Math.Clamp(enemies / (float) own, 0.25f, comp.NormalizationCap);
            points = Math.Max(1, (int) Math.Round(comp.PointsPerTick * scale));
        }

        if (comp.FeedWinPoints)
            ObjCtrl.AwardRawPointsToFaction(controller, points);
        ObjCtrl.AddHotspotPoints(controller, points);

        comp.TicksScored++;
        comp.TicksPerFaction.TryAdd(controller, 0);
        comp.TicksPerFaction[controller]++;
        Dirty(uid, comp);

        if (comp.RelocateAfterTicks > 0 && comp.TicksScored >= comp.RelocateAfterTicks)
            Relocate(uid, comp, xform);
    }

    /// <summary>Alive headcount per objective faction across the planet map network.</summary>
    private Dictionary<string, int> CountAliveByFaction(HashSet<string> competing)
    {
        var counts = new Dictionary<string, int>();
        var mapId = ObjCtrl.GetPlanetMapId();
        if (mapId is not { } planet || planet == MapId.Nullspace)
            return counts;

        var planetMaps = _zLevels.GetAllNetworkMapIds(planet);
        var query = EntityQueryEnumerator<NpcFactionMemberComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var factions, out var mobState, out var xform))
        {
            if (mobState.CurrentState != MobState.Alive || !planetMaps.Contains(xform.MapID))
                continue;

            foreach (var faction in factions.Factions)
            {
                var key = faction.ToString().ToLowerInvariant() switch
                {
                    "auweyu" => "weyu",
                    var id => id,
                };
                if (!competing.Contains(key))
                    continue;

                counts.TryAdd(key, 0);
                counts[key]++;
                break;
            }
        }

        return counts;
    }

    /// <summary>Moves the zone to a random objective marker on the planet, away from its current spot.</summary>
    private void Relocate(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform)
    {
        var mapId = ObjCtrl.GetPlanetMapId();
        if (mapId is not { } planet || planet == MapId.Nullspace)
            return;

        var planetMaps = _zLevels.GetAllNetworkMapIds(planet);
        var currentPos = _transform.GetWorldPosition(xform);
        var halfWidth = comp.HalfWidth;
        var halfHeight = comp.HalfHeight == 0 ? comp.HalfWidth : comp.HalfHeight;
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<CMUObjectiveMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var marker, out _, out var markerXform))
        {
            if (!planetMaps.Contains(markerXform.MapID))
                continue;

            var markerPos = _transform.GetWorldPosition(markerXform);
            if (MathF.Abs(markerPos.X - currentPos.X) < halfWidth * 2 &&
                MathF.Abs(markerPos.Y - currentPos.Y) < halfHeight * 2)
                continue;

            candidates.Add(marker);
        }

        if (candidates.Count == 0)
        {
            _logs.Warning($"[OBJ-HOTSPOT] No relocation markers found for '{ToPrettyString(uid)}', staying put.");
            return;
        }

        var target = _random.Pick(candidates);
        _transform.SetCoordinates(uid, Transform(target).Coordinates);
        comp.CurrentController = string.Empty;
        comp.TicksScored = 0;
        Dirty(uid, comp);
        _tacMap.DrawTacticalMapRectangle(Color.FromHex(comp.ZoneColorHex), _transform.GetWorldPosition(uid), comp.HalfWidth, halfHeight);
        _logs.Info($"[OBJ-HOTSPOT] Relocated '{ToPrettyString(uid)}' to marker {ToPrettyString(target)}.");
    }
}
