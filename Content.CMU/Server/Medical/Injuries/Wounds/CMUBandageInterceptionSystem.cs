using System;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Injuries.Wounds;

public sealed partial class CMUBandageInterceptionSystem : EntitySystem
{
    private const int CorpsmanMedicalSkillLevel = 2;
    private const string BurnKitStack = "CMBurnKit";
    private const string NoWoundsLocId = "cmu-medical-bandage-no-wounds";
    private const string NoWoundsOnBodyPartLocId = "cmu-medical-bandage-no-wounds-on-body-part";
    private const string TraumaKitStack = "CMTraumaKit";
    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _surgery = default!;
    [Dependency] private CMUWoundsSystem _wounds = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private static readonly TimeSpan TreatDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SearchTreatmentDelay = TimeSpan.FromSeconds(0.2);

    private readonly record struct TreatmentTarget(EntityUid Part, bool UsedSearch);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBandagePendingComponent, CMUBandageDoAfterEvent>(OnBandageDoAfter);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    public void HandleAfterInteract(EntityUid medic, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } patient)
            return;
        var used = args.Used;
        if (!TryComp<WoundTreaterComponent>(used, out var treater))
            return;
        if (!IsLayerEnabled())
            return;
        if (!HasComp<CMUHumanMedicalComponent>(patient))
            return;

        var selectedZone = _zoneTargeting.TryGetSelection(args.User);
        var selectedPart = selectedZone is { } zone
            ? PartForZone(patient, zone)
            : null;
        if (treater.Wound == WoundType.Brute
            && selectedPart is { } amputationTarget
            && _surgery.TryCancelPendingAmputation(patient, args.User, amputationTarget))
        {
            args.Handled = true;
            return;
        }

        if (IsSynthPatient(patient))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-bandage-synth-requires-repair-tools"), patient, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var woundTarget = PickBandageTarget(args.User, patient, treater);
        var target = woundTarget;
        if (target is not { } targetSelection)
        {
            var fallbackTarget = PickBleedingTarget(args.User, patient, treater) ??
                                 PickDamageOnlyTarget(args.User, patient, treater);
            if (fallbackTarget is not { } fallbackSelection)
            {
                if (TryHandleArmedSurgeryTool(args.User, patient, used, out var surgeryHandled))
                {
                    args.Handled = surgeryHandled;
                    return;
                }

                var popup = IsTargetedHealingEnabled(args.User)
                    ? NoWoundsOnBodyPartLocId
                    : NoWoundsLocId;
                _popup.PopupEntity(Loc.GetString(popup), patient, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            targetSelection = fallbackSelection;
        }

        var targetPart = targetSelection.Part;
        var canInstantWound = woundTarget != null && CanApplyInstantWoundTreatment(args.User, treater);
        var canInstantKit = CanApplyInstantKit(args.User, used);
        var canApplyInstantTreatment = canInstantWound || canInstantKit;
        if (canApplyInstantTreatment &&
            !targetSelection.UsedSearch &&
            TryApplyInstantTreatment(args.User, patient, targetPart, used, treater))
        {
            args.Handled = true;
            return;
        }

        var deferInstantTreatment = canApplyInstantTreatment && targetSelection.UsedSearch;
        var fumblingDelay = TimeSpan.Zero;
        var delay = ResolveSearchDelay(targetSelection);
        if (!deferInstantTreatment)
            delay += ResolveBandageDelay(args.User, patient, targetPart, used, treater, out fumblingDelay);

        if (fumblingDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("cm-wounds-start-fumbling", ("name", used)), patient, args.User);

        var partHealthCap = ResolveTreaterDamagePartHealthCap(targetPart, treater);
        var doAfterEv = new CMUBandageDoAfterEvent(GetNetEntity(targetPart), deferInstantTreatment);

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, doAfterEv,
            args.User, target: patient, used: used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
            MovementThreshold = 0.5f,
            TargetEffect = "RMCEffectHealBusy",
        };
        args.Handled = true;
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var pending = EnsureComp<CMUBandagePendingComponent>(args.User);
        pending.Patient = patient;
        pending.Treater = used;
        pending.PartHealthCapPart = targetPart;
        pending.PartHealthCap = partHealthCap;

        _audio.PlayPvs(treater.TreatBeginSound, args.User);
        if (args.User != patient && treater.TargetStartPopup is { } startPopup)
            _popup.PopupEntity(Loc.GetString(startPopup, ("user", args.User)), patient, patient, PopupType.Medium);
    }

    private TreatmentTarget? PickBandageTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!treater.CMUTreatsWounds)
            return null;

        return PickTreatmentTarget(medic, patient, part => PartHasTreatableWound(part, treater));
    }

    private bool PartHasTreatableWound(EntityUid part, WoundTreaterComponent treater)
    {
        if (!treater.CMUTreatsWounds)
            return false;

        if (!TryComp<BodyPartWoundComponent>(part, out var pw))
            return false;

        foreach (var entry in _woundLedger.GetEntries(pw))
        {
            var wound = entry.Wound;
            if (!wound.Treated && wound.Type == treater.Wound)
                return true;
        }

        return false;
    }

    private bool TryHandleArmedSurgeryTool(EntityUid medic, EntityUid patient, EntityUid used, out bool handled)
    {
        handled = false;

        if (!TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed))
            return false;

        if (armed.RequiredToolCategory is not { } category
            || !_surgery.ToolMatchesCategory(used, category))
        {
            return false;
        }

        return _surgery.TryHandleArmedToolUse(patient, armed, medic, used, patient, out handled, out _);
    }

    private TreatmentTarget? PickDamageOnlyTarget(
        EntityUid medic,
        EntityUid patient,
        WoundTreaterComponent treater,
        EntityUid? cappedPart = null,
        FixedPoint2? cappedPartHealthCap = null)
    {
        if (!HasTreatableDamage(medic, patient, treater))
            return null;

        return PickTreatmentTarget(medic,
            patient,
            part => PartHasDamageHealingRoom(patient,
                part,
                treater,
                part == cappedPart ? cappedPartHealthCap : null));
    }

    private TreatmentTarget? PickBleedingTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!treater.CMUStopsArterialBleeding)
            return null;

        return PickTreatmentTarget(medic, patient, part => PartHasStoppableBleeding(patient, part, treater));
    }

    private TreatmentTarget? PickTreatmentTarget(EntityUid medic, EntityUid patient, Func<EntityUid, bool> predicate)
    {
        var targetedHealing = IsTargetedHealingEnabled(medic);
        var aimed = targetedHealing
            ? _zoneTargeting.TryGetSelection(medic)
            : _zoneTargeting.TryGetFreshSelection(medic);

        EntityUid? aimedPart = null;
        if (aimed is { } zone && PartForZone(patient, zone) is { } targetPart)
        {
            aimedPart = targetPart;
            if (predicate(targetPart))
                return new TreatmentTarget(targetPart, false);

            if (targetedHealing)
                return null;
        }
        else if (targetedHealing)
        {
            return null;
        }

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is not { } fallback)
                continue;
            if (aimedPart is { } aimedUid && fallback == aimedUid)
                continue;
            if (!predicate(fallback))
                continue;

            return new TreatmentTarget(fallback, true);
        }

        return null;
    }

    private bool IsTargetedHealingEnabled(EntityUid medic)
    {
        return TryComp<ActorComponent>(medic, out var actor) &&
            _netConfig.GetClientCVar(actor.PlayerSession.Channel, CMUMedicalCCVars.TargetedHealingEnabled);
    }

    private static TimeSpan ResolveSearchDelay(TreatmentTarget target)
    {
        return target.UsedSearch ? SearchTreatmentDelay : TimeSpan.Zero;
    }

    private FixedPoint2? ResolveTreaterDamagePartHealthCap(EntityUid part, WoundTreaterComponent treater)
    {
        if (!treater.CMUHealingCurrentPartDamageHalfCap)
            return null;

        if (!TryComp<BodyPartHealthComponent>(part, out var health))
            return null;

        var missing = health.Max - health.Current;
        if (missing <= FixedPoint2.Zero)
            return health.Current;

        var cap = health.Current + FixedPoint2.New(missing.Float() * 0.5f);
        return FixedPoint2.Min(health.Max, cap);
    }

    private FixedPoint2? SetPendingTreaterDamagePartHealthCap(
        Entity<CMUBandagePendingComponent> pending,
        EntityUid part,
        WoundTreaterComponent treater)
    {
        var cap = ResolveTreaterDamagePartHealthCap(part, treater);
        pending.Comp.PartHealthCapPart = part;
        pending.Comp.PartHealthCap = cap;
        return cap;
    }

    private bool PartHasDamageHealingRoom(
        EntityUid patient,
        EntityUid part,
        WoundTreaterComponent treater,
        FixedPoint2? partHealthCap = null)
    {
        if (!IsAttachedPart(patient, part))
            return false;

        if (HasComp<CMURoboticLimbComponent>(part))
            return false;

        if (!TryComp<BodyPartHealthComponent>(part, out var health))
            return false;

        var cap = ResolvePartDamageHealingCap(part, treater, partHealthCap, health);

        return health.Current < cap;
    }

    private FixedPoint2 ResolvePartDamageHealingCap(
        EntityUid part,
        WoundTreaterComponent treater,
        FixedPoint2? partHealthCap,
        BodyPartHealthComponent health)
    {
        var cap = health.Max;
        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
        {
            var woundCapFraction = treater.CMUHealingUsesLargestWoundCap
                ? SharedCMUWoundsSystem.ComputeLargestWoundFieldTreatmentCap(wounds)
                : SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds);

            cap = health.Max * (FixedPoint2) woundCapFraction;
        }

        if (partHealthCap is { } healthCap)
            cap = FixedPoint2.Min(cap, healthCap);

        return cap;
    }

    private bool WillReachDamageHealingCap(
        EntityUid part,
        WoundTreaterComponent treater,
        FixedPoint2? partHealthCap,
        FixedPoint2 treaterDamage)
    {
        if (treaterDamage >= FixedPoint2.Zero)
            return false;

        if (!TryComp<BodyPartHealthComponent>(part, out var health))
            return false;

        var cap = ResolvePartDamageHealingCap(part, treater, partHealthCap, health);
        return health.Current + -treaterDamage >= cap;
    }

    private FixedPoint2? CurrentPartHealth(EntityUid part)
    {
        return TryComp<BodyPartHealthComponent>(part, out var health)
            ? health.Current
            : null;
    }

    private bool PartHasStoppableBleeding(EntityUid patient, EntityUid part, WoundTreaterComponent treater)
    {
        if (!IsAttachedPart(patient, part))
            return false;

        if (!TryComp<BodyPartWoundComponent>(part, out var wounds) ||
            wounds.ExternalBleeding == ExternalBleedTier.None)
        {
            return false;
        }

        return wounds.ExternalBleeding != ExternalBleedTier.Arterial || treater.CMUStopsArterialBleeding;
    }

    private bool TryStopBleedingWithTreater(EntityUid patient, EntityUid part, WoundTreaterComponent treater)
    {
        if (!PartHasStoppableBleeding(patient, part, treater))
            return false;

        return _wounds.StopSurfaceBleedingOnPart(part);
    }

    private bool IsAttachedPart(EntityUid patient, EntityUid part)
    {
        return TryComp<BodyPartComponent>(part, out var partComp) &&
               partComp.Body == patient;
    }

    private bool HasTreatableDamage(EntityUid user, EntityUid patient, WoundTreaterComponent treater)
    {
        if (IsSynthPatient(patient))
            return false;

        if (ResolveTreaterDamage(user, treater) >= FixedPoint2.Zero)
            return false;

        if (!TryComp<DamageableComponent>(patient, out var damageable))
            return false;

        if (!_prototypes.TryIndex<DamageGroupPrototype>(treater.Group, out var group))
            return false;

        foreach (var type in group.DamageTypes)
        {
            if (damageable.Damage.DamageDict.TryGetValue(type, out var amount) && amount > FixedPoint2.Zero)
                return true;
        }

        return false;
    }

    private EntityUid? PartForZone(EntityUid patient, TargetBodyZone zone)
    {
        var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);

        foreach (var (childId, childComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (childComp.PartType != type)
                continue;
            if (symmetry != BodyPartSymmetry.None && childComp.Symmetry != symmetry)
                continue;
            return childId;
        }
        return null;
    }

    private static readonly TargetBodyZone[] BandageFallbackOrder =
    {
        TargetBodyZone.Head,
        TargetBodyZone.RightArm,
        TargetBodyZone.RightHand,
        TargetBodyZone.Chest,
        TargetBodyZone.GroinPelvis,
        TargetBodyZone.LeftArm,
        TargetBodyZone.LeftHand,
        TargetBodyZone.RightLeg,
        TargetBodyZone.RightFoot,
        TargetBodyZone.LeftLeg,
        TargetBodyZone.LeftFoot,
    };

    public TimeSpan ResolveBandageDelay(EntityUid part)
    {
        return ResolveBaseBandageDelay(part);
    }

    private TimeSpan ResolveBandageDelay(
        EntityUid user,
        EntityUid patient,
        EntityUid part,
        EntityUid treaterUid,
        WoundTreaterComponent treater,
        out TimeSpan fumblingDelay)
    {
        fumblingDelay = _skills.GetDelay(user, treaterUid);
        var delay = ResolveBaseBandageDelay(part);

        var skillMultiplier = _skills.GetSkillDelayMultiplier(user, treater.DoAfterSkill, treater.DoAfterSkillMultipliers);
        if (user == patient)
            skillMultiplier *= treater.SelfTargetDoAfterMultiplier;

        return delay * skillMultiplier + fumblingDelay;
    }

    private TimeSpan ResolveBaseBandageDelay(EntityUid part)
    {
        if (!TryComp<BodyPartWoundComponent>(part, out var pw))
            return TreatDelay;

        WoundSize? worst = null;
        var worstRank = -1;
        var worstDamage = 0f;
        foreach (var entry in _woundLedger.GetEntries(pw))
        {
            if (entry.Wound.Treated)
                continue;

            var sz = entry.Size;
            var damage = entry.Wound.Damage.Float();
            var rank = WoundSizeProfile.SeverityRank(sz, damage);
            if (worst is not null &&
                (rank < worstRank || rank == worstRank && damage <= worstDamage))
            {
                continue;
            }

            worst = sz;
            worstRank = rank;
            worstDamage = damage;
        }

        return worst is { } w
            ? TreatDelay + WoundSizeProfile.BandageDelay(w, worstDamage)
            : TreatDelay;
    }

    private bool CanApplyInstantWoundTreatment(EntityUid user, WoundTreaterComponent treater)
    {
        return treater.InstantWoundTreatment ||
               (treater.InstantWoundTreatmentSkills.Count > 0 &&
                _skills.HasAllSkills(user, treater.InstantWoundTreatmentSkills));
    }

    private bool CanApplyInstantKit(EntityUid user, EntityUid treaterUid)
    {
        if (!TryComp<StackComponent>(treaterUid, out var stack))
            return false;

        return (stack.StackTypeId == BurnKitStack || stack.StackTypeId == TraumaKitStack) &&
               _skills.HasSkill(user, MedicalSkill, CorpsmanMedicalSkillLevel);
    }

    private void OnBandageDoAfter(Entity<CMUBandagePendingComponent> ent, ref CMUBandageDoAfterEvent args)
    {
        var medic = ent.Owner;
        var patient = ent.Comp.Patient;
        var treaterUid = ent.Comp.Treater;

        if (args.Cancelled)
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        if (IsSynthPatient(patient))
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        var part = GetEntity(args.Part);
        if (!TryComp<WoundTreaterComponent>(treaterUid, out var treater))
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        if (args.ApplyInstantTreatment)
        {
            TryApplyInstantTreatment(medic, patient, part, treaterUid, treater);
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        var partHealthCap = ent.Comp.PartHealthCapPart == part
            ? ent.Comp.PartHealthCap
            : null;

        if (ent.Comp.PartHealthCapPart != part ||
            treater.CMUHealingCurrentPartDamageHalfCap && partHealthCap is null)
        {
            partHealthCap = SetPendingTreaterDamagePartHealthCap(ent, part, treater);
        }
        var treated = false;
        var damageOnly = false;
        if (treater.CMUTreatsWounds && IsAttachedPart(patient, part))
        {
            var maxWounds = Math.Max(1, treater.WoundsTreatedPerUse);
            treated = maxWounds > 1
                ? TryTreatWoundsWithTreater(part, treater, maxWounds, out _)
                : TryTreatOneWoundWithTreater(part, treater, out _);
        }

        if (!treated)
        {
            if (!PartHasStoppableBleeding(patient, part, treater) &&
                PickBleedingTarget(medic, patient, treater) is { } bleedingTarget)
            {
                var nextPart = bleedingTarget.Part;
                if (nextPart != part)
                {
                    part = nextPart;
                    partHealthCap = SetPendingTreaterDamagePartHealthCap(ent, part, treater);
                }
            }

            treated = TryStopBleedingWithTreater(patient, part, treater);
        }

        if (!treated)
        {
            if (!HasTreatableDamage(medic, patient, treater))
            {
                RemComp<CMUBandagePendingComponent>(ent);
                return;
            }

            if (!PartHasDamageHealingRoom(patient, part, treater, partHealthCap))
            {
                if (PickDamageOnlyTarget(medic, patient, treater, part, partHealthCap) is not { } damageTarget)
                {
                    RemComp<CMUBandagePendingComponent>(ent);
                    return;
                }

                part = damageTarget.Part;
                partHealthCap = SetPendingTreaterDamagePartHealthCap(ent, part, treater);
            }

            treated = true;
            damageOnly = true;
        }

        var treaterDamage = ResolveTreaterDamage(medic, treater);
        var repeatPartHealthCap = WillReachDamageHealingCap(part, treater, partHealthCap, treaterDamage)
            ? CurrentPartHealth(part)
            : partHealthCap;

        var appliedTreaterDamage = _wounds.TryApplyTreaterDamage(patient,
            medic,
            treaterUid,
            treater.Group,
            treaterDamage,
            part,
            partHealthCap,
            treater.CMUHealingUsesLargestWoundCap);
        if (damageOnly && !appliedTreaterDamage)
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        _audio.PlayPvs(treater.TreatEndSound, medic);

        var hasTreater = ConsumeTreater(treaterUid, treater);
        var repeatPart = GetRepeatPart(medic, patient, part, treater, repeatPartHealthCap);
        args.Repeat = hasTreater && repeatPart != null;
        if (args.Repeat && repeatPart is { } nextTarget)
        {
            var nextPart = nextTarget.Part;
            if (nextPart != part)
                SetPendingTreaterDamagePartHealthCap(ent, nextPart, treater);

            args.Part = GetNetEntity(nextPart);
            args.Args.Delay = ResolveBandageDelay(medic, patient, nextPart, treaterUid, treater, out var fumblingDelay) +
                ResolveSearchDelay(nextTarget);

            if (fumblingDelay > TimeSpan.Zero)
                _popup.PopupClient(Loc.GetString("cm-wounds-start-fumbling", ("name", treaterUid)), patient, medic);

            _audio.PlayPvs(treater.TreatBeginSound, medic);
            if (medic != patient && treater.TargetStartPopup is { } startPopup)
                _popup.PopupEntity(Loc.GetString(startPopup, ("user", medic)), patient, patient, PopupType.Medium);
        }
        else
        {
            RemComp<CMUBandagePendingComponent>(ent);
        }

        var userPopup = args.Repeat ? treater.UserPopup : treater.UserFinishPopup ?? treater.UserPopup;
        var targetPopup = args.Repeat ? treater.TargetPopup : treater.TargetFinishPopup ?? treater.TargetPopup;

        if (userPopup != null)
            _popup.PopupEntity(Loc.GetString(userPopup, ("target", patient)), patient, medic);

        if (medic != patient && targetPopup != null)
            _popup.PopupEntity(Loc.GetString(targetPopup, ("user", medic)), patient, patient);
    }

    private TreatmentTarget? GetRepeatPart(
        EntityUid medic,
        EntityUid patient,
        EntityUid currentPart,
        WoundTreaterComponent treater,
        FixedPoint2? partHealthCap)
    {
        if (treater.CMUTreatsWounds &&
            IsAttachedPart(patient, currentPart) &&
            PartHasTreatableWound(currentPart, treater))
        {
            return new TreatmentTarget(currentPart, false);
        }

        if (PickBandageTarget(medic, patient, treater) is { } woundTarget)
            return woundTarget;

        if (PartHasStoppableBleeding(patient, currentPart, treater))
            return new TreatmentTarget(currentPart, false);

        if (PickBleedingTarget(medic, patient, treater) is { } bleedingTarget)
            return bleedingTarget;

        if (!HasTreatableDamage(medic, patient, treater))
            return null;

        if (PartHasDamageHealingRoom(patient, currentPart, treater, partHealthCap))
        {
            return new TreatmentTarget(currentPart, false);
        }

        return PickDamageOnlyTarget(medic, patient, treater, currentPart, partHealthCap);
    }

    private bool TryTreatOneWoundWithTreater(EntityUid part, WoundTreaterComponent treater, out bool completed)
    {
        return _wounds.TryTreatWound(
            part,
            treater.Wound,
            out completed,
            quality: WoundTreatmentQuality.Adequate,
            stopArterialBleeding: treater.CMUStopsArterialBleeding);
    }

    private bool TryTreatWoundsWithTreater(EntityUid part, WoundTreaterComponent treater, int maxWounds, out int treated)
    {
        return _wounds.TryTreatWounds(
            part,
            treater.Wound,
            maxWounds,
            out treated,
            quality: WoundTreatmentQuality.Adequate,
            stopArterialBleeding: treater.CMUStopsArterialBleeding);
    }

    private bool TryApplyInstantTreatment(
        EntityUid medic,
        EntityUid patient,
        EntityUid firstPart,
        EntityUid treaterUid,
        WoundTreaterComponent treater)
    {
        var maxWounds = Math.Max(1, treater.WoundsTreatedPerUse);
        var treatedWounds = 0;
        var part = firstPart;
        var partHealthCap = ResolveTreaterDamagePartHealthCap(part, treater);
        if (treater.CMUTreatsWounds)
            TryTreatWoundsWithTreater(part, treater, maxWounds, out treatedWounds);

        var treated = treatedWounds > 0;
        var damageOnly = false;

        if (!treated)
        {
            if (!PartHasStoppableBleeding(patient, part, treater) &&
                PickBleedingTarget(medic, patient, treater) is { } bleedingTarget)
            {
                var nextPart = bleedingTarget.Part;
                if (nextPart != part)
                {
                    part = nextPart;
                    partHealthCap = ResolveTreaterDamagePartHealthCap(part, treater);
                }
            }

            treated = TryStopBleedingWithTreater(patient, part, treater);
        }

        if (!treated)
        {
            if (!HasTreatableDamage(medic, patient, treater))
                return false;

            if (!PartHasDamageHealingRoom(patient, part, treater, partHealthCap))
            {
                if (PickDamageOnlyTarget(medic, patient, treater, part, partHealthCap) is not { } damageTarget)
                    return false;

                part = damageTarget.Part;
                partHealthCap = ResolveTreaterDamagePartHealthCap(part, treater);
            }

            treated = true;
            damageOnly = true;
        }

        var treaterDamage = ResolveTreaterDamage(medic, treater);
        var appliedTreaterDamage = _wounds.TryApplyTreaterDamage(patient,
            medic,
            treaterUid,
            treater.Group,
            treaterDamage,
            part,
            partHealthCap,
            treater.CMUHealingUsesLargestWoundCap);
        if (damageOnly && !appliedTreaterDamage)
            return false;

        _audio.PlayPvs(treater.TreatEndSound, medic);
        ConsumeTreater(treaterUid, treater);

        var userPopup = treater.UserFinishPopup ?? treater.UserPopup;
        var targetPopup = treater.TargetFinishPopup ?? treater.TargetPopup;

        if (userPopup != null)
            _popup.PopupEntity(Loc.GetString(userPopup, ("target", patient)), patient, medic);

        if (medic != patient && targetPopup != null)
            _popup.PopupEntity(Loc.GetString(targetPopup, ("user", medic)), patient, patient);

        return true;
    }

    private FixedPoint2 ResolveTreaterDamage(EntityUid user, WoundTreaterComponent treater)
    {
        var hasSkills = _skills.HasAllSkills(user, treater.Skills);
        if (!hasSkills && !treater.CanUseUnskilled)
            return FixedPoint2.Zero;

        return hasSkills
            ? treater.Damage ?? FixedPoint2.Zero
            : treater.UnskilledDamage ?? FixedPoint2.Zero;
    }

    private bool ConsumeTreater(EntityUid treaterUid, WoundTreaterComponent treater)
    {
        if (!treater.Consumable)
            return true;

        if (!_net.IsServer)
            return true;

        if (TryComp<StackComponent>(treaterUid, out var stack))
        {
            if (!_stacks.Use(treaterUid, 1, stack))
                return false;

            return stack.Unlimited || stack.Count > 0;
        }

        QueueDel(treaterUid);
        return false;
    }

    private bool IsSynthPatient(EntityUid patient)
    {
        return HasComp<SynthComponent>(patient);
    }
}
