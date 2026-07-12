using System;
using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Components;
using Content.Shared.Tools.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Anatomy.BodyParts;

public sealed partial class SharedCMURoboticLimbSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    private static readonly TargetBodyZone[] RepairFallbackOrder = [
        TargetBodyZone.RightArm,
        TargetBodyZone.RightHand,
        TargetBodyZone.LeftArm,
        TargetBodyZone.LeftHand,
        TargetBodyZone.RightLeg,
        TargetBodyZone.RightFoot,
        TargetBodyZone.LeftLeg,
        TargetBodyZone.LeftFoot,
    ];

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private RMCRepairableSystem _repairable = default!;
    [Dependency] private RMCWeldEffectSystem _weldEffect = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCDamageableSystem _rmcDamageable = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private readonly Dictionary<EntityUid, EntityUid> _pendingRoboticHits = new();
    private readonly Dictionary<HumanoidVisualLayers, ProtoId<HumanoidSpeciesSpriteLayer>> _desiredVisualLayers = new();
    private readonly HashSet<string> _repairableDamageTypes = new();
    private readonly List<HumanoidVisualLayers> _staleVisualLayers = new();

    private bool _medicalEnabled;
    private bool _bodyPartEnabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMURoboticLimbComponent, BodyPartDamagedEvent>(OnRoboticPartDamaged);
        SubscribeLocalEvent<CMURoboticLimbComponent, ComponentShutdown>(OnRoboticLimbShutdown);
        SubscribeLocalEvent<CMUHumanMedicalComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged, before: [typeof(SharedHitLocationSystem)]);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMURoboticLimbRepairDoAfterEvent>(OnRepairDoAfter);
        SubscribeLocalEvent<CMUHumanMedicalComponent, ComponentShutdown>(OnHumanShutdown);
        SubscribeLocalEvent<CMUHumanMedicalComponent, HitLocationResolvedEvent>(OnHitLocationResolved);
        SubscribeLocalEvent<CMUHumanMedicalComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUHumanMedicalComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<CMUHumanMedicalComponent, DamageModifyAfterResistEvent>(OnDamageModifyAfterResist);

        CacheRepairableDamageTypes();

        _cfg.OnValueChanged(CMUMedicalCCVars.Enabled, v => _medicalEnabled = v, true);
        _cfg.OnValueChanged(CMUMedicalCCVars.BodyPartEnabled, v => _bodyPartEnabled = v, true);
    }

    private void OnRoboticPartDamaged(Entity<CMURoboticLimbComponent> ent, ref BodyPartDamagedEvent args)
    {
        var brute = GroupSum(args.Delta, BruteGroup);
        var burn = GroupSum(args.Delta, BurnGroup);
        if (brute <= FixedPoint2.Zero && burn <= FixedPoint2.Zero)
            return;

        ent.Comp.BruteDamage += brute;
        ent.Comp.BurnDamage += burn;
        Dirty(ent);
    }

    private void OnRoboticLimbShutdown(Entity<CMURoboticLimbComponent> ent, ref ComponentShutdown args)
    {
        RemovePendingRoboticHitPart(ent.Owner);

        if (_net.IsClient)
            return;

        if (TryComp<BodyPartComponent>(ent, out var part) && part.Body is { } body)
            RefreshBodyVisuals(body);
    }

    public void BodyPartAdded(EntityUid body, Entity<BodyPartComponent> part)
    {
        if (_net.IsClient)
            return;

        if (TryComp<CMURoboticLimbComponent>(part.Owner, out var robotic))
            EnsureChildExtremity(part, robotic);

        RefreshBodyVisuals(body);
    }

    public void BodyPartRemoved(EntityUid body)
    {
        if (_net.IsClient)
            return;

        RefreshBodyVisuals(body);
    }

    private void OnHumanShutdown(Entity<CMUHumanMedicalComponent> ent, ref ComponentShutdown args)
    {
        _pendingRoboticHits.Remove(ent.Owner);
    }

    private void OnHitLocationResolved(Entity<CMUHumanMedicalComponent> ent, ref HitLocationResolvedEvent args)
    {
        if (HasComp<SynthComponent>(ent.Owner) ||
            args.ResolvedPartEntity is not { } part ||
            !HasComp<CMURoboticLimbComponent>(part))
        {
            _pendingRoboticHits.Remove(ent.Owner);
            return;
        }

        _pendingRoboticHits[ent.Owner] = part;
    }

    private void OnBeforeDamageChanged(Entity<CMUHumanMedicalComponent> ent, ref BeforeDamageChangedEvent args)
    {
        _pendingRoboticHits.Remove(ent.Owner);
    }

    private void OnDamageModifyAfterResist(Entity<CMUHumanMedicalComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (!_pendingRoboticHits.Remove(ent.Owner, out var part) ||
            !HasComp<CMURoboticLimbComponent>(part))
        {
            return;
        }

        args.Damage = FilterToRepairableDamage(args.Damage);
    }

    private void OnDamageChanged(Entity<CMUHumanMedicalComponent> ent, ref DamageChangedEvent args)
    {
        _pendingRoboticHits.Remove(ent.Owner);
    }

    private void OnInteractUsing(Entity<CMUHumanMedicalComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled ||
            !_medicalEnabled ||
            !_bodyPartEnabled ||
            HasComp<SynthComponent>(ent.Owner))
        {
            return;
        }

        var used = args.Used;
        var user = args.User;
        var repairKind = CMURoboticLimbRepairKind.Brute;
        var isRepairTool = false;

        if (HasComp<BlowtorchComponent>(used))
        {
            repairKind = CMURoboticLimbRepairKind.Brute;
            isRepairTool = true;
        }
        else if (HasComp<RMCCableCoilComponent>(used))
        {
            repairKind = CMURoboticLimbRepairKind.Burn;
            isRepairTool = true;
        }

        if (!isRepairTool || !HasRoboticLimb(ent.Owner))
            return;

        if (!TryPickRepairTarget(user, ent.Owner, repairKind, out var part, out var robotic))
        {
            _popup.PopupClient(Loc.GetString("rmc-repairable-not-damaged", ("target", ent.Owner)),
                user,
                user,
                PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (repairKind == CMURoboticLimbRepairKind.Brute)
        {
            if (!_tool.HasQuality(used, robotic.RepairQuality))
                return;

            if (!_repairable.UseFuel(used, user, robotic.FuelUsed, true))
            {
                args.Handled = true;
                return;
            }
        }
        else if (!TryComp<StackComponent>(used, out var stack) || stack.Count <= 0)
        {
            args.Handled = true;
            return;
        }

        var delay = GetRepairDelay(user, ent.Owner, robotic);
        var ev = new CMURoboticLimbRepairDoAfterEvent
        {
            Part = GetNetEntity(part),
            RepairKind = repairKind,
        };

        var doAfter = new DoAfterArgs(EntityManager, user, delay, ev, ent.Owner, target: ent.Owner, used: used)
        {
            BreakOnDropItem = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
            NeedHand = true,
        };

        args.Handled = true;
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        PopupRepairStart(user, ent.Owner, used, part, repairKind);
        if (repairKind == CMURoboticLimbRepairKind.Brute)
            _weldEffect.SpawnWeldEffect(ent.Owner, doAfter.Delay);
    }

    private void OnRepairDoAfter(Entity<CMUHumanMedicalComponent> ent, ref CMURoboticLimbRepairDoAfterEvent args)
    {
        if (args.RepairKind == CMURoboticLimbRepairKind.Brute)
            _weldEffect.ClearWeldEffect(ent.Owner);

        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (args.Used is not { } used ||
            !TryGetEntity(args.Part, out var netPart))
        {
            return;
        }

        var part = netPart.Value;
        if (!IsAttachedPart(ent.Owner, part) ||
            !TryComp<CMURoboticLimbComponent>(part, out var robotic))
        {
            return;
        }

        if (args.RepairKind == CMURoboticLimbRepairKind.Brute)
        {
            if (!HasComp<BlowtorchComponent>(used) ||
                !_tool.HasQuality(used, robotic.RepairQuality) ||
                !_repairable.UseFuel(used, args.User, robotic.FuelUsed))
            {
                return;
            }
        }
        else if (!HasComp<RMCCableCoilComponent>(used) ||
                 !_stack.Use(used, 1))
        {
            return;
        }

        var amount = args.RepairKind == CMURoboticLimbRepairKind.Brute
            ? robotic.WelderRepairAmount
            : robotic.CableRepairAmount;

        var repaired = RepairRoboticPart(ent.Owner, part, robotic, args.RepairKind, amount, used);
        if (repaired <= FixedPoint2.Zero)
            return;

        PopupRepairFinish(args.User, ent.Owner, used, part, args.RepairKind);
    }

    private TimeSpan GetRepairDelay(EntityUid user, EntityUid patient, CMURoboticLimbComponent robotic)
    {
        var delay = user == patient ? robotic.SelfRepairTime : robotic.RepairTime;
        var multiplier = _skills.GetSkillDelayMultiplier(user, robotic.RepairSkill);
        return TimeSpan.FromSeconds(delay.TotalSeconds * multiplier);
    }

    private bool TryPickRepairTarget(
        EntityUid user,
        EntityUid patient,
        CMURoboticLimbRepairKind repairKind,
        out EntityUid part,
        out CMURoboticLimbComponent robotic)
    {
        part = default;
        robotic = default!;

        var aimed = _zoneTargeting.TryGetFreshSelection(user);
        if (aimed is { } zone &&
            TryPartForZone(patient, zone, repairKind, out part, out robotic))
        {
            return true;
        }

        foreach (var fallback in RepairFallbackOrder)
        {
            if (TryPartForZone(patient, fallback, repairKind, out part, out robotic))
                return true;
        }

        return false;
    }

    private bool TryPartForZone(
        EntityUid patient,
        TargetBodyZone zone,
        CMURoboticLimbRepairKind repairKind,
        out EntityUid part,
        out CMURoboticLimbComponent robotic)
    {
        var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);

        if (TryFindRepairablePart(patient, type, symmetry, repairKind, out part, out robotic))
            return true;

        var fallbackType = type switch
        {
            BodyPartType.Hand => BodyPartType.Arm,
            BodyPartType.Foot => BodyPartType.Leg,
            _ => type,
        };

        if (fallbackType != type &&
            TryFindRepairablePart(patient, fallbackType, symmetry, repairKind, out part, out robotic))
        {
            return true;
        }

        part = default;
        robotic = default!;
        return false;
    }

    private bool TryFindRepairablePart(
        EntityUid patient,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        CMURoboticLimbRepairKind repairKind,
        out EntityUid part,
        out CMURoboticLimbComponent robotic)
    {
        foreach (var (childId, childComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (childComp.PartType != type)
                continue;

            if (symmetry != BodyPartSymmetry.None && childComp.Symmetry != symmetry)
                continue;

            if (!TryComp<CMURoboticLimbComponent>(childId, out var roboticComp))
                continue;

            if (GetRepairableDamage(childId, roboticComp, repairKind) <= FixedPoint2.Zero)
                continue;

            part = childId;
            robotic = roboticComp;
            return true;
        }

        part = default;
        robotic = default!;
        return false;
    }

    private FixedPoint2 RepairRoboticPart(
        EntityUid body,
        EntityUid part,
        CMURoboticLimbComponent robotic,
        CMURoboticLimbRepairKind repairKind,
        FixedPoint2 amount,
        EntityUid used)
    {
        if (amount <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        var repairable = GetRepairableDamage(part, robotic, repairKind);
        var repaired = FixedPoint2.Min(amount, repairable);
        if (repaired <= FixedPoint2.Zero)
            return FixedPoint2.Zero;

        if (repairKind == CMURoboticLimbRepairKind.Brute)
            robotic.BruteDamage = FixedPoint2.Max(FixedPoint2.Zero, robotic.BruteDamage - repaired);
        else
            robotic.BurnDamage = FixedPoint2.Max(FixedPoint2.Zero, robotic.BurnDamage - repaired);

        Dirty(part, robotic);

        if (TryComp<BodyPartHealthComponent>(part, out var health))
        {
            var missing = health.Max - health.Current;
            var partHealed = FixedPoint2.Min(missing, repaired);
            if (partHealed > FixedPoint2.Zero)
                _partHealth.SetCurrent((part, health), health.Current + partHealed);
        }

        if (TryComp<DamageableComponent>(body, out var damageable))
        {
            var group = repairKind == CMURoboticLimbRepairKind.Brute ? BruteGroup : BurnGroup;
            var spec = _rmcDamageable.DistributeHealing((body, damageable), group, repaired);
            if (!spec.Empty)
            {
                _damageable.TryChangeDamage(body,
                    spec,
                    ignoreResistances: true,
                    interruptsDoAfters: false,
                    damageable: damageable,
                    origin: part,
                    tool: used);
            }
        }

        return repaired;
    }

    private FixedPoint2 GetRepairableDamage(
        EntityUid part,
        CMURoboticLimbComponent robotic,
        CMURoboticLimbRepairKind repairKind)
    {
        var tracked = repairKind == CMURoboticLimbRepairKind.Brute
            ? robotic.BruteDamage
            : robotic.BurnDamage;

        if (repairKind != CMURoboticLimbRepairKind.Brute ||
            !TryComp<BodyPartHealthComponent>(part, out var health))
        {
            return tracked;
        }

        var missing = health.Max - health.Current;
        var untracked = missing - robotic.BruteDamage - robotic.BurnDamage;
        return tracked + FixedPoint2.Max(FixedPoint2.Zero, untracked);
    }

    private void EnsureChildExtremity(Entity<BodyPartComponent> part, CMURoboticLimbComponent robotic)
    {
        if (_net.IsClient ||
            robotic.ChildPrototype is not { } prototype ||
            string.IsNullOrWhiteSpace(robotic.ChildSlot))
        {
            return;
        }

        var containerId = SharedBodySystem.GetPartSlotContainerId(robotic.ChildSlot);
        if (_containers.TryGetContainer(part.Owner, containerId, out var existing) &&
            existing.ContainedEntities.Count > 0)
        {
            return;
        }

        var child = Spawn(prototype, Transform(part.Owner).Coordinates);
        if (!TryComp<BodyPartComponent>(child, out var childPart))
        {
            QueueDel(child);
            return;
        }

        var attached = _body.AttachPart(part.Owner, robotic.ChildSlot, child, part.Comp, childPart) ||
                       _body.TryCreatePartSlotAndAttach(part.Owner,
                           robotic.ChildSlot,
                           child,
                           childPart.PartType,
                           part.Comp,
                           childPart);

        if (!attached)
            QueueDel(child);
    }

    private void RefreshBodyVisuals(EntityUid body)
    {
        if (!TryComp<HumanoidAppearanceComponent>(body, out var humanoid))
            return;

        var desired = _desiredVisualLayers;
        desired.Clear();
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (!TryComp<CMURoboticLimbComponent>(partUid, out var robotic))
                continue;

            foreach (var (layer, layerId) in robotic.BaseLayers)
            {
                desired[layer] = layerId;
            }
        }

        if (desired.Count == 0 && !HasComp<CMURoboticLimbOverlayComponent>(body))
            return;

        var tracker = EnsureComp<CMURoboticLimbOverlayComponent>(body);
        var dirty = false;

        foreach (var (layer, layerId) in desired)
        {
            if (!tracker.OriginalLayers.ContainsKey(layer))
            {
                tracker.OriginalLayers[layer] = humanoid.CustomBaseLayers.TryGetValue(layer, out var original)
                    ? original
                    : null;
            }

            var next = new CustomBaseLayerInfo(layerId);
            if (!humanoid.CustomBaseLayers.TryGetValue(layer, out var current) ||
                current.Id != next.Id ||
                current.Color != next.Color)
            {
                humanoid.CustomBaseLayers[layer] = next;
                dirty = true;
            }
        }

        _staleVisualLayers.Clear();
        foreach (var (layer, _) in tracker.OriginalLayers)
        {
            if (desired.ContainsKey(layer))
                continue;

            _staleVisualLayers.Add(layer);
        }

        foreach (var layer in _staleVisualLayers)
        {
            var original = tracker.OriginalLayers[layer];
            if (original is { } restore)
                humanoid.CustomBaseLayers[layer] = restore;
            else
                humanoid.CustomBaseLayers.Remove(layer);

            tracker.OriginalLayers.Remove(layer);
            dirty = true;
        }
        _staleVisualLayers.Clear();

        if (dirty)
            Dirty(body, humanoid);

        if (tracker.OriginalLayers.Count == 0)
            RemComp<CMURoboticLimbOverlayComponent>(body);
    }

    private bool HasRoboticLimb(EntityUid body)
    {
        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(body))
        {
            if (HasComp<CMURoboticLimbComponent>(partUid))
                return true;
        }

        return false;
    }

    private bool IsAttachedPart(EntityUid body, EntityUid part)
    {
        return TryComp<BodyPartComponent>(part, out var partComp) &&
               partComp.Body == body;
    }

    private DamageSpecifier FilterToRepairableDamage(DamageSpecifier damage)
    {
        foreach (var type in damage.DamageDict.Keys)
        {
            if (!_repairableDamageTypes.Contains(type))
                return CopyRepairableDamage(damage);
        }

        return damage;
    }

    private DamageSpecifier CopyRepairableDamage(DamageSpecifier damage)
    {
        var filtered = new DamageSpecifier();
        filtered.DamageDict.EnsureCapacity(damage.DamageDict.Count);

        foreach (var (type, amount) in damage.DamageDict)
        {
            if (_repairableDamageTypes.Contains(type))
                filtered.DamageDict[type] = amount;
        }

        return filtered;
    }

    private void CacheRepairableDamageTypes()
    {
        _repairableDamageTypes.Clear();
        AddRepairableDamageGroup(BruteGroup);
        AddRepairableDamageGroup(BurnGroup);
    }

    private void AddRepairableDamageGroup(ProtoId<DamageGroupPrototype> groupId)
    {
        if (!_prototypes.TryIndex(groupId, out var group))
            return;

        foreach (var type in group.DamageTypes)
            _repairableDamageTypes.Add(type);
    }

    private void RemovePendingRoboticHitPart(EntityUid part)
    {
        EntityUid? bodyToRemove = null;
        foreach (var (body, pendingPart) in _pendingRoboticHits)
        {
            if (pendingPart != part)
                continue;

            bodyToRemove = body;
            break;
        }

        if (bodyToRemove is { } pendingBody)
            _pendingRoboticHits.Remove(pendingBody);
    }

    private FixedPoint2 GroupSum(DamageSpecifier delta, ProtoId<DamageGroupPrototype> group)
    {
        if (!_prototypes.TryIndex(group, out var groupProto))
            return FixedPoint2.Zero;

        return delta.TryGetDamageInGroup(groupProto, out var total)
            ? total
            : FixedPoint2.Zero;
    }

    private void PopupRepairStart(
        EntityUid user,
        EntityUid target,
        EntityUid tool,
        EntityUid part,
        CMURoboticLimbRepairKind repairKind)
    {
        var limb = LimbName(part);
        var selfKey = repairKind == CMURoboticLimbRepairKind.Brute
            ? user == target ? "cmu-robotic-limb-repair-brute-start-self" : "cmu-robotic-limb-repair-brute-start-user"
            : user == target ? "cmu-robotic-limb-repair-burn-start-self" : "cmu-robotic-limb-repair-burn-start-user";
        var othersKey = repairKind == CMURoboticLimbRepairKind.Brute
            ? "cmu-robotic-limb-repair-brute-start-others"
            : "cmu-robotic-limb-repair-burn-start-others";

        var selfMsg = Loc.GetString(selfKey, ("target", target), ("tool", tool), ("limb", limb));
        var othersMsg = Loc.GetString(othersKey, ("user", user), ("target", target), ("tool", tool), ("limb", limb));
        _popup.PopupPredicted(selfMsg, othersMsg, target, user);
    }

    private void PopupRepairFinish(
        EntityUid user,
        EntityUid target,
        EntityUid tool,
        EntityUid part,
        CMURoboticLimbRepairKind repairKind)
    {
        var limb = LimbName(part);
        var selfKey = repairKind == CMURoboticLimbRepairKind.Brute
            ? user == target ? "cmu-robotic-limb-repair-brute-finish-self" : "cmu-robotic-limb-repair-brute-finish-user"
            : user == target ? "cmu-robotic-limb-repair-burn-finish-self" : "cmu-robotic-limb-repair-burn-finish-user";
        var othersKey = repairKind == CMURoboticLimbRepairKind.Brute
            ? "cmu-robotic-limb-repair-brute-finish-others"
            : "cmu-robotic-limb-repair-burn-finish-others";

        var selfMsg = Loc.GetString(selfKey, ("target", target), ("tool", tool), ("limb", limb));
        var othersMsg = Loc.GetString(othersKey, ("user", user), ("target", target), ("tool", tool), ("limb", limb));
        _popup.PopupPredicted(selfMsg, othersMsg, target, user);
    }

    private string LimbName(EntityUid part)
    {
        if (!TryComp<BodyPartComponent>(part, out var bodyPart))
            return "limb";

        var type = bodyPart.PartType switch
        {
            BodyPartType.Arm => "arm",
            BodyPartType.Hand => "hand",
            BodyPartType.Leg => "leg",
            BodyPartType.Foot => "foot",
            _ => "limb",
        };
        return bodyPart.Symmetry switch
        {
            BodyPartSymmetry.Left => "left " + type,
            BodyPartSymmetry.Right => "right " + type,
            _ => type,
        };
    }
}

[Serializable, NetSerializable]
public enum CMURoboticLimbRepairKind : byte
{
    Brute,
    Burn,
}

[Serializable, NetSerializable]
public sealed partial class CMURoboticLimbRepairDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetEntity Part;

    [DataField]
    public CMURoboticLimbRepairKind RepairKind;
}
