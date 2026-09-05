using Content.Server._RMC14.Humanoid.Markings;
using Content.Shared._RMC14.Repairable;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.SSDIndicator;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.CMU14.DroneOperator;

public sealed partial class CMUDroneOperatorSystem
{
    [Dependency] private SharedAppearanceSystem _combatAppearance = default!;
    [Dependency] private DamageableSystem _combatDamage = default!;
    [Dependency] private SharedStackSystem _combatStacks = default!;

    private void InitializeCombatDrones()
    {
        SubscribeLocalEvent<CMUCombatDroneHullComponent, ComponentInit>(OnCombatHullInit);
        SubscribeLocalEvent<CMUCombatDroneHullComponent, InteractUsingEvent>(OnCombatHullInteract);
        SubscribeLocalEvent<CMUCombatDroneHullComponent, ExaminedEvent>(OnCombatHullExamine);
        SubscribeLocalEvent<CMUCombatDroneHullComponent, CMUCombatDroneInstallTurretDoAfterEvent>(OnCombatInstallTurret);
        SubscribeLocalEvent<CMUCombatDroneHullComponent, CMUCombatDroneAssembleDoAfterEvent>(OnCombatAssemble);
        SubscribeLocalEvent<CMUCombatDroneComponent, MapInitEvent>(OnCombatDroneMapInit,
            after: [typeof(SSDIndicatorSystem), typeof(RMCIntentsEyeColorSystem)]);
        SubscribeLocalEvent<CMUCombatDroneComponent, InteractUsingEvent>(OnCombatRepairInteract);
        SubscribeLocalEvent<CMUCombatDroneComponent, CMUCombatDroneWeldDoAfterEvent>(OnCombatWeld);
        SubscribeLocalEvent<CMUCombatDroneComponent, CMUCombatDroneWireDoAfterEvent>(OnCombatWire);
        SubscribeLocalEvent<CMUCombatDroneComponent, DamageChangedEvent>(OnCombatDamageChanged);
    }

