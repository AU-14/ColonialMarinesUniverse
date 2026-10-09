using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using System.Linq;
using Content.Client.Administration.Managers;
using Content.Client.IconSmoothing;
using Content.Client.Power;
using Content.Shared._RMC14.Doors;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.Administration;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Doors.Components;
using Content.Shared.Foldable;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Sprite;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.ThreeD.Scene;

/// <summary>
/// First-person presentation on supported maps, with a separate administrator scene workbench.
/// </summary>
public sealed partial class CMU3DLiveSceneSystem : EntitySystem
{
    [Dependency] private IClientAdminManager _admins = default!;
    [Dependency] private Robust.Shared.Network.IClientNetManager _network = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private CMU3DElevationSystem _elevation = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private CMU3DModelLibrary _modelLibrary = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SpriteSystem _sprites = default!;
    [Dependency] private SharedRoofSystem _roofs = default!;
    [Dependency] private Content.Shared.Tag.TagSystem _tags = default!;
    private static readonly ProtoId<Content.Shared.Tag.TagPrototype> WallTag = "Wall";
    private readonly List<CMU3DSceneBox> _combined = [];
    private readonly HashSet<Vector2i> _roofTiles = [];
    private readonly List<EntityUid> _spriteEntities = [];
    private readonly List<EntityUid> _fallbackSprites = [];

    public const float Radius = 6;
    public const int EntityLimit = 256;
    private const int PartLimit = 8192;
    private const float RefreshPeriod = 0.1f;
    private static readonly Vector2i[] FloorNeighbours = [new(0, -1), new(1, 0), new(0, 1), new(-1, 0)];

    private CMU3DLiveSceneWindow? _window;
    private CMU3DModelLibrary.Lease? _modelLease;
    private CMU3DSceneCatalog? _catalog;
    private readonly Dictionary<string, Color[]> _tileColors = new(StringComparer.Ordinal);
    private readonly HashSet<Entity<SpriteComponent>> _candidates = [];
    private readonly HashSet<Entity<SpriteComponent>> _wallTargets = [];
    private readonly List<(EntityUid Uid, SpriteComponent Sprite, MetaDataComponent Meta, Vector2 Position, float Yaw, bool Structural)> _ordered = [];
    private List<Entity<MapGridComponent>> _grids = [];
    private readonly List<CMU3DSceneBox> _boxes = [];
    private readonly List<CMU3DSceneBox> _entityBoxes = [];
    private readonly List<EntityUid> _animatedSprites = [];
    private readonly List<EntityUid> _activeAnimatedSprites = [];
    private readonly List<CMU3DSceneSurface> _surfaces = [];
    private readonly Dictionary<(string Model, int Mask), CMU3DModelPart[]> _connectedParts = [];
    private readonly Dictionary<string, CMU3DModelPart[]> _insideWallParts = [];
    private readonly Dictionary<EntityUid, Vector3> _surfaceOffsets = [];
    private readonly Dictionary<EntityUid, float> _elevationOffsets = [];
    private readonly List<(EntityUid Uid, CMU3DModelPrototype Model, Vector2 Position, float Yaw, bool Exact)> _surfaceProps = [];
    private readonly List<CMU3DSceneRearWall> _surfaceRearWalls = [];
    private readonly HashSet<EntityUid> _surfaceMountFallbacks = [];
    private EntityUid? _actor;
    private MapId _actorMap;
    private EntityUid? _selected;
    private string? _selectedModel;
    private float _untilRefresh;
    private float _subscribedRadius;

    private void UpdateSubscription(float radius)
    {
        if (_subscribedRadius == radius)
            return;
        _subscribedRadius = radius;
        if (_network.IsConnected)
            RaiseNetworkEvent(new CMU3DViewRequest(radius > 0, radius));
    }

