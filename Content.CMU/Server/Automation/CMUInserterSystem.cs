using Content.Server.Storage.EntitySystems;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Grinding;
using Content.Shared.CMU14.Portable;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Item;
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
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Automation;

public sealed partial class CMUInserterSystem : EntitySystem
{
    [Dependency] private readonly ConveyorBeltSystem _belts = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly StorageSystem _storage = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<Entity<TransformComponent>> _nearby = new();
    private readonly List<EntityUid> _sources = new();
    private readonly List<EntityUid> _targets = new();
    private readonly List<EntityUid> _items = new();
    private readonly List<EntityUid> _onBelt = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUInserterComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMUInserterComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUInserterComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<CMUInserterComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<CMUInserterComponent, PortablePickUpAttemptEvent>(OnPickUpAttempt);
        SubscribeLocalEvent<CMUInserterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUInserterComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnStartup(Entity<CMUInserterComponent> arm, ref ComponentStartup args)
    {
        arm.Comp.Claw = _containers.EnsureContainer<ContainerSlot>(arm, CMUInserterComponent.ClawContainerId);
    }

    private void OnShutdown(Entity<CMUInserterComponent> arm, ref ComponentShutdown args) => EmptyClaw(arm);

    private void OnTerminating(Entity<CMUInserterComponent> arm, ref EntityTerminatingEvent args) => EmptyClaw(arm);

