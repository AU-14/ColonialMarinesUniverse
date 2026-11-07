using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Marines;
using Content.Server._RMC14.TacticalMap;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Round.Objectives.Components;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.Round.Objectives.Type;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CCVar;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Server.Player;
using Robust.Shared.Configuration;

namespace Content.Server.CMU14.Round.Objectives.Type;

public sealed partial class ObjHotspotSystem : ObjectiveSystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TacticalMapSystem _tacMap = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _logs = Logger.GetSawmill("obj-hotspot");
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveResetEvent>(OnReset);
        SubscribeLocalEvent<HotspotObjectiveComponent, ObjectiveActivatedEvent>(OnActivated);
        SubscribeLocalEvent<HotspotZoneTilesComponent, ComponentRemove>(OnZoneTilesRemove);
    }

    private void OnZoneTilesRemove(EntityUid uid, HotspotZoneTilesComponent comp, ref ComponentRemove args)
        => RestoreZoneTiles(uid);

    private void OnActivated(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveActivatedEvent args)
    {
        EnsureComp<AppearanceComponent>(uid);
        DrawZoneRectangle(uid, comp, Transform(uid));
        PaintZoneTiles(uid, comp, Transform(uid));
    }

    /// <summary>
    /// Paints the zone tile inside the rectangle, skipping empty tiles: a floor over space
    /// mints walkable ground, and restoring empty can detach anchored entities.
    /// </summary>
    private void PaintZoneTiles(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        if (!_tileDef.TryGetDefinition(comp.ZoneTile, out var def))
            return;

        var zone = EnsureComp<HotspotZoneTilesComponent>(uid);
        if (zone.Originals.Count > 0)
            return;

        zone.Grid = gridUid;
        zone.Painted = new Tile(def.TileId);
        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var halfHeight = EffectiveHalfHeight(comp);
        for (var x = center.X - comp.HalfWidth; x <= center.X + comp.HalfWidth; x++)
        {
            for (var y = center.Y - halfHeight; y <= center.Y + halfHeight; y++)
            {
                var indices = new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                zone.Originals[indices] = tileRef.Tile;
                _map.SetTile(gridUid, grid, indices, zone.Painted);
            }
        }
    }

    /// <summary>
    /// Restores snapshot tiles that still show the zone tile; anything changed
    /// underneath keeps its change.
    /// </summary>
    private void RestoreZoneTiles(EntityUid uid)
    {
        if (!TryComp<HotspotZoneTilesComponent>(uid, out var zone)
            || !TryComp<MapGridComponent>(zone.Grid, out var grid))
            return;

        foreach (var (indices, tile) in zone.Originals)
        {
            if (!_map.TryGetTileRef(zone.Grid, grid, indices, out var tileRef)
                || tileRef.Tile.TypeId != zone.Painted.TypeId)
                continue;

            _map.SetTile(zone.Grid, grid, indices, tile);
        }

        zone.Originals.Clear();
    }

    private static string ZoneColorFor(HotspotObjectiveComponent comp, string? controller)
        => controller != null && comp.FactionZoneColors.TryGetValue(controller, out var hex) ? hex : comp.ZoneColorHex;

    private void DrawZoneRectangle(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform, string? controller = null)
    {
        // A relocation can cross z-level grids: clear the rectangle from the old tracking map
        // first, or the redraw below would strand it there.
        RemoveZoneRectangle(uid, comp, xform);

        if (!TryGetZoneCanvas(uid, comp, xform, out var map, out var canvas, out var depth, out var halfWidthPx, out var halfHeightPx))
            return;

        var hex = ZoneColorFor(comp, controller);
        comp.CurrentMapColorHex = hex;
        _tacMap.DrawTacticalMapRectangle(map, Color.FromHex(hex), canvas, halfWidthPx, halfHeightPx, depth: depth);
    }

    /// <summary>
    /// Clears the zone's drawn rectangle from its tracking map, keyed by the currently drawn
    /// color and geometry, so a recolor or cross-level move cannot orphan the old strokes.
    /// </summary>
    private void RemoveZoneRectangle(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform)
    {
        if (!TryGetZoneCanvas(uid, comp, xform, out var map, out var canvas, out var depth, out var halfWidthPx, out var halfHeightPx))
            return;

        var hex = comp.CurrentMapColorHex == string.Empty ? comp.ZoneColorHex : comp.CurrentMapColorHex;
        _tacMap.RemoveTacticalMapRectangle(map, Color.FromHex(hex), canvas, halfWidthPx, halfHeightPx, depth: depth);
    }

    private bool TryGetZoneCanvas(EntityUid uid, HotspotObjectiveComponent comp, TransformComponent xform,
        out Entity<TacticalMapComponent> map, out Vector2 canvas, out int depth, out float halfWidthPx, out float halfHeightPx)
    {
        map = default;
        canvas = default;
        depth = 0;
        halfWidthPx = 0;
        halfHeightPx = 0;

        if (xform.GridUid is not { } grid || !_tacMap.TryGetTrackingMap(grid, out map))
            return false;

        if (!TryComp<AreaGridComponent>(map.Owner, out var areas) || areas.Colors.Count == 0)
            return false;

        var min = Vector2i.Zero;
        var max = Vector2i.Zero;
        foreach (var tile in areas.Colors.Keys)
        {
            min = Vector2i.ComponentMin(min, tile);
            max = Vector2i.ComponentMax(max, tile);
        }

        var center = _transform.GetGridOrMapTilePosition(uid, xform);
        // Anchor at the tile center and pad by half a tile so the edges land on the zone's tile boundaries.
        canvas = CMUReconDrawingCoordinates.ToCanvas(center + new Vector2(0.5f, 0.5f), min, max);
        if (TryComp<CMUZLevelMapComponent>(grid, out var zLevel))
            depth = zLevel.Depth;

        var halfHeight = EffectiveHalfHeight(comp);
        halfWidthPx = comp.HalfWidth * 3 + 1.5f;
        halfHeightPx = halfHeight * 3 + 1.5f;
        return true;
    }

    private void OnReset(EntityUid uid, HotspotObjectiveComponent comp, ref ObjectiveResetEvent args)
    {
        comp.CurrentController = string.Empty;
        comp.ElapsedTicks = 0;
        _appearance.SetData(uid, HotspotVisuals.ZoneColor, comp.ZoneColorHex);
        RepaintZoneTiles(uid, comp, null);
        DrawZoneRectangle(uid, comp, Transform(uid));
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
                _appearance.SetData(uid, HotspotVisuals.ZoneColor, comp.ZoneColorHex);
                RepaintZoneTiles(uid, comp, null);
                DrawZoneRectangle(uid, comp, Transform(uid));
            }
        }
        else
        {
            if (comp.CurrentController != controller)
            {
                comp.CurrentController = controller;
                comp.ControllerStreak = 1;
                _appearance.SetData(uid, HotspotVisuals.ZoneColor, ZoneColorFor(comp, controller));
            }
            else if (++comp.ControllerStreak == 2)
            {
                AnnounceControllerChange(uid, controller);
                RepaintZoneTiles(uid, comp, controller);
                DrawZoneRectangle(uid, comp, Transform(uid), controller);
            }

            // Engagement tiers: 0-20% of presence scores slow, 20-50% normal, 50%+ fast.
            var totalInZone = presence.Values.Sum();
            var engagementRatio = presence[controller] / (float)totalInZone;
            var tierMultiplier = engagementRatio switch
            {
                <= 0.2f => 0.5f,
                <= 0.5f => 1.0f,
                _ => 1.5f,
            };

            // Flatten tiers past the dampen time so overtime cannot snowball; the CVar override moves the limit with it.
            var dampenAfter = comp.TierDampenAfter;
            var dampenMinutes = _cfg.GetCVar(CCVars.FoFTimeLimitMinutes);
            if (dampenMinutes > 0)
                dampenAfter = TimeSpan.FromMinutes(dampenMinutes);
            if (_gameTicker.RoundDuration() >= dampenAfter)
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
                ObjCtrl.AwardRawPointsToFaction(controller, (int) Math.Round(points));

            var ticksToWin = _cfg.GetCVar(CCVars.FoFHotspotTicksToWin);
            comp.TicksPerFaction.TryAdd(controller, 0);
            comp.TicksPerFaction[controller]++;
            AnnounceZoneProgress(comp, controller, ticksToWin);

            if (ticksToWin > 0 && comp.TicksPerFaction[controller] >= ticksToWin)
            {
                ObjCtrl.CompleteObjectiveForFaction(uid, objComp, controller);
                return;
            }
        }

        if (comp.RelocateAfterTicks > 0 && comp.ElapsedTicks >= comp.RelocateAfterTicks)
            Relocate(uid, comp, xform);
    }

    /// <summary>
    /// First time a faction's hold-ticks cross each quarter of the win target, with a gap
    /// line for the strongest rival. The 100% mark is the win itself.
    /// </summary>
    private void AnnounceZoneProgress(HotspotObjectiveComponent comp, string controller, int ticksToWin)
    {
        if (ticksToWin <= 0)
            return;

        var level = Math.Min(comp.TicksPerFaction[controller] * 4 / ticksToWin, 3);
        if (level <= comp.AnnouncedMilestones.GetValueOrDefault(controller))
            return;

        comp.AnnouncedMilestones[controller] = level;
        var message = Loc.GetString("cmu-hotspot-objective-progress",
            ("faction", controller.ToUpperInvariant()),
            ("progress", level * 25));

        var other = string.Empty;
        var otherTicks = 0;
        foreach (var (faction, ticks) in comp.TicksPerFaction)
        {
            if (faction == controller || ticks <= otherTicks)
                continue;

            other = faction;
            otherTicks = ticks;
        }

        if (other != string.Empty)
        {
            var gap = (comp.TicksPerFaction[controller] - otherTicks) * 100 / ticksToWin;
            message += "\n" + Loc.GetString(gap >= 0
                    ? "cmu-hotspot-objective-progress-trails"
                    : "cmu-hotspot-objective-progress-leads",
                ("other", other.ToUpperInvariant()),
                ("gap", Math.Abs(gap)));
        }

        foreach (var session in _players.Sessions)
            _chat.DispatchServerMessage(session, message);
    }

    private void AnnounceControllerChange(EntityUid uid, string controller)
    {
        _marineAnnounce.AnnounceToMarines(
            Loc.GetString("cmu-hotspot-objective-controller", ("faction", controller.ToUpperInvariant())),
            faction: controller);
    }

    /// <summary>
    /// Recolors the painted tiles to the faction's tile, or neutral on control loss.
    /// Player-changed tiles keep their change.
    /// </summary>
    private void RepaintZoneTiles(EntityUid uid, HotspotObjectiveComponent comp, string? controller)
    {
        if (!TryComp<HotspotZoneTilesComponent>(uid, out var zone)
            || !TryComp<MapGridComponent>(zone.Grid, out var grid))
            return;

        var tileId = controller != null
            && comp.FactionZoneTiles.TryGetValue(controller, out var factionTile)
                ? factionTile
                : comp.ZoneTile;
        if (!_tileDef.TryGetDefinition(tileId, out var def) || zone.Painted.TypeId == def.TileId)
            return;

        var previous = zone.Painted.TypeId;
        zone.Painted = new Tile(def.TileId);
        foreach (var indices in zone.Originals.Keys)
        {
            if (!_map.TryGetTileRef(zone.Grid, grid, indices, out var tileRef)
                || tileRef.Tile.TypeId != previous)
                continue;

            _map.SetTile(zone.Grid, grid, indices, zone.Painted);
        }
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

        // Keep the new position clear of other hotspots so tile snapshots never
        // overlap and fight over restores.
        var zones = new List<(MapId Map, Vector2 Pos, int HalfWidth, int HalfHeight)>();
        var hotspotQuery = EntityQueryEnumerator<HotspotObjectiveComponent, TransformComponent>();
        while (hotspotQuery.MoveNext(out var other, out var zone, out var zoneXform))
        {
            if (other == uid)
                continue;

            zones.Add((zoneXform.MapID, _transform.GetWorldPosition(zoneXform), zone.HalfWidth, EffectiveHalfHeight(zone)));
        }

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

            if (zones.Any(z => z.Map == markerXform.MapID
                    && MathF.Abs(markerPos.X - z.Pos.X) < halfWidth + z.HalfWidth
                    && MathF.Abs(markerPos.Y - z.Pos.Y) < halfHeight + z.HalfHeight))
                continue;

            candidates.Add(marker);
        }

        if (candidates.Count == 0)
        {
            _logs.Warning($"[OBJ-HOTSPOT] No relocation markers found for '{ToPrettyString(uid)}', staying put.");
            return;
        }

        var target = _random.Pick(candidates);
        // Clear the old rectangle before the move: relocation can cross z-level grids.
        RemoveZoneRectangle(uid, comp, xform);
        _transform.SetCoordinates(uid, Transform(target).Coordinates);
        comp.CurrentController = string.Empty;
        comp.ElapsedTicks = 0;
        _appearance.SetData(uid, HotspotVisuals.ZoneColor, comp.ZoneColorHex);
        DrawZoneRectangle(uid, comp, Transform(uid));
        RestoreZoneTiles(uid);
        PaintZoneTiles(uid, comp, Transform(uid));
        _logs.Info($"[OBJ-HOTSPOT] Relocated '{ToPrettyString(uid)}' to marker {ToPrettyString(target)}.");
    }
}
