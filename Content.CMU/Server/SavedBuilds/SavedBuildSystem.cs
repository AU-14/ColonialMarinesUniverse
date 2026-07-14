// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server._AU14.ZLevelBuilding;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.Administration.Managers;
using Content.Shared._AU14.SavedBuilds;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Administration;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.Server._AU14.SavedBuilds;

/// <summary>
/// Server-authoritative save side of the saved-builds feature. Resolves a client's selection
/// descriptor against the soft-whitelist (entities must carry <see cref="PlayerBuiltComponent"/> and
/// be owned by the saver or a build partner), serializes the resulting entity set through the engine
/// entity serializer, wraps it in a metadata header, and SENDS THE RESULT BACK TO THE CLIENT.
/// Saved builds are private LOCAL files: the server never stores, lists, or shares them - the client
/// writes them to its own user-data folder, and players share by copying files between their folders.
/// Placement uploads the file's YAML back (admin/mapper gated + size capped).
/// </summary>
public sealed partial class SavedBuildSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private IMapManager _mapManager = default!;
    [Dependency] private BuildPartnerSystem _partners = default!;
    [Dependency] private PlayerBuiltSystem _playerBuilt = default!;
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private ZLevelBuildingSystem _zBuilding = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;

    // 🔧 TUNABLE: how many z-levels above/below the selection box are also scanned when saving. A build
    // that crosses levels (support beams below, platforms above) is captured whole within this range.
    private const int MaxZRange = 3;

    // ============================================
    // 🔧 TUNABLE: selection / naming / upload limits
    // ============================================
    private const int MaxRadius = 5; // selection half-extent in tiles (5 => 11x11 box)
    private const int MaxSelectionBoxes = 64; // boxes per selection request (spam cap)
    private const int MaxManualEntities = 512; // manual add/remove entities per request (spam cap)
    private const int MaxNameLength = 64; // build name length (also bounds the file name)
    private const int MaxBuildYamlLength = 4_000_000; // max chars of client-uploaded build YAML (~4 MB)
    private const int FormatVersion = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBuildSelectionEvent>(OnRequestSelection);
        SubscribeNetworkEvent<RequestSaveBuildEvent>(OnRequestSave);
        SubscribeNetworkEvent<RequestPlaceBuildEvent>(OnRequestPlace);
        // No list/delete/rename/open-folder handlers: saved builds are the CLIENT's local files. The
        // server never stores or enumerates them, so no player can ever see another player's builds.
    }

    private void OnRequestPlace(RequestPlaceBuildEvent ev, EntitySessionEventArgs args)
    {
        PlaceBuild(args.SenderSession, ev.Id, ev.Yaml, GetCoordinates(ev.Target), new Angle(ev.Rotation), ev.AtOriginal);
    }

    /// <summary>
    /// Loads a CLIENT-UPLOADED saved build and places it on the grid at <paramref name="target"/>, rotated
    /// by <paramref name="rotation"/>. Entities load as orphans (the source grid wasn't serialized), so each
    /// root is repositioned by (savedLocal - anchor) rotated, then re-parented/anchored to the target grid.
    /// Upload security: gated behind Spawn/Mapping admin flags (spawning arbitrary serialized entities is
    /// admin-tier power, same class as loadgamemap) and a hard YAML size cap.
    /// NOTE (Phase 4a): this currently spawns the build for free; material cost is Phase 4c.
    /// </summary>
    public void PlaceBuild(ICommonSession session, string id, string yaml, EntityCoordinates target, Angle rotation, bool atOriginal = false)
    {
        if (session.AttachedEntity is not { } user || string.IsNullOrWhiteSpace(yaml))
            return;

        // Instant, free placement is the privileged version: admins (Spawn) or mappers (Mapping). Non-privileged
        // players use client-side construction ghosts instead. This gate is also what makes accepting client
        // YAML acceptable - never relax it without adding real content validation.
        if (!_adminManager.HasAdminFlag(session, AdminFlags.Spawn) && !_adminManager.HasAdminFlag(session, AdminFlags.Mapping))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-notadmin"), user, user);
            return;
        }

        if (yaml.Length > MaxBuildYamlLength)
        {
            Log.Warning($"{session.Name} sent an oversized saved-build upload ({yaml.Length} chars), rejected.");
            _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
            return;
        }

        MappingDataNode root;
        try
        {
            using var reader = new System.IO.StringReader(yaml);
            root = DataNodeParser.ParseYamlStream(reader).First().Root as MappingDataNode
                   ?? throw new InvalidDataException("Root is not a mapping.");
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to parse saved-build upload '{id}' from {session.Name}: {e.Message}");
            _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
            return;
        }

        if (!root.TryGet<MappingDataNode>("build", out var buildNode))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
            return;
        }

        // "Place at original location": resolve the original grid + anchor and place there, unrotated.
        if (atOriginal)
        {
            if (!TryGetOriginalTarget(root, out target))
            {
                _popup.PopupEntity(Loc.GetString("saved-build-error-noorigin"), user, user);
                return;
            }
            rotation = Angle.Zero;
        }

        if (!target.IsValid(EntityManager))
            return;

        var targetMap = _transform.ToMapCoordinates(target);
        if (!_mapManager.TryFindGridAt(targetMap, out var gridUid, out _))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-nogrid"), user, user);
            return;
        }

        if (Transform(gridUid).MapUid is not { } targetMapUid)
            return;

        var anchor = ReadAnchor(root);
        var savedTiles = ReadSavedTiles(root);
        var placedTiles = PlaceSavedTiles(savedTiles, targetMapUid, gridUid, targetMap, rotation);
        var entityCount = ReadMetaInt(root, "entityCount");

        if (entityCount <= 0)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} (user {session.UserId}) placed saved build '{id}' ({placedTiles} tiles) at {targetMap}");
            _popup.PopupEntity(Loc.GetString("saved-build-placed", ("count", placedTiles)), user, user);
            return;
        }

        LoadResult result;
        try
        {
            // Merge onto the target map so the entities are properly map-initialized (collisions, etc.);
            // they end up parented to the map, and we then re-parent each root onto the grid below.
            var loadOpts = MapLoadOptions.Default with { MergeMap = targetMap.MapId };
            if (!_mapLoader.TryLoadGeneric(buildNode, $"savedbuild:{id}", out var loaded, loadOpts))
            {
                _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
                return;
            }
            result = loaded;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load saved build '{id}' for {session.Name}: {e}");
            _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
            return;
        }

        // After the merge the build's root entities are normally parented to the target map - but if a loaded
        // orphan happened to land where a grid already sits, the loader can parent it straight onto that grid.
        // Those must be repositioned too: skipping them left entities stranded at their ORIGINAL saved spot
        // (the "completely messed up placement" bug).
        var roots = result.Entities
            .Where(e => EntityManager.EntityExists(e)
                && (Transform(e).ParentUid == targetMapUid || HasComp<MapGridComponent>(Transform(e).ParentUid)))
            .ToList();

        // The serialized anchored flag is lost in the nullspace/map phase, so we restore it from the saved
        // preview (keyed by prototype + quarter-tile offset). Only entries that actually recorded "anchored"
        // are used; older saves without it fall back to the physics-body heuristic below, unchanged.
        var anchoredByKey = ReadAnchoredIntent(root);

        // Multi-z: per-entry z-level offsets from the preview. Queued per key because two identical entities
        // can share the same x/y on DIFFERENT levels (that's exactly what multi-z builds do).
        var zByKey = ReadZOffsets(root);
        var skippedZ = 0;

        foreach (var rootEnt in roots)
        {
            // WORLD-frame math throughout, not raw transform locals: a map's transform is identity, so for
            // map-parented roots world == the original saved grid-local (merge offset is zero). For roots the
            // loader parented onto a grid, world position resolves through that grid's own offset/rotation.
            // The old local-frame arithmetic silently mixed a MAP-frame offset into GRID-local coordinates,
            // which scrambled placements whenever the target grid was offset or rotated relative to its map.
            var savedWorld = _transform.GetWorldPosition(rootEnt);
            var savedRot = _transform.GetWorldRotation(rootEnt);

            var desired = new MapCoordinates(targetMap.Position + rotation.RotateVec(savedWorld - anchor), targetMap.MapId);

            var relSave = savedWorld - anchor;
            var protoId = MetaData(rootEnt).EntityPrototype?.ID ?? string.Empty;

            // Multi-z: entities saved on another level go to the matching level relative to the target (levels
            // are world-aligned, so the x/y math is identical). Missing levels are created on demand through
            // the z-building bootstrap; if that fails, the entity is dropped rather than dumped on the wrong level.
            var placeGrid = gridUid;
            var placeMapId = targetMap.MapId;
            if (TakeZOffset(zByKey, protoId, relSave) is { } zOff && zOff != 0)
            {
                if (TryResolveLevel(targetMapUid, gridUid, zOff, desired.Position, out var levelGrid, out var levelMapId))
                {
                    placeGrid = levelGrid;
                    placeMapId = levelMapId;
                }
                else
                {
                    skippedZ++;
                    QueueDel(rootEnt);
                    continue;
                }
            }

            var desiredOnLevel = new MapCoordinates(desired.Position, placeMapId);
            _transform.SetCoordinates(rootEnt, new EntityCoordinates(placeGrid, _transform.ToCoordinates(placeGrid, desiredOnLevel).Position));
            _transform.SetWorldRotation(rootEnt, savedRot + rotation);

            // Restore the original anchored state. Prefer the recorded intent (handles props anchored without a
            // Static physics body - the mapper-mode case); fall back to "Static body => anchored" for old saves.
            var wasAnchored = anchoredByKey.TryGetValue((protoId, QuantizeOffset(relSave.X), QuantizeOffset(relSave.Y)), out var recorded)
                ? recorded
                : TryComp<PhysicsComponent>(rootEnt, out var body) && body.BodyType == BodyType.Static;

            if (wasAnchored)
                _transform.AnchorEntity(rootEnt);

            // Mark placed entities as built by the placer (accountability + makes them re-saveable).
            _playerBuilt.MarkBuilt(rootEnt, user);
        }

        // NOTE: this is effectively the ADMIN/free placement (instant, free, keeps container contents).
        // TODO (player costed version): strip container contents and consume materials via a ghost build.

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):player} (user {session.UserId}) placed saved build '{id}' ({roots.Count} roots, {placedTiles} tiles, {skippedZ} skipped for missing z-levels) at {targetMap}");
        _popup.PopupEntity(Loc.GetString("saved-build-placed", ("count", roots.Count - skippedZ + placedTiles)), user, user);
        if (skippedZ > 0)
            _popup.PopupEntity(Loc.GetString("saved-build-z-skipped", ("count", skippedZ)), user, user, PopupType.MediumCaution);
    }

    /// <summary>Per-entry z-level offsets from the preview, queued per (proto, x, y) key - identical entities
    /// legitimately share the same x/y on different levels in a multi-z build.</summary>
    private Dictionary<(string, int, int), Queue<int>> ReadZOffsets(MappingDataNode root)
    {
        var map = new Dictionary<(string, int, int), Queue<int>>();
        if (!root.TryGet<MappingDataNode>("meta", out var meta) || !meta.TryGet<SequenceDataNode>("preview", out var seq))
            return map;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m)
                continue;

            var proto = MetaString(m, "proto");
            var key = (proto, QuantizeOffset(MetaFloat(m, "x")), QuantizeOffset(MetaFloat(m, "y")));
            int.TryParse(MetaString(m, "z"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var z);
            if (!map.TryGetValue(key, out var queue))
                map[key] = queue = new Queue<int>();
            queue.Enqueue(z);
        }

        return map;
    }

    /// <summary>Consumes the next z offset recorded for this (proto, offset) key; null when unrecorded (old saves).</summary>
    private static int? TakeZOffset(Dictionary<(string, int, int), Queue<int>> zByKey, string protoId, Vector2 relSave)
    {
        if (zByKey.TryGetValue((protoId, QuantizeOffset(relSave.X), QuantizeOffset(relSave.Y)), out var queue) &&
            queue.TryDequeue(out var z))
        {
            return z;
        }

        return null;
    }

    /// <summary>Resolves (creating on demand via the z-building bootstrap) the level <paramref name="zOffset"/>
    /// steps from <paramref name="baseMap"/> and a grid on it under <paramref name="worldPos"/>.</summary>
    private bool TryResolveLevel(EntityUid baseMap, EntityUid baseGrid, int zOffset, Vector2 worldPos, out EntityUid levelGrid, out MapId levelMapId)
    {
        levelGrid = default;
        levelMapId = MapId.Nullspace;

        var currentMap = baseMap;
        var currentGrid = baseGrid;
        var step = Math.Sign(zOffset);
        for (var i = 0; i < Math.Abs(zOffset); i++)
        {
            if (!_zBuilding.EnsureNeighborLevel(currentMap, step, currentGrid, worldPos, out var nextMap, out var nextGrid))
                return false;

            currentMap = nextMap;
            currentGrid = nextGrid;
        }

        if (!TryComp<MapComponent>(currentMap, out var mapComp))
            return false;

        levelGrid = currentGrid;
        levelMapId = mapComp.MapId;
        return true;
    }

    /// <summary>Resolves the build's original grid + anchor coordinates (if that grid still exists this round).</summary>
    private bool TryGetOriginalTarget(MappingDataNode root, out EntityCoordinates target)
    {
        target = EntityCoordinates.Invalid;
        if (!root.TryGet<MappingDataNode>("meta", out var meta))
            return false;

        if (!NetEntity.TryParse(MetaString(meta, "sourceGrid"), out var netGrid))
            return false;

        if (!TryGetEntity(netGrid, out var grid) || !HasComp<MapGridComponent>(grid))
            return false;

        target = new EntityCoordinates(grid.Value, ReadAnchor(root));
        return true;
    }

    private Vector2 ReadAnchor(MappingDataNode root)
    {
        if (!root.TryGet<MappingDataNode>("meta", out var meta))
            return Vector2.Zero;

        float.TryParse(MetaString(meta, "anchorX"), NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
        float.TryParse(MetaString(meta, "anchorY"), NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
        return new Vector2(x, y);
    }

    private int ReadMetaInt(MappingDataNode root, string key)
    {
        if (!root.TryGet<MappingDataNode>("meta", out var meta))
            return 0;

        int.TryParse(MetaString(meta, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    private List<BuildPreviewTile> ReadSavedTiles(MappingDataNode root)
    {
        var tiles = new List<BuildPreviewTile>();
        if (!root.TryGet<MappingDataNode>("meta", out var meta) || !meta.TryGet<SequenceDataNode>("tiles", out var seq))
            return tiles;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m)
                continue;

            tiles.Add(new BuildPreviewTile
            {
                Tile = MetaString(m, "tile"),
                X = MetaFloat(m, "x"),
                Y = MetaFloat(m, "y"),
                Z = ReadInt(m, "z"),
            });
        }

        return tiles;
    }

    private int PlaceSavedTiles(
        List<BuildPreviewTile> tiles,
        EntityUid targetMapUid,
        EntityUid gridUid,
        MapCoordinates targetMap,
        Angle rotation)
    {
        var placed = 0;
        foreach (var tile in tiles)
        {
            if (!_prototype.TryIndex<ContentTileDefinition>(tile.Tile, out var tileDef))
                continue;

            var desired = new MapCoordinates(targetMap.Position + rotation.RotateVec(new Vector2(tile.X, tile.Y)), targetMap.MapId);
            var placeGrid = gridUid;
            var placeMapId = targetMap.MapId;
            if (tile.Z != 0)
            {
                if (TryResolveLevel(targetMapUid, gridUid, tile.Z, desired.Position, out var levelGrid, out var levelMapId))
                {
                    placeGrid = levelGrid;
                    placeMapId = levelMapId;
                }
                else
                {
                    continue;
                }
            }

            var desiredOnLevel = new MapCoordinates(desired.Position, placeMapId);
            if (!TryComp<MapGridComponent>(placeGrid, out var grid))
                continue;

            var indices = _map.TileIndicesFor(placeGrid, grid, desiredOnLevel);
            _map.SetTile(placeGrid, grid, indices, new Tile(tileDef.TileId));
            placed++;
        }

        return placed;
    }

    private static string MetaString(MappingDataNode meta, string key)
    {
        return meta.TryGet<ValueDataNode>(key, out var node) ? node.Value : string.Empty;
    }

    private static float MetaFloat(MappingDataNode meta, string key)
    {
        float.TryParse(MetaString(meta, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    private static int ReadInt(MappingDataNode meta, string key)
    {
        int.TryParse(MetaString(meta, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value);
        return value;
    }

    /// <summary>
    /// Builds a lookup of saved anchored intent, keyed by (prototype, quarter-tile X, quarter-tile Y), from the
    /// build's preview header. Only entries that recorded an "anchored" field are included; older saves without
    /// it leave the dictionary empty, so placement falls back to the physics-body heuristic.
    /// </summary>
    private Dictionary<(string, int, int), bool> ReadAnchoredIntent(MappingDataNode root)
    {
        var map = new Dictionary<(string, int, int), bool>();
        if (!root.TryGet<MappingDataNode>("meta", out var meta) || !meta.TryGet<SequenceDataNode>("preview", out var seq))
            return map;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m || !m.TryGet<ValueDataNode>("anchored", out var anchoredNode))
                continue;

            var proto = MetaString(m, "proto");
            var x = MetaFloat(m, "x");
            var y = MetaFloat(m, "y");
            bool.TryParse(anchoredNode.Value, out var anchored);
            map[(proto, QuantizeOffset(x), QuantizeOffset(y))] = anchored;
        }

        return map;
    }

    /// <summary>Quarter-tile quantisation so a float offset can key a dictionary (structures are tile-aligned).</summary>
    private static int QuantizeOffset(float value) => (int) MathF.Round(value * 4f);

    private void OnRequestSelection(RequestBuildSelectionEvent ev, EntitySessionEventArgs args)
    {
        var resolved = ResolveSelection(args.SenderSession, ev.Selection, ev.Mode, ev.IncludeLoose);
        RaiseNetworkEvent(new BuildSelectionResultEvent
        {
            Entities = resolved.Select(e => GetNetEntity(e)).ToList(),
            Tiles = ResolveTiles(args.SenderSession, ev.Selection, ev.Mode, ev.IncludeTiles),
        }, args.SenderSession);
    }

    private void OnRequestSave(RequestSaveBuildEvent ev, EntitySessionEventArgs args)
    {
        SaveBuild(args.SenderSession, ev.Name, ev.Selection, ev.Mode, ev.IncludeLoose, ev.IncludeTiles);
    }

    /// <summary>
    /// Resolves a selection descriptor to the concrete set of entities the given player may save. In Player/Admin
    /// mode that is anything they (or a build partner) built; in Mapper mode (requires AdminFlags.Mapping) it is
    /// ANY world structure/item regardless of who built it (map-placed, admin-spawned, etc.) minus mobs/players.
    /// The privileged mode is re-validated here against the caller's real flags, so a client can't spoof it.
    /// </summary>
    public HashSet<EntityUid> ResolveSelection(ICommonSession saver, BuildSelectionData selection, BuildSaveMode mode = BuildSaveMode.Player, bool includeLoose = false)
    {
        var result = new HashSet<EntityUid>();
        var saverId = saver.UserId;

        // Mapper mode only takes effect if the caller actually holds the Mapping flag; otherwise fall back to the
        // normal player-built rules (no error - the dropdown just shouldn't have offered it to them).
        var mapperMode = mode == BuildSaveMode.Mapper && _adminManager.HasAdminFlag(saver, AdminFlags.Mapping);

        if (selection.Boxes != null)
        {
            // Take() caps: the selection lists come from the client and are otherwise unbounded - tens of
            // thousands of boxes would be tens of thousands of lookup queries per (spammable) request.
            foreach (var sel in selection.Boxes.Take(MaxSelectionBoxes))
            {
                var radius = Math.Clamp(sel.Radius, 0, MaxRadius);
                var coords = GetCoordinates(sel.Center);
                if (!coords.IsValid(EntityManager))
                    continue;

                var map = _transform.ToMapCoordinates(coords);
                var full = (radius * 2) + 1; // tiles across
                var box = Box2.CenteredAround(map.Position, new Vector2(full, full));

                var found = new HashSet<EntityUid>();
                _lookup.GetEntitiesIntersecting(map.MapId, box, found);

                // Multi-z: z-levels are world-aligned, so the same box is also scanned on the linked levels
                // above/below - a build whose support beams or upper platforms cross levels saves as one.
                if (_mapManager.GetMapEntityId(map.MapId) is { Valid: true } boxMapUid)
                {
                    for (var dz = -MaxZRange; dz <= MaxZRange; dz++)
                    {
                        if (dz == 0 || !_zLevels.TryMapOffset(boxMapUid, dz, out _, out var otherMapComp))
                            continue;

                        _lookup.GetEntitiesIntersecting(otherMapComp.MapId, box, found);
                    }
                }

                foreach (var uid in found)
                {
                    if (CanSave(uid, saverId, mapperMode, includeLoose))
                        result.Add(uid);
                }
            }
        }

        if (selection.ManualAdds != null)
        {
            foreach (var net in selection.ManualAdds.Take(MaxManualEntities))
            {
                if (TryGetEntity(net, out var uid) && CanSave(uid.Value, saverId, mapperMode, includeLoose))
                    result.Add(uid.Value);
            }
        }

        if (selection.ManualRemoves != null)
        {
            foreach (var net in selection.ManualRemoves.Take(MaxManualEntities))
            {
                if (TryGetEntity(net, out var uid))
                    result.Remove(uid.Value);
            }
        }

        return result;
    }

    private List<BuildSelectionTile> ResolveTiles(ICommonSession saver, BuildSelectionData selection, BuildSaveMode mode, bool includeTiles)
    {
        var result = new List<BuildSelectionTile>();
        if (!includeTiles)
            return result;

        var anyTile =
            mode == BuildSaveMode.Admin && _adminManager.HasAdminFlag(saver, AdminFlags.Spawn) ||
            mode == BuildSaveMode.Mapper && _adminManager.HasAdminFlag(saver, AdminFlags.Mapping);

        var allowed = anyTile ? null : GetZBuildableTileIds();
        if ((allowed is { Count: 0 } || selection.Boxes == null))
            return result;

        var seen = new HashSet<(EntityUid Grid, int X, int Y)>();
        foreach (var sel in selection.Boxes.Take(MaxSelectionBoxes))
        {
            var radius = Math.Clamp(sel.Radius, 0, MaxRadius);
            var coords = GetCoordinates(sel.Center);
            if (!coords.IsValid(EntityManager))
                continue;

            var map = _transform.ToMapCoordinates(coords);
            if (!_mapManager.TryFindGridAt(map, out var gridUid, out var grid))
                continue;

            AddTilesInBox(gridUid, grid, map, radius, allowed, seen, result);

            if (_mapManager.GetMapEntityId(map.MapId) is not { Valid: true } boxMapUid)
                continue;

            for (var dz = -MaxZRange; dz <= MaxZRange; dz++)
            {
                if (dz == 0 || !_zLevels.TryMapOffset(boxMapUid, dz, out _, out var otherMapComp))
                    continue;

                var otherMap = new MapCoordinates(map.Position, otherMapComp.MapId);
                if (_mapManager.TryFindGridAt(otherMap, out var otherGridUid, out var otherGrid))
                    AddTilesInBox(otherGridUid, otherGrid, otherMap, radius, allowed, seen, result);
            }
        }

        return result;
    }

    private void AddTilesInBox(
        EntityUid gridUid,
        MapGridComponent grid,
        MapCoordinates center,
        int radius,
        HashSet<string>? allowed,
        HashSet<(EntityUid Grid, int X, int Y)> seen,
        List<BuildSelectionTile> result)
    {
        var centerTile = _map.TileIndicesFor(gridUid, grid, center);
        for (var x = centerTile.X - radius; x <= centerTile.X + radius; x++)
        {
            for (var y = centerTile.Y - radius; y <= centerTile.Y + radius; y++)
            {
                if (!seen.Add((gridUid, x, y)))
                    continue;

                if (!_map.TryGetTileRef(gridUid, grid, new Vector2i(x, y), out var tileRef) || tileRef.Tile.IsEmpty)
                    continue;

                if (!_tileDef.TryGetDefinition(tileRef.Tile.TypeId, out var tileDef) || tileDef is not ContentTileDefinition contentTile)
                    continue;

                if (allowed != null && !allowed.Contains(contentTile.ID))
                    continue;

                result.Add(new BuildSelectionTile
                {
                    Grid = GetNetEntity(gridUid),
                    X = x,
                    Y = y,
                    Tile = contentTile.ID,
                });
            }
        }
    }

    private HashSet<string> GetZBuildableTileIds()
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipe in _prototype.EnumeratePrototypes<ConstructionPrototype>())
        {
            if (!string.Equals(recipe.Spawnlist, "Tiles", StringComparison.Ordinal))
                continue;

            if (TryGetRecipeTarget(recipe, out var target) &&
                _prototype.TryIndex<EntityPrototype>(target, out var targetProto) &&
                targetProto.TryGetComponent<TileApplierComponent>(out var applier, _componentFactory))
                allowed.Add(applier.Tile);
        }

        return allowed;
    }

    private bool TryGetRecipeTarget(ConstructionPrototype recipe, out string targetProto)
    {
        targetProto = string.Empty;
        if (!_prototype.TryIndex(recipe.Graph, out ConstructionGraphPrototype? graph) ||
            !graph.Nodes.TryGetValue(recipe.TargetNode, out var targetNode))
            return false;

        var stack = new Stack<ConstructionGraphNode>();
        stack.Push(targetNode);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.Entity.GetId(null, null, new(EntityManager)) is { } entityId &&
                _prototype.HasIndex<EntityPrototype>(entityId))
            {
                targetProto = entityId;
                return true;
            }

            foreach (var edge in node.Edges)
            {
                if (graph.Nodes.TryGetValue(edge.Target, out var next))
                    stack.Push(next);
            }
        }

        return false;
    }

    private bool CanSave(EntityUid uid, NetUserId saver, bool mapperMode, bool includeLoose)
    {
        // Only world-placed entities (directly parented to the grid) — never things held in a hand or
        // inside a container, whose LocalPosition is in a different frame and would skew the anchor.
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || xform.ParentUid != grid)
            return false;

        if (MetaData(uid).EntityPrototype == null)
            return false;

        // Mapper mode: any structure counts no matter who built it (map-placed, admin-spawned, etc.). By default
        // only ANCHORED structures are captured (a clean building); the "include loose items" toggle also grabs
        // unanchored floor items. Mobs/players are always excluded - you save builds, not creatures.
        if (mapperMode)
        {
            if (HasComp<MobStateComponent>(uid))
                return false;
            return includeLoose || xform.Anchored;
        }

        if (!TryComp<PlayerBuiltComponent>(uid, out var built))
            return false;

        return _partners.CanInclude(saver, new NetUserId(built.BuilderUserId));
    }

    /// <summary>Convenience entry used by the test command: save a single box centred on the player.</summary>
    public void SaveAroundPlayer(ICommonSession session, string name, int radius)
    {
        if (session.AttachedEntity is not { } user)
            return;

        var selection = new BuildSelectionData
        {
            Boxes = new() { new BuildSelectionBox { Center = GetNetCoordinates(Transform(user).Coordinates), Radius = radius } },
            ManualAdds = new(),
            ManualRemoves = new(),
        };
        SaveBuild(session, name, selection);
    }

    private void SaveBuild(ICommonSession saver, string rawName, BuildSelectionData selection, BuildSaveMode mode = BuildSaveMode.Player, bool includeLoose = false, bool includeTiles = false)
    {
        if (saver.AttachedEntity is not { } user)
            return;

        var name = rawName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-no-name"), user, user);
            return;
        }
        // Bounded: the name flows into the file name; a multi-KB name would throw in the file open.
        if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        var entities = ResolveSelection(saver, selection, mode, includeLoose);
        var tiles = ResolveTiles(saver, selection, mode, includeTiles);
        entities = FilterSerializableEntities(entities, name, saver);
        if (entities.Count == 0 && tiles.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-empty"), user, user);
            return;
        }

        // Multi-z bookkeeping: each entity's level relative to the BASE level (the level holding the most of
        // the selection). Levels are world-aligned, so an entity's x/y offsets are valid on every level.
        var depthByEntity = new Dictionary<EntityUid, int>();
        var depthCounts = new Dictionary<int, int>();
        foreach (var uid in entities)
        {
            var d = Transform(uid).MapUid is { } m && TryComp<CMUZLevelMapComponent>(m, out var zm) ? zm.Depth : 0;
            depthByEntity[uid] = d;
            depthCounts[d] = depthCounts.GetValueOrDefault(d) + 1;
        }
        var tileDepths = new List<int>(tiles.Count);
        foreach (var tile in tiles)
        {
            var depth = 0;
            if (TryGetEntity(tile.Grid, out var gridUid) &&
                Transform(gridUid.Value).MapUid is { } mapUid &&
                TryComp<CMUZLevelMapComponent>(mapUid, out var zMap))
                depth = zMap.Depth;

            tileDepths.Add(depth);
            depthCounts[depth] = depthCounts.GetValueOrDefault(depth) + 1;
        }
        var baseDepth = depthCounts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
        var multiZ = depthCounts.Count > 1;

        // Bounds + anchor (in grid-local space, matching the serialized transforms) + source naming. Bounds
        // come from the BASE level's entities so the footprint matches what the ghost lays on your own level.
        var baseEntities = entities.Where(e => depthByEntity[e] == baseDepth).ToHashSet();
        var baseTilePositions = TileLocalPositions(tiles, tileDepths, baseDepth).ToList();
        var (boundsMin, boundsMax) = ComputeBounds(baseEntities.Count > 0 ? baseEntities : entities, baseTilePositions);
        var anchor = (boundsMin + boundsMax) / 2f;
        var relMin = boundsMin - anchor;
        var relMax = boundsMax - anchor;
        var sample = baseEntities.Count > 0 ? baseEntities.First() : (entities.Count > 0 ? entities.First() : EntityUid.Invalid);
        // Multi-z builds are tagged so the menu category makes their nature obvious.
        var gridName = (sample.Valid ? ResolveSourceName(sample) : ResolveTileSourceName(tiles.First())) + (multiZ ? " (Multi-Z)" : "");
        var sourceGrid = sample.Valid ? Transform(sample).GridUid : TryGetTileGrid(tiles.First());

        // Per-entity preview (prototype + offset from anchor + z-level offset) for the placement ghost.
        var preview = new SequenceDataNode();
        foreach (var uid in entities)
        {
            if (MetaData(uid).EntityPrototype is not { } proto)
                continue;

            var rel = Transform(uid).LocalPosition - anchor;
            var entry = new MappingDataNode();
            entry.Add("proto", new ValueDataNode(proto.ID));
            entry.Add("x", new ValueDataNode(rel.X.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("y", new ValueDataNode(rel.Y.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("rot", new ValueDataNode(Transform(uid).LocalRotation.Theta.ToString("R", CultureInfo.InvariantCulture)));
            // Record whether the entity was anchored, so placement restores the exact anchored state instead of
            // guessing from physics body type (mapper-mode saves can include props anchored without a Static body).
            entry.Add("anchored", new ValueDataNode(Transform(uid).Anchored ? "true" : "false"));
            // Which level (relative to the base) this entity belongs on; omitted when on the base level.
            var zOff = depthByEntity[uid] - baseDepth;
            if (zOff != 0)
                entry.Add("z", new ValueDataNode(zOff.ToString(CultureInfo.InvariantCulture)));
            preview.Add(entry);
        }

        var tilePreview = new SequenceDataNode();
        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            if (!TryGetTileLocalPosition(tile, out var local))
                continue;

            var rel = local - anchor;
            var entry = new MappingDataNode();
            entry.Add("tile", new ValueDataNode(tile.Tile));
            entry.Add("x", new ValueDataNode(rel.X.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("y", new ValueDataNode(rel.Y.ToString("R", CultureInfo.InvariantCulture)));
            var zOff = tileDepths[i] - baseDepth;
            if (zOff != 0)
                entry.Add("z", new ValueDataNode(zOff.ToString(CultureInfo.InvariantCulture)));
            tilePreview.Add(entry);
        }

        MappingDataNode buildData = new();
        if (entities.Count > 0)
        try
        {
            var opts = SerializationOptions.Default with
            {
                Category = FileCategory.Entity,
                ErrorOnOrphan = false,
                // Fail loudly (Rethrow, the default) if an entity can't serialize, rather than silently
                // dropping it — a silent drop previously produced empty (0-entity) build files.
            };
            (buildData, _) = _mapLoader.SerializeEntitiesRecursive(entities, opts);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to serialize saved build '{name}' for {saver.Name}: {e}");
            _popup.PopupEntity(Loc.GetString("saved-build-error-serialize"), user, user);
            return;
        }

        var root = new MappingDataNode();
        var meta = new MappingDataNode();
        meta.Add("version", new ValueDataNode(FormatVersion.ToString()));
        meta.Add("name", new ValueDataNode(name));
        meta.Add("author", new ValueDataNode(Name(user)));
        meta.Add("authorUserId", new ValueDataNode(saver.UserId.ToString()));
        meta.Add("source", new ValueDataNode(gridName));
        meta.Add("savedAt", new ValueDataNode(DateTime.UtcNow.ToString("o")));
        meta.Add("anchorX", new ValueDataNode(anchor.X.ToString("R", CultureInfo.InvariantCulture)));
        meta.Add("anchorY", new ValueDataNode(anchor.Y.ToString("R", CultureInfo.InvariantCulture)));
        meta.Add("relMinX", new ValueDataNode(relMin.X.ToString("R", CultureInfo.InvariantCulture)));
        meta.Add("relMinY", new ValueDataNode(relMin.Y.ToString("R", CultureInfo.InvariantCulture)));
        meta.Add("relMaxX", new ValueDataNode(relMax.X.ToString("R", CultureInfo.InvariantCulture)));
        meta.Add("relMaxY", new ValueDataNode(relMax.Y.ToString("R", CultureInfo.InvariantCulture)));
        if (sourceGrid != null)
            meta.Add("sourceGrid", new ValueDataNode(GetNetEntity(sourceGrid.Value).ToString()));
        meta.Add("entityCount", new ValueDataNode(entities.Count.ToString()));
        meta.Add("tileCount", new ValueDataNode(tilePreview.Sequence.Count.ToString()));
        meta.Add("multiZ", new ValueDataNode(multiZ ? "true" : "false"));
        meta.Add("preview", preview);
        meta.Add("tiles", tilePreview);
        root.Add("meta", meta);
        root.Add("build", buildData);

        var fileName = $"{Sanitize(saver.UserId.ToString())}__{Sanitize(name)}.build.yml";

        // Serialize to a string and hand it to the SAVER's client: saved builds are private local files.
        // The server keeps nothing, so no other player can ever list or read them.
        string yaml;
        try
        {
            using var writer = new StringWriter(CultureInfo.InvariantCulture);
            var stream = new YamlStream { new YamlDocument(root.ToYaml()) };
            stream.Save(new YamlMappingFix(new Emitter(writer)), false);
            yaml = writer.ToString();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to emit saved build '{name}' for {saver.Name}: {e}");
            _popup.PopupEntity(Loc.GetString("saved-build-error-write"), user, user);
            return;
        }

        RaiseNetworkEvent(new SavedBuildDataEvent { FileName = fileName, Yaml = yaml }, saver);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} (user {saver.UserId}) saved build '{name}' with {entities.Count} entities and {tilePreview.Sequence.Count} tiles (sent to client)");
        _popup.PopupEntity(
            Loc.GetString("saved-build-success", ("name", name), ("count", entities.Count), ("tiles", tilePreview.Sequence.Count)), user, user);
    }

    /// <summary>
    /// Grid-local bounding-box centre of the selection. This matches the frame the serializer stores each
    /// entity's <see cref="TransformComponent.LocalPosition"/> in, so placement can reposition by
    /// (savedLocal - anchor) without a world/grid frame mismatch.
    /// </summary>
    private HashSet<EntityUid> FilterSerializableEntities(HashSet<EntityUid> entities, string name, ICommonSession saver)
    {
        var safe = new HashSet<EntityUid>();
        var opts = SerializationOptions.Default with
        {
            Category = FileCategory.Entity,
            ErrorOnOrphan = false,
        };

        foreach (var uid in entities)
        {
            try
            {
                _mapLoader.SerializeEntitiesRecursive(new HashSet<EntityUid> { uid }, opts);
                safe.Add(uid);
            }
            catch (Exception e)
            {
                Log.Warning($"Skipping unserializable entity {ToPrettyString(uid)} while saving build '{name}' for {saver.Name}: {e.Message}");
            }
        }

        return safe;
    }

    private (Vector2 Min, Vector2 Max) ComputeBounds(HashSet<EntityUid> entities, IReadOnlyList<Vector2> tilePositions)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (var uid in entities)
        {
            var local = Transform(uid).LocalPosition;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        foreach (var local in tilePositions)
        {
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return (min, max);
    }

    private IEnumerable<Vector2> TileLocalPositions(List<BuildSelectionTile> tiles, List<int> depths, int depth)
    {
        for (var i = 0; i < tiles.Count; i++)
        {
            if (depths[i] != depth)
                continue;

            if (TryGetTileLocalPosition(tiles[i], out var local))
                yield return local;
        }
    }

    private bool TryGetTileLocalPosition(BuildSelectionTile tile, out Vector2 local)
    {
        local = default;
        if (!TryGetEntity(tile.Grid, out var gridUid) ||
            !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return false;

        local = _map.GridTileToLocal(gridUid.Value, grid, new Vector2i(tile.X, tile.Y)).Position;
        return true;
    }

    private EntityUid? TryGetTileGrid(BuildSelectionTile tile)
    {
        return TryGetEntity(tile.Grid, out var gridUid) ? gridUid.Value : null;
    }

    private string ResolveTileSourceName(BuildSelectionTile tile)
    {
        if (TryGetTileGrid(tile) is { } grid)
        {
            if (!string.IsNullOrWhiteSpace(Name(grid)))
                return Name(grid);

            if (Transform(grid).MapUid is { } map && !string.IsNullOrWhiteSpace(Name(map)))
                return Name(map);
        }

        return "Map";
    }

    /// <summary>Friendly name of the build's source grid (falls back to the map) for the menu category.</summary>
    private string ResolveSourceName(EntityUid sample)
    {
        var xform = Transform(sample);
        if (xform.GridUid is { } grid && !string.IsNullOrWhiteSpace(Name(grid)))
            return Name(grid);
        if (xform.MapUid is { } map && !string.IsNullOrWhiteSpace(Name(map)))
            return Name(map);
        return "Unknown";
    }

    private static string Sanitize(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        return sb.ToString();
    }
}
