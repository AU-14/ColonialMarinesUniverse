using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared.Actions;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaCannonPackSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaCannonPackComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<YautjaCannonPackComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<YautjaCannonPackComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<YautjaCannonPackComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<YautjaCannonPackComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<YautjaCannonPackComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaCannonPackComponent, YautjaUsePlasmaCannonsActionEvent>(OnUseCannons);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, DroppedEvent>(OnLinkedCannonDropped);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, TakeAmmoEvent>(OnLinkedCannonTakeAmmo, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<YautjaCannonPackLinkedCannonComponent, AmmoShotEvent>(OnLinkedCannonAmmoShot);
        SubscribeLocalEvent<YautjaCannonPackProjectileRefundComponent, EntityTerminatingEvent>(OnCannonProjectileTerminating);
    }

    private void OnStartup(Entity<YautjaCannonPackComponent> ent, ref ComponentStartup args)
    {
        EnsureInternalCannon(ent);
    }

    private void OnShutdown(Entity<YautjaCannonPackComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Cannon is { } cannon && !TerminatingOrDeleted(cannon))
            QueueDel(cannon);

        ent.Comp.Cannon = null;
        ent.Comp.CannonContainer = null;
        ent.Comp.CannonsDeployed = false;
        ent.Comp.User = null;
    }

    private void OnExamined(Entity<YautjaCannonPackComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup($"It currently has <bold>{(int) ent.Comp.Charge}/{(int) ent.Comp.MaxCharge}</bold> charge.");
    }

    private void OnEquipped(Entity<YautjaCannonPackComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.BACK) == 0)
            return;

        ent.Comp.User = args.Equipee;
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenEvery;
    }

    private void OnUnequipped(Entity<YautjaCannonPackComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & SlotFlags.BACK) == 0)
            return;

        if (ent.Comp.CannonsDeployed &&
            ent.Comp.Cannon is { } cannon &&
            !TerminatingOrDeleted(cannon))
        {
            RetractCannons(ent, args.Equipee, cannon, false);
        }

        ent.Comp.User = null;
    }

    private void OnGetItemActions(Entity<YautjaCannonPackComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands ||
            args.SlotFlags == null ||
            (args.SlotFlags.Value & SlotFlags.BACK) == 0)
        {
            return;
        }

        args.AddAction(ref ent.Comp.UseCannonsAction, ent.Comp.UseCannonsActionId);
    }

    private void OnUseCannons(Entity<YautjaCannonPackComponent> ent, ref YautjaUsePlasmaCannonsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleCannons(ent, args.Performer);
    }

    private void ToggleCannons(Entity<YautjaCannonPackComponent> ent, EntityUid user)
    {
        if (_mobState.IsIncapacitated(user) ||
            !HasComp<HumanoidAppearanceComponent>(user) ||
            Transform(user).MapID == MapId.Nullspace)
        {
            return;
        }

        var cannon = EnsureInternalCannon(ent);
        if (cannon == null)
            return;

        if (ent.Comp.CannonsDeployed)
        {
            RetractCannons(ent, user, cannon.Value, true);
            return;
        }

        if (!TryDrainPackPower(ent, user, ent.Comp.DeployCost))
            return;

        if (!TryGetActivePickupHand(user, cannon.Value, out var hand))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cannon-pack-hands-full"), user, user, PopupType.SmallCaution);
            return;
        }

        if (HasComp<YautjaYoungbloodComponent>(user) || HasComp<YautjaThrallComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cannon-pack-role-denied"), user, user, PopupType.SmallCaution);
            return;
        }

        if (!_hands.TryPickup(user, cannon.Value, hand, checkActionBlocker: false))
            return;

        ent.Comp.CannonsDeployed = true;
        _actions.SetToggled(ent.Comp.UseCannonsAction, true);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-cannon-pack-activated"), user, user);
    }

    private bool TryGetActivePickupHand(EntityUid user, EntityUid cannon, out string hand)
    {
        hand = string.Empty;
        var activeHand = _hands.GetActiveHand(user);
        if (activeHand == null || !_hands.HandIsEmpty(user, activeHand))
            return false;

        if (!_hands.CanPickupToHand(user, cannon, activeHand, checkActionBlocker: false))
            return false;

        hand = activeHand;
        return true;
    }

    private void OnLinkedCannonDropped(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref DroppedEvent args)
    {
        if (TerminatingOrDeleted(ent.Comp.Pack) ||
            !TryComp(ent.Comp.Pack, out YautjaCannonPackComponent? pack) ||
            pack.Cannon != ent.Owner)
        {
            return;
        }

        RetractCannons((ent.Comp.Pack, pack), args.User, ent.Owner, true);
    }

    private void OnLinkedCannonTakeAmmo(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref TakeAmmoEvent args)
    {
        if (args.Ammo.Count != 0 ||
            args.Shots <= 0 ||
            TerminatingOrDeleted(ent.Comp.Pack) ||
            !TryComp(ent.Comp.Pack, out YautjaCannonPackComponent? pack) ||
            pack.Cannon != ent.Owner ||
            args.User is not { } user)
        {
            return;
        }

        if (!TryDrainPackPower((ent.Comp.Pack, pack), user, ent.Comp.ChargeCost))
            return;

        var projectile = Spawn(ent.Comp.Projectile, args.Coordinates);
        var refund = EnsureComp<YautjaCannonPackProjectileRefundComponent>(projectile);
        refund.Pack = ent.Comp.Pack;
        refund.ChargeCost = ent.Comp.ChargeCost;
        args.Ammo.Add((projectile, EnsureComp<AmmoComponent>(projectile)));
    }

    private void OnLinkedCannonAmmoShot(Entity<YautjaCannonPackLinkedCannonComponent> ent, ref AmmoShotEvent args)
    {
        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp(projectile, out YautjaCannonPackProjectileRefundComponent? refund) ||
                refund.Pack != ent.Comp.Pack)
            {
                continue;
            }

            refund.Fired = true;
        }
    }

    private void OnCannonProjectileTerminating(Entity<YautjaCannonPackProjectileRefundComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Fired ||
            TerminatingOrDeleted(ent.Comp.Pack) ||
            !TryComp(ent.Comp.Pack, out YautjaCannonPackComponent? pack))
        {
            return;
        }

        pack.Charge = FixedPoint2.Min(pack.Charge + ent.Comp.ChargeCost, pack.MaxCharge);
    }

    private void RetractCannons(Entity<YautjaCannonPackComponent> ent, EntityUid user, EntityUid cannon, bool showPopup)
    {
        if (_hands.IsHolding(user, cannon) && !_hands.TryDrop(user, cannon, checkActionBlocker: false, doDropInteraction: false))
            return;

        var container = EnsureCannonContainer(ent);
        if (!_containers.Insert(cannon, container, force: true))
            return;

        ent.Comp.CannonsDeployed = false;
        _actions.SetToggled(ent.Comp.UseCannonsAction, false);
        if (showPopup)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-cannon-pack-deactivated"), user, user);
    }

    private EntityUid? EnsureInternalCannon(Entity<YautjaCannonPackComponent> ent)
    {
        if (ent.Comp.Cannon is { } existing && !TerminatingOrDeleted(existing))
            return existing;

        var cannon = Spawn(ent.Comp.CannonPrototype, Transform(ent).Coordinates);
        var container = EnsureCannonContainer(ent);
        if (!_containers.Insert(cannon, container, force: true))
        {
            QueueDel(cannon);
            return null;
        }

        ent.Comp.Cannon = cannon;
        EnsureComp<YautjaCannonPackLinkedCannonComponent>(cannon).Pack = ent.Owner;
        ent.Comp.CannonsDeployed = false;
        return cannon;
    }

    private ContainerSlot EnsureCannonContainer(Entity<YautjaCannonPackComponent> ent)
    {
        ent.Comp.CannonContainer ??= _containers.EnsureContainer<ContainerSlot>(ent.Owner, ent.Comp.CannonContainerId);
        return ent.Comp.CannonContainer;
    }

    private bool TryDrainPackPower(Entity<YautjaCannonPackComponent> ent, EntityUid user, FixedPoint2 amount)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (ent.Comp.Charge < amount)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "cmu-yautja-cannon-pack-drain-failed",
                    ("charge", (int) ent.Comp.Charge),
                    ("max", (int) ent.Comp.MaxCharge),
                    ("amount", (int) amount)),
                user,
                user,
                PopupType.MediumCaution);
            return false;
        }

        ent.Comp.Charge -= amount;
        return true;
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaCannonPackComponent>();
        while (query.MoveNext(out var uid, out var pack))
        {
            if (pack.User is not { } user || time < pack.NextRegen)
                continue;

            if (!IsWornBackPack(uid, user))
            {
                pack.User = null;
                continue;
            }

            pack.NextRegen = time + pack.RegenEvery;
            RegenPack((uid, pack), GetCmss13RegenAmount(pack, user));
        }
    }

    private bool IsWornBackPack(EntityUid pack, EntityUid user)
    {
        return _inventory.TryGetSlotEntity(user, "back", out var worn) && worn == pack;
    }

    private FixedPoint2 GetCmss13RegenAmount(YautjaCannonPackComponent pack, EntityUid user)
    {
        if (IsGroundLevel(user))
            return pack.Regen / 6f;

        if (IsMainshipLevel(user))
            return pack.Regen / 3f;

        return pack.Regen;
    }

    private bool IsGroundLevel(EntityUid user)
    {
        var xform = Transform(user);
        return xform.GridUid is { } grid && HasComp<RMCPlanetComponent>(grid) ||
               xform.MapUid is { } map && HasComp<RMCPlanetComponent>(map);
    }

    private bool IsMainshipLevel(EntityUid user)
    {
        return _areas.TryGetArea(user, out var area, out var areaPrototype) &&
               YautjaPowerSystem.IsCmss13MainshipRechargeArea(area.Value.Comp.PowerNet, areaPrototype.ID);
    }

    private static void RegenPack(Entity<YautjaCannonPackComponent> pack, FixedPoint2 amount)
    {
        if (pack.Comp.Charge >= pack.Comp.MaxCharge)
            return;

        pack.Comp.Charge = FixedPoint2.Min(pack.Comp.Charge + amount, pack.Comp.MaxCharge);
    }
}
