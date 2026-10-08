using Content.Shared._RMC14.Vehicle;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Tools.Systems;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Vehicle.Jeep;

public sealed class CMUJeepSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CMUVehicleFuelSystem _fuel = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPointLightSystem _lights = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public const string WindshieldSlot = "jeep-windshield";
    public const string HeadlightsSlot = "jeep-headlights";
    public const string JerryCanSlot = "jeep-jerrycan";

    private readonly List<(EntityUid Vehicle, EntityUid Projectile)> _passedThrough = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUJeepComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CMUJeepComponent, DamageModifyEvent>(OnDamageModify, before: [typeof(HardpointSystem)]);
        SubscribeLocalEvent<CMUJeepComponent, VehicleCanRunEvent>(OnCanRun);
        SubscribeLocalEvent<CMUJeepComponent, PreventCollideEvent>(OnPreventCollide, after: [typeof(RequireProjectileTargetSystem)]);
        SubscribeLocalEvent<CMUJeepComponent, EntInsertedIntoContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUJeepComponent, EntRemovedFromContainerMessage>(OnSlotsChanged);
        SubscribeLocalEvent<CMUJeepComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepEngineRepairDoAfterEvent>(OnEngineRepair);
        SubscribeLocalEvent<CMUJeepComponent, CMUJeepPartRemoveDoAfterEvent>(OnPartRemove);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractHandEvent>(OnPartInteractHand);
        SubscribeLocalEvent<CMUVehiclePartComponent, InteractUsingEvent>(OnPartInteractUsing);
    }

    private void OnMapInit(Entity<CMUJeepComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.PartEntities.Count > 0)
            return;

        foreach (var data in ent.Comp.Parts)
        {
            var part = SpawnAttachedTo(ent.Comp.PartPrototype, new EntityCoordinates(ent, default));
            _metaData.SetEntityName(part, Loc.GetString(data.Name));

            var comp = EnsureComp<CMUVehiclePartComponent>(part);
            comp.Vehicle = ent;
            comp.Part = data.Id;
            comp.Slot = data.Slot;
            Dirty(part, comp);

            ent.Comp.PartEntities.Add(part);
        }

        RefreshParts(ent);
    }

    private void OnCanRun(Entity<CMUJeepComponent> ent, ref VehicleCanRunEvent args)
    {
        if (ent.Comp.EngineIntegrity <= 0f)
            args.CanRun = false;
    }

    /// <summary>
    /// A stray shot passes through the open jeep to whoever is behind, leaving some of its damage in
    /// the bodywork. Shots fired from aboard don't count.
    /// </summary>
    private void OnPreventCollide(Entity<CMUJeepComponent> ent, ref PreventCollideEvent args)
    {
        if (_net.IsClient ||
            !args.Cancelled ||
            !TryComp(args.OtherEntity, out ProjectileComponent? projectile) ||
            IsAboard(projectile.Shooter, ent) ||
            IsAboard(projectile.Weapon, ent))
        {
            return;
        }

        if (EnsureComp<CMUPassedThroughComponent>(args.OtherEntity).Vehicles.Add(ent))
            _passedThrough.Add((ent, args.OtherEntity));
    }

    private bool IsAboard(EntityUid? entity, EntityUid vehicle)
    {
        for (var i = 0; i < 5 && entity is { } current && current.IsValid(); i++)
        {
            if (current == vehicle)
                return true;

            entity = Transform(current).ParentUid;
        }

        return false;
    }

    private void OnSlotsChanged<T>(Entity<CMUJeepComponent> ent, ref T args) where T : ContainerModifiedMessage
    {
        if (_net.IsClient)
            return;

        if (args.Container.ID == JerryCanSlot && args is EntRemovedFromContainerMessage && ent.Comp.JerryCanLeaking)
            EnsureComp<CMUFuelLeakComponent>(args.Entity);

        RefreshParts(ent);
    }

    /// <summary>
    /// Reads each hit before RMC routes it into the hardpoints and frame, and wears the jeep's own
    /// parts by their shares of it.
    /// </summary>
    private void OnDamageModify(Entity<CMUJeepComponent> ent, ref DamageModifyEvent args)
    {
        if (_net.IsClient)
            return;

        var total = (float) DamageSpecifier.GetPositive(args.Damage).GetTotal();
        if (total <= 0f)
            return;

        var jeep = ent.Comp;
        var engineWasRunning = jeep.EngineIntegrity > 0f;
        jeep.EngineIntegrity = MathF.Max(0f, jeep.EngineIntegrity - total * jeep.EngineDamageShare);
        if (engineWasRunning && jeep.EngineIntegrity <= 0f && TryComp(ent, out VehicleComponent? vehicle) && vehicle.Operator is { } driver)
            _popup.PopupEntity(Loc.GetString("cmu-jeep-engine-dead"), ent, driver, PopupType.MediumCaution);

        WearPart(ent, WindshieldSlot, total * jeep.WindshieldDamageShare);
        WearPart(ent, HeadlightsSlot, total * jeep.HeadlightDamageShare);

        if (total >= jeep.PunctureMinDamage &&
            _random.Prob(jeep.JerryCanPunctureChance) &&
            _itemSlots.TryGetSlot(ent.Owner, JerryCanSlot, out var slot) &&
            slot.Item is { } can)
        {
            EnsureComp<CMUFuelLeakComponent>(can);
        }

        RefreshParts(ent);
    }

    private void WearPart(EntityUid vehicle, string slotId, float damage)
    {
        if (damage <= 0f ||
            !_itemSlots.TryGetSlot(vehicle, slotId, out var slot) ||
            !TryComp(slot.Item, out CMUJeepPartIntegrityComponent? part))
        {
            return;
        }

        part.Integrity = MathF.Max(0f, part.Integrity - damage);
        Dirty(slot.Item.Value, part);
    }

    /// <summary>
    /// Recomputes the damage flags the client draws from, and lets the headlight beam shine only
    /// through intact headlights.
    /// </summary>
    private void RefreshParts(Entity<CMUJeepComponent> ent)
    {
        var jeep = ent.Comp;
        jeep.WindshieldDamaged = IsBroken(ent, WindshieldSlot);
        jeep.HeadlightsBroken = IsBroken(ent, HeadlightsSlot);
        jeep.JerryCanLeaking = _itemSlots.TryGetSlot(ent.Owner, JerryCanSlot, out var can) &&
                               HasComp<CMUFuelLeakComponent>(can.Item);
        Dirty(ent);
        UpdateBeam(ent);
    }

    private bool IsBroken(EntityUid vehicle, string slotId)
    {
        return _itemSlots.TryGetSlot(vehicle, slotId, out var slot) &&
               TryComp(slot.Item, out CMUJeepPartIntegrityComponent? part) &&
               part.Integrity <= part.MaxIntegrity * part.BrokenFraction;
    }

    public bool HeadlightsWork(EntityUid vehicle)
    {
        return _itemSlots.TryGetSlot(vehicle, HeadlightsSlot, out var slot) &&
               slot.HasItem &&
               !IsBroken(vehicle, HeadlightsSlot);
    }

    private void UpdateBeam(EntityUid vehicle)
    {
        if (!TryComp(vehicle, out VehicleSpotlightComponent? spotlight))
            return;

        _lights.SetEnabled(vehicle, spotlight.Enabled && HeadlightsWork(vehicle));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        foreach (var (vehicle, projectileUid) in _passedThrough)
        {
            if (TryComp(vehicle, out CMUJeepComponent? jeep) &&
                TryComp(projectileUid, out ProjectileComponent? projectile))
            {
                _damageable.TryChangeDamage(vehicle, projectile.Damage * jeep.PassThroughDamage,
                    origin: projectile.Shooter, tool: projectileUid);
            }
        }

        _passedThrough.Clear();

        var jeeps = EntityQueryEnumerator<CMUJeepComponent, VehicleSpotlightComponent>();
        while (jeeps.MoveNext(out var uid, out _, out _))
        {
            UpdateBeam(uid);
        }

        var leaks = EntityQueryEnumerator<CMUFuelLeakComponent>();
        while (leaks.MoveNext(out var uid, out var leak))
        {
            if (_solution.TryGetSolution(uid, leak.Solution, out var solution, out var contents) && contents.Volume > 0)
                _solution.SplitSolution(solution.Value, leak.Rate * frameTime);
        }
    }

    private void OnGetVerbs(Entity<CMUJeepComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasComp<HardpointSlotsComponent>(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-jeep-hardpoints-verb"),
            Act = () => _ui.OpenUi(ent.Owner, HardpointUiKey.Key, user),
        });
    }

    private void OnPartInteractHand(Entity<CMUVehiclePartComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUJeepComponent? jeep))
        {
            return;
        }

        var user = args.User;
        switch (ent.Comp.Part)
        {
            // Toggled by the server alone: a predicted flip is undone and redone while the server
            // catches up, which replays the swing animation.
            case "hood" when _net.IsServer:
                if (jeep.WindshieldDown && HasWindshield(vehicle))
                    _popup.PopupEntity(Loc.GetString("cmu-jeep-hood-blocked"), vehicle, user);
                else
                    SetHood((vehicle, jeep), !jeep.HoodOpen);
                break;
            case "windshield" when _net.IsServer:
                if (!HasWindshield(vehicle))
                    return;

                if (jeep.HoodOpen)
                    _popup.PopupEntity(Loc.GetString("cmu-jeep-windshield-blocked"), vehicle, user);
                else
                    SetWindshieldDown((vehicle, jeep), !jeep.WindshieldDown);
                break;
            case "fuel_door" when _net.IsServer:
                jeep.FuelDoorOpen = !jeep.FuelDoorOpen;
                Dirty(vehicle, jeep);
                break;
            case "engine" when _net.IsServer:
                if (!jeep.HoodOpen)
                    return;

                var percent = (int) MathF.Round(jeep.EngineIntegrity / jeep.EngineMaxIntegrity * 100);
                _popup.PopupEntity(Loc.GetString("cmu-jeep-engine-examine", ("percent", percent)), vehicle, user);
                break;
            case "hood" or "windshield" or "fuel_door" or "engine":
                break;
            case "headlights":
                return;
            default:
                if (ent.Comp.Slot is not { } slotId ||
                    !_itemSlots.TryGetSlot(vehicle, slotId, out var slot) ||
                    !slot.HasItem)
                {
                    return;
                }

                _itemSlots.TryEjectToHands(vehicle, slot, user, true);
                break;
        }

        args.Handled = true;
    }

    private void OnPartInteractUsing(Entity<CMUVehiclePartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            ent.Comp.Vehicle is not { } vehicle ||
            !TryComp(vehicle, out CMUJeepComponent? jeep))
        {
            return;
        }

        var user = args.User;
        var used = args.Used;
        switch (ent.Comp.Part)
        {
            case "fuel_door":
                if (!jeep.FuelDoorOpen)
                {
                    _popup.PopupClient(Loc.GetString("cmu-vehicle-fuel-door-closed"), vehicle, user);
                    args.Handled = true;
                    return;
                }

                if (_fuel.TryStartRefuel(vehicle, user, used))
                {
                    args.Handled = true;
                    return;
                }

                break;
            case "engine" when jeep.HoodOpen && _tool.HasQuality(used, jeep.EngineRepairQuality):
                if (jeep.EngineIntegrity >= jeep.EngineMaxIntegrity)
                {
                    _popup.PopupClient(Loc.GetString("cmu-jeep-engine-fine"), vehicle, user);
                }
                else
                {
                    _tool.UseTool(used, user, vehicle, (float) jeep.EngineRepairDelay.TotalSeconds,
                        jeep.EngineRepairQuality, new CMUJeepEngineRepairDoAfterEvent(), fuel: 5f);
                }

                args.Handled = true;
                return;
            case "windshield" or "headlights" when ent.Comp.Slot is { } removable &&
                                                     _itemSlots.TryGetSlot(vehicle, removable, out var fitted) &&
                                                     fitted.HasItem &&
                                                     _tool.HasQuality(used, jeep.PartRemoveQuality):
                _tool.UseTool(used, user, vehicle, (float) jeep.PartRemoveDelay.TotalSeconds,
                    jeep.PartRemoveQuality, new CMUJeepPartRemoveDoAfterEvent(removable));
                args.Handled = true;
                return;
        }

        if (ent.Comp.Slot is { } slotId &&
            _itemSlots.TryGetSlot(vehicle, slotId, out var slot) &&
            !slot.HasItem &&
            _itemSlots.TryInsert(vehicle, slotId, used, user))
        {
            args.Handled = true;
            return;
        }

        var ev = new InteractUsingEvent(user, used, vehicle, args.ClickLocation);
        RaiseLocalEvent(vehicle, ev);
        args.Handled = ev.Handled;
    }

    private void OnEngineRepair(Entity<CMUJeepComponent> ent, ref CMUJeepEngineRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var jeep = ent.Comp;
        jeep.EngineIntegrity = MathF.Min(jeep.EngineMaxIntegrity, jeep.EngineIntegrity + jeep.EngineRepairAmount);
        Dirty(ent);
        args.Repeat = jeep.HoodOpen && jeep.EngineIntegrity < jeep.EngineMaxIntegrity;
    }

    private void OnPartRemove(Entity<CMUJeepComponent> ent, ref CMUJeepPartRemoveDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_itemSlots.TryGetSlot(ent.Owner, args.Slot, out var slot))
            return;

        args.Handled = true;
        _itemSlots.TryEjectToHands(ent, slot, args.User, true);
    }

    private bool HasWindshield(EntityUid vehicle)
    {
        return _itemSlots.TryGetSlot(vehicle, WindshieldSlot, out var slot) && slot.HasItem;
    }

    public void SetHood(Entity<CMUJeepComponent> ent, bool open)
    {
        ent.Comp.HoodOpen = open;
        Dirty(ent);
    }

    public void SetWindshieldDown(Entity<CMUJeepComponent> ent, bool down)
    {
        ent.Comp.WindshieldDown = down;
        Dirty(ent);
    }
}
