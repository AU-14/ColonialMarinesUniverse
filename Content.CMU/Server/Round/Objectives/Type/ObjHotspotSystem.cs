using System.Linq;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.TacticalMap;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjHotspotSystem : ObjectiveSystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedTacticalMapSystem _tacMap = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TacticalMapSystem _tacMap = default!;
    [Dependency] private MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-hotspot");
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveActivatedEvent args)
    {
        EnsureComp<AppearanceComponent>(uid);
        DrawZoneRectangle(uid, comp, Transform(uid));
    }

    private void DrawZoneRectangle(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform)
    {
        if (xform.GridUid is not { } grid || !_tacMap.TryGetTrackingMap(grid, out var map))
            return;

        if (!TryComp<AreaGridComponent>(map.Owner, out var areas) || areas.Colors.Count == 0)
            return;

        var min = Vector2i.Zero;
        var max = Vector2i.Zero;
        foreach (var tile in areas.Colors.Keys)
        {
            min = Vector2i.ComponentMin(min, tile);
            max = Vector2i.ComponentMax(max, tile);
        }

        var halfHeight = EffectiveHalfHeight(comp);
        var center = _transform.GetGridOrMapTilePosition(uid, xform);
        var canvas = CMUReconDrawingCoordinates.ToCanvas(center, min, max);
        var depth = 0;
        if (TryComp<CMUZLevelMapComponent>(grid, out var zLevel))
            depth = zLevel.Depth;
        _tacMap.DrawTacticalMapRectangle(map, Color.FromHex(comp.ZoneColorHex), canvas, comp.HalfWidth * 3, halfHeight * 3, depth: depth);
    }

    private void OnReset(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CurrentController = string.Empty;
        comp.ElapsedTicks = 0;
        _appearance.SetData(uid, HotspotVisuals.Controller, string.Empty);
    }

    private static string FactionKey(ProtoId<NpcFactionPrototype> faction)
        => faction.ToString().ToLowerInvariant() switch
        {
            "auweyu" => "weyu",
            var id => id,
        };

    private static int EffectiveHalfHeight(HotspotObjectiveComponent comp)
        => comp.HalfHeight == 0 ? comp.HalfWidth : comp.HalfHeight;

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

        var center = _transform.GetWorldPosition(xform);
        var halfWidth = comp.HalfWidth;
        var halfHeight = EffectiveHalfHeight(comp);

        var competing = objComp.Factions.Select(f => f.ToLowerInvariant()).ToHashSet();

        var planetMaps = _zLevels.GetAllNetworkMapIds(planet);
        var presence = new Dictionary<string, int>();
        var alive = new Dictionary<string, int>();

        comp.ElapsedTicks++;

        var query = EntityQueryEnumerator<NpcFactionMemberComponent, MobStateComponent, TransformComponent>();
        while (query.MoveNext(out _, out var factions, out var mobState, out var memberXform))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            var inPlanet = planetMaps.Contains(memberXform.MapID);
            var inZone = memberXform.MapID == xform.MapID;
            if (!inPlanet && !inZone)
                continue;

            string? counted = null;
            foreach (var faction in factions.Factions)
            {
                var key = FactionKey(faction);
                if (!competing.Contains(key))
                    continue;

                alive.TryAdd(key, 0);
                alive[key]++;
                counted = key;
                break;
            }

            if (counted == null || !inZone)
                continue;

            var pos = _transform.GetWorldPosition(memberXform);
            if (pos.X < center.X - halfWidth || pos.X > center.X + halfWidth
                || pos.Y < center.Y - halfHeight || pos.Y > center.Y + halfHeight)
                continue;

            presence.TryAdd(counted, 0);
            presence[counted]++;
        }

        string? controller = null;
        if (presence.Count > 0)
        {
            var strongest = presence.MaxBy(kvp => kvp.Value);
            var best = strongest.Value;
            if (presence.Values.Count(v => v == best) == 1)
                controller = strongest.Key;
        }

        if (controller == null)
        {
            if (comp.CurrentController != string.Empty)
            {
                comp.CurrentController = string.Empty;
                _appearance.SetData(uid, HotspotVisuals.Controller, string.Empty);
            }
        }
        else
        {
            if (comp.CurrentController != controller)
            {
                comp.CurrentController = controller;
                _appearance.SetData(uid, HotspotVisuals.Controller, controller);
                AnnounceControllerChange(uid, controller);
            }

            // Engagement tier: scale speed with your share of the total bodies here.
            // 0-20% of presence = slow, 20-50% = standard, 50%+ = fast.
            // Overtime (75min+) flattens to 1.0x to prevent score snowballing.
            var totalInZone = presence.Values.Sum();
            var engagementRatio = presence[controller] / (float)totalInZone;
            var tierMultiplier = engagementRatio switch
            {
                <= 0.2f => 0.5f,
                <= 0.5f => 1.0f,
                _ => 1.5f,
            };

            // Dampen tiers during overtime (after 75min / ~450 ticks) to cap comeback power.
            if (comp.ElapsedTicks >= 450)
                tierMultiplier = 1.0f;

            var points = comp.PointsPerTick * tierMultiplier;
            if (comp.NormalizeByTeamSize)
            {
                alive.TryGetValue(controller, out var own);
                var enemies = 0;
                foreach (var (faction, count) in alive)
                {
                    if (faction != controller)
                        enemies += count;
                }

                own = Math.Max(1, own);
                var scale = Math.Clamp(enemies / (float) own, 0.25f, comp.NormalizationCap);
                points = Math.Max(1, (int) Math.Round(points * scale));
            }

            if (comp.FeedWinPoints)
                ObjCtrl.AwardRawPointsToFaction(controller, points);

            comp.TicksPerFaction.TryAdd(controller, 0);
            comp.TicksPerFaction[controller]++;

            if (comp.TicksToWin > 0 && comp.TicksPerFaction[controller] >= comp.TicksToWin)
            {
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, controller);
                return;
            }
        }

        if (comp.RelocateAfterTicks > 0 && comp.ElapsedTicks >= comp.RelocateAfterTicks)
            Relocate(uid, comp, xform);
    }

    private void AnnounceControllerChange(EntityUid uid, string controller)
    {
        _marineAnnounce.AnnounceToMarines(
            Loc.GetString("cmu-hotspot-objective-controller", ("faction", controller.ToUpperInvariant())),
            faction: controller);
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
        var halfHeight = EffectiveHalfHeight(comp);
        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<CMUObjectiveMarkerComponent, TransformComponent>();
        while (query.MoveNext(out var marker, out _, out var markerXform))
        {
            if (!planetMaps.Contains(markerXform.MapID))
                continue;

            var markerPos = _transform.GetWorldPosition(markerXform);
            if (markerXform.MapID == xform.MapID
                && MathF.Abs(markerPos.X - currentPos.X) < halfWidth * 2
                && MathF.Abs(markerPos.Y - currentPos.Y) < halfHeight * 2)
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
        comp.ElapsedTicks = 0;
        _appearance.SetData(uid, HotspotVisuals.Controller, string.Empty);
        DrawZoneRectangle(uid, comp, Transform(uid));
        _logs.Info($"[OBJ-HOTSPOT] Relocated '{ToPrettyString(uid)}' to marker {ToPrettyString(target)}.");
    }
}
