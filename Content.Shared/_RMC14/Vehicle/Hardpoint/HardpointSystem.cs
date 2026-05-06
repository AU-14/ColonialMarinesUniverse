using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Whitelist;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Damage;
using Content.Shared._RMC14.Repairable;
using Content.Shared.Tools.Components;
using Content.Shared._RMC14.Tools;
using Robust.Shared.GameObjects;
using Content.Shared.Hands.Components;
using Robust.Shared.Containers;
using Robust.Shared.Localization;
using Robust.Shared.Network;
using Robust.Shared.Audio.Systems;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.UserInterface;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion.Components;
using Robust.Shared.Utility;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Random;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly Content.Shared.Vehicle.VehicleSystem _vehicles = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly VehicleWheelSystem _wheels = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly RMCRepairableSystem _repairable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly SharedExplosionSystem _explosion = default!;
    [Dependency] private readonly VehicleTopologySystem _topology = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly VehicleLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HardpointSlotsComponent, ComponentInit>(OnSlotsInit);
        SubscribeLocalEvent<HardpointSlotsComponent, MapInitEvent>(OnSlotsMapInit);
        SubscribeLocalEvent<HardpointSlotsComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<HardpointSlotsComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<HardpointSlotsComponent, VehicleCanRunEvent>(OnVehicleCanRun);
        SubscribeLocalEvent<HardpointSlotsComponent, DamageModifyEvent>(OnVehicleDamageModify);
        SubscribeLocalEvent<HardpointIntegrityComponent, ComponentInit>(OnHardpointIntegrityInit);
        SubscribeLocalEvent<HardpointIntegrityComponent, InteractUsingEvent>(
            OnHardpointRepair,
            before: new[] { typeof(ItemSlotsSystem) });
        SubscribeLocalEvent<HardpointIntegrityComponent, ExaminedEvent>(OnHardpointExamined);
        SubscribeLocalEvent<HardpointIntegrityComponent, HardpointRepairDoAfterEvent>(OnHardpointRepairDoAfter);
        SubscribeLocalEvent<VehicleHardpointFailureComponent, VehicleHardpointFailureRepairDoAfterEvent>(OnFailureRepairDoAfter);
    }

    private void OnSlotsInit(Entity<HardpointSlotsComponent> ent, ref ComponentInit args)
    {
        EnsureSlots(ent.Owner, ent.Comp);
    }

    private void OnSlotsMapInit(Entity<HardpointSlotsComponent> ent, ref MapInitEvent args)
    {
        EnsureSlots(ent.Owner, ent.Comp);
        RefreshContainingVehicleFrameIntegrityFromHardpoints(ent.Owner);
    }

    private void OnInserted(Entity<HardpointSlotsComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryGetSlot(ent.Comp, args.Container.ID, out var slot))
            return;

        var state = EnsureState(ent.Owner);
        state.PendingRemovals.Clear();

        if (!IsValidHardpoint(args.Entity, ent.Comp, slot))
        {
            if (TryComp<ItemSlotsComponent>(ent.Owner, out var itemSlots))
                _itemSlots.TryEject(ent.Owner, args.Container.ID, null, out _, itemSlots, excludeUserAudio: true);

            return;
        }

        state.LastUiError = null;

        if (TryComp(args.Entity, out GunComponent? gun))
            _guns.RefreshModifiers((args.Entity, gun));

        ApplyArmorHardpointModifiers(ent.Owner, args.Entity, adding: true);
        RefreshSupportModifiers(ent.Owner);
        RefreshContainingVehicleFrameIntegrityFromHardpoints(ent.Owner);
        RefreshVehicleArmorModifiers(ent.Owner);
        RefreshVehicleMechanicalFailureModifiers(ent.Owner);

        RefreshCanRun(ent.Owner);
        UpdateHardpointUi(ent.Owner, ent.Comp, state: state);
        UpdateContainingVehicleUi(ent.Owner);
        RaiseHardpointSlotsChanged(ent.Owner);
        RaiseVehicleSlotsChanged(ent.Owner);
    }

    private void OnRemoved(Entity<HardpointSlotsComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (!TryGetSlot(ent.Comp, args.Container.ID, out _))
            return;

        var state = EnsureState(ent.Owner);
        ApplyArmorHardpointModifiers(ent.Owner, args.Entity, adding: false);
        RefreshSupportModifiers(ent.Owner);
        RefreshContainingVehicleFrameIntegrityFromHardpoints(ent.Owner);
        RefreshVehicleArmorModifiers(ent.Owner);
        RefreshVehicleMechanicalFailureModifiers(ent.Owner);

        state.LastUiError = null;
        RefreshCanRun(ent.Owner);
        state.PendingRemovals.Remove(args.Container.ID);
        UpdateHardpointUi(ent.Owner, ent.Comp, state: state);
        UpdateContainingVehicleUi(ent.Owner);
        RaiseHardpointSlotsChanged(ent.Owner);
        RaiseVehicleSlotsChanged(ent.Owner);
    }

    private void RefreshSupportModifiers(EntityUid owner)
    {
        var vehicle = owner;
        if (!HasComp<VehicleComponent>(vehicle) && !TryGetContainingVehicleFrame(owner, out vehicle))
            return;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            return;
        }

        if (_net.IsClient)
        {
            RefreshVehicleGunModifiers(vehicle, hardpoints, itemSlots);
            return;
        }

        var accuracyMult = FixedPoint2.New(1);
        var fireRateMult = 1f;
        var speedMult = 1f;
        var accelMult = 1f;
        var viewScale = 0f;
        var cursorMaxOffset = 0f;
        var cursorOffsetSpeed = 0.5f;
        var cursorPvsIncrease = 0f;
        var hasWeaponMods = false;
        var hasSpeedMods = false;
        var hasAccelMods = false;
        var hasViewMods = false;

        void Accumulate(EntityUid item)
        {
            var performance = GetHardpointPerformanceMultiplier(item);
            if (performance <= 0f)
                return;

            if (TryComp(item, out VehicleWeaponSupportAttachmentComponent? weaponMod))
            {
                accuracyMult *= ScaleMultiplierTowardNeutral(weaponMod.AccuracyMultiplier, performance);
                fireRateMult *= ScaleMultiplierTowardNeutral(weaponMod.FireRateMultiplier, performance);
                hasWeaponMods = true;
            }

            if (TryComp(item, out VehicleSpeedModifierAttachmentComponent? speedMod))
            {
                speedMult *= ScaleMultiplierTowardNeutral(speedMod.SpeedMultiplier, performance);
                hasSpeedMods = true;
            }

            if (TryComp(item, out VehicleAccelerationModifierAttachmentComponent? accelMod))
            {
                accelMult *= ScaleMultiplierTowardNeutral(accelMod.AccelerationMultiplier, performance);
                hasAccelMods = true;
            }

            if (TryComp(item, out VehicleGunnerViewAttachmentComponent? viewMod))
            {
                viewScale = Math.Max(viewScale, viewMod.PvsScale * performance);
                cursorMaxOffset = Math.Max(cursorMaxOffset, viewMod.CursorMaxOffset * performance);
                cursorOffsetSpeed = MathF.Max(cursorOffsetSpeed, ScaleMultiplierTowardNeutral(viewMod.CursorOffsetSpeed, performance));
                cursorPvsIncrease = Math.Max(cursorPvsIncrease, viewMod.CursorPvsIncrease * performance);
                hasViewMods = true;
            }
        }

        foreach (var slot in hardpoints.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                continue;

            var item = itemSlot.Item!.Value;
            Accumulate(item);

            if (!TryComp(item, out HardpointSlotsComponent? turretSlots) ||
                !TryComp(item, out ItemSlotsComponent? turretItemSlots))
            {
                continue;
            }

            foreach (var turretSlot in turretSlots.Slots)
            {
                if (string.IsNullOrWhiteSpace(turretSlot.Id))
                    continue;

                if (!_itemSlots.TryGetSlot(item, turretSlot.Id, out var turretItemSlot, turretItemSlots) ||
                    !turretItemSlot.HasItem)
                {
                    continue;
                }

                Accumulate(turretItemSlot.Item!.Value);
            }
        }

        if (hasWeaponMods)
        {
            var mods = EnsureComp<VehicleWeaponSupportModifierComponent>(vehicle);
            mods.AccuracyMultiplier = accuracyMult;
            mods.FireRateMultiplier = fireRateMult;
            Dirty(vehicle, mods);
        }
        else
        {
            RemCompDeferred<VehicleWeaponSupportModifierComponent>(vehicle);
        }

        if (hasSpeedMods)
        {
            var speed = EnsureComp<VehicleSpeedModifierComponent>(vehicle);
            speed.SpeedMultiplier = speedMult;
            Dirty(vehicle, speed);
        }
        else
        {
            RemCompDeferred<VehicleSpeedModifierComponent>(vehicle);
        }

        if (hasAccelMods)
        {
            var accel = EnsureComp<VehicleAccelerationModifierComponent>(vehicle);
            accel.AccelerationMultiplier = accelMult;
            Dirty(vehicle, accel);
        }
        else
        {
            RemCompDeferred<VehicleAccelerationModifierComponent>(vehicle);
        }

        if (hasViewMods && viewScale > 0f)
        {
            var view = EnsureComp<VehicleGunnerViewComponent>(vehicle);
            view.PvsScale = viewScale;
            view.CursorMaxOffset = cursorMaxOffset;
            view.CursorOffsetSpeed = cursorOffsetSpeed;
            view.CursorPvsIncrease = cursorPvsIncrease;
            Dirty(vehicle, view);
        }
        else
        {
            RemCompDeferred<VehicleGunnerViewComponent>(vehicle);
        }

        RefreshVehicleGunModifiers(vehicle, hardpoints, itemSlots);
    }

    private void RefreshVehicleGunModifiers(EntityUid vehicle, HardpointSlotsComponent hardpoints, ItemSlotsComponent itemSlots)
    {
        foreach (var slot in hardpoints.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                continue;

            RefreshGunModifiers(itemSlot.Item!.Value);

            if (!TryComp(itemSlot.Item.Value, out HardpointSlotsComponent? turretSlots) ||
                !TryComp(itemSlot.Item.Value, out ItemSlotsComponent? turretItemSlots))
            {
                continue;
            }

            foreach (var turretSlot in turretSlots.Slots)
            {
                if (string.IsNullOrWhiteSpace(turretSlot.Id))
                    continue;

                if (_itemSlots.TryGetSlot(itemSlot.Item.Value, turretSlot.Id, out var turretItemSlot, turretItemSlots) &&
                    turretItemSlot.HasItem)
                {
                    RefreshGunModifiers(turretItemSlot.Item!.Value);
                }
            }
        }
    }

    private void RefreshGunModifiers(EntityUid item)
    {
        if (TryComp(item, out GunComponent? gun))
            _guns.RefreshModifiers((item, gun));
    }

    private static float ScaleMultiplierTowardNeutral(float multiplier, float performance)
    {
        var clamped = Math.Clamp(performance, 0f, 1f);
        return 1f + (multiplier - 1f) * clamped;
    }

    private static FixedPoint2 ScaleMultiplierTowardNeutral(FixedPoint2 multiplier, float performance)
    {
        return FixedPoint2.New(ScaleMultiplierTowardNeutral(multiplier.Float(), performance));
    }

    public float GetHardpointIntegrityFraction(EntityUid hardpoint, HardpointIntegrityComponent? integrity = null)
    {
        if (!Resolve(hardpoint, ref integrity, logMissing: false))
            return 1f;

        if (integrity.MaxIntegrity <= 0f)
            return 1f;

        return Math.Clamp(integrity.Integrity / integrity.MaxIntegrity, 0f, 1f);
    }

    public bool IsHardpointFunctional(EntityUid hardpoint, HardpointIntegrityComponent? integrity = null)
    {
        var fraction = GetHardpointIntegrityFraction(hardpoint, integrity);
        var disabledFraction = GetDisabledIntegrityFraction(hardpoint);
        return fraction > disabledFraction;
    }

    public float GetHardpointPerformanceMultiplier(EntityUid hardpoint, HardpointIntegrityComponent? integrity = null)
    {
        var fraction = GetHardpointIntegrityFraction(hardpoint, integrity);
        var disabledFraction = GetDisabledIntegrityFraction(hardpoint);
        if (fraction <= disabledFraction)
            return 0f;

        var minimum = GetMinimumPerformanceMultiplier(hardpoint);
        var range = 1f - disabledFraction;
        if (range <= 0f)
            return 1f;

        var scaled = (fraction - disabledFraction) / range;
        var multiplier = Math.Clamp(minimum + (1f - minimum) * scaled, 0f, 1f);

        if (HasHardpointFailure(hardpoint, VehicleHardpointFailure.DamagedMount))
            multiplier *= 0.75f;

        return multiplier;
    }

    public bool HasHardpointFailure(
        EntityUid hardpoint,
        VehicleHardpointFailure failure,
        VehicleHardpointFailureComponent? failures = null)
    {
        if (!Resolve(hardpoint, ref failures, logMissing: false))
            return false;

        return failures.ActiveFailures.Contains(failure);
    }

    public bool ShouldVehicleGunMisfire(EntityUid gun)
    {
        return HasHardpointFailure(gun, VehicleHardpointFailure.FeedJam) &&
               _random.Prob(0.25f);
    }

    public float GetTurretRotationMultiplier(EntityUid turret)
    {
        var multiplier = GetHardpointPerformanceMultiplier(turret);

        if (HasHardpointFailure(turret, VehicleHardpointFailure.TurretTraverseDamage))
            multiplier *= 0.35f;

        if (HasHardpointFailure(turret, VehicleHardpointFailure.DamagedMount))
            multiplier *= 0.75f;

        return Math.Clamp(multiplier, 0f, 1f);
    }

    public bool DamageVehicleHull(EntityUid vehicle, float amount)
    {
        if (_net.IsClient || amount <= 0f)
            return false;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            return DamageHardpoint(vehicle, vehicle, amount);
        }

        var targets = new List<EntityUid>();
        CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: false);

        if (targets.Count == 0)
            CollectHullDamageTargets(vehicle, hardpoints, itemSlots, targets, includeWheels: true);

        if (targets.Count == 0)
            return DamageHardpoint(vehicle, vehicle, amount);

        var changed = false;
        foreach (var target in targets)
        {
            if (DamageHardpoint(vehicle, target, amount))
                changed = true;
        }

        if (changed)
        {
            RefreshVehicleFrameIntegrityFromHardpoints(vehicle, hardpoints, itemSlots);
            TryTriggerVehicleStructuralFailure(vehicle, amount);
        }

        return changed;
    }

    private float GetDisabledIntegrityFraction(EntityUid hardpoint)
    {
        if (!TryComp(hardpoint, out HardpointItemComponent? item))
            return 0.15f;

        return Math.Clamp(item.DisabledIntegrityFraction, 0f, 1f);
    }

    private float GetMinimumPerformanceMultiplier(EntityUid hardpoint)
    {
        if (!TryComp(hardpoint, out HardpointItemComponent? item))
            return 0.35f;

        return Math.Clamp(item.MinimumPerformanceMultiplier, 0f, 1f);
    }

    private void CollectHullDamageTargets(
        EntityUid vehicle,
        HardpointSlotsComponent hardpoints,
        ItemSlotsComponent itemSlots,
        List<EntityUid> targets,
        bool includeWheels)
    {
        var visited = new HashSet<EntityUid>();
        foreach (var mountedSlot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mountedSlot.Item is not { } item)
                continue;

            if (!visited.Add(item))
                continue;

            if (!includeWheels && HasComp<VehicleWheelItemComponent>(item))
                continue;

            if (!TryComp(item, out HardpointIntegrityComponent? integrity) || integrity.Integrity <= 0f)
                continue;

            targets.Add(item);
        }
    }

    private bool RefreshContainingVehicleFrameIntegrityFromHardpoints(EntityUid owner)
    {
        return _topology.TryGetVehicle(owner, out var vehicle) &&
               RefreshVehicleFrameIntegrityFromHardpoints(vehicle);
    }

    private bool RefreshVehicleFrameIntegrityFromHardpoints(
        EntityUid vehicle,
        HardpointSlotsComponent? hardpoints = null,
        ItemSlotsComponent? itemSlots = null,
        HardpointIntegrityComponent? frameIntegrity = null)
    {
        if (!Resolve(vehicle, ref hardpoints, logMissing: false) ||
            !Resolve(vehicle, ref itemSlots, logMissing: false) ||
            !Resolve(vehicle, ref frameIntegrity, logMissing: false))
        {
            return false;
        }

        var totalIntegrity = 0f;
        var totalMaxIntegrity = 0f;
        var visited = new HashSet<EntityUid>();

        foreach (var mountedSlot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mountedSlot.Item is not { } item)
                continue;

            if (!visited.Add(item))
                continue;

            if (!TryComp(item, out HardpointIntegrityComponent? integrity) || integrity.MaxIntegrity <= 0f)
                continue;

            totalIntegrity += Math.Clamp(integrity.Integrity, 0f, integrity.MaxIntegrity);
            totalMaxIntegrity += integrity.MaxIntegrity;
        }

        if (totalMaxIntegrity <= 0f)
            return false;

        var previous = frameIntegrity.Integrity;
        var previousMax = frameIntegrity.MaxIntegrity;
        frameIntegrity.MaxIntegrity = totalMaxIntegrity;
        frameIntegrity.Integrity = Math.Clamp(totalIntegrity, 0f, totalMaxIntegrity);

        if (Math.Abs(previous - frameIntegrity.Integrity) < 0.01f &&
            Math.Abs(previousMax - frameIntegrity.MaxIntegrity) < 0.01f)
        {
            return false;
        }

        Dirty(vehicle, frameIntegrity);
        UpdateFrameDamageAppearance(vehicle, frameIntegrity);

        if ((previous > 0f) != (frameIntegrity.Integrity > 0f))
            RefreshCanRun(vehicle);

        _lock.RefreshForcedOpen(vehicle);
        return true;
    }

    private void TryTriggerVehicleStructuralFailure(EntityUid vehicle, float amount)
    {
        if (!TryComp(vehicle, out HardpointIntegrityComponent? frame) || frame.MaxIntegrity <= 0f)
            return;

        var fraction = Math.Clamp(frame.Integrity / frame.MaxIntegrity, 0f, 1f);
        var damageFraction = amount / frame.MaxIntegrity;

        if (fraction > 0.75f && damageFraction < 0.03f)
            return;

        var chance = Math.Clamp(0.04f + damageFraction * 1.5f + (1f - fraction) * 0.25f, 0f, 0.45f);
        if (!_random.Prob(chance))
            return;

        var candidates = new List<VehicleHardpointFailure>
        {
            VehicleHardpointFailure.WarpedFrame,
            VehicleHardpointFailure.EngineMisfire,
            VehicleHardpointFailure.TransmissionSlip,
        };

        TryAddRandomFailure(vehicle, vehicle, candidates);
    }

    private void TryTriggerHardpointFailure(
        EntityUid vehicle,
        EntityUid hardpoint,
        float amount,
        float previousIntegrity,
        HardpointIntegrityComponent integrity)
    {
        if (integrity.MaxIntegrity <= 0f)
            return;

        var previousFraction = Math.Clamp(previousIntegrity / integrity.MaxIntegrity, 0f, 1f);
        var currentFraction = Math.Clamp(integrity.Integrity / integrity.MaxIntegrity, 0f, 1f);
        var damageFraction = amount / integrity.MaxIntegrity;

        if (previousFraction > 0.75f && currentFraction > 0.75f && damageFraction < 0.08f)
            return;

        var chance = Math.Clamp(0.06f + damageFraction * 1.1f + (1f - currentFraction) * 0.22f, 0f, 0.5f);
        if (!_random.Prob(chance))
            return;

        var candidates = GetFailureCandidates(vehicle, hardpoint);
        TryAddRandomFailure(vehicle, hardpoint, candidates);
    }

    private List<VehicleHardpointFailure> GetFailureCandidates(EntityUid vehicle, EntityUid hardpoint)
    {
        var candidates = new List<VehicleHardpointFailure>();

        if (hardpoint == vehicle)
        {
            candidates.Add(VehicleHardpointFailure.WarpedFrame);
            candidates.Add(VehicleHardpointFailure.EngineMisfire);
            candidates.Add(VehicleHardpointFailure.TransmissionSlip);
            return candidates;
        }

        if (HasComp<VehicleArmorHardpointComponent>(hardpoint))
            candidates.Add(VehicleHardpointFailure.ArmorCompromised);

        if (HasComp<GunComponent>(hardpoint))
        {
            candidates.Add(VehicleHardpointFailure.FeedJam);
            candidates.Add(VehicleHardpointFailure.RunawayTrigger);
        }

        if (HasComp<VehicleTurretComponent>(hardpoint))
        {
            candidates.Add(VehicleHardpointFailure.TurretTraverseDamage);
            candidates.Add(VehicleHardpointFailure.DamagedMount);
        }

        if (HasComp<VehicleWheelItemComponent>(hardpoint))
            candidates.Add(VehicleHardpointFailure.TransmissionSlip);

        if (TryComp(hardpoint, out HardpointItemComponent? item))
        {
            if (string.Equals(item.HardpointType, "Support", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.HardpointType, "SupportAttachment", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(VehicleHardpointFailure.EngineMisfire);
            }

            if (string.Equals(item.HardpointType, "Cannon", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.HardpointType, "Launcher", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.HardpointType, "Secondary", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(VehicleHardpointFailure.DamagedMount);
            }
        }

        return candidates;
    }

    private bool TryAddRandomFailure(
        EntityUid vehicle,
        EntityUid hardpoint,
        List<VehicleHardpointFailure> candidates)
    {
        if (candidates.Count == 0)
            return false;

        VehicleHardpointFailureComponent? failures = null;
        if (TryComp(hardpoint, out failures))
        {
            candidates.RemoveAll(failure => failures.ActiveFailures.Contains(failure));
            if (failures.ActiveFailures.Count >= failures.MaxActiveFailures)
                return false;
        }

        if (candidates.Count == 0)
            return false;

        var picked = candidates[_random.Next(0, candidates.Count)];
        return AddHardpointFailure(vehicle, hardpoint, picked, failures);
    }

    private bool AddHardpointFailure(
        EntityUid vehicle,
        EntityUid hardpoint,
        VehicleHardpointFailure failure,
        VehicleHardpointFailureComponent? failures = null)
    {
        failures ??= EnsureComp<VehicleHardpointFailureComponent>(hardpoint);

        if (failures.ActiveFailures.Contains(failure))
            return false;

        if (failures.ActiveFailures.Count >= failures.MaxActiveFailures)
            return false;

        failures.ActiveFailures.Add(failure);
        failures.RepairProgress.Remove(failure);
        Dirty(hardpoint, failures);

        NotifyFailureDetected(vehicle, hardpoint, failure);
        RefreshFailureEffects(vehicle, hardpoint);
        UpdateHardpointUi(vehicle);
        RaiseHardpointSlotsChanged(vehicle);
        return true;
    }

    private bool RemoveHardpointFailure(
        EntityUid vehicle,
        EntityUid hardpoint,
        VehicleHardpointFailure failure,
        VehicleHardpointFailureComponent failures)
    {
        if (!failures.ActiveFailures.Remove(failure))
            return false;

        failures.Repairing.Remove(failure);
        failures.RepairProgress.Remove(failure);
        Dirty(hardpoint, failures);

        if (failures.ActiveFailures.Count == 0)
            RemCompDeferred<VehicleHardpointFailureComponent>(hardpoint);

        RefreshFailureEffects(vehicle, hardpoint);
        UpdateHardpointUi(vehicle);
        RaiseHardpointSlotsChanged(vehicle);
        return true;
    }

    private void RefreshFailureEffects(EntityUid vehicle, EntityUid hardpoint)
    {
        RefreshVehicleArmorModifiers(vehicle);
        RefreshVehicleMechanicalFailureModifiers(vehicle);
        RefreshSupportModifiers(vehicle);
        RefreshGunModifiers(hardpoint);
    }

    private void RefreshVehicleArmorModifiers(EntityUid vehicle)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            return;
        }

        var allArmorModifierSets = new HashSet<ProtoId<DamageModifierSetPrototype>>();
        var activeArmorModifierSets = new HashSet<ProtoId<DamageModifierSetPrototype>>();
        var knownExplosionCoefficients = new List<float>();
        float? activeExplosionCoefficient = null;

        foreach (var mountedSlot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mountedSlot.Item is not { } item ||
                !TryComp(item, out VehicleArmorHardpointComponent? armor))
            {
                continue;
            }

            foreach (var setId in armor.ModifierSets)
                allArmorModifierSets.Add(setId);

            if (armor.ExplosionCoefficient is { } coefficient)
                knownExplosionCoefficients.Add(coefficient);

            if (!IsHardpointFunctional(item) ||
                HasHardpointFailure(item, VehicleHardpointFailure.ArmorCompromised))
            {
                continue;
            }

            foreach (var setId in armor.ModifierSets)
                activeArmorModifierSets.Add(setId);

            activeExplosionCoefficient ??= armor.ExplosionCoefficient;
        }

        if (allArmorModifierSets.Count > 0 && TryComp(vehicle, out DamageProtectionBuffComponent? buff))
        {
            foreach (var setId in allArmorModifierSets)
                buff.Modifiers.Remove(setId);

            foreach (var setId in activeArmorModifierSets)
            {
                if (_prototypeManager.TryIndex(setId, out DamageModifierSetPrototype? modifier))
                    buff.Modifiers[setId] = modifier;
            }

            if (buff.Modifiers.Count == 0)
                RemComp<DamageProtectionBuffComponent>(vehicle);
            else
                Dirty(vehicle, buff);
        }
        else if (activeArmorModifierSets.Count > 0)
        {
            var ensuredBuff = EnsureComp<DamageProtectionBuffComponent>(vehicle);
            foreach (var setId in activeArmorModifierSets)
            {
                if (_prototypeManager.TryIndex(setId, out DamageModifierSetPrototype? modifier))
                    ensuredBuff.Modifiers[setId] = modifier;
            }

            Dirty(vehicle, ensuredBuff);
        }

        if (activeExplosionCoefficient != null)
        {
            _explosion.SetExplosionResistance(vehicle, activeExplosionCoefficient.Value, worn: false);
        }
        else if (TryComp(vehicle, out ExplosionResistanceComponent? resistance))
        {
            foreach (var coefficient in knownExplosionCoefficients)
            {
                if (MathF.Abs(resistance.DamageCoefficient - coefficient) < 0.0001f)
                {
                    RemComp<ExplosionResistanceComponent>(vehicle);
                    break;
                }
            }
        }
    }

    private void RefreshVehicleMechanicalFailureModifiers(EntityUid vehicle)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(vehicle, out HardpointSlotsComponent? hardpoints) ||
            !TryComp(vehicle, out ItemSlotsComponent? itemSlots))
        {
            RemCompDeferred<VehicleMechanicalFailureModifierComponent>(vehicle);
            return;
        }

        var speed = 1f;
        var reverse = 1f;
        var accel = 1f;
        var hasFailure = false;

        void Accumulate(EntityUid uid)
        {
            if (!TryComp(uid, out VehicleHardpointFailureComponent? failures))
                return;

            foreach (var failure in failures.ActiveFailures)
            {
                switch (failure)
                {
                    case VehicleHardpointFailure.EngineMisfire:
                        speed *= 0.8f;
                        reverse *= 0.9f;
                        accel *= 0.65f;
                        hasFailure = true;
                        break;
                    case VehicleHardpointFailure.TransmissionSlip:
                        speed *= 0.7f;
                        reverse *= 0.5f;
                        accel *= 0.6f;
                        hasFailure = true;
                        break;
                    case VehicleHardpointFailure.WarpedFrame:
                        speed *= 0.85f;
                        reverse *= 0.85f;
                        accel *= 0.85f;
                        hasFailure = true;
                        break;
                }
            }
        }

        Accumulate(vehicle);
        foreach (var mountedSlot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mountedSlot.Item is { } item)
                Accumulate(item);
        }

        if (!hasFailure)
        {
            RemCompDeferred<VehicleMechanicalFailureModifierComponent>(vehicle);
            return;
        }

        var modifier = EnsureComp<VehicleMechanicalFailureModifierComponent>(vehicle);
        modifier.SpeedMultiplier = Math.Clamp(speed, 0.1f, 1f);
        modifier.ReverseSpeedMultiplier = Math.Clamp(reverse, 0.1f, 1f);
        modifier.AccelerationMultiplier = Math.Clamp(accel, 0.1f, 1f);
        Dirty(vehicle, modifier);
    }

    private string GetFailureName(VehicleHardpointFailure failure)
    {
        return failure switch
        {
            VehicleHardpointFailure.ArmorCompromised => "armor plating breach",
            VehicleHardpointFailure.FeedJam => "jammed feed system",
            VehicleHardpointFailure.RunawayTrigger => "runaway trigger",
            VehicleHardpointFailure.TurretTraverseDamage => "damaged traverse ring",
            VehicleHardpointFailure.EngineMisfire => "engine misfire",
            VehicleHardpointFailure.TransmissionSlip => "transmission slip",
            VehicleHardpointFailure.WarpedFrame => "warped frame",
            VehicleHardpointFailure.DamagedMount => "damaged mount",
            _ => "hardpoint failure",
        };
    }

    private string GetFailureEffect(VehicleHardpointFailure failure)
    {
        return failure switch
        {
            VehicleHardpointFailure.ArmorCompromised => "Armor protection from this hardpoint is offline.",
            VehicleHardpointFailure.FeedJam => "This weapon can randomly jam or misfire.",
            VehicleHardpointFailure.RunawayTrigger => "This weapon can discharge on its own while mounted.",
            VehicleHardpointFailure.TurretTraverseDamage => "Turret traverse speed is severely reduced.",
            VehicleHardpointFailure.EngineMisfire => "Vehicle acceleration and top speed are reduced.",
            VehicleHardpointFailure.TransmissionSlip => "Vehicle acceleration, reverse speed, and top speed are reduced.",
            VehicleHardpointFailure.WarpedFrame => "The vehicle frame drags and reduces movement performance.",
            VehicleHardpointFailure.DamagedMount => "This hardpoint's output is weakened until the mount is reseated.",
            _ => "The hardpoint is malfunctioning.",
        };
    }

    private static IReadOnlyList<VehicleHardpointFailureRepairStep> GetFailureRepairSteps(VehicleHardpointFailure failure)
    {
        return failure switch
        {
            VehicleHardpointFailure.ArmorCompromised => ArmorCompromisedRepairSteps,
            VehicleHardpointFailure.FeedJam => FeedJamRepairSteps,
            VehicleHardpointFailure.RunawayTrigger => RunawayTriggerRepairSteps,
            VehicleHardpointFailure.TurretTraverseDamage => TurretTraverseRepairSteps,
            VehicleHardpointFailure.EngineMisfire => EngineMisfireRepairSteps,
            VehicleHardpointFailure.TransmissionSlip => TransmissionSlipRepairSteps,
            VehicleHardpointFailure.WarpedFrame => WarpedFrameRepairSteps,
            VehicleHardpointFailure.DamagedMount => DamagedMountRepairSteps,
            _ => DamagedMountRepairSteps,
        };
    }

    private static int GetFailureRepairProgress(VehicleHardpointFailureComponent failures, VehicleHardpointFailure failure)
    {
        return failures.RepairProgress.TryGetValue(failure, out var step)
            ? Math.Max(0, step)
            : 0;
    }

    private static void SetFailureRepairProgress(
        VehicleHardpointFailureComponent failures,
        VehicleHardpointFailure failure,
        int step)
    {
        if (step <= 0)
        {
            failures.RepairProgress.Remove(failure);
            return;
        }

        failures.RepairProgress[failure] = step;
    }

    private static bool TryGetFailureRepairStep(
        VehicleHardpointFailure failure,
        int stepIndex,
        out VehicleHardpointFailureRepairStep step)
    {
        var steps = GetFailureRepairSteps(failure);
        if (stepIndex < 0 || stepIndex >= steps.Count)
        {
            step = default;
            return false;
        }

        step = steps[stepIndex];
        return true;
    }

    private string GetFailureRepairToolName(VehicleHardpointFailureRepairStep step)
    {
        var tool = step.Tool;
        if (_prototypeManager.TryIndex(tool, out ToolQualityPrototype? prototype))
            return Loc.GetString(prototype.ToolName);

        return tool.ToString();
    }

    private string GetFailureRepairPath(VehicleHardpointFailure failure)
    {
        var tools = new List<string>();
        foreach (var step in GetFailureRepairSteps(failure))
        {
            tools.Add(GetFailureRepairToolName(step));
        }

        return string.Join(" -> ", tools);
    }

    private string GetFailureStatus(VehicleHardpointFailureComponent failures, VehicleHardpointFailure failure)
    {
        var stepIndex = GetFailureRepairProgress(failures, failure);
        if (!TryGetFailureRepairStep(failure, stepIndex, out var step))
            return GetFailureName(failure);

        return $"{GetFailureName(failure)} ({stepIndex + 1}/{GetFailureRepairSteps(failure).Count}: {GetFailureRepairToolName(step)})";
    }

    private string GetFailureDiagnosticStatus(VehicleHardpointFailure failure)
    {
        return $"{GetFailureName(failure)} - {GetFailureEffect(failure)}";
    }

    private List<string> GetFailureStatuses(EntityUid uid, bool includeRepairStep)
    {
        if (!TryComp(uid, out VehicleHardpointFailureComponent? failures) ||
            failures.ActiveFailures.Count == 0)
        {
            return new List<string>();
        }

        var statuses = new List<string>(failures.ActiveFailures.Count);
        foreach (var failure in failures.ActiveFailures)
        {
            statuses.Add(includeRepairStep
                ? GetFailureStatus(failures, failure)
                : GetFailureDiagnosticStatus(failure));
        }

        return statuses;
    }

    private string GetFailureAlertName(VehicleHardpointFailure failure)
    {
        return failure switch
        {
            VehicleHardpointFailure.ArmorCompromised => "Armor plating breach",
            VehicleHardpointFailure.FeedJam => "Weapon feed jam",
            VehicleHardpointFailure.RunawayTrigger => "Runaway trigger",
            VehicleHardpointFailure.TurretTraverseDamage => "Turret traverse damage",
            VehicleHardpointFailure.EngineMisfire => "Engine misfire",
            VehicleHardpointFailure.TransmissionSlip => "Transmission slip",
            VehicleHardpointFailure.WarpedFrame => "Warped frame",
            VehicleHardpointFailure.DamagedMount => "Damaged mount",
            _ => "Hardpoint failure",
        };
    }

    private List<string> GetVehicleFailureSummaryLines(
        EntityUid vehicle,
        HardpointSlotsComponent? hardpoints = null,
        ItemSlotsComponent? itemSlots = null)
    {
        var lines = new List<string>();

        var frameFailures = GetFailureStatuses(vehicle, includeRepairStep: false);
        if (frameFailures.Count > 0)
            lines.Add($"Hull: {string.Join(", ", frameFailures)}");

        if (!Resolve(vehicle, ref hardpoints, logMissing: false) ||
            !Resolve(vehicle, ref itemSlots, logMissing: false))
        {
            return lines;
        }

        foreach (var mountedSlot in _topology.GetMountedSlots(vehicle, hardpoints, itemSlots))
        {
            if (mountedSlot.Item is not { } item)
                continue;

            var statuses = GetFailureStatuses(item, includeRepairStep: false);
            if (statuses.Count == 0)
                continue;

            lines.Add($"{mountedSlot.CompositeId}: {string.Join(", ", statuses)}");
        }

        return lines;
    }

    private void NotifyFailureDetected(EntityUid vehicle, EntityUid hardpoint, VehicleHardpointFailure failure)
    {
        if (_net.IsClient)
            return;

        var recipients = new HashSet<EntityUid>();
        if (TryComp(vehicle, out VehicleComponent? vehicleComp) && vehicleComp.Operator is { } driver)
            recipients.Add(driver);

        if (TryComp(vehicle, out VehicleWeaponsComponent? weapons))
        {
            if (weapons.Operator is { } weaponsOperator)
                recipients.Add(weaponsOperator);

            foreach (var operatorUid in weapons.OperatorSelections.Keys)
            {
                recipients.Add(operatorUid);
            }

            foreach (var operatorUid in weapons.HardpointOperators.Values)
            {
                recipients.Add(operatorUid);
            }
        }

        if (recipients.Count == 0)
            return;

        var message = hardpoint == vehicle
            ? $"{GetFailureAlertName(failure)} detected."
            : $"{GetFailureAlertName(failure)} detected on {Name(hardpoint)}.";

        foreach (var recipient in recipients)
        {
            if (Exists(recipient))
                _popup.PopupCursor(message, recipient, PopupType.SmallCaution);
        }
    }

    private void ApplyArmorHardpointModifiers(EntityUid vehicle, EntityUid hardpointItem, bool adding)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(hardpointItem, out VehicleArmorHardpointComponent? armor))
            return;

        if (armor.ModifierSets.Count > 0)
        {
            var buff = EnsureComp<DamageProtectionBuffComponent>(vehicle);

            foreach (var setId in armor.ModifierSets)
            {
                if (!_prototypeManager.TryIndex(setId, out var modifier))
                    continue;

                if (adding)
                {
                    buff.Modifiers[setId] = modifier;
                }
                else
                {
                    buff.Modifiers.Remove(setId);
                }
            }

            if (!adding && buff.Modifiers.Count == 0)
            {
                RemComp<DamageProtectionBuffComponent>(vehicle);
            }
            else
            {
                Dirty(vehicle, buff);
            }
        }

        if (armor.ExplosionCoefficient != null)
        {
            if (adding)
            {
                _explosion.SetExplosionResistance(vehicle, armor.ExplosionCoefficient.Value, worn: false);
            }
            else if (TryComp(vehicle, out ExplosionResistanceComponent? resistance) &&
                     MathF.Abs(resistance.DamageCoefficient - armor.ExplosionCoefficient.Value) < 0.0001f)
            {
                RemComp<ExplosionResistanceComponent>(vehicle);
            }
        }
    }

    private void RaiseHardpointSlotsChanged(EntityUid vehicle)
    {
        var ev = new HardpointSlotsChangedEvent(vehicle);
        RaiseLocalEvent(vehicle, ev, broadcast: true);
    }

    private void RaiseVehicleSlotsChanged(EntityUid owner)
    {
        if (!TryGetContainingVehicleFrame(owner, out var vehicle))
            return;

        RaiseHardpointSlotsChanged(vehicle);
    }

    private void OnVehicleCanRun(Entity<HardpointSlotsComponent> ent, ref VehicleCanRunEvent args)
    {
        if (!args.CanRun || HasAllRequired(ent.Owner, ent.Comp))
            return;

        args.CanRun = false;
    }

    private void EnsureSlots(EntityUid uid, HardpointSlotsComponent component, ItemSlotsComponent? itemSlots = null)
    {
        if (component.Slots.Count == 0)
            return;

        EnsureState(uid);
        itemSlots ??= EnsureComp<ItemSlotsComponent>(uid);

        foreach (var slot in component.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (_itemSlots.TryGetSlot(uid, slot.Id, out var existingSlot, itemSlots))
            {
                if (slot.DisableEject && !existingSlot.DisableEject)
                    _itemSlots.SetDisableEject(uid, existingSlot, true, itemSlots);

                continue;
            }

            var whitelist = slot.Whitelist;
            if (whitelist == null)
            {
                whitelist = new EntityWhitelist
                {
                    Components = new[] { HardpointItemComponent.ComponentId },
                };
            }
            else
            {
                var hasComponents = whitelist.Components != null && whitelist.Components.Length > 0;
                var hasTags = whitelist.Tags != null && whitelist.Tags.Count > 0;
                var hasSizes = whitelist.Sizes != null && whitelist.Sizes.Count > 0;
                var hasSkills = whitelist.Skills != null && whitelist.Skills.Count > 0;
                var hasMinMobSize = whitelist.MinMobSize != null;

                if (!hasComponents && !hasTags && !hasSizes && !hasSkills && !hasMinMobSize)
                    whitelist.Components = new[] { HardpointItemComponent.ComponentId };
            }

            var itemSlot = new ItemSlot
            {
                Whitelist = whitelist,
            };

            _itemSlots.AddItemSlot(uid, slot.Id, itemSlot, itemSlots);

            if (slot.DisableEject)
                _itemSlots.SetDisableEject(uid, itemSlot, true, itemSlots);
        }
    }

    internal bool TryGetSlot(HardpointSlotsComponent component, string? id, [NotNullWhen(true)] out HardpointSlot? slot)
    {
        slot = null;

        if (id == null)
            return false;

        foreach (var hardpoint in component.Slots)
        {
            if (hardpoint.Id == id)
            {
                slot = hardpoint;
                return true;
            }
        }

        return false;
    }

    internal bool IsValidHardpoint(EntityUid item, HardpointSlotsComponent slots, HardpointSlot slot)
    {
        if (!TryComp<HardpointItemComponent>(item, out var hardpoint))
            return false;

        if (slots.VehicleFamily is not null)
        {
            if (hardpoint.VehicleFamily is not { } vehicleFamily)
                return false;

            if (vehicleFamily != slots.VehicleFamily.Value)
                return false;
        }

        if (slot.SlotType is not null)
        {
            if (hardpoint.SlotType is not { } slotType)
                return false;

            if (slotType != slot.SlotType.Value)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(slot.CompatibilityId))
        {
            if (string.IsNullOrWhiteSpace(hardpoint.CompatibilityId))
                return false;

            if (!string.Equals(hardpoint.CompatibilityId, slot.CompatibilityId, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (string.IsNullOrWhiteSpace(slot.HardpointType))
            return slot.Whitelist == null || _whitelist.IsValid(slot.Whitelist, item);

        if (!string.Equals(hardpoint.HardpointType, slot.HardpointType, StringComparison.OrdinalIgnoreCase))
            return false;

        return slot.Whitelist == null || _whitelist.IsValid(slot.Whitelist, item);
    }

    private bool HasAllRequired(EntityUid uid, HardpointSlotsComponent component, ItemSlotsComponent? itemSlots = null)
    {
        if (component.Slots.Count == 0)
            return true;

        if (!Resolve(uid, ref itemSlots, logMissing: false))
            return true;

        foreach (var slot in component.Slots)
        {
            if (!slot.Required)
                continue;

            if (!_itemSlots.TryGetSlot(uid, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                return false;

            if (itemSlot.Item is { } item && TryComp(item, out HardpointIntegrityComponent? integrity) && integrity.Integrity <= 0f)
                return false;
        }

        return true;
    }

    internal void RefreshCanRun(EntityUid uid)
    {
        if (!TryComp<VehicleComponent>(uid, out var vehicle))
            return;

        _vehicles.RefreshCanRun((uid, vehicle));
    }

    private void OnVehicleDamageModify(Entity<HardpointSlotsComponent> ent, ref DamageModifyEvent args)
    {
        if (_net.IsClient)
            return;

        var incomingMultiplier = GetVehicleIncomingDamageMultiplier(args.Origin, args.Tool);
        if (incomingMultiplier > 1f)
            args.Damage = ScaleDamage(args.Damage, incomingMultiplier);

        var totalDamage = args.Damage.GetTotal().Float();
        if (totalDamage <= 0f)
            return;

        if (!TryComp(ent.Owner, out ItemSlotsComponent? itemSlots))
            return;

        var topLevelHardpoints = new List<(EntityUid Item, HardpointIntegrityComponent Integrity)>();
        CollectIntactTopLevelHardpoints(ent.Owner, ent.Comp, itemSlots, topLevelHardpoints);

        var anyTopLevelIntact = topLevelHardpoints.Count > 0;

        if (anyTopLevelIntact)
        {
            var visited = new HashSet<EntityUid>();
            foreach (var (item, integrity) in topLevelHardpoints)
            {
                ApplyDamageToHardpointTree(ent.Owner, item, integrity, args.Damage, visited);
            }
        }

        var hullFraction = anyTopLevelIntact ? ent.Comp.FrameDamageFractionWhileIntact : 1f;
        if (TryComp(ent.Owner, out HardpointIntegrityComponent? frameIntegrity))
        {
            var frameDamage = ScaleDamage(args.Damage, hullFraction);
            var frameAmount = GetVehicleFrameDamageAmount(ent.Owner, frameDamage);

            if (frameAmount > 0f)
                DamageHardpoint(ent.Owner, ent.Owner, frameAmount, frameIntegrity);
        }

        RefreshVehicleFrameIntegrityFromHardpoints(ent.Owner, ent.Comp, itemSlots);

        args.Damage = ScaleDamage(args.Damage, hullFraction);
    }

    private float GetVehicleIncomingDamageMultiplier(EntityUid? origin, EntityUid? tool)
    {
        var multiplier = 1f;

        if (TryGetVehicleDamageMultiplier(origin, out var originMultiplier))
            multiplier = MathF.Max(multiplier, originMultiplier);

        if (TryGetVehicleDamageMultiplier(tool, out var toolMultiplier))
            multiplier = MathF.Max(multiplier, toolMultiplier);

        return multiplier;
    }

    private bool TryGetVehicleDamageMultiplier(EntityUid? source, out float multiplier)
    {
        multiplier = 1f;

        if (source == null || !TryComp<VehicleDamageMultiplierComponent>(source.Value, out var vehicleDamage))
            return false;

        multiplier = MathF.Max(vehicleDamage.Multiplier, 0f);
        return multiplier > 0f;
    }

    private void CollectIntactTopLevelHardpoints(
        EntityUid owner,
        HardpointSlotsComponent slots,
        ItemSlotsComponent itemSlots,
        List<(EntityUid Item, HardpointIntegrityComponent Integrity)> intactHardpoints)
    {
        foreach (var slot in slots.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(owner, slot.Id, out var itemSlot, itemSlots) || !itemSlot.HasItem)
                continue;

            if (itemSlot.Item is not { } item)
                continue;

            if (TryComp(item, out HardpointIntegrityComponent? integrity) && integrity.Integrity > 0f)
                intactHardpoints.Add((item, integrity));
        }
    }

    private void ApplyDamageToHardpointTree(
        EntityUid vehicle,
        EntityUid hardpoint,
        HardpointIntegrityComponent integrity,
        DamageSpecifier damage,
        HashSet<EntityUid> visited)
    {
        if (!visited.Add(hardpoint))
            return;

        ApplyDamageToHardpoint(vehicle, hardpoint, integrity, damage);

        if (!TryComp(hardpoint, out HardpointSlotsComponent? childSlots) ||
            !TryComp(hardpoint, out ItemSlotsComponent? childItemSlots))
        {
            return;
        }

        foreach (var slot in childSlots.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(hardpoint, slot.Id, out var itemSlot, childItemSlots) ||
                itemSlot.Item is not { } childHardpoint)
            {
                continue;
            }

            if (!TryComp(childHardpoint, out HardpointIntegrityComponent? childIntegrity) ||
                childIntegrity.Integrity <= 0f)
            {
                continue;
            }

            ApplyDamageToHardpointTree(vehicle, childHardpoint, childIntegrity, damage, visited);
        }
    }

    private DamageSpecifier ScaleDamage(DamageSpecifier source, float fraction)
    {
        if (MathF.Abs(fraction - 1f) < 0.0001f)
            return source;

        var scaled = new DamageSpecifier();
        foreach (var (type, value) in source.DamageDict)
        {
            scaled.DamageDict[type] = value * fraction;
        }

        return scaled;
    }

    private void ApplyDamageToHardpoint(EntityUid vehicle, EntityUid hardpoint, HardpointIntegrityComponent integrity, DamageSpecifier damage)
    {
        var amount = GetHardpointDamageAmount(hardpoint, damage);

        if (amount <= 0f)
            return;

        DamageHardpoint(vehicle, hardpoint, amount, integrity);
    }

    private float GetHardpointDamageAmount(EntityUid hardpoint, DamageSpecifier damage)
    {
        var modifiedTotal = MathF.Max(damage.GetTotal().Float(), 0f);
        var modifierSets = new List<DamageModifierSet>();
        CollectHardpointDamageModifierSets(hardpoint, modifierSets);

        if (modifierSets.Count > 0)
        {
            var modifiedDamage = DamageSpecifier.ApplyModifierSets(damage, modifierSets);
            modifiedTotal = MathF.Max(modifiedDamage.GetTotal().Float(), 0f);
        }

        var total = modifiedTotal;
        var damageMultiplier = 1f;
        if (TryComp<HardpointItemComponent>(hardpoint, out var hardpointItem))
        {
            damageMultiplier = MathF.Max(hardpointItem.DamageMultiplier, 0f);
            total *= damageMultiplier;
        }

        return total;
    }

    private void CollectHardpointDamageModifierSets(EntityUid hardpoint, List<DamageModifierSet> modifierSets)
    {
        if (TryComp(hardpoint, out HardpointDamageModifierComponent? hardpointModifiers))
        {
            foreach (var modifierSetId in hardpointModifiers.ModifierSets)
            {
                if (_prototypeManager.TryIndex<DamageModifierSetPrototype>(modifierSetId, out var modifierSet))
                    modifierSets.Add(modifierSet);
            }
        }

        if (TryComp(hardpoint, out VehicleArmorHardpointComponent? armorHardpoint))
        {
            foreach (var modifierSetId in armorHardpoint.ModifierSets)
            {
                if (_prototypeManager.TryIndex<DamageModifierSetPrototype>(modifierSetId, out var modifierSet))
                    modifierSets.Add(modifierSet);
            }
        }
    }

    private float GetVehicleFrameDamageAmount(EntityUid vehicle, DamageSpecifier damage)
    {
        var total = MathF.Max(damage.GetTotal().Float(), 0f);
        if (!TryComp(vehicle, out DamageProtectionBuffComponent? protection) ||
            protection.Modifiers.Count == 0)
        {
            return total;
        }

        var modifiedDamage = damage;
        foreach (var modifier in protection.Modifiers.Values)
        {
            modifiedDamage = DamageSpecifier.ApplyModifierSet(modifiedDamage, modifier);
        }

        return MathF.Max(modifiedDamage.GetTotal().Float(), 0f);
    }

    private void OnHardpointIntegrityInit(Entity<HardpointIntegrityComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.Integrity <= 0f)
            ent.Comp.Integrity = ent.Comp.MaxIntegrity;

        UpdateFrameDamageAppearance(ent.Owner, ent.Comp);
    }

    private void OnHardpointExamined(Entity<HardpointIntegrityComponent> ent, ref ExaminedEvent args)
    {
        var current = ent.Comp.Integrity;
        var max = ent.Comp.MaxIntegrity;
        if (TryComp(ent.Owner, out VehicleComponent? _) &&
            TryComp(ent.Owner, out HardpointSlotsComponent? slots) &&
            TryComp(ent.Owner, out ItemSlotsComponent? itemSlots) &&
            TryGetVehicleEffectiveIntegrity(ent.Owner, ent.Comp, slots, itemSlots, out var effectiveCurrent, out var effectiveMax))
        {
            current = effectiveCurrent;
            max = effectiveMax;
        }

        var percent = max > 0f ? current / max : 0f;

        if (HasComp<XenoComponent>(args.Examiner))
        {
            args.PushMarkup(Loc.GetString(GetHardpointConditionString(percent)));
            return;
        }

        var color = GetHardpointIntegrityColor(percent);
        args.PushMarkup(Loc.GetString("rmc-hardpoint-integrity-examine",
            ("color", color),
            ("current", (int)MathF.Ceiling(current)),
            ("max", (int)MathF.Ceiling(max)),
            ("percent", (int)MathF.Round(percent * 100f))));

        if (TryGetArmorExamineModifiers(ent.Owner, out var acid, out var slash, out var bullet, out var explosive, out var blunt))
        {
            args.PushMarkup(Loc.GetString("rmc-hardpoint-armor-modifiers-examine",
                ("acid", FormatModifierValue(acid)),
                ("slash", FormatModifierValue(slash)),
                ("bullet", FormatModifierValue(bullet)),
                ("explosive", FormatModifierValue(explosive)),
                ("blunt", FormatModifierValue(blunt))));
        }

        if (TryComp(ent.Owner, out HardpointSlotsComponent? hardpointSlots) &&
            TryComp(ent.Owner, out ItemSlotsComponent? hardpointItemSlots))
        {
            var vehicleFailures = GetVehicleFailureSummaryLines(ent.Owner, hardpointSlots, hardpointItemSlots);
            if (vehicleFailures.Count > 0)
            {
                args.PushMarkup("[color=orange]Vehicle failure diagnostics:[/color]");
                foreach (var failureLine in vehicleFailures)
                {
                    args.PushMarkup($"[color=orange]{failureLine}[/color]");
                }
            }
        }

        if (!HasComp<HardpointSlotsComponent>(ent.Owner) &&
            !_topology.TryGetVehicle(ent.Owner, out _, includeSelf: false) &&
            TryComp(ent.Owner, out VehicleHardpointFailureComponent? failures) &&
            failures.ActiveFailures.Count > 0)
        {
            foreach (var failure in failures.ActiveFailures)
            {
                var steps = GetFailureRepairSteps(failure);
                var stepIndex = Math.Clamp(GetFailureRepairProgress(failures, failure), 0, Math.Max(steps.Count - 1, 0));
                if (!TryGetFailureRepairStep(failure, stepIndex, out var step))
                    continue;

                args.PushMarkup(
                    $"[color=orange]{GetFailureName(failure)}[/color]: {GetFailureEffect(failure)} " +
                    $"Repair step {stepIndex + 1}/{steps.Count}: {step.Instruction} " +
                    $"Use {GetFailureRepairToolName(step)}. Path: {GetFailureRepairPath(failure)}.");
            }
        }
    }

    private bool TryGetArmorExamineModifiers(
        EntityUid uid,
        out float acid,
        out float slash,
        out float bullet,
        out float explosive,
        out float blunt)
    {
        acid = 1f;
        slash = 1f;
        bullet = 1f;
        explosive = 1f;
        blunt = 1f;

        if (!TryComp(uid, out VehicleArmorHardpointComponent? armor))
            return false;

        if (TryComp(uid, out HardpointItemComponent? item) &&
            item.VehicleFamily == "Tank" &&
            _prototypeManager.TryIndex<DamageModifierSetPrototype>("VehicleFrameTank", out var tankBase))
        {
            ApplyDamageModifierCoefficients(tankBase, ref acid, ref slash, ref bullet, ref explosive, ref blunt);
        }

        foreach (var modifierSetId in armor.ModifierSets)
        {
            if (!_prototypeManager.TryIndex(modifierSetId, out DamageModifierSetPrototype? modifierSet))
                continue;

            ApplyDamageModifierCoefficients(modifierSet, ref acid, ref slash, ref bullet, ref explosive, ref blunt);
        }

        return true;
    }

    private bool TryGetVehicleEffectiveIntegrity(
        EntityUid vehicle,
        HardpointIntegrityComponent frame,
        HardpointSlotsComponent slots,
        ItemSlotsComponent itemSlots,
        out float current,
        out float max)
    {
        current = frame.Integrity;
        max = frame.MaxIntegrity;
        var maxTopLevelCurrent = 0f;
        var maxTopLevelMax = 0f;
        var intactTopLevelHardpoints = 0;

        foreach (var slot in slots.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (!_itemSlots.TryGetSlot(vehicle, slot.Id, out var itemSlot, itemSlots) ||
                itemSlot.Item is not { } item ||
                !TryComp(item, out HardpointIntegrityComponent? integrity))
            {
                continue;
            }

            if (integrity.Integrity > 0f)
            {
                intactTopLevelHardpoints++;
                maxTopLevelCurrent = MathF.Max(maxTopLevelCurrent, integrity.Integrity);
            }

            maxTopLevelMax = MathF.Max(maxTopLevelMax, integrity.MaxIntegrity);
        }

        if (maxTopLevelMax <= 0f)
            return false;

        var hullFraction = intactTopLevelHardpoints > 0 ? slots.FrameDamageFractionWhileIntact : 1f;
        current = GetVehicleEffectiveIntegrity(frame.Integrity, maxTopLevelCurrent, hullFraction);
        max = GetVehicleEffectiveIntegrity(frame.MaxIntegrity, maxTopLevelMax, slots.FrameDamageFractionWhileIntact);
        return true;
    }

    private static float GetVehicleEffectiveIntegrity(float frameIntegrity, float topLevelHardpointIntegrity, float hullFraction)
    {
        frameIntegrity = MathF.Max(0f, frameIntegrity);
        topLevelHardpointIntegrity = MathF.Max(0f, topLevelHardpointIntegrity);
        hullFraction = Math.Clamp(hullFraction, 0f, 1f);

        if (topLevelHardpointIntegrity <= 0f)
            return frameIntegrity;

        var protectedHullRemaining = frameIntegrity - topLevelHardpointIntegrity * hullFraction;
        if (protectedHullRemaining <= 0f)
            return topLevelHardpointIntegrity;

        return topLevelHardpointIntegrity + protectedHullRemaining;
    }

    private static void ApplyDamageModifierCoefficients(
        DamageModifierSet modifierSet,
        ref float acid,
        ref float slash,
        ref float bullet,
        ref float explosive,
        ref float blunt)
    {
        if (modifierSet.Coefficients.TryGetValue("Caustic", out var acidCoefficient))
            acid *= acidCoefficient;

        if (modifierSet.Coefficients.TryGetValue("Slash", out var slashCoefficient))
            slash *= slashCoefficient;

        if (modifierSet.Coefficients.TryGetValue("Piercing", out var bulletCoefficient))
            bullet *= bulletCoefficient;

        if (modifierSet.Coefficients.TryGetValue("Structural", out var explosiveCoefficient))
            explosive *= explosiveCoefficient;

        if (modifierSet.Coefficients.TryGetValue("Blunt", out var bluntCoefficient))
            blunt *= bluntCoefficient;
    }

    private static string FormatModifierValue(float value)
    {
        return value.ToString("0.###");
    }

    private string GetHardpointIntegrityColor(float percent)
    {
        if (percent >= 0.9f)
            return "green";

        if (percent >= 0.7f)
            return "yellow";

        if (percent >= 0.4f)
            return "orange";

        if (percent >= 0.15f)
            return "red";

        return "crimson";
    }

    private string GetHardpointConditionString(float percent)
    {
        if (percent >= 0.9f)
            return "rmc-hardpoint-condition-pristine";

        if (percent >= 0.7f)
            return "rmc-hardpoint-condition-good";

        if (percent >= 0.4f)
            return "rmc-hardpoint-condition-worn";

        if (percent >= 0.15f)
            return "rmc-hardpoint-condition-bad";

        return "rmc-hardpoint-condition-critical";
    }

    public bool DamageHardpoint(EntityUid vehicle, EntityUid hardpoint, float amount, HardpointIntegrityComponent? integrity = null)
    {
        if (_net.IsClient || amount <= 0f)
            return false;

        if (!Resolve(hardpoint, ref integrity, logMissing: false))
            return false;

        if (integrity.Integrity <= 0f)
            return false;

        if (integrity.Integrity > integrity.MaxIntegrity && integrity.MaxIntegrity > 0f)
            integrity.Integrity = integrity.MaxIntegrity;

        var wasFunctional = IsHardpointFunctional(hardpoint, integrity);
        var previous = integrity.Integrity;
        integrity.Integrity = MathF.Max(0f, integrity.Integrity - amount);

        if (Math.Abs(previous - integrity.Integrity) < 0.01f)
            return false;

        Dirty(hardpoint, integrity);
        UpdateFrameDamageAppearance(hardpoint, integrity);

        if (hardpoint == vehicle)
            _lock.RefreshForcedOpen(vehicle);

        if (TryComp(hardpoint, out VehicleWheelItemComponent? _))
            _wheels.OnWheelDamaged(vehicle);

        if (previous > 0f && integrity.Integrity <= 0f)
            RefreshCanRun(vehicle);

        UpdateHardpointUi(vehicle);
        HandleHardpointDamageSideEffects(vehicle, hardpoint, amount, previous, integrity, wasFunctional);
        return true;
    }

    private bool TryStartFailureRepair(Entity<HardpointIntegrityComponent> ent, InteractUsingEvent args)
    {
        if (!TryComp(ent.Owner, out VehicleHardpointFailureComponent? failures) ||
            failures.ActiveFailures.Count == 0)
        {
            return false;
        }

        foreach (var failure in failures.ActiveFailures)
        {
            if (failures.Repairing.TryGetValue(failure, out var repairing) && repairing)
            {
                args.Handled = true;
                return true;
            }

            var stepIndex = GetFailureRepairProgress(failures, failure);
            if (!TryGetFailureRepairStep(failure, stepIndex, out var step))
                continue;

            if (!_tool.HasQuality(args.Used, step.Tool))
                continue;

            if (step.RequiresWelder && !HasComp<BlowtorchComponent>(args.Used))
                continue;

            if (step.RequiresWelder &&
                !_repairable.UseFuel(args.Used, args.User, ent.Comp.RepairFuelCost, true))
            {
                args.Handled = true;
                return true;
            }

            var time = step.Time * _skills.GetSkillDelayMultiplier(args.User, EngineerSkill);
            failures.Repairing[failure] = true;
            var doAfter = new DoAfterArgs(
                EntityManager,
                args.User,
                time,
                new VehicleHardpointFailureRepairDoAfterEvent(failure, stepIndex),
                ent.Owner,
                ent.Owner,
                args.Used)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
                NeedHand = true,
            };

            if (!_doAfter.TryStartDoAfter(doAfter))
            {
                failures.Repairing.Remove(failure);
                return false;
            }

            args.Handled = true;
            return true;
        }

        return false;
    }

    private void OnFailureRepairDoAfter(Entity<VehicleHardpointFailureComponent> ent, ref VehicleHardpointFailureRepairDoAfterEvent args)
    {
        ent.Comp.Repairing.Remove(args.Failure);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var used = args.Used;
        var stepIndex = GetFailureRepairProgress(ent.Comp, args.Failure);
        if (args.Step != stepIndex ||
            !TryGetFailureRepairStep(args.Failure, stepIndex, out var step))
        {
            return;
        }

        if (used == null || !_tool.HasQuality(used.Value, step.Tool))
            return;

        if (step.RequiresWelder)
        {
            if (!HasComp<BlowtorchComponent>(used.Value) ||
                !TryComp(ent.Owner, out HardpointIntegrityComponent? integrity) ||
                !_repairable.UseFuel(used.Value, args.User, integrity.RepairFuelCost))
            {
                return;
            }
        }

        var vehicle = _topology.TryGetVehicle(ent.Owner, out var containingVehicle)
            ? containingVehicle
            : ent.Owner;

        var nextStep = stepIndex + 1;
        var steps = GetFailureRepairSteps(args.Failure);
        if (nextStep < steps.Count)
        {
            SetFailureRepairProgress(ent.Comp, args.Failure, nextStep);
            Dirty(ent.Owner, ent.Comp);
            UpdateHardpointUi(vehicle);

            if (TryGetFailureRepairStep(args.Failure, nextStep, out var next))
            {
                _popup.PopupClient(
                    $"{GetFailureName(args.Failure)} repair step complete. Next: {GetFailureRepairToolName(next)}.",
                    ent.Owner,
                    args.User);
            }

            return;
        }

        if (!RemoveHardpointFailure(vehicle, ent.Owner, args.Failure, ent.Comp))
            return;

        _popup.PopupClient($"{GetFailureName(args.Failure)} repaired.", ent.Owner, args.User);
    }

    private void OnHardpointRepair(Entity<HardpointIntegrityComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || args.User == null)
            return;

        var used = args.Used;
        var isFrame = HasComp<HardpointSlotsComponent>(ent.Owner);
        if (TryStartFailureRepair(ent, args))
            return;

        var usedWelder = _tool.HasQuality(used, ent.Comp.RepairToolQuality) && HasComp<BlowtorchComponent>(used);
        var usedWrench = isFrame && _tool.HasQuality(used, ent.Comp.FrameFinishToolQuality);

        if (!usedWelder && !usedWrench)
            return;

        if (isFrame)
            RefreshVehicleFrameIntegrityFromHardpoints(ent.Owner);

        if (ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
        {
            _popup.PopupClient(Loc.GetString("rmc-hardpoint-intact"), ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (isFrame)
        {
            _popup.PopupClient("Repair the vehicle's hardpoints to restore hull integrity.", ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (ent.Comp.Repairing)
        {
            args.Handled = true;
            return;
        }

        var weldCap = ent.Comp.MaxIntegrity * ent.Comp.FrameWeldCapFraction;

        if (usedWelder && isFrame && ent.Comp.Integrity >= weldCap - ent.Comp.FrameRepairEpsilon)
        {
            _popup.PopupClient("Finish tightening the frame with a wrench.", ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (usedWrench && ent.Comp.Integrity < weldCap - ent.Comp.FrameRepairEpsilon)
        {
            _popup.PopupClient("Weld the frame before tightening it.", ent.Owner, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (usedWelder && !_repairable.UseFuel(used, args.User, ent.Comp.RepairFuelCost, true))
        {
            args.Handled = true;
            return;
        }

        var repairAmount = GetRepairAmountForCurrentStep(ent.Owner, ent.Comp, usedWelder, usedWrench, isFrame);
        if (repairAmount <= 0f)
        {
            args.Handled = true;
            return;
        }

        var repairTime = GetRepairTimeForCurrentStep(ent.Owner, args.User, ent.Comp, repairAmount, isFrame);

        ent.Comp.Repairing = true;

        var doAfter = new DoAfterArgs(EntityManager, args.User, repairTime, new HardpointRepairDoAfterEvent(), ent.Owner, ent.Owner, used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
        {
            ent.Comp.Repairing = false;
            return;
        }

        args.Handled = true;
    }

    private void OnHardpointRepairDoAfter(Entity<HardpointIntegrityComponent> ent, ref HardpointRepairDoAfterEvent args)
    {
        ent.Comp.Repairing = false;

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var used = args.Used;
        var isFrame = HasComp<HardpointSlotsComponent>(ent.Owner);
        var usedWelder = used != null && _tool.HasQuality(used.Value, ent.Comp.RepairToolQuality) && HasComp<BlowtorchComponent>(used);
        var usedWrench = isFrame && used != null && _tool.HasQuality(used.Value, ent.Comp.FrameFinishToolQuality);

        if (!usedWelder && !usedWrench)
            return;

        if (usedWelder)
        {
            if (used == null || !_repairable.UseFuel(used.Value, args.User, ent.Comp.RepairFuelCost))
                return;
        }

        var repairAmount = GetRepairAmountForCurrentStep(ent.Owner, ent.Comp, usedWelder, usedWrench, isFrame);
        if (repairAmount <= 0f)
            return;

        ent.Comp.Integrity = MathF.Min(ent.Comp.MaxIntegrity, ent.Comp.Integrity + repairAmount);

        Dirty(ent.Owner, ent.Comp);
        UpdateFrameDamageAppearance(ent.Owner, ent.Comp);
        if (isFrame)
            _lock.RefreshForcedOpen(ent.Owner);

        RefreshGunModifiers(ent.Owner);

        if (isFrame)
            _lock.RefreshForcedOpen(ent.Owner);

        if (ent.Comp.RepairSound != null)
            _audio.PlayPredicted(ent.Comp.RepairSound, ent.Owner, args.User);

        _popup.PopupClient(Loc.GetString("rmc-hardpoint-repaired"), ent.Owner, args.User);

        var vehicle = _topology.TryGetVehicle(ent.Owner, out var containingVehicle)
            ? containingVehicle
            : ent.Owner;

        if (TryComp(ent.Owner, out VehicleWheelItemComponent? _))
        {
            _wheels.OnWheelDamaged(vehicle);
        }
        else
        {
            RefreshCanRun(vehicle);
        }

        if (ent.Owner != vehicle)
            RefreshVehicleFrameIntegrityFromHardpoints(vehicle);

        RefreshSupportModifiers(vehicle);
        RefreshVehicleArmorModifiers(vehicle);
        RefreshVehicleMechanicalFailureModifiers(vehicle);

        if (ent.Comp.BypassEntryOnZero)
            RefreshCanRun(vehicle);

        UpdateHardpointUi(vehicle);
        RaiseHardpointSlotsChanged(vehicle);

        if (ShouldRepeatRepair(ent.Owner, ent.Comp, usedWelder, usedWrench, isFrame))
            args.Repeat = true;
    }

    private float GetRepairAmountForCurrentStep(
        EntityUid uid,
        HardpointIntegrityComponent integrity,
        bool usedWelder,
        bool usedWrench,
        bool isFrame)
    {
        if (integrity.MaxIntegrity <= 0f)
            return 0f;

        var chunkSize = MathF.Max(integrity.RepairChunkMinimum, integrity.MaxIntegrity * integrity.RepairChunkFraction);
        var weldCap = integrity.MaxIntegrity * integrity.FrameWeldCapFraction;

        if (usedWelder)
        {
            var target = isFrame ? MathF.Min(weldCap, integrity.MaxIntegrity) : integrity.MaxIntegrity;
            return MathF.Max(0f, MathF.Min(chunkSize, target - integrity.Integrity));
        }

        if (usedWrench)
            return MathF.Max(0f, MathF.Min(chunkSize, integrity.MaxIntegrity - integrity.Integrity));

        return 0f;
    }

    private float GetRepairTimeForCurrentStep(
        EntityUid uid,
        EntityUid user,
        HardpointIntegrityComponent integrity,
        float repairAmount,
        bool isFrame)
    {
        if (integrity.MaxIntegrity <= 0f || repairAmount <= 0f)
            return 0f;

        var repairFraction = repairAmount / integrity.MaxIntegrity;
        var skillMultiplier = _skills.GetSkillDelayMultiplier(user, EngineerSkill);

        if (isFrame)
            return integrity.FrameRepairChunkSeconds * (repairFraction / integrity.RepairChunkFraction) * skillMultiplier;

        var repairRate = GetHardpointRepairRate(uid);
        return (repairFraction / repairRate) * skillMultiplier;
    }

    private float GetHardpointRepairRate(EntityUid uid)
    {
        if (TryComp(uid, out HardpointItemComponent? hardpoint))
            return hardpoint.RepairRate > 0f ? hardpoint.RepairRate : 0.01f;

        return 0.01f;
    }

    private bool ShouldRepeatRepair(
        EntityUid uid,
        HardpointIntegrityComponent integrity,
        bool usedWelder,
        bool usedWrench,
        bool isFrame)
    {
        if (integrity.Integrity >= integrity.MaxIntegrity)
            return false;

        if (isFrame)
        {
            var weldCap = integrity.MaxIntegrity * integrity.FrameWeldCapFraction;

            if (usedWelder)
                return integrity.Integrity < weldCap - integrity.FrameRepairEpsilon;

            if (usedWrench)
                return integrity.Integrity >= weldCap - integrity.FrameRepairEpsilon &&
                       integrity.Integrity < integrity.MaxIntegrity;

            return false;
        }

        return usedWelder && integrity.Integrity > 0f && integrity.Integrity < integrity.MaxIntegrity;
    }

    private EntityUid? GetVehicleFromPart(EntityUid part)
    {
        if (!_containers.TryGetContainingContainer(part, out var container))
            return null;

        return container.Owner;
    }

    internal void UpdateHardpointUi(
        EntityUid uid,
        HardpointSlotsComponent? component = null,
        ItemSlotsComponent? itemSlots = null,
        HardpointStateComponent? state = null)
    {
        if (_net.IsClient)
            return;

        if (!Resolve(uid, ref component, logMissing: false))
            return;

        if (!Resolve(uid, ref state, logMissing: false))
            return;

        if (!Resolve(uid, ref itemSlots, logMissing: false))
            return;

        var entries = new List<HardpointUiEntry>(component.Slots.Count);
        float frameIntegrity = 0f;
        float frameMaxIntegrity = 0f;
        var hasFrameIntegrity = false;

        if (TryComp(uid, out HardpointIntegrityComponent? frame))
        {
            frameIntegrity = frame.Integrity;
            frameMaxIntegrity = frame.MaxIntegrity;
            hasFrameIntegrity = true;
        }

        foreach (var slot in component.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            var hasItem = _itemSlots.TryGetSlot(uid, slot.Id, out var itemSlot, itemSlots) && itemSlot.HasItem;
            string? installedName = null;
            NetEntity? installedEntity = null;
            float integrity = 0f;
            float maxIntegrity = 0f;
            var hasIntegrity = false;

            if (hasItem && itemSlot!.Item is { } item)
            {
                installedEntity = GetNetEntity(item);
                installedName = Name(item);

                if (TryComp(item, out HardpointIntegrityComponent? hardpointIntegrity))
                {
                    integrity = hardpointIntegrity.Integrity;
                    maxIntegrity = hardpointIntegrity.MaxIntegrity;
                    hasIntegrity = true;
                }
            }

            entries.Add(new HardpointUiEntry(
                slot.Id,
                slot.HardpointType,
                installedName,
                installedEntity,
                integrity,
                maxIntegrity,
                hasIntegrity,
                hasItem,
                slot.Required,
                state.PendingRemovals.Contains(slot.Id)));

            if (hasItem && itemSlot?.Item is { } turretItem &&
                TryComp(turretItem, out HardpointSlotsComponent? turretSlots) &&
                TryComp(turretItem, out ItemSlotsComponent? turretItemSlots))
            {
                AppendTurretEntries(entries, slot.Id, turretItem, turretSlots, turretItemSlots);
            }
        }

        PopulateHardpointUiFailures(entries, uid, component, itemSlots);

        _ui.SetUiState(uid,
            HardpointUiKey.Key,
            new HardpointBoundUserInterfaceState(
                entries,
                frameIntegrity,
                frameMaxIntegrity,
                hasFrameIntegrity,
                state.LastUiError));
    }

    internal bool HasAttachedHardpoints(EntityUid owner, HardpointSlotsComponent slots, ItemSlotsComponent itemSlots)
    {
        foreach (var slot in slots.Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.Id))
                continue;

            if (_itemSlots.TryGetSlot(owner, slot.Id, out var itemSlot, itemSlots) && itemSlot.HasItem)
                return true;
        }

        return false;
    }

    private void AppendTurretEntries(
        List<HardpointUiEntry> entries,
        string parentSlotId,
        EntityUid turretUid,
        HardpointSlotsComponent turretSlots,
        ItemSlotsComponent turretItemSlots)
    {
        foreach (var turretSlot in turretSlots.Slots)
        {
            if (string.IsNullOrWhiteSpace(turretSlot.Id))
                continue;

            var compositeId = VehicleTurretSlotIds.Compose(parentSlotId, turretSlot.Id);
            var hasItem = _itemSlots.TryGetSlot(turretUid, turretSlot.Id, out var itemSlot, turretItemSlots) &&
                          itemSlot.HasItem;
            string? installedName = null;
            NetEntity? installedEntity = null;
            float integrity = 0f;
            float maxIntegrity = 0f;
            var hasIntegrity = false;

            if (hasItem && itemSlot!.Item is { } installedItem)
            {
                installedEntity = GetNetEntity(installedItem);
                installedName = Name(installedItem);

                if (TryComp(installedItem, out HardpointIntegrityComponent? hardpointIntegrity))
                {
                    integrity = hardpointIntegrity.Integrity;
                    maxIntegrity = hardpointIntegrity.MaxIntegrity;
                    hasIntegrity = true;
                }
            }

            var turretState = EnsureState(turretUid);

            entries.Add(new HardpointUiEntry(
                compositeId,
                turretSlot.HardpointType,
                installedName,
                installedEntity,
                integrity,
                maxIntegrity,
                hasIntegrity,
                hasItem,
                turretSlot.Required,
                turretState.PendingRemovals.Contains(turretSlot.Id)));
        }
    }

    internal void UpdateContainingVehicleUi(EntityUid owner)
    {
        if (!TryGetContainingVehicleFrame(owner, out var vehicle))
            return;

        UpdateHardpointUi(vehicle);
    }

    internal void SetContainingVehicleUiError(EntityUid owner, string? error)
    {
        if (!TryGetContainingVehicleFrame(owner, out var vehicle))
            return;

        EnsureState(vehicle).LastUiError = error;
    }

    internal bool TryGetContainingVehicleFrame(EntityUid owner, out EntityUid vehicle)
    {
        return _topology.TryGetVehicle(owner, out vehicle);
    }

    private void UpdateFrameDamageAppearance(EntityUid uid, HardpointIntegrityComponent component)
    {
        if (_net.IsClient)
            return;

        if (!TryComp(uid, out AppearanceComponent? appearance))
            return;

        var max = component.MaxIntegrity > 0f ? component.MaxIntegrity : 1f;
        var fraction = Math.Clamp(max > 0f ? component.Integrity / max : 1f, 0f, 1f);

        _appearance.SetData(uid, VehicleFrameDamageVisuals.IntegrityFraction, fraction, appearance);
    }

    internal bool TryGetPryingTool(EntityUid user, ProtoId<ToolQualityPrototype> quality, out EntityUid tool)
    {
        tool = default;

        if (!TryComp(user, out HandsComponent? hands))
            return false;

        var activeHand = _hands.GetActiveHand((user, hands));
        if (activeHand == null)
            return false;

        if (!_hands.TryGetHeldItem((user, hands), activeHand, out var held))
            return false;

        if (!TryComp(held.Value, out ToolComponent? toolComp))
            return false;

        if (!_tool.HasQuality(held.Value, quality, toolComp))
            return false;

        tool = held.Value;
        return true;
    }
}
