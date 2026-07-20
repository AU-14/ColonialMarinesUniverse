// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
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
using Content.Shared.Ghost;
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
    [Dependency] private ZStairSystem _zStairs = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private ITileDefinitionManager _tileDef = default!;

    /// <summary>Saved builds part-way through placement, advanced a slice per tick by <see cref="Update"/>.</summary>
    private readonly List<PendingPlacement> _pendingPlacements = new();

    /// <summary>Component type -> YAML keys of its required data fields. Reflection is done once per type.</summary>
    private readonly Dictionary<Type, string[]> _requiredDataFields = new();

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
    // 🔧 TUNABLE: roots repositioned per tick when placing a saved build. A 300-entity build doing all of
    // its re-parenting, rotating and anchoring in one tick was enough to take a server down; slicing it
    // trades a visible fraction of a second on huge builds for not stalling everyone else.
    private const int PlacementRootsPerTick = 25;
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

        // Drop component entries the deserializer would choke on before handing the node to the loader.
        // Applied on LOAD (not just on save) so builds saved before this existed are still placeable.
        var stripped = StripUnreadableComponents(buildNode);
        if (stripped > 0)
            Log.Info($"Saved build '{id}': dropped {stripped} incomplete component entries that would not deserialize.");

        if (!HasValidZBounds(root))
        {
            Log.Warning($"{session.Name} sent saved build '{id}' with a malformed or out-of-range z offset.");
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
        // Shift applied to the whole build at load time so it materialises at the cursor rather than at the
        // saved grid's local origin. Subtracted back out below to recover serializer-frame positions.
        var loadOffset = targetMap.Position - anchor;
        var savedTiles = ReadSavedTiles(root);
        if (!TryPlanSavedTiles(savedTiles, targetMapUid, gridUid, targetMap, rotation, out var tilePlan) ||
            !PreflightEntityLevels(root, targetMapUid, gridUid, targetMap, rotation))
        {
            _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
            return;
        }

        var entityCount = ReadMetaInt(root, "entityCount");

        if (entityCount <= 0)
        {
            int tileOnlyCount;
            try
            {
                tileOnlyCount = ApplyPlannedTiles(tilePlan);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply saved-build tiles '{id}' for {session.Name}: {e}");
                _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);
                return;
            }

            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} (user {session.UserId}) placed saved build '{id}' ({tileOnlyCount} tiles) at {targetMap}");
            _popup.PopupEntity(Loc.GetString("saved-build-placed", ("count", tileOnlyCount)), user, user);
            return;
        }

        LoadResult result;
        _zStairs.BeginDeferredSetup();
        try
        {
            // Merge onto the target map so the entities are properly map-initialized (collisions, etc.);
            // they end up parented to the map, and we then re-parent each root onto the grid below.
            //
            // The serialized transforms are GRID-LOCAL to the grid the build was saved on, so loading them
            // unshifted makes the engine read them as MAP coordinates. For a build saved on an admin/custom
            // grid those coordinates sit near the grid origin, which dumped the whole build on top of map
            // 0,0 - every entity map-initialized in one pile before being teleported out again (the "spawns
            // at 0,0" bug, and a large part of why a big build could take the server down). Shifting the
            // load by (target - anchor) lands each root at its final spot immediately instead.
            var loadOpts = MapLoadOptions.Default with
            {
                MergeMap = targetMap.MapId,
                Offset = loadOffset,
            };
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
        finally
        {
            _zStairs.EndDeferredSetup();
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
        var savedOffsets = ReadSavedEntityOffsets(root, out var savedOffsetsByProto);

        // Multi-z: per-entry z-level offsets from the preview. Queued per key because two identical entities
        // can share the same x/y on DIFFERENT levels (that's exactly what multi-z builds do).
        var zByKey = ReadZOffsets(root);

        var job = new PendingPlacement
        {
            Session = session,
            User = user,
            Id = id,
            Result = result,
            Roots = roots,
            SavedOffsets = savedOffsets,
            SavedOffsetsByProto = savedOffsetsByProto,
            ZByKey = zByKey,
            AnchoredByKey = anchoredByKey,
            LoadOffset = loadOffset,
            Anchor = anchor,
            TargetMap = targetMap,
            TargetMapUid = targetMapUid,
            GridUid = gridUid,
            Rotation = rotation,
            TilePlan = tilePlan,
        };

        // Small builds finish in the same tick they were requested, exactly as before - the multi-tick path
        // only earns its keep once the repositioning work is big enough to blow a tick budget on its own.
        if (roots.Count <= PlacementRootsPerTick)
        {
            AdvancePlacement(job, roots.Count);
            return;
        }

        _pendingPlacements.Add(job);
    }

    /// <summary>
    /// Drains queued placements a slice at a time. A large saved build (hundreds of roots, each one a
    /// re-parent + rotate + anchor) does far too much work to run in a single tick without stalling the
    /// server, so the reposition pass is spread across ticks and the tiles/stairs/logging only commit
    /// once the last root has landed.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        for (var i = _pendingPlacements.Count - 1; i >= 0; i--)
        {
            if (AdvancePlacement(_pendingPlacements[i], PlacementRootsPerTick))
                _pendingPlacements.RemoveAt(i);
        }
    }

    /// <summary>Places up to <paramref name="budget"/> more roots. Returns true when the job is finished
    /// (successfully or not) and should be dropped from the queue.</summary>
    private bool AdvancePlacement(PendingPlacement job, int budget)
    {
        var user = job.User;
        try
        {
            var end = Math.Min(job.Index + budget, job.Roots.Count);
            for (; job.Index < end; job.Index++)
            {
                var rootEnt = job.Roots[job.Index];
                // Entities can be deleted between slices (admin cleanup, a cave-in, gibbing).
                if (!EntityManager.EntityExists(rootEnt))
                    continue;

                PlaceOneRoot(job, rootEnt);
            }

            if (job.Index < job.Roots.Count)
                return false;

            // Tiles commit only after every entity has loaded and reached its final transform.
            var placedTiles = ApplyPlannedTiles(job.TilePlan);

            // Anchoring happens LAST, after the build's own floor exists. AddToSnapGridCell refuses an empty
            // tile, so anchoring during the reposition pass silently failed for anything standing on floor
            // this build had not laid yet - which is why part of a build arrived wrenched and part did not.
            // (An occupied tile is fine; the engine happily stacks anchored entities.)
            var anchorWanted = 0;
            foreach (var (ent, anchored) in job.AnchorIntents)
            {
                if (!EntityManager.EntityExists(ent))
                    continue;

                var xform = Transform(ent);
                if (anchored)
                {
                    anchorWanted++;
                    if (!xform.Anchored && !_transform.AnchorEntity(ent, xform))
                    {
                        job.AnchorFailures++;
                        Log.Warning($"Saved build '{job.Id}': could not anchor {ToPrettyString(ent)} at {_transform.GetMapCoordinates(ent)} - grid={xform.GridUid}, tile empty or off-grid.");
                    }
                }
                else if (xform.Anchored)
                {
                    // Force the saved state both ways: an entity saved loose must not arrive anchored just
                    // because its prototype or physics body says otherwise.
                    _transform.Unanchor(ent, xform);
                }
            }

            // Saved roots were map-initialized before relocation, while stair package setup was deliberately
            // deferred. Rebuild each package now at its final level and coordinates.
            foreach (var rootEnt in job.Roots)
            {
                if (EntityManager.EntityExists(rootEnt) && TryComp<ZStairComponent>(rootEnt, out var stair))
                    _zStairs.EnsureSetup((rootEnt, stair));
            }

            // NOTE: this is effectively the ADMIN/free placement (instant, free, keeps container contents).
            // TODO (player costed version): strip container contents and consume materials via a ghost build.

            // One line that says whether the saved state actually survived the round trip: how many roots
            // failed to match a preview entry (so their rotation/anchored state was guessed) and how many
            // anchors were refused. Both should be 0; anything else points straight at the culprit.
            Log.Info($"Saved build '{job.Id}': {job.Roots.Count} roots, {placedTiles} tiles, {anchorWanted} wanted anchoring, {job.AnchorFailures} anchor failures, {job.UnmatchedRoots} roots unmatched to preview.");

            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):player} (user {job.Session.UserId}) placed saved build '{job.Id}' ({job.Roots.Count} roots, {placedTiles} tiles) at {job.TargetMap}");

            if (EntityManager.EntityExists(user))
                _popup.PopupEntity(Loc.GetString("saved-build-placed", ("count", job.Roots.Count + placedTiles)), user, user);

            return true;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to commit saved build '{job.Id}' for {job.Session.Name}; deleting staged entities: {e}");
            _mapLoader.Delete(job.Result);
            if (EntityManager.EntityExists(user))
                _popup.PopupEntity(Loc.GetString("saved-build-error-load"), user, user);

            return true;
        }
    }

    private void PlaceOneRoot(PendingPlacement job, EntityUid rootEnt)
    {
        // WORLD-frame math throughout, not raw transform locals: a map's transform is identity, so for
        // map-parented roots world == the original saved grid-local (merge offset is zero). For roots the
        // loader parented onto a grid, world position resolves through that grid's own offset/rotation.
        var savedWorld = _transform.GetWorldPosition(rootEnt);
        var savedRot = _transform.GetWorldRotation(rootEnt);
        var protoId = MetaData(rootEnt).EntityPrototype?.ID ?? string.Empty;
        // Undo the load shift to get back to the serializer-frame position the preview was keyed on.
        // World frame (not LocalPosition) so roots the loader parented onto a grid resolve the same
        // way as map-parented ones - a grid-frame local would miss the key and silently fall through
        // to the offset fallback below, which is exactly how a whole build ends up misplaced.
        var savedSerialized = savedWorld - job.LoadOffset;
        var savedPlacement = TakeSavedEntityOffset(job.SavedOffsets, job.SavedOffsetsByProto, protoId, savedSerialized);
        if (savedPlacement == null)
            job.UnmatchedRoots++;
        var relSave = savedPlacement?.Offset ?? savedSerialized - job.Anchor;
        var relativeRotation = savedPlacement?.Rotation ?? savedRot;
        var desired = new MapCoordinates(job.TargetMap.Position + job.Rotation.RotateVec(relSave), job.TargetMap.MapId);

        var placeGrid = job.GridUid;
        var placeMapId = job.TargetMap.MapId;
        var zOff = savedPlacement?.ZOffset ?? TakeZOffset(job.ZByKey, protoId, relSave);
        if (zOff is { } levelOffset && levelOffset != 0)
        {
            if (!TryResolveLevel(job.TargetMapUid, job.GridUid, levelOffset, desired.Position, out var levelGrid, out var levelMapId))
                throw new InvalidOperationException($"Preflighted z-level {levelOffset} could not be resolved during placement.");

            placeGrid = levelGrid;
            placeMapId = levelMapId;
        }

        var desiredOnLevel = new MapCoordinates(desired.Position, placeMapId);
        _transform.SetCoordinates(rootEnt, new EntityCoordinates(placeGrid, _transform.ToCoordinates(placeGrid, desiredOnLevel).Position));
        _transform.SetWorldRotation(rootEnt, relativeRotation + job.Rotation);

        // Prefer the anchored flag carried on the matched preview entry: it shares the key that already
        // resolved this entity's position, so it cannot drift the way the separate offset-keyed lookup can.
        // That lookup stays as the fallback for saves written before the flag moved onto the entry, and the
        // physics-body guess is the last resort for saves older still.
        var wasAnchored = savedPlacement?.Anchored
                          ?? (job.AnchoredByKey.TryGetValue((protoId, QuantizeOffset(relSave.X), QuantizeOffset(relSave.Y)), out var recorded)
                              ? recorded
                              : TryComp<PhysicsComponent>(rootEnt, out var body) && body.BodyType == BodyType.Static);

        // Deferred to the anchoring pass, once the build's tiles are down (see AdvancePlacement).
        job.AnchorIntents.Add((rootEnt, wasAnchored));

        _playerBuilt.MarkBuilt(rootEnt, job.User);
    }

    /// <summary>One saved build mid-placement: the staged entities plus everything needed to keep
    /// repositioning them on later ticks.</summary>
    private sealed class PendingPlacement
    {
        public required ICommonSession Session;
        public required EntityUid User;
        public required string Id;
        public required LoadResult Result;
        public required List<EntityUid> Roots;
        public required Dictionary<(string, int, int), Queue<SavedEntityPlacement>> SavedOffsets;
        public required Dictionary<string, Queue<SavedEntityPlacement>> SavedOffsetsByProto;
        public required Dictionary<(string, int, int), Queue<int>> ZByKey;
        public required Dictionary<(string, int, int), bool> AnchoredByKey;
        public required Vector2 LoadOffset;
        public required Vector2 Anchor;
        public required MapCoordinates TargetMap;
        public required EntityUid TargetMapUid;
        public required EntityUid GridUid;
        public required Angle Rotation;
        public required List<PlannedTile> TilePlan;

        /// <summary>How many roots have already been repositioned.</summary>
        public int Index;

        /// <summary>Saved anchored state per repositioned root, applied after the tiles are down.</summary>
        public readonly List<(EntityUid Ent, bool Anchored)> AnchorIntents = new();

        /// <summary>Diagnostics for the placement summary log.</summary>
        public int UnmatchedRoots;
        public int AnchorFailures;
    }

    /// <summary>
    /// Removes component entries that the deserializer would reject, so one bad component cannot abort the
    /// whole build load.
    ///
    /// The entity serializer writes each component as a DELTA against its prototype, but a field marked
    /// <c>[DataField(required: true)]</c> must be present for the mapping to read back at all. A drink whose
    /// only divergence is a runtime <c>nextAttack</c> therefore serialises as a MeleeWeapon component with no
    /// <c>damage</c> field - valid to write, impossible to read. That threw mid-load, and the engine's own
    /// cleanup then threw on top of it ("anchored but has no parent?"), leaving half-initialised entities
    /// alive in the world; a microwave among them took the server down on the next power tick.
    ///
    /// Dropping the entry means the entity keeps its prototype's values for that component, which is what the
    /// delta was implicitly relying on for every field it left out anyway.
    /// </summary>
    private int StripUnreadableComponents(MappingDataNode buildNode)
    {
        if (!buildNode.TryGet<SequenceDataNode>("entities", out var protoGroups))
            return 0;

        var stripped = 0;
        foreach (var groupNode in protoGroups)
        {
            if (groupNode is not MappingDataNode group ||
                !group.TryGet<SequenceDataNode>("entities", out var entities))
                continue;

            foreach (var entityNode in entities)
            {
                if (entityNode is not MappingDataNode entity ||
                    !entity.TryGet<SequenceDataNode>("components", out var components))
                    continue;

                for (var i = components.Count - 1; i >= 0; i--)
                {
                    if (components[i] is not MappingDataNode comp ||
                        !comp.TryGet<ValueDataNode>("type", out var typeNode))
                        continue;

                    if (!IsComponentMappingReadable(typeNode.Value, comp))
                    {
                        components.RemoveAt(i);
                        stripped++;
                    }
                }
            }
        }

        return stripped;
    }

    /// <summary>True when every required data field of <paramref name="compName"/> is present in the written
    /// mapping. Unknown component names are left alone - the loader has its own handling for those.</summary>
    private bool IsComponentMappingReadable(string compName, MappingDataNode comp)
    {
        if (!_componentFactory.TryGetRegistration(compName, out var registration))
            return true;

        foreach (var field in GetRequiredDataFields(registration.Type))
        {
            if (!comp.Has(field))
                return false;
        }

        return true;
    }

    /// <summary>YAML keys of a component's required data fields, walked once per type and cached.</summary>
    private string[] GetRequiredDataFields(Type type)
    {
        if (_requiredDataFields.TryGetValue(type, out var cached))
            return cached;

        var required = new List<string>();
        for (var t = type; t != null && t != typeof(object); t = t.BaseType)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach (var member in t.GetFields(flags).Cast<MemberInfo>().Concat(t.GetProperties(flags)))
            {
                if (member.GetCustomAttribute<DataFieldAttribute>() is not { Required: true } attr)
                    continue;

                // Tag wins, else the member name with a lowercased first letter (the serializer's rule).
                required.Add(attr.Tag ?? char.ToLowerInvariant(member.Name[0]) + member.Name[1..]);
            }
        }

        var result = required.ToArray();
        _requiredDataFields[type] = result;
        return result;
    }

    /// <summary><paramref name="Anchored"/> is null for saves written before the flag was recorded on the
    /// preview entry itself; those fall back to the separate offset-keyed anchored lookup.</summary>
    /// <summary>Mapper mode is open to mappers and to admins: Spawn already grants free placement of anything,
    /// so withholding the mapper SELECTION ruleset from them bought no safety, only friction.</summary>
    private bool CanUseMapperMode(ICommonSession session) =>
        _adminManager.HasAdminFlag(session, AdminFlags.Mapping) ||
        _adminManager.HasAdminFlag(session, AdminFlags.Spawn);

    private readonly record struct SavedEntityPlacement(Vector2 Offset, Angle Rotation, int ZOffset, bool? Anchored);

    /// <summary>
    /// New-format saves record both the serializer's root-local position and the world-aligned placement offset.
    /// This lookup reconnects a loaded root to that offset without assuming that every z-level grid has the same
    /// local origin. Older files have no savedX/savedY fields and continue through the original fallback path.
    /// </summary>
    private Dictionary<(string, int, int), Queue<SavedEntityPlacement>> ReadSavedEntityOffsets(
        MappingDataNode root,
        out Dictionary<string, Queue<SavedEntityPlacement>> byProto)
    {
        var map = new Dictionary<(string, int, int), Queue<SavedEntityPlacement>>();
        byProto = new Dictionary<string, Queue<SavedEntityPlacement>>();
        if (!root.TryGet<MappingDataNode>("meta", out var meta) || !meta.TryGet<SequenceDataNode>("preview", out var seq))
            return map;

        foreach (var node in seq)
        {
            if (node is not MappingDataNode entry ||
                !entry.TryGet<ValueDataNode>("savedX", out var savedXNode) ||
                !entry.TryGet<ValueDataNode>("savedY", out var savedYNode) ||
                !float.TryParse(savedXNode.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var savedX) ||
                !float.TryParse(savedYNode.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var savedY))
                continue;

            var proto = MetaString(entry, "proto");
            var key = (proto, QuantizeOffset(savedX), QuantizeOffset(savedY));
            bool? anchored = entry.TryGet<ValueDataNode>("anchored", out var anchoredNode) &&
                             bool.TryParse(anchoredNode.Value, out var anchoredValue)
                ? anchoredValue
                : null;

            var placement = new SavedEntityPlacement(
                new Vector2(MetaFloat(entry, "x"), MetaFloat(entry, "y")),
                new Angle(MetaFloat(entry, "rot")),
                ReadInt(entry, "z"),
                anchored);
            if (!map.TryGetValue(key, out var queue))
                map[key] = queue = new Queue<SavedEntityPlacement>();
            queue.Enqueue(placement);

            if (!byProto.TryGetValue(proto, out var protoQueue))
                byProto[proto] = protoQueue = new Queue<SavedEntityPlacement>();
            protoQueue.Enqueue(placement);
        }

        return map;
    }

    private static SavedEntityPlacement? TakeSavedEntityOffset(
        Dictionary<(string, int, int), Queue<SavedEntityPlacement>> offsets,
        Dictionary<string, Queue<SavedEntityPlacement>> byProto,
        string proto,
        Vector2 savedLocal)
    {
        if (offsets.TryGetValue((proto, QuantizeOffset(savedLocal.X), QuantizeOffset(savedLocal.Y)), out var queue) &&
            queue.TryDequeue(out var placement))
            return placement;

        // Position key missed. Rather than drop to guessing anchored state from the physics body - which
        // reads Dynamic on a freshly loaded structure and therefore guesses "not anchored" for things that
        // were saved anchored - take the next unused entry recorded for this prototype. Identical prototypes
        // in one build share their anchored state in practice, so this recovers it even when the coordinate
        // key does not line up.
        return byProto.TryGetValue(proto, out var protoQueue) && protoQueue.TryDequeue(out var byProtoPlacement)
            ? byProtoPlacement
            : null;
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

        if (zOffset is < -MaxZRange or > MaxZRange)
            return false;

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

    /// <summary>
    /// Rejects malformed or out-of-range z values before any tile, map, or entity mutation occurs.
    /// Missing z fields belong to the original 2D format and are treated as zero.
    /// </summary>
    private static bool HasValidZBounds(MappingDataNode root)
    {
        if (!root.TryGet<MappingDataNode>("meta", out var meta))
            return true;

        return SequenceHasValidZ(meta, "preview") && SequenceHasValidZ(meta, "tiles");
    }

    private static bool SequenceHasValidZ(MappingDataNode meta, string key)
    {
        if (!meta.TryGet<SequenceDataNode>(key, out var sequence))
            return true;

        foreach (var node in sequence)
        {
            if (node is not MappingDataNode mapping)
                continue;

            var raw = MetaString(mapping, "z");
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z) ||
                z is < -MaxZRange or > MaxZRange)
            {
                return false;
            }
        }

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

    private readonly record struct PlannedTile(EntityUid Grid, Vector2i Indices, Tile Previous, Tile Desired);

    private bool TryPlanSavedTiles(
        List<BuildPreviewTile> tiles,
        EntityUid targetMapUid,
        EntityUid gridUid,
        MapCoordinates targetMap,
        Angle rotation,
        out List<PlannedTile> plan)
    {
        var planned = new Dictionary<(EntityUid Grid, Vector2i Indices), PlannedTile>();
        foreach (var tile in tiles)
        {
            if (!_prototype.TryIndex<ContentTileDefinition>(tile.Tile, out var tileDef))
            {
                plan = new();
                return false;
            }

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
                    plan = new();
                    return false;
                }
            }

            var desiredOnLevel = new MapCoordinates(desired.Position, placeMapId);
            if (!TryComp<MapGridComponent>(placeGrid, out var grid))
            {
                plan = new();
                return false;
            }

            var indices = _map.TileIndicesFor(placeGrid, grid, desiredOnLevel);
            var key = (placeGrid, indices);
            var previous = _map.TryGetTileRef(placeGrid, grid, indices, out var tileRef) ? tileRef.Tile : Tile.Empty;
            if (planned.TryGetValue(key, out var existing))
                previous = existing.Previous;
            planned[key] = new PlannedTile(placeGrid, indices, previous, new Tile(tileDef.TileId));
        }

        plan = planned.Values.ToList();
        return true;
    }

    private bool PreflightEntityLevels(
        MappingDataNode root,
        EntityUid targetMapUid,
        EntityUid gridUid,
        MapCoordinates targetMap,
        Angle rotation)
    {
        if (!root.TryGet<MappingDataNode>("meta", out var meta) ||
            !meta.TryGet<SequenceDataNode>("preview", out var preview))
            return true;

        foreach (var node in preview)
        {
            if (node is not MappingDataNode mapping)
                continue;

            var z = ReadInt(mapping, "z");
            if (z == 0)
                continue;

            var offset = new Vector2(MetaFloat(mapping, "x"), MetaFloat(mapping, "y"));
            var position = targetMap.Position + rotation.RotateVec(offset);
            if (!TryResolveLevel(targetMapUid, gridUid, z, position, out _, out _))
                return false;
        }

        return true;
    }

    private int ApplyPlannedTiles(List<PlannedTile> plan)
    {
        var applied = 0;
        try
        {
            foreach (var tile in plan)
            {
                if (!TryComp<MapGridComponent>(tile.Grid, out var grid))
                    throw new InvalidOperationException($"Saved-build target grid {tile.Grid} disappeared before commit.");

                _map.SetTile(tile.Grid, grid, tile.Indices, tile.Desired);
                applied++;
            }
        }
        catch
        {
            for (var i = applied - 1; i >= 0; i--)
            {
                var tile = plan[i];
                if (TryComp<MapGridComponent>(tile.Grid, out var grid))
                    _map.SetTile(tile.Grid, grid, tile.Indices, tile.Previous);
            }

            throw;
        }

        return applied;
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
        var resolved = ResolveSelection(args.SenderSession, ev.Selection, ev.Mode, ev.IncludeLoose, ev.IncludeMultiZ);
        RaiseNetworkEvent(new BuildSelectionResultEvent
        {
            Entities = resolved.Select(e => GetNetEntity(e)).ToList(),
            Tiles = ResolveTiles(args.SenderSession, ev.Selection, ev.Mode, ev.IncludeTiles, ev.IncludeMultiZ),
        }, args.SenderSession);
    }

    private void OnRequestSave(RequestSaveBuildEvent ev, EntitySessionEventArgs args)
    {
        SaveBuild(args.SenderSession, ev.Name, ev.Selection, ev.Mode, ev.IncludeLoose, ev.IncludeTiles, ev.IncludeMultiZ);
    }

    /// <summary>
    /// Resolves a selection descriptor to the concrete set of entities the given player may save. In Player/Admin
    /// mode that is anything they (or a build partner) built; in Mapper mode (requires Mapping or Spawn) it is
    /// ANY world structure/item regardless of who built it (map-placed, admin-spawned, etc.) minus mobs/players.
    /// The privileged mode is re-validated here against the caller's real flags, so a client can't spoof it.
    /// </summary>
    public HashSet<EntityUid> ResolveSelection(ICommonSession saver, BuildSelectionData selection, BuildSaveMode mode = BuildSaveMode.Player, bool includeLoose = false, bool includeMultiZ = false)
    {
        var result = new HashSet<EntityUid>();
        var saverId = saver.UserId;

        // Mapper mode only takes effect if the caller actually holds the flags for it; otherwise fall back to
        // the normal player-built rules (no error - the dropdown just shouldn't have offered it to them).
        var mapperMode = mode == BuildSaveMode.Mapper && CanUseMapperMode(saver);

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
                // Opt-in, because scanning other levels unconditionally swept in structures directly above
                // or below the selection that the builder never intended to capture.
                if (includeMultiZ && _mapManager.GetMapEntityId(map.MapId) is { Valid: true } boxMapUid)
                {
                    for (var dz = -MaxZRange; dz <= MaxZRange; dz++)
                    {
                        if (dz == 0 || !_zLevels.TryMapOffset(boxMapUid, dz, out _, out var otherMapComp))
                            continue;

                        _lookup.GetEntitiesIntersecting(otherMapComp.MapId, box, found);
                        // Sparse/generated z-level grids can have incomplete broadphase bounds around freshly
                        // built structures. CanSave only accepts direct grid children anyway, so supplement the
                        // physics lookup with those roots and select them by their world position.
                        AddGridChildrenInBox(otherMapComp.MapId, box, found);
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

    private void AddGridChildrenInBox(MapId mapId, Box2 box, HashSet<EntityUid> found)
    {
        foreach (var grid in _mapManager.GetAllGrids(mapId))
        {
            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (box.Contains(_transform.GetWorldPosition(child)))
                    found.Add(child);
            }
        }
    }

    private List<BuildSelectionTile> ResolveTiles(ICommonSession saver, BuildSelectionData selection, BuildSaveMode mode, bool includeTiles, bool includeMultiZ = false)
    {
        var result = new List<BuildSelectionTile>();
        if (!includeTiles)
            return result;

        var anyTile =
            mode == BuildSaveMode.Admin && _adminManager.HasAdminFlag(saver, AdminFlags.Spawn) ||
            mode == BuildSaveMode.Mapper && CanUseMapperMode(saver);

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

            // Same opt-in as the entity scan above: only reach onto other levels when asked to.
            if (!includeMultiZ)
                continue;

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

        // Creatures and observers are never part of a build, in ANY mode. MobState covers living mobs;
        // Ghost covers both ghosts and aghosts (an aghost is a ghost with extra powers, same component);
        // Actor covers anything a client is currently attached to, which catches observer shells and any
        // other player-controlled entity that carries neither of the first two.
        if (HasComp<MobStateComponent>(uid) || HasComp<GhostComponent>(uid) || HasComp<ActorComponent>(uid))
            return false;

        // Mapper mode: any structure counts no matter who built it (map-placed, admin-spawned, etc.). By default
        // only ANCHORED structures are captured (a clean building); the "include loose items" toggle also grabs
        // unanchored floor items.
        if (mapperMode)
            return includeLoose || xform.Anchored;

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

    private void SaveBuild(ICommonSession saver, string rawName, BuildSelectionData selection, BuildSaveMode mode = BuildSaveMode.Player, bool includeLoose = false, bool includeTiles = false, bool includeMultiZ = false)
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

        var entities = ResolveSelection(saver, selection, mode, includeLoose, includeMultiZ);
        var tiles = ResolveTiles(saver, selection, mode, includeTiles, includeMultiZ);
        ExcludeGeneratedStairParts(entities, tiles);
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
        var baseTileIndex = tileDepths.FindIndex(depth => depth == baseDepth);
        var sourceGrid = baseEntities.Count > 0
            ? Transform(baseEntities.First()).GridUid
            : baseTileIndex >= 0
                ? TryGetTileGrid(tiles[baseTileIndex])
                : sample.Valid
                    ? Transform(sample).GridUid
                    : null;
        var anchorWorld = sourceGrid is { } source
            ? _transform.ToMapCoordinates(new EntityCoordinates(source, anchor)).Position
            : anchor;

        // Per-entity preview (prototype + offset from anchor + z-level offset) for the placement ghost.
        var preview = new SequenceDataNode();
        foreach (var uid in entities)
        {
            if (MetaData(uid).EntityPrototype is not { } proto)
                continue;

            var savedLocal = Transform(uid).LocalPosition;
            var rel = _transform.GetWorldPosition(uid) - anchorWorld;
            var entry = new MappingDataNode();
            entry.Add("proto", new ValueDataNode(proto.ID));
            entry.Add("x", new ValueDataNode(rel.X.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("y", new ValueDataNode(rel.Y.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("rot", new ValueDataNode(_transform.GetWorldRotation(uid).Theta.ToString("R", CultureInfo.InvariantCulture)));
            // Keep the serializer-frame position solely as a stable key for reconnecting this metadata to the
            // root after load. Placement itself always uses the world-aligned x/y offset above.
            entry.Add("savedX", new ValueDataNode(savedLocal.X.ToString("R", CultureInfo.InvariantCulture)));
            entry.Add("savedY", new ValueDataNode(savedLocal.Y.ToString("R", CultureInfo.InvariantCulture)));
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
            if (!TryGetTileWorldPosition(tile, out var world))
                continue;

            var rel = world - anchorWorld;
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
    /// A staircase is the single player-authored part of its saved build. Its linked beam and platform tiles are
    /// setup products that <see cref="ZStairSystem"/> regenerates when the stair is constructed; serializing them
    /// as separate ghosts either duplicates them or blocks the stair ghost on placement.
    /// </summary>
    private void ExcludeGeneratedStairParts(HashSet<EntityUid> entities, List<BuildSelectionTile> tiles)
    {
        var stairs = entities.Where(HasComp<ZStairComponent>).ToHashSet();
        if (stairs.Count == 0)
            return;

        var generatedTiles = new HashSet<(EntityUid Grid, int X, int Y)>();
        var links = EntityQueryEnumerator<ZStairBeamLinkComponent>();
        while (links.MoveNext(out var beam, out var link))
        {
            if (!link.Stair.Valid || !stairs.Contains(link.Stair))
                continue;

            entities.Remove(beam);
            if (!link.HasPlatform)
                continue;

            foreach (var tile in link.LaidTiles)
                generatedTiles.Add((link.PlatformGrid, tile.X, tile.Y));
        }

        tiles.RemoveAll(tile =>
            TryGetEntity(tile.Grid, out var grid) && generatedTiles.Contains((grid.Value, tile.X, tile.Y)));
    }

    /// <summary>
    /// Filters the selection down to roots the engine serializer can safely persist. Placement metadata records
    /// the serializer-local position separately from its world-aligned offset, so roots on different z grids do
    /// not have to share a local coordinate frame.
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

    private bool TryGetTileWorldPosition(BuildSelectionTile tile, out Vector2 world)
    {
        world = default;
        if (!TryGetEntity(tile.Grid, out var gridUid) ||
            !TryComp<MapGridComponent>(gridUid.Value, out var grid))
            return false;

        world = _transform.ToMapCoordinates(
            _map.GridTileToLocal(gridUid.Value, grid, new Vector2i(tile.X, tile.Y))).Position;
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
