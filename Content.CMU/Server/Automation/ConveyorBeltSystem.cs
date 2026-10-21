// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Server.Storage.EntitySystems;
using Content.Shared.CMU14.Automation;
using Content.Shared.Conveyor;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Automation;

// The belts of the factory floor: they are always on, they are put down from a stack in the hand and taken up again, and a crate or a box at the end of a belt takes what
// the belt brings.
public sealed class ConveyorBeltSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    // How many things a belt tile holds before it counts as full: a machine does not put more on it.
    public const int TileCapacity = 4;

    private readonly HashSet<Entity<ItemComponent>> _items = new();
    private readonly HashSet<Entity<StorageComponent>> _boxes = new();
    private readonly HashSet<Entity<EntityStorageComponent>> _crates = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ConveyorBeltComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ConveyorBeltComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BeltPlacerComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BeltPlacerComponent, UseInHandEvent>(OnUseInHand);
    }

    // The belt is on from the start: the conveyor of the game waits for power and for a signal, and the belts here ask for neither.
    private void OnStartup(Entity<ConveyorBeltComponent> belt, ref ComponentStartup args)
    {
        if (!TryComp<ConveyorComponent>(belt, out var conveyor))
            return;

        conveyor.Powered = true;
        conveyor.State = ConveyorState.Forward;
        Dirty(belt, conveyor);
    }

    // The way a belt carries, as a vector on the map.
    public Vector2 DirectionOf(EntityUid belt)
    {
        var angle = _transform.GetWorldRotation(belt);
        if (TryComp<ConveyorComponent>(belt, out var conveyor))
            angle += conveyor.Angle;

        return angle.ToWorldVec();
    }

    // The way a belt faces for a number of quarter turns clockwise from north.
    public static Angle RotationFor(int turns)
    {
        var vector = (turns & 3) switch
        {
            0 => new Vector2(0, 1),
            1 => new Vector2(1, 0),
            2 => new Vector2(0, -1),
            _ => new Vector2(-1, 0),
        };

        return vector.ToWorldAngle();
    }

    #region Putting down and taking up

    private void OnUseInHand(Entity<BeltPlacerComponent> placer, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        placer.Comp.Turns = (placer.Comp.Turns + 1) & 3;
        _popup.PopupEntity(Loc.GetString("au14-belt-facing", ("side", Loc.GetString($"au14-belt-side-{placer.Comp.Turns}"))), placer, args.User);
    }

    private void OnAfterInteract(Entity<BeltPlacerComponent> placer, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var coordinates = args.ClickLocation;
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, coordinates);
        args.Handled = true;

        if (_map.GetTileRef(gridUid, grid, tile).Tile.IsEmpty || _turf.IsTileBlocked(gridUid, tile, CollisionGroup.Impassable, grid))
        {
            _popup.PopupEntity(Loc.GetString("au14-belt-blocked"), placer, args.User, PopupType.SmallCaution);
            return;
        }

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var other))
        {
            if (HasComp<ConveyorBeltComponent>(other.Value))
            {
                _popup.PopupEntity(Loc.GetString("au14-belt-taken"), placer, args.User, PopupType.SmallCaution);
                return;
            }
        }

        Put(placer, gridUid, grid, tile);
    }

    private void Put(Entity<BeltPlacerComponent> placer, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        // One belt off the stack (a belt alone in the hand is used up).
        if (TryComp<StackComponent>(placer, out var stack))
            _stack.TryUse((placer, stack), 1);
        else
            QueueDel(placer);

        // The belt comes anchored (its prototype says so); it is turned the way it is to carry.
        var belt = Spawn(placer.Comp.Belt, _map.GridTileToLocal(gridUid, grid, tile));
        _transform.SetLocalRotation(belt, RotationFor(placer.Comp.Turns));
    }

    private void OnGetVerbs(Entity<ConveyorBeltComponent> belt, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var target = belt;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-belt-take-up"),
            Priority = 10,
            Act = () => TakeUp(target, user),
        });
    }

    // A belt comes up off the floor as the stack it was put down from.
    public void TakeUp(Entity<ConveyorBeltComponent> belt, EntityUid user)
    {
        var item = Spawn(belt.Comp.Item, Transform(belt).Coordinates);
        QueueDel(belt);
        _hands.TryPickupAnyHand(user, item);
    }

    #endregion

    #region What the belt brings

    // Puts something into a box or a crate: the one way in that the machines and the belts share.
    public bool TryPut(EntityUid container, EntityUid item)
    {
        if (TryComp<StorageComponent>(container, out var storage))
            return _storage.Insert(container, item, out _, user: null, storageComp: storage, playSound: false);

        if (TryComp<EntityStorageComponent>(container, out var crate) && _entityStorage.CanInsert(item, container, crate))
            return _entityStorage.Insert(item, container, crate);

        return false;
    }

    // The things lying loose on the tile of a belt.
    public void ItemsOn(EntityUid belt, List<EntityUid> found)
    {
        found.Clear();
        _items.Clear();
        _lookup.GetEntitiesInRange(Transform(belt).Coordinates, 0.45f, _items);

        foreach (var item in _items)
        {
            if (!Transform(item).Anchored)
                found.Add(item.Owner);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ConveyorBeltComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var belt, out var xform))
        {
            belt.Next -= frameTime;
            if (belt.Next > 0f)
                continue;

            belt.Next = belt.Interval;
            FeedFront(uid, xform);
        }
    }

    // A crate or a box on the tile in front of a belt takes what lies on the belt.
    private void FeedFront(EntityUid belt, TransformComponent xform)
    {
        var position = _transform.GetMapCoordinates(belt, xform);
        var front = new MapCoordinates(position.Position + DirectionOf(belt), position.MapId);

        _boxes.Clear();
        _crates.Clear();
        _lookup.GetEntitiesInRange(front, 0.4f, _boxes);
        _lookup.GetEntitiesInRange(front, 0.4f, _crates);
        if (_boxes.Count == 0 && _crates.Count == 0)
            return;

        var items = new List<EntityUid>();
        ItemsOn(belt, items);

        foreach (var item in items)
        {
            if (TerminatingOrDeleted(item))
                continue;

            foreach (var box in _boxes)
            {
                if (TryPut(box.Owner, item))
                    break;
            }

            // What a box took is in it now.
            if (TerminatingOrDeleted(item) || _container.IsEntityInContainer(item))
                continue;

            foreach (var crate in _crates)
            {
                if (TryPut(crate.Owner, item))
                    break;
            }
        }
    }

    #endregion
}
