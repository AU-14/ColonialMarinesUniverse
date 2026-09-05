using Content.Server._RMC14.Humanoid.Markings;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
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
    [Dependency] private SharedSolutionContainerSystem _combatSolutions = default!;
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
        SubscribeLocalEvent<CMUFlamerDroneComponent, GunShotEvent>(OnFlamerDroneShot);
        SubscribeLocalEvent<CMUFlamerDroneComponent, EntInsertedIntoContainerMessage>(OnFlamerTankInserted);
        SubscribeLocalEvent<CMUFlamerDroneComponent, EntRemovedFromContainerMessage>(OnFlamerTankRemoved);
        SubscribeLocalEvent<CMUFlamerDroneComponent, MobStateChangedEvent>(OnFlamerMobStateChanged);
        SubscribeLocalEvent<RMCFlamerTankComponent, SolutionChangedEvent>(OnFlamerFuelChanged);
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
        args.PushMarkup(Loc.GetString(CombatAssemblyMessage(ent, GetCombatHullTurret(ent) == null
            ? "assembly-needs-turret"
            : "assembly-needs-ammo")));
    }

    private static string CombatAssemblyMessage(Entity<CMUCombatDroneHullComponent> hull, string message)
    {
        return (hull.Comp.Weapon == CMUCombatDroneWeapon.Flamer ? "cmu-flamer-drone-" : "cmu-combat-drone-") + message;
    }

    private bool IsCombatHullAmmo(Entity<CMUCombatDroneHullComponent> hull, EntityUid used)
    {
        return hull.Comp.Weapon == CMUCombatDroneWeapon.Flamer
            ? HasComp<RMCFlamerTankComponent>(used)
            : HasComp<CMUCombatDroneAmmoBoxComponent>(used);
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

        var turret = TryComp<CMUCombatDroneTurretAssemblyComponent>(args.Used, out var assembly);
        var ammo = IsCombatHullAmmo(ent, args.Used);
        if (!turret && !ammo && !HasComp<CMUCombatDroneAmmoBoxComponent>(args.Used) && !HasComp<RMCFlamerTankComponent>(args.Used))
            return;

        args.Handled = true;
        if (!CanAssembleCombatHull(ent, args.User, args.Used))
            return;

        if (turret && assembly!.Weapon != ent.Comp.Weapon || !turret && !ammo)
        {
            _popup.PopupEntity(Loc.GetString("cmu-combat-drone-incompatible-part"), ent, args.User);
            return;
        }

        var installed = GetCombatHullTurret(ent) != null;
        if (turret && installed || ammo && !installed)
        {
            _popup.PopupEntity(Loc.GetString(CombatAssemblyMessage(ent, installed
                ? "assembly-needs-ammo"
                : "assembly-needs-turret")), ent, args.User);
            return;
        }

        if (ammo && !HasCombatAmmo(args.Used))
        {
            _popup.PopupEntity(Loc.GetString(CombatAssemblyMessage(ent, "assembly-empty-ammo")), ent, args.User);
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
            _popup.PopupEntity(Loc.GetString(CombatAssemblyMessage(ent, turret ? "install-turret-start" : "activate-start")), ent, args.User);
    }

    private bool HasCombatAmmo(EntityUid magazine)
    {
        if (TryComp<RMCFlamerTankComponent>(magazine, out var tank))
            return _combatSolutions.TryGetSolution(magazine, tank.SolutionId, out _, out var solution) && solution.Volume > 0;

        var ammo = new GetAmmoCountEvent();
        RaiseLocalEvent(magazine, ref ammo);
        return ammo.Count > 0;
    }

    private void OnCombatInstallTurret(Entity<CMUCombatDroneHullComponent> ent, ref CMUCombatDroneInstallTurretDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        args.Handled = true;
        if (args.Used is not { } used || !TryComp<CMUCombatDroneTurretAssemblyComponent>(used, out var assembly) ||
            assembly.Weapon != ent.Comp.Weapon ||
            !CanAssembleCombatHull(ent, args.User, used) || GetCombatHullTurret(ent) != null)
            return;

        var slot = _containers.EnsureContainer<ContainerSlot>(ent, ent.Comp.TurretContainerId);
        if (_containers.Insert(used, slot))
        {
            _combatAppearance.SetData(ent, CMUCombatDroneVisuals.Turret, true);
            _popup.PopupEntity(Loc.GetString(CombatAssemblyMessage(ent, "assembly-needs-ammo")), ent, args.User);
        }
    }

    private void OnCombatAssemble(Entity<CMUCombatDroneHullComponent> ent, ref CMUCombatDroneAssembleDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;
        args.Handled = true;
        if (args.Used is not { } ammo || !IsCombatHullAmmo(ent, ammo) ||
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
        if (ent.Comp.TurretVisualPrototype is { } prototype)
        {
            ent.Comp.TurretVisual = SpawnAttachedTo(prototype, new(ent, System.Numerics.Vector2.Zero));
            Dirty(ent);
        }
        UpdateCombatDroneAppearance(ent, _combatDamage.GetTotalDamage((ent, null)));
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

        UpdateCombatDroneAppearance(ent, total);

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

    private void UpdateCombatDroneAppearance(Entity<CMUCombatDroneComponent> ent, FixedPoint2 total)
    {
        var state = ent.Comp.Wrecked ? CMUCombatDroneDamageState.Destroyed
            : total >= ent.Comp.DamagedVisualThreshold ? CMUCombatDroneDamageState.Damaged
            : CMUCombatDroneDamageState.Healthy;
        _combatAppearance.SetData(ent, CMUCombatDroneVisuals.DamageState, state);
        if (ent.Comp.TurretVisual is { } turret && !TerminatingOrDeleted(turret))
            _combatAppearance.SetData(turret, CMUCombatDroneVisuals.DamageState, state);
    }

    private void OnFlamerDroneShot(Entity<CMUFlamerDroneComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.FlameUntil = _timing.CurTime + ent.Comp.FlameDuration;
        Dirty(ent);
    }

    private void OnFlamerTankInserted(Entity<CMUFlamerDroneComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateFlamerPilot(ent);
    }

    private void OnFlamerTankRemoved(Entity<CMUFlamerDroneComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateFlamerPilot(ent);
    }

    private void OnFlamerMobStateChanged(Entity<CMUFlamerDroneComponent> ent, ref MobStateChangedEvent args)
    {
        UpdateFlamerPilot(ent);
    }

    private void OnFlamerFuelChanged(Entity<RMCFlamerTankComponent> ent, ref SolutionChangedEvent args)
    {
        if (_containers.TryGetContainingContainer((ent, null), out var container) &&
            TryComp<CMUFlamerDroneComponent>(container.Owner, out var flamer))
            UpdateFlamerPilot((container.Owner, flamer));
    }

    private void UpdateFlamerPilot(Entity<CMUFlamerDroneComponent> ent)
    {
        var lit = _mobState.IsAlive(ent) &&
            (!TryComp<CMUCombatDroneComponent>(ent, out var drone) || !drone.Wrecked) &&
            _containers.TryGetContainer(ent, SharedGunSystem.MagazineSlot, out var slot) &&
            slot.ContainedEntities.Count > 0 && HasCombatAmmo(slot.ContainedEntities[0]);
        if (lit == ent.Comp.PilotLit)
            return;

        ent.Comp.PilotLit = lit;
        Dirty(ent);
    }

    private void SetCombatDroneWrecked(Entity<CMUCombatDroneComponent> ent, bool wrecked)
    {
        ent.Comp.Wrecked = wrecked;
        Dirty(ent);
        if (TryComp<CMUFlamerDroneComponent>(ent, out var pilot))
            UpdateFlamerPilot((ent, pilot));
        _combatAppearance.SetData(ent, CMUCombatDroneVisuals.Wrecked, wrecked);
        if (ent.Comp.TurretVisual is { } turret && !TerminatingOrDeleted(turret))
            _combatAppearance.SetData(turret, CMUCombatDroneVisuals.Wrecked, wrecked);

        if (wrecked)
        {
            if (TryComp<CMUFlamerDroneComponent>(ent, out var flamer))
            {
                flamer.FlameUntil = TimeSpan.Zero;
                Dirty(ent, flamer);
            }
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