    private void OnAnchorChanged(Entity<CMUInserterComponent> arm, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored)
        {
            EmptyClaw(arm);
            SetPhase(arm, CMUInserterPhase.Idle);
        }
    }

    private void OnPickUpAttempt(Entity<CMUInserterComponent> arm, ref PortablePickUpAttemptEvent args)
    {
        if (HeldItem(arm.Comp) == null)
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("cmu-inserter-busy"), arm, args.User);
    }

    private void EmptyClaw(Entity<CMUInserterComponent> arm)
    {
        if (arm.Comp.Claw != null)
            _containers.EmptyContainer(arm.Comp.Claw, force: true, destination: Transform(arm).Coordinates);
    }

    private EntityUid? HeldItem(CMUInserterComponent arm)
    {
        return arm.Claw?.ContainedEntity is { } item && !TerminatingOrDeleted(item)
            && !EntityManager.IsQueuedForDeletion(item) ? item : null;
    }

    private void OnExamined(Entity<CMUInserterComponent> arm, ref ExaminedEvent args)
    {
        var facing = Transform(arm).LocalRotation.ToWorldVec();
        var side = Math.Abs(facing.X) > Math.Abs(facing.Y) ? (facing.X > 0 ? 1 : 3) : (facing.Y > 0 ? 0 : 2);
        args.PushMarkup(Loc.GetString("cmu-inserter-direction", ("side", Loc.GetString($"au14-belt-side-{side}"))));
    }

    private void OnGetVerbs(Entity<CMUInserterComponent> arm, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(arm.Comp.Enabled ? "cmu-inserter-disable" : "cmu-inserter-enable"),
            Act = () => SetEnabled(arm, !arm.Comp.Enabled),
        });

        if (HeldItem(arm.Comp) != null)
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cmu-inserter-release"),
                Act = () =>
                {
                    EmptyClaw(arm);
                    SetPhase(arm, CMUInserterPhase.Returning);
                },
            });
        }
    }

    /// <summary>Pauses the real transfer and its client animation at the same point.</summary>
    public void SetEnabled(Entity<CMUInserterComponent> arm, bool enabled)
    {
        if (arm.Comp.Enabled == enabled)
            return;

        arm.Comp.PausedProgress = Math.Clamp(1f - arm.Comp.Remaining / Math.Max(0.01f, arm.Comp.PhaseDuration), 0f, 1f);
        if (enabled)
            arm.Comp.PhaseStartedAt = _timing.CurTime - TimeSpan.FromSeconds(arm.Comp.PausedProgress * arm.Comp.PhaseDuration);
        arm.Comp.Enabled = enabled;
        Dirty(arm);
    }

    private void SetPhase(Entity<CMUInserterComponent> arm, CMUInserterPhase phase)
    {
        arm.Comp.Phase = phase;
        arm.Comp.PhaseStartedAt = _timing.CurTime;
        arm.Comp.PhaseDuration = Math.Max(0.05f, arm.Comp.Interval * 0.5f);
        arm.Comp.Remaining = arm.Comp.PhaseDuration;
        arm.Comp.PausedProgress = 0f;
        arm.Comp.Next = 0f;
        Dirty(arm);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var query = EntityQueryEnumerator<CMUInserterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var arm, out var xform))
        {
            if (!arm.Enabled || !xform.Anchored)
                continue;

            var ent = new Entity<CMUInserterComponent>(uid, arm);
            switch (arm.Phase)
            {
                case CMUInserterPhase.Idle:
                    arm.Next -= frameTime;
                    if (arm.Next <= 0f && !TryPickup(ent, xform))
                        arm.Next = arm.RetryInterval;
                    break;
                case CMUInserterPhase.Swinging:
                    arm.Remaining -= frameTime;
                    if (arm.Remaining <= 0f)
                        Deliver(ent, xform);
                    break;
                case CMUInserterPhase.Waiting:
                    arm.Next -= frameTime;
                    if (arm.Next <= 0f)
                        Deliver(ent, xform);
                    break;
                case CMUInserterPhase.Returning:
                    arm.Remaining -= frameTime;
                    if (arm.Remaining <= 0f)
                        SetPhase(ent, CMUInserterPhase.Idle);
                    break;
            }
        }
    }

    private bool TryPickup(Entity<CMUInserterComponent> arm, TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var facing = xform.LocalRotation.ToWorldVec();
        var offset = new Vector2i((int) Math.Round(facing.X), (int) Math.Round(facing.Y));
        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        FindAt(gridUid, grid, tile - offset, _sources);
        FindAt(gridUid, grid, tile + offset, _targets);
        if (_targets.Count == 0)
            return false;

        foreach (var source in _sources)
        {
            _items.Clear();
            if (TryComp<WindowMachineComponent>(source, out var machine))
            {
                if (!Transform(source).Anchored)
                    continue;

                // Never extract ingredients, catalysts, molds, cells or upgrades.
                foreach (var id in machine.OutputSlots)
                {
                    if (_itemSlots.TryGetSlot(source, id, out var slot)
                        && _itemSlots.CanEject(source, slot, null)
                        && slot.Item is { } item)
                        _items.Add(item);
                }

                // CMU14 (COUPLING-03): slots a player may fill but an inserter may empty; the machine gates them by eject attempt.
                foreach (var id in machine.AutomationOutputSlots)
                {
                    if (_itemSlots.TryGetSlot(source, id, out var slot)
                        && _itemSlots.CanEject(source, slot, null)
                        && slot.Item is { } item
                        && !_items.Contains(item))
                        _items.Add(item);
                }
            }
            else if (TryComp<StorageComponent>(source, out var storage))
                _items.AddRange(storage.Container.ContainedEntities);
            else if (TryComp<EntityStorageComponent>(source, out var crate))
                _items.AddRange(crate.Contents.ContainedEntities);
            else if (HasComp<ItemComponent>(source) && !Transform(source).Anchored)
                _items.Add(source);

            foreach (var item in _items)
            {
                if (TerminatingOrDeleted(item) || EntityManager.IsQueuedForDeletion(item)
                    || (_containers.TryGetContainingContainer(item, out var container)
                        && !_containers.CanRemove(item, container)))
                    continue;

                foreach (var target in _targets)
                {
                    if (CanDeposit(target, item) && _containers.Insert(item, arm.Comp.Claw))
                    {
                        arm.Comp.TransferRotation = xform.LocalRotation;
                        SetPhase(arm, CMUInserterPhase.Swinging);
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void Deliver(Entity<CMUInserterComponent> arm, TransformComponent xform)
    {
        if (HeldItem(arm.Comp) is not { } item)
        {
            SetPhase(arm, CMUInserterPhase.Returning);
            return;
        }

        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var facing = arm.Comp.TransferRotation.ToWorldVec();
            var offset = new Vector2i((int) Math.Round(facing.X), (int) Math.Round(facing.Y));
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            FindAt(gridUid, grid, tile + offset, _targets);
            foreach (var target in _targets)
            {
                if (!TryDeposit(target, item))
                    continue;

                if (HeldItem(arm.Comp) == null || !arm.Comp.Claw.Contains(item))
                {
                    SetPhase(arm, CMUInserterPhase.Returning);
                    return;
                }
                break;
            }
        }

        if (arm.Comp.Phase != CMUInserterPhase.Waiting)
            SetPhase(arm, CMUInserterPhase.Waiting);
        arm.Comp.Next = arm.Comp.RetryInterval;
    }

    private bool CanDeposit(EntityUid target, EntityUid item)
    {
        if (TryComp<WindowMachineComponent>(target, out var machine))
        {
            if (!Transform(target).Anchored)
                return false;

            foreach (var id in machine.StackSlots)
            {
                if (!_itemSlots.TryGetSlot(target, id, out var slot)
                    || !_itemSlots.CanInsert(target, slot, item, null, swap: true))
                    continue;
                if (slot.Item == null)
                    return true;
                if (TryComp<StackComponent>(item, out var stack)
                    && TryComp<StackComponent>(slot.Item, out var held)
                    && stack.StackTypeId == held.StackTypeId && _stacks.GetAvailableSpace(held) > 0)
                    return true;
            }
            return false;
        }
        if (TryComp<StorageComponent>(target, out var storage))
            return _storage.CanInsert(target, item, out _, storage);
        if (TryComp<EntityStorageComponent>(target, out var crate))
            return _entityStorage.CanInsert(item, target, crate);
        if (!HasComp<ConveyorBeltComponent>(target) || !Transform(target).Anchored)
            return false;
        _belts.ItemsOn(target, _onBelt);
        return _onBelt.Count < ConveyorBeltSystem.TileCapacity;
    }

    /// <summary>The anchored or loose entities standing on a tile (not in containers), sorted by id. Shared with the tray arms.</summary>
    public void FindAt(EntityUid gridUid, MapGridComponent grid, Vector2i tile, List<EntityUid> found)
    {
        found.Clear();
        _nearby.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _nearby,
            enlargement: 0f, flags: LookupFlags.Uncontained, gridComp: grid);
        foreach (var (uid, xform) in _nearby)
        {
            if (xform.GridUid == gridUid && !_containers.IsEntityOrParentInContainer(uid)
                && _map.TileIndicesFor(gridUid, grid, xform.Coordinates) == tile
                && !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid))
                found.Add(uid);
        }
        found.Sort((a, b) => a.Id.CompareTo(b.Id));
    }

    private bool TryDeposit(EntityUid target, EntityUid item)
    {
        if (TryComp<WindowMachineComponent>(target, out var machine))
        {
            if (!Transform(target).Anchored)
                return false;

            foreach (var id in machine.StackSlots)
            {
                if (!_itemSlots.TryGetSlot(target, id, out var slot))
                    continue;

                if (slot.Item is { } held)
                {
                    // Validate the occupied slot's whitelist, lock and insert/eject events before merging.
                    if (_itemSlots.CanInsert(target, slot, item, null, swap: true)
                        && _stacks.TryMergeStacks(item, held, out _))
                        return true;
                }
                else if (_itemSlots.TryInsert(target, slot, item, null))
                    return true;
            }
            return false;
        }

        if (HasComp<StorageComponent>(target) || HasComp<EntityStorageComponent>(target))
            return _belts.TryPut(target, item);

        if (!HasComp<ConveyorBeltComponent>(target) || !Transform(target).Anchored)
            return false;

        _belts.ItemsOn(target, _onBelt);
        if (_onBelt.Count >= ConveyorBeltSystem.TileCapacity)
            return false;

        // Container insertion moves items directly. A belt needs an explicit removal, only after checking room.
        if (_containers.IsEntityInContainer(item) && !_containers.TryRemoveFromContainer(item))
            return false;

        _transform.SetCoordinates(item, Transform(target).Coordinates);
        return true;
    }
}
