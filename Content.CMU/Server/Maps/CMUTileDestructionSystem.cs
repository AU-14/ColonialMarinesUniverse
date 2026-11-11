using Content.Shared._RMC14.Explosion;
using Content.Shared.CMU14.Maps;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Sticky;
using Content.Shared.Sticky.Components;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Maps;

/// <summary>
/// Destroyable tiles: rolls a tile's <see cref="ContentTileDefinition.DestroyDrops"/> when it's destroyed, and lets
/// C4 and breaching charges be planted on tiles with <see cref="ContentTileDefinition.AllowCharges"/>.
/// A planted charge sticks to an invisible anchor on the tile; when the charge goes off it deletes the anchor like a
/// wall, and the anchor takes the tile with it.
/// </summary>
public sealed partial class CMUTileDestructionSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TileSystem _tile = default!;
    [Dependency] private ITileDefinitionManager _tileDefs = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly EntProtoId AnchorPrototype = "CMUTileChargeAnchor";
    private const string StickerContainer = "stickers_container";

    private static readonly Vector2i[] Cardinals = [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    // Tiles next to a removed tile, checked next tick in case they were left floating.
    private readonly HashSet<(EntityUid Grid, Vector2i Indices)> _collapseChecks = new();
    private readonly List<(EntityUid Grid, Vector2i Indices)> _collapseProcessing = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCExplosiveDeleteComponent, AfterInteractEvent>(OnChargeAfterInteract);
        SubscribeLocalEvent<CMUTileChargeAnchorComponent, EntityTerminatingEvent>(OnAnchorTerminating);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            if (IsSupportTile(change.NewTile) || !IsSupportTile(change.OldTile))
                continue;

            foreach (var dir in Cardinals)
            {
                _collapseChecks.Add((args.Entity.Owner, change.GridIndices + dir));
            }
        }
    }

    private void ProcessCollapseChecks()
    {
        if (_collapseChecks.Count == 0)
            return;

        _collapseProcessing.Clear();
        _collapseProcessing.AddRange(_collapseChecks);
        _collapseChecks.Clear();

        foreach (var (gridUid, indices) in _collapseProcessing)
        {
            if (TerminatingOrDeleted(gridUid) || !TryComp(gridUid, out MapGridComponent? grid))
                continue;

            if (!_map.TryGetTileRef(gridUid, grid, indices, out var tileRef) ||
                tileRef.Tile.IsEmpty ||
                _tileDefs[tileRef.Tile.TypeId] is not ContentTileDefinition { CollapseWhenUnsupported: true })
            {
                continue;
            }

            if (HasNeighbourTile((gridUid, grid), indices))
                continue;

            // Destroying it raises its own tile change, which queues anything it was holding up in turn.
            DestroyTile(tileRef);
        }
    }

    private bool HasNeighbourTile(Entity<MapGridComponent> grid, Vector2i indices)
    {
        foreach (var dir in Cardinals)
        {
            if (_map.TryGetTileRef(grid, grid, indices + dir, out var neighbour) && IsSupportTile(neighbour.Tile))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a tile can hold up a destroyable tile next to it. Multi-Z levels fill open sky with transparent
    /// filler tiles (like CMUFloorEmpty), so a transparent tile only counts if it's a destroyable tile itself.
    /// </summary>
    private bool IsSupportTile(Tile tile)
    {
        if (tile.IsEmpty || _tileDefs[tile.TypeId] is not ContentTileDefinition def)
            return false;

        return !def.Transparent || def.CollapseWhenUnsupported;
    }

    /// <summary>Rolls the tile's destroy drops and spawns them in its place.</summary>
    public void SpawnDestroyDrops(TileRef tileRef)
    {
        if (_tileDefs[tileRef.Tile.TypeId] is not ContentTileDefinition def || def.DestroyDrops.Count == 0)
            return;

        if (!TryComp(tileRef.GridUid, out MapGridComponent? grid))
            return;

        var center = _map.GridTileToLocal(tileRef.GridUid, grid, tileRef.GridIndices);
        foreach (var id in EntitySpawnCollection.GetSpawns(def.DestroyDrops, _random))
        {
            var offset = new System.Numerics.Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.3f, 0.3f));
            var debris = Spawn(id, center.Offset(offset));
            _transform.SetLocalRotation(debris, _random.NextAngle());

            // Scatter the debris a short way from where the tile was.
            var direction = _random.NextAngle().ToVec() * _random.NextFloat(0.5f, 1.5f);
            _throwing.TryThrow(debris, direction, baseThrowSpeed: _random.NextFloat(3f, 5f), recoil: false,
                playSound: false, compensateFriction: true);
        }
    }

    /// <summary>Breaks the tile down to the layer below it and drops its debris.</summary>
    public void DestroyTile(TileRef tileRef)
    {
        if (tileRef.Tile.IsEmpty)
            return;

        SpawnDestroyDrops(tileRef);
        _tile.DeconstructTile(tileRef, spawnItem: false);
    }

    private void OnChargeAfterInteract(Entity<RMCExplosiveDeleteComponent> ent, ref AfterInteractEvent args)
    {
        // Charges on walls and other entities are handled by the sticky system; this is only for bare floor.
        if (args.Handled || !args.CanReach || args.Target != null || !TryComp(ent, out StickyComponent? sticky) ||
            sticky.StuckTo != null)
        {
            return;
        }

        if (!TryGetChargeableTile(args.ClickLocation, out var tileRef, out var grid))
            return;

        args.Handled = true;

        var anchor = GetOrSpawnAnchor(tileRef, grid);
        if (sticky.StickPopupStart != null)
            _popup.PopupEntity(Loc.GetString(sticky.StickPopupStart), args.User, args.User);

        // Same do-after the sticky system uses, so finishing it sticks the charge to the anchor and arms it.
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, sticky.StickDelay, new StickyDoAfterEvent(), ent,
            target: anchor, used: ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            ForceVisible = true,
        });
    }

    private bool TryGetChargeableTile(EntityCoordinates coords, out TileRef tileRef, out Entity<MapGridComponent> grid)
    {
        tileRef = default;
        grid = default;
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? gridComp))
            return false;

        if (!_map.TryGetTileRef(gridUid, gridComp, coords, out tileRef) ||
            _tileDefs[tileRef.Tile.TypeId] is not ContentTileDefinition { AllowCharges: true })
        {
            return false;
        }

        grid = (gridUid, gridComp);
        return true;
    }

    private EntityUid GetOrSpawnAnchor(TileRef tileRef, Entity<MapGridComponent> grid)
    {
        var anchored = _map.GetAnchoredEntities(grid, grid, tileRef.GridIndices);
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<CMUTileChargeAnchorComponent>(uid))
                return uid.Value;
        }

        var anchor = Spawn(AnchorPrototype, _map.GridTileToLocal(grid, grid, tileRef.GridIndices));
        var comp = EnsureComp<CMUTileChargeAnchorComponent>(anchor);
        comp.ExpireAt = _timing.CurTime + comp.UnarmedLifetime;
        return anchor;
    }

    private void OnAnchorTerminating(Entity<CMUTileChargeAnchorComponent> ent, ref EntityTerminatingEvent args)
    {
        // Only a detonated charge takes the tile with it, not the round ending or an anchor nobody used.
        if (!ent.Comp.Armed)
            return;

        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || TerminatingOrDeleted(gridUid) || !TryComp(gridUid, out MapGridComponent? grid))
            return;

        if (_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
            DestroyTile(tileRef);
    }

    public override void Update(float frameTime)
    {
        ProcessCollapseChecks();

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUTileChargeAnchorComponent>();
        while (query.MoveNext(out var uid, out var anchor))
        {
            if (anchor.Armed)
                continue;

            if (_container.TryGetContainer(uid, StickerContainer, out var container) && container.ContainedEntities.Count > 0)
            {
                anchor.Armed = true;
                continue;
            }

            // Nobody finished planting a charge here; clean up.
            if (time >= anchor.ExpireAt)
                QueueDel(uid);
        }
    }
}
