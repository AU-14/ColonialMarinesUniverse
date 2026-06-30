// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Server.Administration.Managers;
using Content.Shared._AU14.SavedBuilds;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
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
using Robust.Shared.Serialization;
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
/// entity serializer, wraps it in a metadata header, and writes it to the user-data dir so players can
/// share the file externally. Selection resolution is echoed back to the client for highlighting.
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
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;

    public const string SaveDir = "/saved_builds";
    private const int MaxRadius = 5; // 11x11
    private const int FormatVersion = 1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBuildSelectionEvent>(OnRequestSelection);
        SubscribeNetworkEvent<RequestSaveBuildEvent>(OnRequestSave);
        SubscribeNetworkEvent<RequestSavedBuildListEvent>(OnRequestList);
        SubscribeNetworkEvent<RequestPlaceBuildEvent>(OnRequestPlace);
        SubscribeNetworkEvent<RequestDeleteSavedBuildEvent>(OnRequestDelete);
        SubscribeNetworkEvent<RequestOpenSavedBuildsFolderEvent>(OnRequestOpenFolder);
        SubscribeNetworkEvent<RequestRenameSavedBuildEvent>(OnRequestRename);
    }

    /// <summary>Renames a saved build (updates the metadata name and the file), if the requester is an admin or author.</summary>
    private void OnRequestRename(RequestRenameSavedBuildEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var user = session.AttachedEntity;

        var id = ev.Id;
        if (string.IsNullOrEmpty(id)
            || !id.EndsWith(".build.yml", StringComparison.Ordinal)
            || id.Contains('/') || id.Contains('\\') || id.Contains("..")
            || !id.Contains("__", StringComparison.Ordinal))
        {
            return;
        }

        var newName = ev.NewName.Trim();
        if (string.IsNullOrEmpty(newName))
            return;

        var dir = new ResPath(SaveDir).ToRootedPath();
        var path = dir / id;
        if (!_resource.UserData.Exists(path) || !_mapLoader.TryReadFile(path, out var root))
            return;

        var isAdmin = _adminManager.HasAdminFlag(session, AdminFlags.Spawn);
        if (!isAdmin && !IsAuthor(path, session.UserId))
        {
            if (user is { } notYours)
                _popup.PopupEntity(Loc.GetString("saved-build-error-delete-notyours"), notYours, notYours);
            return;
        }

        // Update the metadata name and write under a new file name (same author prefix), then drop the old file.
        if (root.TryGet<MappingDataNode>("meta", out var meta))
        {
            meta.Remove("name");
            meta.Add("name", new ValueDataNode(newName));
        }

        var prefix = id[..id.IndexOf("__", StringComparison.Ordinal)];
        var newPath = dir / $"{prefix}__{Sanitize(newName)}.build.yml";

        try
        {
            using (var writer = _resource.UserData.OpenWriteText(newPath))
            {
                var stream = new YamlStream { new YamlDocument(root.ToYaml()) };
                stream.Save(new YamlMappingFix(new Emitter(writer)), false);
            }

            if (newPath != path)
                _resource.UserData.Delete(path);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to rename saved build '{id}' -> '{newName}' for {session.Name}: {e}");
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{session.Name} (user {session.UserId}) renamed saved build '{id}' to '{newName}'");
        RaiseNetworkEvent(new SavedBuildListEvent { Builds = EnumerateSavedBuilds() }, session);
    }

    /// <summary>Deletes a saved-build file, if the requester is an admin or the build's own author.</summary>
    private void OnRequestDelete(RequestDeleteSavedBuildEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        var user = session.AttachedEntity;

        // Reject anything that isn't a plain build file name (no path traversal out of the save dir).
        var id = ev.Id;
        if (string.IsNullOrEmpty(id)
            || !id.EndsWith(".build.yml", StringComparison.Ordinal)
            || id.Contains('/') || id.Contains('\\') || id.Contains(".."))
        {
            return;
        }

        var dir = new ResPath(SaveDir).ToRootedPath();
        var path = dir / id;
        if (!_resource.UserData.Exists(path))
            return;

        // Permission: server admins (Spawn) may delete any build; otherwise only the original author.
        var isAdmin = _adminManager.HasAdminFlag(session, AdminFlags.Spawn);
        if (!isAdmin && !IsAuthor(path, session.UserId))
        {
            if (user is { } ent)
                _popup.PopupEntity(Loc.GetString("saved-build-error-delete-notyours"), ent, ent);
            return;
        }

        try
        {
            _resource.UserData.Delete(path);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to delete saved build '{id}' for {session.Name}: {e}");
            if (user is { } ent)
                _popup.PopupEntity(Loc.GetString("saved-build-error-delete"), ent, ent);
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{session.Name} (user {session.UserId}) deleted saved build '{id}'");
        if (user is { } popupEnt)
            _popup.PopupEntity(Loc.GetString("saved-build-deleted"), popupEnt, popupEnt);

        // Refresh the requester's list so the deleted build disappears from the menu immediately.
        RaiseNetworkEvent(new SavedBuildListEvent { Builds = EnumerateSavedBuilds() }, session);
    }

    /// <summary>Opens the saved-builds folder in the host's OS file explorer (admin only; localhost host).</summary>
    private void OnRequestOpenFolder(RequestOpenSavedBuildsFolderEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_adminManager.HasAdminFlag(session, AdminFlags.Spawn))
            return;

        var dir = new ResPath(SaveDir).ToRootedPath();
        try
        {
            _resource.UserData.CreateDir(dir);
            _resource.UserData.OpenOsWindow(dir);
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to open saved-builds folder for {session.Name}: {e}");
        }
    }

    /// <summary>True if the saved-build file at <paramref name="path"/> records <paramref name="user"/> as its author.</summary>
    private bool IsAuthor(ResPath path, NetUserId user)
    {
        if (!_mapLoader.TryReadFile(path, out var root) || !root.TryGet<MappingDataNode>("meta", out var meta))
            return false;

        return Guid.TryParse(MetaString(meta, "authorUserId"), out var author) && new NetUserId(author) == user;
    }

    private void OnRequestPlace(RequestPlaceBuildEvent ev, EntitySessionEventArgs args)
    {
        PlaceBuild(args.SenderSession, ev.Id, GetCoordinates(ev.Target), new Angle(ev.Rotation), ev.AtOriginal);
    }

    /// <summary>
    /// Loads a saved build and places it on the grid at <paramref name="target"/>, rotated by
    /// <paramref name="rotation"/>. Entities load as orphans (the source grid wasn't serialized), so each
    /// root is repositioned by (savedLocal - anchor) rotated, then re-parented/anchored to the target grid.
    /// NOTE (Phase 4a): this currently spawns the build for free; material cost is Phase 4c.
    /// </summary>
    public void PlaceBuild(ICommonSession session, string id, EntityCoordinates target, Angle rotation, bool atOriginal = false)
    {
        if (session.AttachedEntity is not { } user || string.IsNullOrEmpty(id))
            return;

        // Instant, free placement is the privileged version: admins (Spawn) or mappers (Mapping). Non-privileged
        // players use client-side construction ghosts instead.
        if (!_adminManager.HasAdminFlag(session, AdminFlags.Spawn) && !_adminManager.HasAdminFlag(session, AdminFlags.Mapping))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-notadmin"), user, user);
            return;
        }

        var path = new ResPath(SaveDir).ToRootedPath() / id;
        if (!_resource.UserData.Exists(path)
            || !_mapLoader.TryReadFile(path, out var root)
            || !root.TryGet<MappingDataNode>("build", out var buildNode))
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
        var targetLocal = _transform.ToCoordinates(gridUid, targetMap).Position;

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

        // After the merge the build's root entities are parented to the target map.
        var roots = result.Entities
            .Where(e => EntityManager.EntityExists(e) && Transform(e).ParentUid == targetMapUid)
            .ToList();

        // The serialized anchored flag is lost in the nullspace/map phase, so we restore it from the saved
        // preview (keyed by prototype + quarter-tile offset). Only entries that actually recorded "anchored"
        // are used; older saves without it fall back to the physics-body heuristic below, unchanged.
        var anchoredByKey = ReadAnchoredIntent(root);

        foreach (var rootEnt in roots)
        {
            var xform = Transform(rootEnt);
            var savedLocal = xform.LocalPosition; // map-local == original grid-local (merge offset is zero)
            var savedRot = xform.LocalRotation;

            var rel = rotation.RotateVec(savedLocal - anchor);
            _transform.SetCoordinates(rootEnt, new EntityCoordinates(gridUid, targetLocal + rel));
            _transform.SetLocalRotation(rootEnt, savedRot + rotation);

            // Restore the original anchored state. Prefer the recorded intent (handles props anchored without a
            // Static physics body - the mapper-mode case); fall back to "Static body => anchored" for old saves.
            var relSave = savedLocal - anchor;
            var protoId = MetaData(rootEnt).EntityPrototype?.ID ?? string.Empty;
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
            $"{ToPrettyString(user):player} (user {session.UserId}) placed saved build '{id}' ({roots.Count} roots) at {targetMap}");
        _popup.PopupEntity(Loc.GetString("saved-build-placed", ("count", roots.Count)), user, user);
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

    private void OnRequestList(RequestSavedBuildListEvent ev, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(new SavedBuildListEvent { Builds = EnumerateSavedBuilds() }, args.SenderSession);
    }

    /// <summary>Reads the metadata header of every saved-build file in the save directory.</summary>
    public List<SavedBuildInfo> EnumerateSavedBuilds()
    {
        var result = new List<SavedBuildInfo>();
        var dir = new ResPath(SaveDir).ToRootedPath();
        if (!_resource.UserData.Exists(dir))
            return result;

        foreach (var entry in _resource.UserData.DirectoryEntries(dir))
        {
            if (!entry.EndsWith(".build.yml", StringComparison.Ordinal))
                continue;

            var path = dir / entry;
            if (!_mapLoader.TryReadFile(path, out var root))
                continue;

            if (!root.TryGet<MappingDataNode>("meta", out var meta))
                continue;

            int.TryParse(MetaString(meta, "entityCount"), out var count);
            NetEntity.TryParse(MetaString(meta, "sourceGrid"), out var sourceGrid);
            result.Add(new SavedBuildInfo
            {
                Id = entry,
                Name = MetaString(meta, "name"),
                Source = MetaString(meta, "source"),
                Author = MetaString(meta, "author"),
                EntityCount = count,
                RelMinX = MetaFloat(meta, "relMinX"),
                RelMinY = MetaFloat(meta, "relMinY"),
                RelMaxX = MetaFloat(meta, "relMaxX"),
                RelMaxY = MetaFloat(meta, "relMaxY"),
                Preview = ReadPreview(meta),
                SourceGrid = sourceGrid,
                AnchorX = MetaFloat(meta, "anchorX"),
                AnchorY = MetaFloat(meta, "anchorY"),
            });
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.InvariantCultureIgnoreCase));
        return result;
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

    private static List<BuildPreviewEntity> ReadPreview(MappingDataNode meta)
    {
        var list = new List<BuildPreviewEntity>();
        if (!meta.TryGet<SequenceDataNode>("preview", out var seq))
            return list;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode m)
                continue;

            list.Add(new BuildPreviewEntity
            {
                Proto = MetaString(m, "proto"),
                X = MetaFloat(m, "x"),
                Y = MetaFloat(m, "y"),
                Rot = MetaFloat(m, "rot"),
            });
        }

        return list;
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
        }, args.SenderSession);
    }

    private void OnRequestSave(RequestSaveBuildEvent ev, EntitySessionEventArgs args)
    {
        SaveBuild(args.SenderSession, ev.Name, ev.Selection, ev.Mode, ev.IncludeLoose);
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
            foreach (var sel in selection.Boxes)
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
                foreach (var uid in found)
                {
                    if (CanSave(uid, saverId, mapperMode, includeLoose))
                        result.Add(uid);
                }
            }
        }

        if (selection.ManualAdds != null)
        {
            foreach (var net in selection.ManualAdds)
            {
                if (TryGetEntity(net, out var uid) && CanSave(uid.Value, saverId, mapperMode, includeLoose))
                    result.Add(uid.Value);
            }
        }

        if (selection.ManualRemoves != null)
        {
            foreach (var net in selection.ManualRemoves)
            {
                if (TryGetEntity(net, out var uid))
                    result.Remove(uid.Value);
            }
        }

        return result;
    }

    private bool CanSave(EntityUid uid, NetUserId saver, bool mapperMode, bool includeLoose)
    {
        // Only world-placed entities (directly parented to the grid) — never things held in a hand or
        // inside a container, whose LocalPosition is in a different frame and would skew the anchor.
        var xform = Transform(uid);
        if (xform.GridUid is not { } grid || xform.ParentUid != grid)
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

    private void SaveBuild(ICommonSession saver, string rawName, BuildSelectionData selection, BuildSaveMode mode = BuildSaveMode.Player, bool includeLoose = false)
    {
        if (saver.AttachedEntity is not { } user)
            return;

        var name = rawName.Trim();
        if (string.IsNullOrEmpty(name))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-no-name"), user, user);
            return;
        }

        var entities = ResolveSelection(saver, selection, mode, includeLoose);
        if (entities.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-empty"), user, user);
            return;
        }

        // Bounds + anchor (in grid-local space, matching the serialized transforms) + source naming.
        var (boundsMin, boundsMax) = ComputeBounds(entities);
        var anchor = (boundsMin + boundsMax) / 2f;
        var relMin = boundsMin - anchor;
        var relMax = boundsMax - anchor;
        var sample = entities.First();
        var gridName = ResolveSourceName(sample);
        var sourceGrid = Transform(sample).GridUid;

        // Per-entity preview (prototype + offset from anchor) for the placement ghost.
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
            preview.Add(entry);
        }

        MappingDataNode buildData;
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
        meta.Add("preview", preview);
        root.Add("meta", meta);
        root.Add("build", buildData);

        var fileName = $"{Sanitize(saver.UserId.ToString())}__{Sanitize(name)}.build.yml";
        var path = new ResPath(SaveDir).ToRootedPath() / fileName;

        try
        {
            _resource.UserData.CreateDir(path.Directory);
            using var writer = _resource.UserData.OpenWriteText(path);
            var stream = new YamlStream { new YamlDocument(root.ToYaml()) };
            stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to write saved build '{name}' to {path}: {e}");
            _popup.PopupEntity(Loc.GetString("saved-build-error-write"), user, user);
            return;
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(user):player} (user {saver.UserId}) saved build '{name}' with {entities.Count} entities to {path}");
        _popup.PopupEntity(
            Loc.GetString("saved-build-success", ("name", name), ("count", entities.Count)), user, user);
    }

    /// <summary>
    /// Grid-local bounding-box centre of the selection. This matches the frame the serializer stores each
    /// entity's <see cref="TransformComponent.LocalPosition"/> in, so placement can reposition by
    /// (savedLocal - anchor) without a world/grid frame mismatch.
    /// </summary>
    private (Vector2 Min, Vector2 Max) ComputeBounds(HashSet<EntityUid> entities)
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        foreach (var uid in entities)
        {
            var local = Transform(uid).LocalPosition;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return (min, max);
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