    private void OnCombatHullInit(Entity<CMUCombatDroneHullComponent> ent, ref ComponentInit args)
    {
        _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.TurretContainerId);
        _combatAppearance.SetData(ent, CMUCombatDroneVisuals.Turret, false);
    }

    private EntityUid? GetCombatHullTurret(Entity<CMUCombatDroneHullComponent> ent)
    {
        return _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.TurretContainerId).ContainedEntity;
    }

    private void OnCombatHullExamine(Entity<CMUCombatDroneHullComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(GetCombatHullTurret(ent) == null
            ? "cmu-combat-drone-assembly-needs-turret"
            : "cmu-combat-drone-assembly-needs-ammo"));
    }

    private bool CanAssembleCombatHull(Entity<CMUCombatDroneHullComponent> hull, EntityUid user, EntityUid used)
    {
        if (TerminatingOrDeleted(hull) || TerminatingOrDeleted(used) || !_hands.IsHolding(user, used))
            return false;

        string? message = null;
        if (!TryComp<CMUDroneOperatorComponent>(user, out var op))
            message = "cmu-drone-operator-required";
        else if (HasExistingDrone((user, op)))
            message = "cmu-drone-assembly-existing";
        else if (_containers.IsEntityInContainer(hull))
            message = "cmu-drone-frame-must-place";

        if (message == null)
            return true;

        _popup.PopupEntity(Loc.GetString(message), hull, user, PopupType.SmallCaution);
        return false;
    }

    private void OnCombatHullInteract(Entity<CMUCombatDroneHullComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var turret = HasComp<CMUCombatDroneTurretAssemblyComponent>(args.Used);
        var ammo = HasComp<CMUCombatDroneAmmoBoxComponent>(args.Used);
        if (!turret && !ammo)
            return;

        args.Handled = true;
        if (!CanAssembleCombatHull(ent, args.User, args.Used))
            return;

        var installed = GetCombatHullTurret(ent) != null;
        if (turret && installed || ammo && !installed)
        {
            _popup.PopupEntity(Loc.GetString(installed
                ? "cmu-combat-drone-assembly-needs-ammo"
                : "cmu-combat-drone-assembly-needs-turret"), ent, args.User);
            return;
        }

        if (ammo && !HasCombatAmmo(args.Used))
        {
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-assembly-empty-ammo"), ent, args.User);
            return;
        }

        SimpleDoAfterEvent ev = turret
            ? new CMUCombatDroneInstallTurretDoAfterEvent()
            : new CMUCombatDroneAssembleDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.AssemblyDelay, ev, ent, ent, used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString(turret ? "cmu-combat-drone-install-turret-start" : "cmu-combat-drone-activate-start"), ent, args.User);
    }

    private bool HasCombatAmmo(EntityUid magazine)
    {
        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(magazine, ref ammo);
        return ammo.Count > 0;
    }

    private void OnCombatInstallTurret(Entity<CMUCombatDroneHullComponent> ent, ref CMUCombatDroneInstallTurretDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        args.Handled = true;
        if (args.Used is not { } used || !HasComp<CMUCombatDroneTurretAssemblyComponent>(used) ||
            !CanAssembleCombatHull(ent, args.User, used) || GetCombatHullTurret(ent) != null)
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.TurretContainerId);
        if (_containers.Insert(used, slot))
        {
            _combatAppearance.SetData(ent, CMUCombatDroneVisuals.Turret, true);
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-assembly-needs-ammo"), ent, args.User);
        }
    }

    private void OnCombatAssemble(Entity<CMUCombatDroneHullComponent> ent, ref CMUCombatDroneAssembleDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        args.Handled = true;
        if (args.Used is not { } ammo || !HasComp<CMUCombatDroneAmmoBoxComponent>(ammo) ||
            !CanAssembleCombatHull(ent, args.User, ammo) || GetCombatHullTurret(ent) == null || !HasCombatAmmo(ammo) ||
            !TryComp<CMUDroneOperatorComponent>(args.User, out var op))
            return;

        var xform = Transform(ent);
        var drone = Spawn(ent.Comp.DronePrototype, xform.Coordinates);
        _transform.SetLocalRotation(drone, xform.LocalRotation);
        var slot = _containers.EnsureContainer<ContainerSlot>(drone, SharedGunSystem.MagazineSlot);
        // Transfer the actual box: loading must never conjure or duplicate ammunition.
        if (!_containers.Insert(ammo, slot))
        {
            QueueDel(drone);
            return;
        }

        RegisterAssembledDrone(drone, args.User, op);
        QueueDel(ent);
    }

    private void OnCombatDroneMapInit(Entity<CMUCombatDroneComponent> ent, ref MapInitEvent args)
    {
        var visual = SpawnAttachedTo(ent.Comp.TurretVisualPrototype, new(ent, System.Numerics.Vector2.Zero));
        ent.Comp.TurretVisual = visual;
        Dirty(ent);
    }

    private DamageSpecifier GetCombatRepair(Entity<CMUCombatDroneComponent> ent, bool wiring)
    {
        var damage = _combatDamage.GetAllDamage((ent, null));
        return CMUCombatDroneSystem.GetRepair(damage,
            wiring ? ent.Comp.WiringDamageTypes : ent.Comp.FrameDamageTypes,
            ent.Comp.RepairAmount);
    }

    private void OnCombatRepairInteract(Entity<CMUCombatDroneComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var wire = HasComp<RMCCableCoilComponent>(args.Used);
        if (!wire && !_tool.HasQuality(args.Used, ent.Comp.WeldQuality))
            return;

        args.Handled = true;
        if (args.User == ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("cmu-drone-self-repair-blocked"), ent, args.User);
            return;
        }

        if (GetCombatRepair(ent, wire).Empty)
        {
            _popup.PopupEntity(Loc.GetString(wire ? "cmu-combat-drone-wires-intact" : "cmu-combat-drone-frame-intact"), ent, args.User);
            return;
        }

        if (!wire)
        {
            _tool.UseTool(args.Used, args.User, ent, ent.Comp.RepairDelay,
                new[] { ent.Comp.WeldQuality }, new CMUCombatDroneWeldDoAfterEvent(), out _, ent.Comp.WeldFuel,
                duplicateCondition: DuplicateConditions.SameEvent | DuplicateConditions.SameTarget);
            return;
        }

        if (!TryComp<StackComponent>(args.Used, out var stack) || _combatStacks.GetCount((args.Used, stack)) < ent.Comp.RepairWireCost)
        {
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-needs-wires", ("amount", ent.Comp.RepairWireCost)), ent, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.RepairDelay, new CMUCombatDroneWireDoAfterEvent(), ent, ent, used: args.Used)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };
        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-wire-start"), ent, args.User);
    }

    private void OnCombatWeld(Entity<CMUCombatDroneComponent> ent, ref CMUCombatDroneWeldDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.User == ent.Owner)
            return;
        args.Handled = true;
        var repair = GetCombatRepair(ent, false);
        if (repair.Empty)
            return;
        _combatDamage.TryChangeDamage(ent, repair, ignoreResistances: true, origin: args.User);
        _popup.PopupEntity(Loc.GetString("cmu-combat-drone-weld-finish"), ent, args.User);
    }

    private void OnCombatWire(Entity<CMUCombatDroneComponent> ent, ref CMUCombatDroneWireDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.User == ent.Owner)
            return;
        args.Handled = true;
        if (args.Used is not { } used || !HasComp<RMCCableCoilComponent>(used) || !_hands.IsHolding(args.User, used))
            return;
        var repair = GetCombatRepair(ent, true);
        if (repair.Empty || !_combatStacks.TryUse((used, null), ent.Comp.RepairWireCost))
            return;
        _combatDamage.TryChangeDamage(ent, repair, ignoreResistances: true, origin: args.User);
        _popup.PopupEntity(Loc.GetString("cmu-combat-drone-wire-finish"), ent, args.User);
    }

    private void OnCombatDamageChanged(Entity<CMUCombatDroneComponent> ent, ref DamageChangedEvent args)
    {
        var total = _combatDamage.GetTotalDamage((ent, args.Damageable));
        if (!ent.Comp.Wrecked && total >= ent.Comp.WreckDamageThreshold)
            SetCombatDroneWrecked(ent, true);
        else if (ent.Comp.Wrecked && total < ent.Comp.WreckRecoveryThreshold)
            SetCombatDroneWrecked(ent, false);

        if (total < ent.Comp.SparkDamageThreshold)
        {
            RemComp<CMUCombatDroneSparkingComponent>(ent);
            return;
        }

        if (!HasComp<CMUCombatDroneSparkingComponent>(ent))
        {
            var sparking = EnsureComp<CMUCombatDroneSparkingComponent>(ent);
            sparking.NextSpark = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(ent.Comp.SparkIntervalMin, ent.Comp.SparkIntervalMax));
        }
    }

    private void SetCombatDroneWrecked(Entity<CMUCombatDroneComponent> ent, bool wrecked)
    {
        ent.Comp.Wrecked = wrecked;
        Dirty(ent);
        _combatAppearance.SetData(ent, CMUCombatDroneVisuals.Wrecked, wrecked);
        if (ent.Comp.TurretVisual is { } turret && !TerminatingOrDeleted(turret))
            _combatAppearance.SetData(turret, CMUCombatDroneVisuals.Wrecked, wrecked);

        if (wrecked)
        {
            ent.Comp.PreWreckName = Name(ent);
            _metaData.SetEntityName(ent, Loc.GetString("cmu-combat-drone-wreck-name", ("name", ent.Comp.PreWreckName)));
            StopEntityMotion(ent);
            EndControlForDrone(ent, Loc.GetString("cmu-drone-control-ended-drone-disabled"));
        }
        else
        {
            if (ent.Comp.PreWreckName is { } name)
                _metaData.SetEntityName(ent, name);
            ent.Comp.PreWreckName = null;
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-wreck-restored"), ent);
        }
    }

    private void UpdateCombatDrones()
    {
        var query = EntityQueryEnumerator<CMUCombatDroneComponent, CMUCombatDroneSparkingComponent>();
        while (query.MoveNext(out var uid, out var drone, out var sparking))
        {
            if (Paused(uid) || sparking.NextSpark > _timing.CurTime)
                continue;
            sparking.NextSpark = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(drone.SparkIntervalMin, drone.SparkIntervalMax));
            Spawn(drone.SparkEffect, Transform(uid).Coordinates);
        }
    }
}
