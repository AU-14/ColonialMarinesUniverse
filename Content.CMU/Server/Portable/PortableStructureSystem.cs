// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Power.Components;
using Content.Shared.CMU14.Portable;
using Content.Shared.DoAfter;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Portable;

// Portable structures: pick up a built structure into an item (do-after) and put it down again where there is room.
public sealed class PortableStructureSystem : SharedPortableStructureSystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    // Structures that were just welded down and have their item parts taken off once the hands are done with them.
    private readonly List<Entity<PortableStructureComponent>> _settled = new();

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<PortableStructureComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<PortableStructureComponent, PortablePickUpDoAfterEvent>(OnPickUpDoAfter);
        SubscribeLocalEvent<PortableStructureComponent, AfterInteractEvent>(OnAfterInteract);

        // A structure that is not welded down is only an item: it does nothing.
        SubscribeLocalEvent<PortableStructureComponent, ActivatableUIOpenAttemptEvent>(OnUiAttempt);
        SubscribeLocalEvent<PortableStructureComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<PortableStructureComponent, UseInHandEvent>(OnUseInHand);
    }

    #region Picking up

    private void OnGetVerbs(Entity<PortableStructureComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<GhostComponent>(args.User) || args.Hands == null)
            return;

        if (_container.IsEntityInContainer(ent))
            return;

        var user = args.User;
        var target = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-portable-pick-up"),
            Priority = 20,
            Act = () => StartPickUp(target, user),
        });
    }

    private void StartPickUp(Entity<PortableStructureComponent> ent, EntityUid user)
    {
        if (!CanPickUp(ent, user))
            return;

        if (_hands.CountFreeHands(user) < 2)
        {
            _popup.PopupEntity(Loc.GetString("au14-portable-need-hands"), ent, user, PopupType.SmallCaution);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.PickUpTime, new PortablePickUpDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("au14-portable-picking-up", ("name", Name(ent))), ent, user);
    }

    private void OnPickUpDoAfter(Entity<PortableStructureComponent> ent, ref PortablePickUpDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        TryPickUp(ent, args.User);
    }

    // Frees the structure from the floor and takes it into both hands.
    public bool TryPickUp(Entity<PortableStructureComponent> ent, EntityUid user)
    {
        if (!CanPickUp(ent, user))
            return false;

        if (_hands.CountFreeHands(user) < 2)
        {
            _popup.PopupEntity(Loc.GetString("au14-portable-need-hands"), ent, user, PopupType.SmallCaution);
            return false;
        }

        var xform = Transform(ent);
        var wasAnchored = xform.Anchored;
        var coordinates = xform.Coordinates;

        // The body type is read first: coming loose from the floor changes it.
        TryComp<PhysicsComponent>(ent, out var body);
        if (body != null)
            ent.Comp.OriginalBodyType ??= body.BodyType;

        if (wasAnchored)
            _transform.Unanchor(ent, xform);

        if (body != null && body.BodyType == BodyType.Static)
            _physics.SetBodyType(ent, BodyType.Dynamic, body: body);

        // Anything can be carried once it is an item that takes both hands.
        ent.Comp.AddedItem = !HasComp<ItemComponent>(ent);
        if (ent.Comp.AddedItem)
        {
            EnsureComp<ItemComponent>(ent);
            _item.SetSize(ent, "Huge");
        }

        ent.Comp.AddedMultiHanded = !HasComp<MultiHandedItemComponent>(ent);
        if (ent.Comp.AddedMultiHanded)
            EnsureComp<MultiHandedItemComponent>(ent);

        SwitchPower(ent, off: true);

        if (_hands.TryPickupAnyHand(user, ent))
        {
            _popup.PopupEntity(Loc.GetString("au14-portable-picked-up", ("name", Name(ent))), ent, user);
            return true;
        }

        // It did not fit in the hands after all: put it back where it was.
        MakeStructure(ent);
        if (wasAnchored)
            Anchor(ent, coordinates);

        return false;
    }

    private bool CanPickUp(Entity<PortableStructureComponent> ent, EntityUid user)
    {
        var attempt = new PortablePickUpAttemptEvent(user);
        RaiseLocalEvent(ent, attempt);
        return !attempt.Cancelled;
    }

    #endregion

    #region Putting down

    // Clicking a tile with the structure in the hands welds it there.
    private void OnAfterInteract(Entity<PortableStructureComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || !_hands.IsHolding(args.User, ent.Owner))
            return;

        args.Handled = true;

        if (!TryGetPlacement(ent, args.User, args.ClickLocation, out var coordinates))
        {
            _popup.PopupEntity(Loc.GetString("au14-portable-cannot-place"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        _hands.TryDrop(args.User, ent.Owner, coordinates, checkActionBlocker: false, doDropInteraction: false);
        Anchor(ent, coordinates);
        _settled.Add(ent);

        _popup.PopupEntity(Loc.GetString("au14-portable-placed", ("name", Name(ent))), ent, args.User);
    }

    // The clicked tile, if the structure can stand there.
    private bool TryGetPlacement(Entity<PortableStructureComponent> ent, EntityUid user, EntityCoordinates clicked, out EntityCoordinates coordinates)
    {
        coordinates = default;

        if (_transform.GetGrid(clicked) is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, clicked);
        if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return false;

        // Not on the tile the carrier stands on: they would be walled in.
        var userCoordinates = Transform(user).Coordinates;
        if (_transform.GetGrid(userCoordinates) == gridUid && _map.TileIndicesFor(gridUid, grid, userCoordinates) == tile)
            return false;

        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (anchored != ent.Owner && !IsStoodOn(ent, anchored))
                return false;
        }

        // Anything a person cannot walk through, except the structure itself, is in the way.
        if (_turf.IsTileBlocked(gridUid, tile, CollisionGroup.MobMask, grid, ignore: other => other == ent.Owner || IsStoodOn(ent, other)))
            return false;

        coordinates = _map.GridTileToLocal(gridUid, grid, tile);
        return true;
    }

    // The thing is what the structure is made to stand on (the water under a pump, the deposit under a drill), so it does not count as being in the way.
    private bool IsStoodOn(Entity<PortableStructureComponent> ent, EntityUid other)
    {
        var stood = new PortablePlaceableOverEvent(other);
        RaiseLocalEvent(ent, ref stood);
        return stood.Allowed;
    }

    // Takes off what was added to make it carryable and gives it back its body.
    private void MakeStructure(Entity<PortableStructureComponent> ent)
    {
        if (ent.Comp.AddedMultiHanded)
            RemComp<MultiHandedItemComponent>(ent);

        if (ent.Comp.AddedItem)
            RemComp<ItemComponent>(ent);

        ent.Comp.AddedMultiHanded = false;
        ent.Comp.AddedItem = false;

        if (ent.Comp.OriginalBodyType is { } type && TryComp<PhysicsComponent>(ent, out var body) && body.BodyType != type)
            _physics.SetBodyType(ent, type, body: body);
    }

    private void Anchor(Entity<PortableStructureComponent> ent, EntityCoordinates coordinates)
    {
        _transform.SetCoordinates(ent, coordinates);
        _transform.AnchorEntity(ent);
        SwitchPower(ent, off: false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_settled.Count == 0)
            return;

        // The hands are done with it by now, so it can stop being an item.
        foreach (var ent in _settled)
        {
            if (!TerminatingOrDeleted(ent) && Transform(ent).Anchored)
                MakeStructure(ent);
        }

        _settled.Clear();
    }

    #endregion

    #region Not working as an item

    // Machines lose their power while they are carried or lying around, and get it back when they are welded down.
    private void SwitchPower(Entity<PortableStructureComponent> ent, bool off)
    {
        if (off)
        {
            ent.Comp.PowerSwitchedOff = TryComp<ApcPowerReceiverComponent>(ent, out var receiver) && !receiver.PowerDisabled;
            if (ent.Comp.PowerSwitchedOff)
                _power.SetPowerDisabled(ent, true);

            return;
        }

        if (ent.Comp.PowerSwitchedOff)
            _power.SetPowerDisabled(ent, false);

        ent.Comp.PowerSwitchedOff = false;
    }

    private void OnUiAttempt(Entity<PortableStructureComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (!Transform(ent).Anchored)
            args.Cancel();
    }

    private void OnActivate(Entity<PortableStructureComponent> ent, ref ActivateInWorldEvent args)
    {
        if (!Transform(ent).Anchored)
            args.Handled = true;
    }

    private void OnUseInHand(Entity<PortableStructureComponent> ent, ref UseInHandEvent args)
    {
        if (!Transform(ent).Anchored)
            args.Handled = true;
    }

    #endregion
}