    public bool IsOpen => View != null;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(TransformSystem));
        InitializeCaptureBinding();
        _admins.AdminStatusUpdated += OnAdminStatusUpdated;
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    public bool Open()
    {
        if (!_admins.HasFlag(AdminFlags.Debug) || !TryContext(out _, out _))
            return false;
        CloseFirstPerson();
        _modelLease ??= _modelLibrary.AcquireWorld();
        if (_window == null)
        {
            var window = new CMU3DLiveSceneWindow();
            _window = window;
            window.OnClose += () => ReleaseWindow(window);
            window.SettingsChanged += () =>
            {
                SelectEntity(null);
                _untilRefresh = 0;
            };
            window.View.EntitySelected += SelectEntity;
            window.Inspect.OnPressed += _ =>
            {
                if (_selectedModel != null && _admins.HasFlag(AdminFlags.Debug))
                    EntityManager.System<CMU3DPreviewSystem>().Open(_selectedModel);
            };
        }
        _window.OpenCentered();
        _untilRefresh = 0;
        Refresh();
        return true;
    }

    public void Close()
    {
        ClearGeometryCache();
        CloseFirstPerson();
        var window = _window;
        if (window == null)
            return;
        ReleaseWindow(window);
        window.Close();
    }

    private void ReleaseWindow(CMU3DLiveSceneWindow window)
    {
        ClearGeometryCache();
        window.View.ReleaseResources();
        if (_window != window)
            return;
        _window = null;
        UpdateSubscription(0);
        ReleaseSceneData();
    }

    private void OnAdminStatusUpdated()
    {
        if (_window != null && !_admins.HasFlag(AdminFlags.Debug))
            Close();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        ClearGeometryCache();
        _catalog = null;
        View?.InvalidateSurfaces();
        _connectedParts.Clear();
        _prisonWindowParts.Clear();
        _prisonWallReliefParts.Clear();
        _insideWallParts.Clear();
        _tileColors.Clear();
        SelectEntity(null);
        _untilRefresh = 0;
    }

    private bool TryContext(out EntityUid actor, out TransformComponent xform, bool firstPerson = false)
    {
        actor = default;
        xform = default!;
        if (_players.LocalEntity is not { } controlled ||
            TerminatingOrDeleted(controlled) || !TryComp(controlled, out TransformComponent? transform) ||
            transform.MapID == MapId.Nullspace ||
            !TryComp(controlled, out MetaDataComponent? meta) ||
            (meta.Flags & MetaDataFlags.Detached) != 0)
            return false;
        if (firstPerson || _firstPersonView != null)
        {
            if (transform.MapUid is not { } map || !HasComp<CMU3DMapComponent>(map))
                return false;
        }
        else if (!_admins.HasFlag(AdminFlags.Debug))
            return false;
        actor = controlled;
        xform = transform;
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        if (!IsOpen && _viewConfig.GetCVar(CMU3DViewSettings.Enabled))
            OpenFirstPerson();
        if (!IsOpen)
            return;
        // Revoke immediately, including while waiting for the next sample.
        if (!TryContext(out var actor, out var xform))
        {
            Close();
            return;
        }
        _untilRefresh -= frameTime;
        if (_firstPersonView != null)
            AdvanceRefresh(actor, xform);
        else if (_untilRefresh <= 0 || _actor != actor || _actorMap != xform.MapID)
            Refresh();
        // Replacing the 2D world draw must not freeze its existing RSI clock.
        // SpriteSystem deduplicates this with its ordinary render update queue.
        foreach (var uid in _activeAnimatedSprites)
        {
            if (!TerminatingOrDeleted(uid))
                _sprites.ForceUpdate(uid);
        }
        UpdateFirstPersonCamera();
    }

    private void EnsureCatalog()
    {
        if (_catalog != null)
            return;
        _modelLease!.EnsureLoaded();
        _catalog = new CMU3DSceneCatalog(_prototypes.EnumeratePrototypes<CMU3DModelPrototype>(), id =>
            _prototypes.TryIndex<EntityPrototype>(id, out var prototype) ? prototype.Parents : null);
        foreach (var material in _prototypes.EnumeratePrototypes<CMU3DTileMaterialPrototype>())
            _tileColors[material.Tile] = material.Colors;
    }

    private void Refresh()
    {
        CancelRefresh();
        using var steps = GatherScene().GetEnumerator();
        while (steps.MoveNext()) { }
    }

    private IEnumerable<bool> GatherScene()
    {
        if (View is not { } view || !TryContext(out var actor, out var actorXform))
        {
            Close();
            yield break;
        }
        EnsureCatalog();
        UpdateSubscription(ViewRadius);
        if (_actor != actor || !_zLevels.IsSameZNetwork(_actorMap, actorXform.MapID))
        {
            view.ClearScene();
            view.ResetCamera();
            SelectEntity(null);
        }
        // Prefetch geometry around the stable origin while the camera moves independently.
        var origin = SampleOrigin(_transform.GetWorldPosition(actorXform));
        var bounds = new Box2(origin - new Vector2(SampleRadius), origin + new Vector2(SampleRadius));
        var maps = _zLevels.GetAllNetworkMaps(actorXform.MapUid!.Value)
            .Where(uid => Math.Abs((long) _elevation.Depth(uid) - _elevation.Depth(actorXform.MapUid)) <= CMU3DViewRequest.MaximumDepth)
            .OrderBy(uid => Math.Abs((long) _elevation.Depth(uid) - _elevation.Depth(actorXform.MapUid))).ToArray();
        var combined = _combined;
        combined.Clear();
        _geometryUsed.Clear();
        _nextNearSources.Clear();
        _nextDetailedWalls.Clear();
        var sceneDepth = _elevation.Depth(actorXform.MapUid);
        _animatedSprites.Clear();
        _spriteEntities.Clear();
        var floorCount = 0;
        var exact = 0;
        var inherited = 0;
        var fallback = 0;
        var states = 0;
        var omitted = 0;
        var budgets = CMU3DSceneBudget.Allocate(ScenePartLimit,
            maps.Select(uid => _mapDemand.GetValueOrDefault(uid, ScenePartLimit)).ToArray());
        for (var mapIndex = 0; mapIndex < maps.Length; mapIndex++)
        {
            var mapUid = maps[mapIndex];
            if (TerminatingOrDeleted(mapUid)) continue;
            var mapId = _transform.GetMapId(mapUid);
            var remaining = ScenePartLimit - combined.Count;
            // Last sample's admitted and rejected parts reveal each floor's demand.
            // Return sparse upper floors' spare capacity to the occupied floor too.
            _mapPartLimit = _firstPersonView != null ? Math.Min(remaining, budgets[mapIndex])
                : remaining / (maps.Length - mapIndex);
            _mapRejectedParts = 0;
            _zStairEntities.Clear();
            CollectTerrainCutouts(origin, bounds, mapId);
            if (ShouldYieldRefresh()) yield return true;
            if (TerminatingOrDeleted(mapUid)) continue;
            _boxes.Clear();
            _surfaces.Clear();
            _surfaceProps.Clear();
            _surfaceOffsets.Clear();
            _slabTiles.Clear();
            _paperOffsets.Clear();
            _slabPlans.Clear();
            _slabAdmitted.Clear();
            _slabCladdings.Clear();
            foreach (var step in AddFloors(origin, bounds, mapId))
                yield return step;
            floorCount += _floorsAdded;
            if (TerminatingOrDeleted(mapUid)) continue;
            _candidates.Clear();
            _ordered.Clear();
            // We select model pivots below, so physics narrow-phase intersections add
            // work without deciding which authored geometry belongs in the scene.
            _lookup.GetEntitiesIntersecting(mapId, bounds, _candidates, LookupFlags.Uncontained | LookupFlags.Approximate);
            foreach (var (uid, sprite) in _candidates)
            {
                if (_firstPersonView != null && uid == actor)
                    continue;
                if (TerminatingOrDeleted(uid) || !sprite.Visible || sprite.Color.A <= 0 || sprite.ContainerOccluded || !sprite.AddToTree ||
                    !TryComp(uid, out TransformComponent? xform) || xform.MapID != mapId ||
                    !TryComp(uid, out MetaDataComponent? meta) ||
                    (meta.Flags & (MetaDataFlags.Detached | MetaDataFlags.InContainer)) != 0 ||
                    _containers.IsEntityOrParentInContainer(uid, meta, xform))
                    continue;
                var (position, rotation) = _transform.GetWorldPositionRotation(xform);
                if (!bounds.Contains(position))
                    continue;
                _ordered.Add((uid, sprite, meta, position, (float) rotation.Theta,
                    HasComp<OccluderComponent>(uid) || HasComp<DoorComponent>(uid)));
            }
            _ordered.Sort((a, b) =>
            {
                // Keep the local room complete before spending its budget on distant
                // terrain. Within each band walls/doors still precede furniture.
                if (_firstPersonView != null)
                {
                    var nearby = NearSource(b.Uid, b.Position, origin).CompareTo(NearSource(a.Uid, a.Position, origin));
                    if (nearby != 0) return nearby;
                }
                var structure = b.Structural.CompareTo(a.Structural);
                if (structure != 0)
                    return structure;
                if (_firstPersonView != null)
                {
                    var retained = _publishedSources.Contains(b.Uid).CompareTo(_publishedSources.Contains(a.Uid));
                    if (retained != 0) return retained;
                    if (_publishedSources.Contains(a.Uid)) return a.Uid.CompareTo(b.Uid);
                }
                var order = Vector2.DistanceSquared(a.Position, origin).CompareTo(Vector2.DistanceSquared(b.Position, origin));
                return order != 0 ? order : a.Uid.CompareTo(b.Uid);
            });

            var entities = 0;
            _fallbackSprites.Clear();
            var examined = 0;
            foreach (var candidate in _ordered)
            {
                if (++examined % 32 == 0 && ShouldYieldRefresh()) yield return true;
                // Discovery may resume after another frame has deleted a replicated source.
                if (TerminatingOrDeleted(candidate.Uid)) continue;
                if (_firstPersonView != null && HasComp<MobStateComponent>(candidate.Uid))
                {
                    _spriteEntities.Add(candidate.Uid);
                    continue;
                }
                if (_firstPersonView == null && entities >= EntityLimit)
                {
                    omitted++;
                    continue;
                }
                if (_elevation.SuppressStair(candidate.Uid))
                    continue;
                if (TryAddZStair(candidate.Uid, origin, out var stairAdmitted))
                {
                    if (stairAdmitted)
                    {
                        entities++;
                        exact++;
                    }
                    else
                        omitted++;
                    continue;
                }
                var id = candidate.Meta.EntityPrototype?.ID;
                var match = id == null ? null : _catalog!.Resolve(id);
                match = VehicleTurretMatch(candidate.Uid, match);
                var unsupportedState = false;
                if (id != null && _catalog!.HasRandomSpriteVariants(id))
                {
                    match = TryComp(candidate.Uid, out RandomSpriteComponent? random)
                        ? _catalog.ResolveRandomSprite(id, random.Selected)
                        : null;
                    unsupportedState = !match.HasValue;
                }
                if (TryComp(candidate.Uid, out DoorComponent? door))
                {
                    var posed = _catalog!.WithDoorState(match, door.State);
                    unsupportedState |= !posed.HasValue && (match.HasValue || door.State != DoorState.Closed);
                    match = posed;
                }
                if (TryComp(candidate.Uid, out FoldableComponent? foldable))
                {
                    var posed = _catalog!.WithFoldState(match, foldable.IsFolded);
                    unsupportedState |= !posed.HasValue && (match.HasValue || foldable.IsFolded);
                    match = posed;
                }
                if (match is { } anchorMatch && (anchorMatch.Model.Anchored != null || anchorMatch.Model.AlternateAnchorModel != null))
                {
                    var posed = TryComp(candidate.Uid, out TransformComponent? anchorTransform)
                        ? _catalog!.WithAnchorState(match, anchorTransform.Anchored) : null;
                    unsupportedState |= !posed.HasValue;
                    match = posed;
                }
                var directed = _catalog!.WithDirection(match, candidate.Yaw);
                unsupportedState |= match.HasValue && !directed.HasValue;
                match = directed;
                IReadOnlyList<CMU3DModelPart>? stateParts = null;
                var appearanceKey = string.Empty;
                var paperOffset = Vector2.Zero;
                var xenoAnimated = false;
                if (match is { } xenoMatch && xenoMatch.Model.XenoStates.Count > 0)
                    unsupportedState |= !TryXenoParts(candidate.Sprite, xenoMatch.Model, out stateParts, out xenoAnimated);
                if (match is { } vehicleMatch && vehicleMatch.Model.VehicleLayers.Count > 0)
                    unsupportedState |= !TryVehicleParts(candidate.Uid, candidate.Sprite, vehicleMatch.Model, out stateParts);
                if (match is { } paperMatch && paperMatch.Model.WallPaper)
                    unsupportedState |= !paperMatch.Exact ||
                        !TryPaperOffset(candidate.Uid, candidate.Sprite, paperMatch.Model, out paperOffset);
                if (match is { } tankMatch && tankMatch.Model.ReagentTankAppearance != null)
                {
                    unsupportedState |= !TryReagentTankParts(candidate.Uid, candidate.Sprite, tankMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } chargerMatch && chargerMatch.Model.ChargerAppearance != null)
                {
                    unsupportedState |= !TryChargerParts(candidate.Uid, candidate.Sprite, chargerMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } foamMatch && foamMatch.Model.FoamAppearance != null)
                {
                    unsupportedState |= !TryFoamParts(candidate.Uid, candidate.Sprite, foamMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } solutionMatch && solutionMatch.Model.SolutionAppearance != null)
                {
                    unsupportedState |= !TrySolutionGlassParts(candidate.Uid, candidate.Sprite, solutionMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } spriteMatch && spriteMatch.Model.SpriteStates.Count > 0)
                {
                    unsupportedState |= !TrySpriteParts(candidate.Sprite, spriteMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } slabMatch && HasSlabOpening(slabMatch.Model))
                    unsupportedState |= !slabMatch.Exact ||
                        !TrySlabPlan(candidate.Uid, candidate.Sprite, slabMatch.Model, candidate.Yaw);
                if (match is { } doorMatch && doorMatch.Model.DoorSpriteStates.Count > 0)
                {
                    unsupportedState |= !TryDoorParts(candidate.Uid, candidate.Sprite, doorMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } lightMatch && lightMatch.Model.PoweredLightStates.Count > 0)
                {
                    unsupportedState |= !TryPoweredLightParts(candidate.Uid, candidate.Sprite, lightMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } buttonMatch && buttonMatch.Model.DoorButtonStates.Count > 0)
                {
                    unsupportedState |= !TryDoorButtonParts(candidate.Uid, candidate.Sprite, buttonMatch.Model,
                        out stateParts, out appearanceKey);
                }
                if (match is { } barricadeMatch && barricadeMatch.Model.BarricadeDamageStates.Count > 0)
                {
                    unsupportedState |= !TryBarricadeParts(candidate.Uid, candidate.Sprite, barricadeMatch.Model,
                        out stateParts, out appearanceKey);
                }
                var useModel = match.HasValue && (match.Value.Exact || _window?.Inherited.Pressed == true) && !unsupportedState;
                if (!useModel && _window?.Fallbacks.Pressed == false)
                {
                    omitted++;
                    continue;
                }
                _entityBoxes.Clear();
                var position = candidate.Position - origin;
                var renderYaw = candidate.Yaw;
                IReadOnlyList<CMU3DModelPart>? resolvedParts = null;
                if (useModel)
                {
                    var model = match!.Value.Model;
                    position += model.GroundOffset;
                    renderYaw = CMU3DSceneLayout.RenderYaw(model, candidate.Yaw, candidate.Sprite.NoRotation,
                        candidate.Sprite.SnapCardinals) + (float) candidate.Sprite.Rotation.Theta;
                    if (model.VehicleTurretPrototypes.Length > 0 &&
                        TryVehiclePose(candidate.Uid, out var mountPosition, out var mountYaw))
                    {
                        position = mountPosition - origin + model.GroundOffset;
                        renderYaw = CMU3DSceneLayout.RenderYaw(model, (float) mountYaw.Theta, false, false);
                    }
                    if (model.OpeningFacingTargets.Length > 0)
                        renderYaw = OpeningFacingYaw(candidate.Uid, model, renderYaw);
                    IReadOnlyList<CMU3DModelPart> parts = stateParts ?? model.Parts;
                    if (model.CornerSurfaces.Length == 32 && TryConnections(candidate.Uid, out var cornerMask, out var cornerGridYaw,
                            alignToWalls: false, corners: true))
                    {
                        renderYaw = cornerGridYaw;
                        if (!_connectedParts.TryGetValue((model.ID, cornerMask), out var corners))
                        {
                            corners = CMU3DSceneLayout.CornerParts(model, cornerMask);
                            _connectedParts[(model.ID, cornerMask)] = corners;
                        }
                        parts = corners;
                    }
                    else if (model.ConnectToNeighbours && TryConnections(candidate.Uid, out var mask, out var gridYaw,
                            string.IsNullOrEmpty(model.SupportSurface)))
                    {
                        renderYaw = gridYaw;
                        if (!_connectedParts.TryGetValue((model.ID, mask), out var connected))
                        {
                            connected = CMU3DSceneLayout.ConnectedParts(model.Parts, mask, model.SupportSurface, model.ConnectionEndInset);
                            _connectedParts[(model.ID, mask)] = connected;
                        }
                        parts = connected;
                        if (TryPrisonWindowParts(candidate.Uid, model, mask, gridYaw, connected, out var prisonParts, out _))
                            parts = prisonParts;
                    }
                    else if (model.WallMounted && TryWallMount(candidate.Uid, model, ref renderYaw, out var inside, out var wallOffset))
                    {
                        position += wallOffset;
                        if (inside)
                        {
                            var mountedKey = model.ID + ":" + appearanceKey;
                            if (!_insideWallParts.TryGetValue(mountedKey, out var mounted))
                            {
                                mounted = CMU3DSceneLayout.InsideWallParts(parts);
                                _insideWallParts[mountedKey] = mounted;
                            }
                            parts = mounted;
                            if (model.FitInsideWall && model.BackWallMountTargets.Length > 0)
                                position += BackWallMountOffset(candidate.Uid, model, renderYaw, wallOffset, roomSide: true);
                        }
                        else if (model.BackWallMountTargets.Length > 0)
                            position += BackWallMountOffset(candidate.Uid, model, renderYaw, wallOffset);
                    }
                    else if (model.WallMounted && HasComp<CMU3DVehicleCabinComponent>(mapUid))
                    {
                        // Cabin fixtures are placed directly against custom hull art.
                        // Undo the ordinary model's half-tile wall-face offset.
                        position += new Vector2(-MathF.Sin(renderYaw), MathF.Cos(renderYaw)) * .5f;
                    }
                    else if (model.FaceAwayFromWall)
                        renderYaw = ApplianceYaw(candidate.Uid, model, renderYaw);
                    else if (model.BackWallMountTargets.Length > 0 && model.Placement != "surface")
                        position += BackWallMountOffset(candidate.Uid, model, renderYaw);
                    else if (model.WindowMountTargets.Length > 0)
                        position += WindowMountOffset(candidate.Uid, model, renderYaw);
                    if (TryPrisonWallReliefParts(candidate.Uid, model, renderYaw, parts, out var joinedPrisonWall))
                        parts = joinedPrisonWall;
                    if (model.PanelEndTargets.Length > 0)
                        parts = FitPanelEnds(candidate.Uid, model, renderYaw, parts,
                            position - (candidate.Position - origin + model.GroundOffset));
                    if (_firstPersonView != null && _tags.HasTag(candidate.Uid, WallTag) && door == null &&
                        model.FloorOpening == null && model.CeilingOpening == null)
                        parts = WallDetail(candidate.Uid, parts, candidate.Position - origin,
                            (_elevation.Depth(mapUid) - sceneDepth) * CMU3DZProjection.StoryHeight);
                    if (match.Value.Exact && id != null)
                        parts = FitTerrain(candidate.Uid, id, parts, position, renderYaw);
                    if (_elevation.TryStair(candidate.Uid, out var stairRamp) && Transform(candidate.Uid).GridUid is { } stairGrid)
                        renderYaw = (float) _transform.GetWorldRotation(stairGrid).Theta +
                            MathF.Atan2(-stairRamp.Direction.X, stairRamp.Direction.Y);
                    resolvedParts = parts;
                    position += paperOffset;
                    var wallHeight = _window?.Cutaway.Pressed == true &&
                        (HasComp<OccluderComponent>(candidate.Uid) || door != null || model.WallMounted) ? 0.9f : float.PositiveInfinity;
                    if (!AddModel(candidate.Uid, parts, position, renderYaw,
                            CMU3DSceneLayout.PresentationTint(model, candidate.Sprite.Color), wallHeight))
                    {
                        omitted++;
                        continue;
                    }
                    var claddingOffset = ReagentTankFloorOffset(candidate.Uid, model);
                    if (claddingOffset > 0)
                        for (var i = 0; i < _entityBoxes.Count; i++)
                            _entityBoxes[i] = _entityBoxes[i] with { Center = _entityBoxes[i].Center + new Vector3(0, 0, claddingOffset) };
                }
                else
                {
                    if (_firstPersonView != null)
                    {
                        _fallbackSprites.Add(candidate.Uid);
                        continue;
                    }
                    var height = unsupportedState ? 0.15f : HasComp<MobStateComponent>(candidate.Uid) ? 1.6f : 0.45f;
                    _entityBoxes.Add(new CMU3DSceneBox(new Vector3(position, height / 2), new Vector3(0.2f, 0.2f, height / 2),
                        candidate.Yaw, unsupportedState ? Color.FromHex("#DDAD60") : Color.FromHex("#6BA8AC"), candidate.Uid));
                }
                if (_boxes.Count + _entityBoxes.Count > ViewPartLimit)
                {
                    _mapRejectedParts += _entityBoxes.Count;
                    omitted++;
                    continue;
                }
                _boxes.AddRange(_entityBoxes);
                if (NearSource(candidate.Uid, candidate.Position, origin)) _nextNearSources.Add(candidate.Uid);
                if (useModel && HasSlabOpening(match!.Value.Model))
                    _slabAdmitted.Add(candidate.Uid);
                if (useModel)
                {
                    var model = match!.Value.Model;
                    if (model.SpriteStates.Count > 0 || xenoAnimated || model.ChargerAppearance != null ||
                        model.FoamAppearance != null || model.SolutionAppearance != null)
                        _animatedSprites.Add(candidate.Uid);
                    if (model.Placement == "surface")
                        _surfaceProps.Add((candidate.Uid, model, position, renderYaw, match.Value.Exact));
                    if (match.Value.Exact)
                    {
                        CMU3DScenePlacement.CollectSurfaces(model, candidate.Uid, position, renderYaw, _surfaces, resolvedParts);
                        if (id != null)
                            CollectSlabCladding(candidate.Uid, id, position);
                    }
                }
                entities++;
                if (unsupportedState) states++;
                if (!useModel) fallback++;
                else if (match!.Value.Exact) exact++;
                else inherited++;
            }
            if (TerminatingOrDeleted(mapUid)) continue;
            // Placement and slab clipping can replace an admitted model with a sprite.
            // Keep its reservation in the demand estimate: counting it only when rejected
            // makes this floor reclaim and release the same slots on alternate samples.
            var reservedParts = _boxes.Count;
            _surfaceMountFallbacks.Clear();
            foreach (var prop in _surfaceProps)
            {
                if (prop.Model.BackWallMountTargets.Length == 0)
                {
                    _surfaceOffsets[prop.Uid] = new Vector3(0, 0, CMU3DScenePlacement.Offset(prop.Model, prop.Uid, prop.Position, _surfaces, prop.Yaw));
                    continue;
                }
                CollectRearWalls(prop.Uid, prop.Model);
                if (CMU3DScenePlacement.TryRearWallOffset(prop.Model, prop.Uid, prop.Position, prop.Yaw,
                        _surfaces, _surfaceRearWalls, out var mounted))
                {
                    _surfaceOffsets[prop.Uid] = mounted;
                    continue;
                }
                _surfaceMountFallbacks.Add(prop.Uid);
                _fallbackSprites.Add(prop.Uid);
                _animatedSprites.Remove(prop.Uid);
                if (prop.Exact) exact--;
                else inherited--;
                fallback++;
                states++;
            }
            for (var i = _boxes.Count - 1; i >= 0; i--)
            {
                if (_boxes[i].Source is not { } source)
                    continue;
                if (_surfaceMountFallbacks.Contains(source))
                    _boxes.RemoveAt(i);
                else if (_surfaceOffsets.TryGetValue(source, out var offset))
                    _boxes[i] = _boxes[i] with { Center = _boxes[i].Center + offset };
            }
            ApplySlabOpenings(ref exact, ref fallback);
            _elevationOffsets.Clear();
            for (var i = 0; i < _boxes.Count; i++)
            {
                if (_boxes[i].Source is { } source)
                {
                    if (!_elevationOffsets.TryGetValue(source, out var height))
                        _elevationOffsets[source] = height = _zStairEntities.Contains(source) ? 0 : _elevation.PhysicalHeight(source, _elevation.Depth(mapUid), true);
                    _boxes[i] = _boxes[i] with
                    {
                        Center = _boxes[i].Center + new Vector3(0, 0, height),
                    };
                }
            }
            _spriteEntities.AddRange(_fallbackSprites);
            _mapDemand[mapUid] = Math.Max(reservedParts, _boxes.Count) + _mapRejectedParts;
            var mapHeight = (_elevation.Depth(mapUid) - sceneDepth) * CMU3DZProjection.StoryHeight;
            foreach (var box in _boxes)
                combined.Add(box with { Center = box.Center + new Vector3(0, 0, mapHeight) });
            if (ShouldYieldRefresh()) yield return true;
        }
        foreach (var step in view.StageScene(combined, ShouldYieldRefresh))
            yield return step;
        // Publish one coherent snapshot. The camera keeps using the previous origin
        // and depth while the next stack is assembled, so walking never shifts models.
        _untilRefresh = RefreshPeriod;
        _actor = actor;
        _actorMap = actorXform.MapID;
        _sceneOrigin = origin;
        view.SceneDepth = sceneDepth;
        view.SceneMaps.Clear();
        foreach (var mapUid in maps)
            if (!TerminatingOrDeleted(mapUid)) view.SceneMaps.Add(_transform.GetMapId(mapUid));
        _activeAnimatedSprites.Clear();
        _activeAnimatedSprites.AddRange(_animatedSprites);
        _boxes.Clear();
        _boxes.AddRange(combined);
        PruneGeometryCache();
        view.SetBillboards(_spriteEntities);
        _publishedSources.Clear();
        foreach (var box in _boxes)
            if (box.Source is { } source) _publishedSources.Add(source);
        _nearSources.Clear();
        foreach (var source in _nextNearSources)
            if (_publishedSources.Contains(source)) _nearSources.Add(source);
        _detailedWalls.Clear();
        foreach (var source in _nextDetailedWalls)
            if (_publishedSources.Contains(source)) _detailedWalls.Add(source);
        _window?.Statistics.SetMessage(Loc.GetString("cmu-3d-live-statistics", ("exact", exact), ("inherited", inherited),
            ("fallback", fallback), ("states", states), ("floors", floorCount), ("boxes", view.RenderedBoxes),
            ("omitted", omitted), ("dropped", view.OmittedBoxes)));
        if (_selected is { } selected && (TerminatingOrDeleted(selected) || !view.ContainsEntity(selected)))
            SelectEntity(null);
        else if (_selected is { } current)
            SelectEntity(current);
    }

    private IEnumerable<bool> AddFloors(Vector2 origin, Box2 bounds, MapId mapId)
    {
        _grids.Clear();
        _map.FindGridsIntersecting(mapId, bounds, ref _grids);
        _floorsAdded = 0;
        var examined = 0;
        var addCeiling = _firstPersonView != null && !_zLevels.TryMapUp(_map.GetMap(mapId), out _);
        foreach (var grid in _grids)
        {
            if (TerminatingOrDeleted(grid.Owner)) continue;
            var yaw = (float) _transform.GetWorldRotation(grid.Owner).Theta;
            var elevation = grid.Comp.TileSize == 1 ? _elevation.Field(grid.Owner) : null;
            TryComp(grid.Owner, out RoofComponent? roof);
            var implicitRoof = HasComp<ImplicitRoofComponent>(grid.Owner);
            if (addCeiling && roof != null)
                _roofs.GetEntityRoofTiles(grid, _transform.GetInvWorldMatrix(grid.Owner).TransformBox(bounds), _roofTiles);
            var tiles = _map.GetTilesIntersecting(grid.Owner, grid.Comp, bounds, ignoreEmpty: _firstPersonView == null);
            while (tiles.MoveNext(out var tile))
            {
                if (++examined % 64 == 0 && ShouldYieldRefresh()) yield return true;
                if (TerminatingOrDeleted(grid.Owner)) break;
                if (elevation?.StairOpenings.Contains(tile.GridIndices) == true)
                    continue;
                if (_boxes.Count >= ViewPartLimit - 2)
                {
                    _mapRejectedParts += addCeiling ? 2 : 1;
                    continue;
                }
                var position = _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile.GridIndices) - origin;
                var groundHeight = elevation?.Floor(tile.GridIndices) ?? 0;
                if (MathF.Abs(position.X) > SampleRadius + 0.5f || MathF.Abs(position.Y) > SampleRadius + 0.5f)
                    continue;
                // Roof masks include multi-Z coverage, explicit roofs and RMC area ceilings.
                // Roof.Color is a shadow tint, not a physical ceiling material.
                if (addCeiling && (roof != null &&
                    _roofs.GetColor((grid.Owner, grid.Comp, roof), tile.GridIndices, _roofTiles.Contains(tile.GridIndices)) != null ||
                    implicitRoof && !tile.Tile.IsEmpty))
                    AddSlab(new CMU3DSceneBox(new Vector3(position, groundHeight + 2.85f),
                        new Vector3(grid.Comp.TileSize / 2f, grid.Comp.TileSize / 2f, .1f), yaw, Color.FromHex("#747D80")),
                        grid.Owner, tile.GridIndices, true);
                if (tile.Tile.IsEmpty)
                    continue;
                var color = Color.FromHex("#59696B");
                var definition = _tiles[tile.Tile.TypeId];
                if (definition is ContentTileDefinition { Sprite: null })
                    continue;
                if (_tileColors.TryGetValue(definition.ID, out var colors) && colors.Length > 0)
                    color = colors[Math.Min(tile.Tile.Variant, colors.Length - 1)];
                var bottom = groundHeight - .07f;
                if (elevation != null)
                {
                    foreach (var delta in FloorNeighbours)
                        bottom = MathF.Min(bottom, elevation.Floor(tile.GridIndices + delta) - .07f);
                }
                AddSlab(new CMU3DSceneBox(new Vector3(position, (groundHeight + bottom) / 2),
                    new Vector3(grid.Comp.TileSize / 2f, grid.Comp.TileSize / 2f, (groundHeight - bottom) / 2), yaw, color),
                    grid.Owner, tile.GridIndices, false);
                if (elevation?.Ramp((Vector2) tile.GridIndices + new Vector2(.5f)) is { SourcePrototype: "" } ramp)
                {
                    // Threshold ramps use the same ascent as actor/camera height sampling.
                    _boxes.Add(new CMU3DSceneBox(new Vector3(position, (ramp.Bottom + ramp.Top) / 2),
                        new Vector3(.5f, .5f, (ramp.Top - ramp.Bottom) / 2),
                        yaw + MathF.Atan2(-ramp.Direction.X, ramp.Direction.Y), color, Shape: CMU3DPartShape.WedgeY));
                }
                _floorsAdded++;
            }
        }
    }

    private Vector2 BackWallMountOffset(EntityUid uid, CMU3DModelPrototype model, float yaw, Vector2 wallOffset = default, bool roomSide = false)
    {
        CollectRearWalls(uid, model, wallOffset);
        var result = Vector2.Zero;
        foreach (var wall in _surfaceRearWalls)
        {
            if (CMU3DSceneLayout.TryBackWallMountOffset(model.Parts, yaw, wall.Parts, wall.Yaw, wall.Delta, out var offset, roomSide) &&
                offset.LengthSquared() > result.LengthSquared())
                result = offset;
        }
        return result;
    }

    private void CollectRearWalls(EntityUid uid, CMU3DModelPrototype model, Vector2 wallOffset = default)
    {
        _surfaceRearWalls.Clear();
        if (!TryComp(uid, out TransformComponent? xform) || !TryComp(xform.GridUid, out MapGridComponent? grid))
            return;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var position = _transform.GetWorldPosition(xform) + model.GroundOffset + wallOffset;
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile + new Vector2i(x, y)))
        {
            if (other == uid || !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                Array.IndexOf(model.BackWallMountTargets, prototype.ID) < 0 ||
                _catalog?.Resolve(prototype.ID) is not { Exact: true } match || !TryComp(other, out SpriteComponent? sprite))
                continue;
            var wall = match.Model;
            if (wall.ConnectToNeighbours || wall.WallMounted ||
                wall.WindowMountTargets.Length > 0 || wall.BackWallMountTargets.Length > 0 || wall.FaceAwayFromWall)
                continue;
            var wallYaw = CMU3DSceneLayout.RenderYaw(wall, (float) _transform.GetWorldRotation(other).Theta,
                sprite.NoRotation, sprite.SnapCardinals) + (float) sprite.Rotation.Theta;
            // Corner selection changes artwork only; the support solids keep their authored bounds.
            if (wall.CornerSurfaces.Length == 32 && TryConnections(other, out _, out var gridYaw, alignToWalls: false, corners: true))
                wallYaw = gridYaw;
            var delta = _transform.GetWorldPosition(other) + wall.GroundOffset - position;
            _surfaceRearWalls.Add(new CMU3DSceneRearWall(wall.Parts, wallYaw, delta));
        }
    }

    private bool TryDoorParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!TryComp(uid, out DoorComponent? door) || sprite.Scale != Vector2.One ||
            sprite.Offset != Vector2.Zero || sprite.Color != model.BakedSpriteTint ||
            !_sprites.LayerMapTryGet((uid, sprite), DoorVisualLayers.Base, out var index, false) ||
            !_sprites.TryGetLayer((uid, sprite), index, out var basis, false))
            return false;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer != basis && layer.Visible && layer.Color.A > 0 && (layer.RsiState.IsValid || layer.Texture != null))
                return false;
        }
        return CMU3DDoorAppearance.TryParts(model, door.State, Snapshot(basis), false, out parts, out key);
    }

    private bool TryPoweredLightParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        // The existing visualizer owns both state changes and randomized blink timing.
        // RMCLightOffset's padded screen offset is intentionally not a ground translation.
        if (!HasComp<Content.Client.Light.Visualizers.PoweredLightVisualsComponent>(uid) ||
            sprite.Scale != Vector2.One || sprite.Color != model.BakedSpriteTint ||
            !_sprites.LayerMapTryGet((uid, sprite), Content.Shared.Light.PoweredLightLayers.Base, out var index, false) ||
            !_sprites.TryGetLayer((uid, sprite), index, out var basis, false))
            return false;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer != basis && layer.Visible && layer.Color.A > 0 && (layer.RsiState.IsValid || layer.Texture != null))
                return false;
        }
        return CMU3DLightAppearance.TryParts(model, Snapshot(basis), false, out parts, out key);
    }

    private bool TryBarricadeParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        if (!HasComp<BarricadeComponent>(uid) || sprite.Scale != Vector2.One || sprite.Offset != Vector2.Zero ||
            sprite.Rotation != Angle.Zero || sprite.Color != Color.White ||
            !_sprites.LayerMapTryGet((uid, sprite), RMCDamageOverlayVisuals.DamageOverlay, out var bodyIndex, false) ||
            !_sprites.LayerMapTryGet((uid, sprite), RMCDamageOverlayVisuals.AdditionalDamageOverlay, out var reinforcementIndex, false) ||
            bodyIndex >= reinforcementIndex ||
            !_sprites.TryGetLayer((uid, sprite), bodyIndex, out var body, false) ||
            !_sprites.TryGetLayer((uid, sprite), reinforcementIndex, out var reinforcement, false))
            return false;
        SpriteComponent.Layer? wire = null;
        if (_sprites.LayerMapTryGet((uid, sprite), "barbWired", out var wireIndex, false))
        {
            if (!_sprites.TryGetLayer((uid, sprite), wireIndex, out wire, false) ||
                wire.Visible && wireIndex <= reinforcementIndex)
                return false;
        }
        SpriteComponent.Layer? acid = null;
        var acidIndex = -1;
        if (_sprites.LayerMapTryGet((uid, sprite), "acided", out acidIndex, false) &&
            !_sprites.TryGetLayer((uid, sprite), acidIndex, out acid, false))
            return false;
        // The reserved acid slot starts blank. A blank slot has no visible effect even when Visible defaults to true.
        var acidVisible = acid is { Visible: true } && (((ISpriteLayer) acid).RsiState.IsValid || acid.Texture != null);
        if (!CMU3DBarricadeAppearance.ValidLayerOrder(bodyIndex, reinforcementIndex,
                acidVisible ? acidIndex : null, wire is { Visible: true } ? wireIndex : null))
            return false;
        foreach (var layer in sprite.AllLayers)
        {
            // Fire and unknown overlays still require their own authored geometry.
            if (layer != body && layer != reinforcement && layer != wire && layer != acid && (layer.RsiState.IsValid || layer.Texture != null) &&
                layer.Visible && layer.Color.A > 0)
                return false;
        }
        return CMU3DBarricadeAppearance.TryParts(model, BarricadeSnapshot(body), BarricadeSnapshot(reinforcement),
            false, out parts, out key, wire == null ? null : BarricadeSnapshot(wire),
            acidVisible ? BarricadeSnapshot(acid!) : null);
    }

    private static CMU3DBarricadeLayer BarricadeSnapshot(SpriteComponent.Layer layer) => new(
        ((ISpriteLayer) layer).ActualRsi?.Path.ToString(), layer.State.Name, layer.AnimationFrame, layer.Visible,
        layer.Color, layer.Scale == Vector2.One && layer.Rotation == Angle.Zero && layer.Offset == Vector2.Zero &&
                     layer.DirOffset == SpriteComponent.DirectionOffset.None && layer.Texture == null &&
                     layer.Shader == null && layer.ShaderPrototype == null);

    private bool TryDoorButtonParts(EntityUid uid, SpriteComponent sprite, CMU3DModelPrototype model,
        out IReadOnlyList<CMU3DModelPart> parts, out string key)
    {
        parts = [];
        key = string.Empty;
        // These layer keys belong to the existing visualizer. No second event owner or timer is installed.
        if (!HasComp<RMCDoorButtonComponent>(uid) || sprite.Scale != Vector2.One ||
            !_sprites.LayerMapTryGet((uid, sprite), RMCPodDoorButtonLayers.Animation, out var animationIndex, false) ||
            !_sprites.LayerMapTryGet((uid, sprite), PowerDeviceVisualLayers.Powered, out var powerIndex, false) ||
            animationIndex >= powerIndex ||
            !_sprites.TryGetLayer((uid, sprite), animationIndex, out var animation, false) ||
            !_sprites.TryGetLayer((uid, sprite), powerIndex, out var power, false))
            return false;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer != animation && layer != power && layer.Visible && layer.Color.A > 0)
                return false;
        }
        return CMU3DDoorButtonAppearance.TryParts(model, Snapshot(animation), Snapshot(power), false, out parts, out key);
    }

    private static CMU3DButtonLayer Snapshot(SpriteComponent.Layer layer) => new(
        ((ISpriteLayer) layer).ActualRsi?.Path.ToString(), layer.State.Name, layer.AnimationFrame, layer.Visible,
        layer.Color, layer.Scale == Vector2.One && layer.Rotation == Angle.Zero && layer.Offset == Vector2.Zero &&
                     layer.Texture == null && layer.Shader == null && layer.ShaderPrototype == null);

    private Vector2 WindowMountOffset(EntityUid uid, CMU3DModelPrototype model, float yaw)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return Vector2.Zero;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var position = _transform.GetWorldPosition(xform) + model.GroundOffset;
        var result = Vector2.Zero;
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (other == uid || !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                Array.IndexOf(model.WindowMountTargets, prototype.ID) < 0 ||
                _catalog?.Resolve(prototype.ID) is not { Exact: true } match || !TryComp(other, out SpriteComponent? sprite))
                continue;
            var windowModel = match.Model;
            var exterior = CMU3DSceneLayout.IsShutterExteriorTarget(model.ID, prototype.ID);
            var mask = 0;
            IReadOnlyList<CMU3DModelPart> parts = windowModel.Parts;
            var windowYaw = CMU3DSceneLayout.RenderYaw(windowModel, (float) _transform.GetWorldRotation(other).Theta,
                sprite.NoRotation, sprite.SnapCardinals) + (float) sprite.Rotation.Theta;
            if (windowModel.ConnectToNeighbours)
            {
                if (!TryConnections(other, out mask, out windowYaw, string.IsNullOrEmpty(windowModel.SupportSurface)))
                    continue;
                if (!_connectedParts.TryGetValue((windowModel.ID, mask), out var connected))
                {
                    connected = CMU3DSceneLayout.ConnectedParts(windowModel.Parts, mask, windowModel.SupportSurface, windowModel.ConnectionEndInset);
                    _connectedParts[(windowModel.ID, mask)] = connected;
                }
                parts = connected;
            }
            int? exteriorAxis = null;
            if (prototype.ID == "RMCWindowPrisonCell")
            {
                if (!TryPrisonWindowParts(other, windowModel, mask, windowYaw, parts, out var prisonParts, out var sides) ||
                    !TryComp(uid, out MetaDataComponent? shutterMeta) || shutterMeta.EntityPrototype is not { } shutterPrototype)
                    continue;
                exteriorAxis = CMU3DSceneLayout.ShutterExteriorAxis(prototype.ID, mask);
                var shutterDelta = position - (_transform.GetWorldPosition(other) + windowModel.GroundOffset);
                var side = CMU3DSceneLayout.PrisonShutterSide(shutterPrototype.ID, model.ID, exteriorAxis, yaw,
                    windowYaw, shutterDelta, model.WindowMountInside);
                if ((sides & side) == 0)
                    continue;
                parts = prisonParts;
            }
            else if (exterior)
            {
                exteriorAxis = CMU3DSceneLayout.ShutterExteriorAxis(prototype.ID, mask);
                if (exteriorAxis == null)
                    continue;
                if (!windowModel.ConnectToNeighbours)
                {
                    CMU3DModelPrototype? alternate = null;
                    if (windowModel.AlternateDoorModel is { } alternateId && !_prototypes.TryIndex(alternateId, out alternate))
                        continue;
                    if (!CMU3DSceneLayout.TryShutterMountEnvelope(windowModel, alternate, out parts))
                        continue;
                }
            }
            var delta = _transform.GetWorldPosition(other) + windowModel.GroundOffset - position;
            if (CMU3DSceneLayout.TryWindowMountOffset(model.Parts, yaw, parts, windowYaw, delta, out var offset, model.WindowMountInside, exteriorAxis) &&
                offset.LengthSquared() > result.LengthSquared())
                result = offset;
        }
        return result;
    }

    private IReadOnlyList<CMU3DModelPart> FitPanelEnds(EntityUid uid, CMU3DModelPrototype model, float yaw,
        IReadOnlyList<CMU3DModelPart> parts, Vector2 mountedOffset)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid) || parts.Count == 0)
            return parts;
        var gridUid = xform.GridUid.Value;
        var gridYaw = (float) _transform.GetWorldRotation(gridUid).Theta;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var position = _transform.GetWorldPosition(xform) + model.GroundOffset + mountedOffset;
        var ends = new Vector2(parts.Min(p => p.Min.X), parts.Max(p => p.Max.X));
        for (var x = -1; x <= 1; x++)
        for (var y = -1; y <= 1; y++)
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile + new Vector2i(x, y)))
        {
            if (other == uid || !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                Array.IndexOf(model.PanelEndTargets, prototype.ID) < 0 ||
                _catalog?.Resolve(prototype.ID) is not { Exact: true } match || !TryComp(other, out SpriteComponent? sprite))
                continue;
            var obstacle = match.Model;
            if (obstacle.ConnectToNeighbours || obstacle.WallMounted || obstacle.FaceAwayFromWall ||
                obstacle.WindowMountTargets.Length > 0 || obstacle.BackWallMountTargets.Length > 0)
                continue;
            var otherYaw = CMU3DSceneLayout.RenderYaw(obstacle, (float) _transform.GetWorldRotation(other).Theta,
                sprite.NoRotation, sprite.SnapCardinals) + (float) sprite.Rotation.Theta;
            if (obstacle.CornerSurfaces.Length == 32)
                otherYaw = gridYaw;
            // Grid-horizontal panes continue through a corner; the perpendicular pane forms the butt end.
            // The decision is independent of entity ID, iteration order and rotation of the whole grid.
            if (obstacle.PanelEndTargets.Length > 0 && (MathF.Abs(MathF.Sin(otherYaw - yaw)) < .99999f ||
                (int) MathF.Floor((yaw - gridYaw) / (MathF.PI / 2) + .5f) % 2 == 0))
                continue;
            var delta = _transform.GetWorldPosition(other) + obstacle.GroundOffset - position;
            var limit = CMU3DSceneLayout.PanelEndLimits(parts, yaw, obstacle.Parts, otherYaw, delta);
            ends = new Vector2(MathF.Max(ends.X, limit.X), MathF.Min(ends.Y, limit.Y));
        }
        return CMU3DSceneLayout.FitPanelEnds(parts, ends);
    }

    private float OpeningFacingYaw(EntityUid uid, CMU3DModelPrototype model, float yaw)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return yaw;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var walls = 0;
        foreach (var (flag, delta, _) in CMU3DSceneLayout.Cardinals)
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile + delta))
        {
            if (TryComp(other, out IconSmoothComponent? smooth) && smooth.Enabled && smooth.SmoothKey == "walls")
                walls |= flag;
        }
        var gridYaw = (float) _transform.GetWorldRotation(gridUid).Theta;
        var position = _transform.GetWorldPosition(xform);
        float? result = null;
        // Decorative showers are static map fixtures but are not anchored.
        _wallTargets.Clear();
        var extent = new Vector2(grid.TileSize);
        _lookup.GetEntitiesIntersecting(xform.MapID, new Box2(position - extent, position + extent), _wallTargets, LookupFlags.Uncontained);
        foreach (var (other, _) in _wallTargets)
        {
            if (other == uid || !TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                Array.IndexOf(model.OpeningFacingTargets, prototype.ID) < 0 ||
                !TryComp(other, out TransformComponent? targetXform) || targetXform.GridUid != gridUid ||
                _map.TileIndicesFor(gridUid, grid, targetXform.Coordinates) != tile ||
                _containers.IsEntityOrParentInContainer(other, meta, targetXform) ||
                Vector2.Distance(_transform.GetWorldPosition(other), position) > .125f)
                continue;
            var sourceYaw = (float) _transform.GetWorldRotation(other).Theta;
            var adjusted = CMU3DSceneLayout.OpeningFixtureYaw(yaw, gridYaw, walls, sourceYaw);
            if (MathF.Abs(MathF.Sin((adjusted - yaw) / 2)) < .00001f)
                continue;
            if (result is { } previous && MathF.Abs(MathF.Sin((adjusted - previous) / 2)) > .00001f)
                return yaw;
            result = adjusted;
        }
        return result ?? yaw;
    }

    private float ApplianceYaw(EntityUid uid, CMU3DModelPrototype model, float yaw)
    {
        if (!TryComp(uid, out TransformComponent? xform) || !TryComp(xform.GridUid, out MapGridComponent? grid))
            return yaw;
        var gridUid = xform.GridUid.Value;
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var walls = 0;
        foreach (var (flag, offset, _) in CMU3DSceneLayout.Cardinals)
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile + offset))
        {
            if (TryComp(other, out IconSmoothComponent? smooth) && smooth.Enabled &&
                CMU3DSceneLayout.IsApplianceBacking(model, smooth.SmoothKey))
                walls |= flag;
        }
        return CMU3DSceneLayout.FaceAwayFromWallYaw(yaw, (float) _transform.GetWorldRotation(gridUid).Theta, walls);
    }

    private bool TryWallMount(EntityUid uid, CMU3DModelPrototype model, ref float yaw, out bool inside, out Vector2 offset)
    {
        inside = false;
        offset = Vector2.Zero;
        if (!TryComp(uid, out TransformComponent? xform) || !TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
        var gridUid = xform.GridUid.Value;
        var position = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        bool WallAt(Vector2i tile)
        {
            foreach (var other in _map.GetAnchoredEntities(gridUid, grid, tile))
            {
                if (TryComp(other, out IconSmoothComponent? smooth) && smooth.Enabled && smooth.SmoothKey == "walls")
                    return true;
            }
            return false;
        }
        var onWall = WallAt(position);
        var gridYaw = (float) _transform.GetWorldRotation(gridUid).Theta;
        if (model.WallFacingTargets.Length > 0)
        {
            var targets = 0;
            var walls = 0;
            foreach (var (flag, delta, _) in CMU3DSceneLayout.Cardinals)
            {
                if (WallAt(position + delta))
                    walls |= flag;
            }
            // Decorative workstation bodies are static but not anchored. Include neighbours beyond the review crop.
            _wallTargets.Clear();
            var center = _transform.GetWorldPosition(xform);
            var extent = new Vector2(grid.TileSize * 2);
            _lookup.GetEntitiesIntersecting(xform.MapID, new Box2(center - extent, center + extent), _wallTargets, LookupFlags.Uncontained);
            foreach (var (other, _) in _wallTargets)
            {
                if (!TryComp(other, out MetaDataComponent? meta) || meta.EntityPrototype is not { } prototype ||
                    Array.IndexOf(model.WallFacingTargets, prototype.ID) < 0 ||
                    !TryComp(other, out TransformComponent? targetXform) || targetXform.GridUid != gridUid ||
                    _containers.IsEntityOrParentInContainer(other, meta, targetXform))
                    continue;
                var delta = _map.TileIndicesFor(gridUid, grid, targetXform.Coordinates) - position;
                var offsetMask = CMU3DSceneLayout.OffsetTargetMask(_transform.GetWorldPosition(targetXform) - center, gridYaw);
                if (delta == Vector2i.Zero)
                    targets |= offsetMask;
                foreach (var (flag, cardinal, _) in CMU3DSceneLayout.Cardinals)
                {
                    if (delta == cardinal)
                        targets |= offsetMask != 0 ? offsetMask : flag;
                }
            }
            yaw = CMU3DSceneLayout.WallTargetYaw(yaw, gridYaw, onWall, targets, walls);
        }
        var localYaw = yaw - gridYaw;
        var dx = MathF.Sin(localYaw);
        var dy = -MathF.Cos(localYaw);
        if (MathF.Abs(dx - MathF.Round(dx)) > .00001f || MathF.Abs(dy - MathF.Round(dy)) > .00001f)
            return false;
        inside = !onWall && WallAt(position + new Vector2i((int) MathF.Round(dx), (int) MathF.Round(dy)));
        if (!onWall && !inside)
            return false;
        offset = CMU3DSceneLayout.WallMountOffset(_transform.GetWorldPosition(xform),
            _map.GridTileToWorldPos(gridUid, grid, position), yaw);
        return true;
    }

    private bool TryConnections(EntityUid uid, out int mask, out float gridYaw, bool alignToWalls = true, bool corners = false)
    {
        mask = 0;
        gridYaw = 0;
        if (!TryComp(uid, out IconSmoothComponent? smooth) || !smooth.Enabled ||
            (corners && smooth.Mode != IconSmoothingMode.Corners) ||
            !TryComp(uid, out TransformComponent? xform) || !xform.Anchored ||
            !TryComp(xform.GridUid, out MapGridComponent? grid))
            return false;
        var gridUid = xform.GridUid.Value;
        var position = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        gridYaw = (float) _transform.GetWorldRotation(gridUid).Theta;
        if (corners)
        {
            foreach (var (flag, offset) in CMU3DSceneLayout.CornerNeighbours)
            foreach (var other in _map.GetAnchoredEntities(gridUid, grid, position + offset))
            {
                if (TryComp(other, out IconSmoothComponent? neighbour) && neighbour.Enabled &&
                    (neighbour.SmoothKey == smooth.SmoothKey || smooth.AdditionalKeys.Contains(neighbour.SmoothKey)))
                    mask |= flag;
            }
            return true;
        }
        var walls = 0;
        foreach (var (flag, offset, _) in CMU3DSceneLayout.Cardinals)
        {
            foreach (var other in _map.GetAnchoredEntities(gridUid, grid, position + offset))
            {
                if (!TryComp(other, out IconSmoothComponent? neighbour) || !neighbour.Enabled)
                    continue;
                if (neighbour.SmoothKey == "walls")
                    walls |= flag;
                if (neighbour.SmoothKey == smooth.SmoothKey || smooth.AdditionalKeys.Contains(neighbour.SmoothKey))
                    mask |= flag;
            }
        }
        // Only an unambiguous wall axis can orient an otherwise isolated panel.
        if (alignToWalls && mask == 0 && walls != 0 && ((walls & 3) == 0 || (walls & 12) == 0))
            mask = walls;
        return true;
    }

    private bool AddModel(EntityUid uid, IReadOnlyList<CMU3DModelPart> parts, Vector2 position, float yaw, Color tint, float wallHeight)
    {
        var cosine = MathF.Cos(yaw);
        var sine = MathF.Sin(yaw);
        var heightScale = 1f;
        if (_elevation.TryStair(uid, out var ramp) && parts.Count > 0)
        {
            var top = parts.Max(part => part.Max.Z);
            if (top > 0)
                heightScale = (ramp.Top - ramp.Bottom) / top;
        }
        // Layout, neighbours and appearance are resolved above on every sample. Cache
        // only the resulting local assembly; camera translation and floor height remain live.
        var key = new GeometryKey(parts, yaw, heightScale, wallHeight, tint);
        _geometryUsed.Add(uid);
        var count = -1;
        if (_geometryCache.TryGetValue(uid, out var cached))
        {
            if (cached.Key == key)
            {
                if (cached.PartCount > ViewPartLimit - _boxes.Count)
                {
                    _mapRejectedParts += cached.PartCount;
                    return false;
                }
                if (cached.Boxes != null)
                {
                    AppendGeometry(cached.Boxes, position);
                    return true;
                }
                count = cached.PartCount;
            }
            _cachedParts -= cached.Boxes?.Count ?? 0;
            _geometryCache.Remove(uid);
        }
        if (count < 0)
        {
            count = 0;
            for (var i = 0; i < parts.Count; i++)
            {
                if (parts[i].Valid && MathF.Min(parts[i].Max.Z, wallHeight) > parts[i].Min.Z)
                    count++;
            }
        }
        // A full map budget must not assemble and allocate every remaining prop only
        // to discard it. Reject whole sources before transformation and atlas lookup.
        if (count > ViewPartLimit - _boxes.Count)
        {
            _mapRejectedParts += count;
            _geometryCache[uid] = new Geometry(key, null, count);
            return false;
        }
        var assembly = new List<CMU3DSceneBox>(count);
        foreach (var part in parts)
        {
            if (!part.Valid)
                continue;
            var max = part.Max;
            max.Z = MathF.Min(max.Z, wallHeight);
            if (max.Z <= part.Min.Z)
                continue;
            var min = part.Min;
            min.Z *= heightScale;
            max.Z *= heightScale;
            var center = (min + max) / 2;
            var rotated = new Vector3(center.X * cosine - center.Y * sine,
                center.X * sine + center.Y * cosine, center.Z);
            var surface = part.Surface is { } surfaceId ? _prototypes.Index(surfaceId).AtlasIndex : (byte) 0;
            assembly.Add(new CMU3DSceneBox(rotated, (max - min) / 2, yaw + part.YawRadians, part.Color * tint, uid, part.Shape,
                surface, part.SurfaceAxis, (max.Z - min.Z) / ((part.Max.Z - part.Min.Z) * heightScale), part.SurfaceFlipU) { Pitch = part.PitchRadians });
        }
        if (assembly.Count > 0 && _cachedParts + assembly.Count <= ScenePartLimit * 2)
        {
            _geometryCache[uid] = new Geometry(key, assembly, assembly.Count);
            _cachedParts += assembly.Count;
        }
        AppendGeometry(assembly, position);
        return true;
    }

    private void SelectEntity(EntityUid? uid)
    {
        _selected = uid;
        _selectedModel = null;
        if (_window is not { } window)
            return;
        window.Inspect.Disabled = true;
        if (uid is not { } entity || !TryComp(entity, out MetaDataComponent? meta))
        {
            window.Selection.Text = Loc.GetString("cmu-3d-live-select");
            return;
        }
        var prototype = meta.EntityPrototype?.ID ?? "?";
        var match = _catalog?.Resolve(prototype);
        if (_catalog?.HasRandomSpriteVariants(prototype) == true)
            match = TryComp(entity, out RandomSpriteComponent? random)
                ? _catalog.ResolveRandomSprite(prototype, random.Selected)
                : null;
        if (TryComp(entity, out DoorComponent? door))
            match = _catalog?.WithDoorState(match, door.State);
        if (TryComp(entity, out FoldableComponent? folded))
            match = _catalog?.WithFoldState(match, folded.IsFolded);
        match = _catalog?.WithDirection(match, (float) _transform.GetWorldRotation(entity).Theta);
        _selectedModel = match?.Model.ID;
        window.Selection.Text = Loc.GetString("cmu-3d-live-selected", ("prototype", prototype),
            ("model", _selectedModel ?? Loc.GetString("cmu-3d-live-no-model")));
        window.Inspect.Disabled = _selectedModel == null;
    }

    public override void Shutdown()
    {
        _admins.AdminStatusUpdated -= OnAdminStatusUpdated;
        ShutdownCaptureBinding();
        Close();
        base.Shutdown();
    }
}
