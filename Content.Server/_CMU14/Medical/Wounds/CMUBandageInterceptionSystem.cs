using System;
using Content.Server._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
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
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Wounds;

public sealed partial class CMUBandageInterceptionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _surgery = default!;
    [Dependency] private CMUWoundsSystem _wounds = default!;

    private static readonly TimeSpan TreatDelay = TimeSpan.FromSeconds(1);

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

        if (IsSynthPatient(patient))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-bandage-synth-requires-repair-tools"), patient, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var woundTarget = PickBandageTarget(args.User, patient, treater);
        if (woundTarget is not { } targetPart)
        {
            var fallbackTarget = PickBleedingTarget(args.User, patient, treater) ??
                                 PickDamageOnlyTarget(args.User, patient, treater);
            if (fallbackTarget is not { } fallbackPart)
            {
                if (TryHandleArmedSurgeryTool(args.User, patient, used, out var surgeryHandled))
                {
                    args.Handled = surgeryHandled;
                    return;
                }

                _popup.PopupEntity(Loc.GetString("cmu-medical-bandage-no-wounds"), patient, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            targetPart = fallbackPart;
        }

        if (woundTarget != null
            && CanApplyInstantWoundTreatment(args.User, treater)
            && TryApplyInstantWoundTreatment(args.User, patient, targetPart, used, treater))
        {
            args.Handled = true;
            return;
        }

        var delay = ResolveBandageDelay(args.User, patient, targetPart, used, treater, out var fumblingDelay);
        if (fumblingDelay > TimeSpan.Zero)
            _popup.PopupClient(Loc.GetString("cm-wounds-start-fumbling", ("name", used)), patient, args.User);

        var doAfterEv = new CMUBandageDoAfterEvent(GetNetEntity(targetPart));

        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, doAfterEv,
            args.User, target: patient, used: used)
        {
            BreakOnMove = true,
            BreakOnHandChange = true,
            NeedHand = true,
            BlockDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTool | DuplicateConditions.SameTarget,
            MovementThreshold = 0.5f,
            TargetEffect = "RMCEffectHealBusy",
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var pending = EnsureComp<CMUBandagePendingComponent>(args.User);
        pending.Patient = patient;
        pending.Treater = used;
        Dirty(args.User, pending);

        _audio.PlayPvs(treater.TreatBeginSound, args.User);
        if (args.User != patient && treater.TargetStartPopup is { } startPopup)
            _popup.PopupEntity(Loc.GetString(startPopup, ("user", args.User)), patient, patient, PopupType.Medium);

        args.Handled = true;
    }

    private EntityUid? PickBandageTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!treater.CMUTreatsWounds)
            return null;

        var aimed = _zoneTargeting.TryGetFreshSelection(medic);

        if (aimed is { } zone &&
            PartForZone(patient, zone) is { } aimedPart &&
            PartHasTreatableWound(aimedPart, treater, exactMechanism: true))
        {
            return aimedPart;
        }

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback && PartHasTreatableWound(fallback, treater, exactMechanism: true))
                return fallback;
        }

        if (treater.CMUMechanisms == WoundMechanismFlags.None)
            return null;

        if (aimed is { } fallbackZoneAimed &&
            PartForZone(patient, fallbackZoneAimed) is { } fallbackAimedPart &&
            PartHasTreatableWound(fallbackAimedPart, treater, exactMechanism: false))
        {
            return fallbackAimedPart;
        }

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback && PartHasTreatableWound(fallback, treater, exactMechanism: false))
                return fallback;
        }

        return null;
    }

    private bool PartHasTreatableWound(EntityUid part, WoundTreaterComponent treater, bool exactMechanism)
    {
        if (!treater.CMUTreatsWounds)
            return false;

        if (HasComp<CMUEscharComponent>(part))
            return false;
        if (!TryComp<BodyPartWoundComponent>(part, out var pw))
            return false;

        for (var i = 0; i < pw.Wounds.Count; i++)
        {
            var wound = pw.Wounds[i];

            if (exactMechanism && !MatchesMechanism(pw, i, treater.CMUMechanisms))
                continue;

            if (wound.Treated)
            {
                if (treater.CMUTreatmentQuality == WoundTreatmentQuality.Optimal &&
                    CleanupCanBeTreated(GetCleanup(pw, i), treater.CMUCleanupClears))
                {
                    return true;
                }

                continue;
            }

            if (wound.Type == treater.Wound)
                return true;
        }

        return false;
    }

    private static WoundCleanupFlags GetCleanup(BodyPartWoundComponent comp, int index)
    {
        return index >= 0 && index < comp.Cleanup.Count
            ? comp.Cleanup[index]
            : WoundCleanupFlags.None;
    }

    private static bool CleanupCanBeTreated(WoundCleanupFlags cleanup, WoundCleanupFlags cleanupClears)
    {
        if (cleanup == WoundCleanupFlags.None)
            return false;

        return cleanupClears == WoundCleanupFlags.None ||
               (cleanup & cleanupClears) != WoundCleanupFlags.None;
    }

    private static bool MatchesMechanism(
        BodyPartWoundComponent comp,
        int index,
        WoundMechanismFlags mechanismMask)
    {
        if (mechanismMask == WoundMechanismFlags.None)
            return true;

        var primary = index >= 0 && index < comp.Mechanisms.Count
            ? ToFlag(comp.Mechanisms[index])
            : WoundMechanismFlags.Generic;
        var secondary = index >= 0 && index < comp.SecondaryMechanisms.Count
            ? comp.SecondaryMechanisms[index]
            : WoundMechanismFlags.None;

        return ((primary | secondary) & mechanismMask) != WoundMechanismFlags.None;
    }

    private static WoundMechanismFlags ToFlag(WoundMechanism mechanism)
    {
        return mechanism switch
        {
            WoundMechanism.Bullet => WoundMechanismFlags.Bullet,
            WoundMechanism.Stab => WoundMechanismFlags.Stab,
            WoundMechanism.Slash => WoundMechanismFlags.Slash,
            WoundMechanism.Crush => WoundMechanismFlags.Crush,
            WoundMechanism.Burn => WoundMechanismFlags.Burn,
            WoundMechanism.Blast => WoundMechanismFlags.Blast,
            WoundMechanism.Fragment => WoundMechanismFlags.Fragment,
            WoundMechanism.Surgical => WoundMechanismFlags.Surgical,
            _ => WoundMechanismFlags.Generic,
        };
    }

    private bool TryHandleArmedSurgeryTool(EntityUid medic, EntityUid patient, EntityUid used, out bool handled)
    {
        handled = false;

        if (!TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed))
            return false;

        if (armed.Surgeon != medic
            || armed.RequiredToolCategory is not { } category
            || !_surgery.ToolMatchesCategory(used, category))
        {
            return false;
        }

        return _surgery.TryHandleArmedToolUse(patient, armed, medic, used, patient, out handled, out _);
    }

    private EntityUid? PickDamageOnlyTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!HasTreatableDamage(medic, patient, treater))
            return null;

        var aimed = _zoneTargeting.TryGetFreshSelection(medic);
        if (aimed is { } zone &&
            PartForZone(patient, zone) is { } aimedPart &&
            PartHasDamageHealingRoom(patient, aimedPart))
        {
            return aimedPart;
        }

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback &&
                PartHasDamageHealingRoom(patient, fallback))
            {
                return fallback;
            }
        }

        return null;
    }

    private EntityUid? PickBleedingTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!treater.CMUStopsArterialBleeding)
            return null;

        var aimed = _zoneTargeting.TryGetFreshSelection(medic);
        if (aimed is { } zone &&
            PartForZone(patient, zone) is { } aimedPart &&
            PartHasStoppableBleeding(patient, aimedPart, treater))
        {
            return aimedPart;
        }

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback &&
                PartHasStoppableBleeding(patient, fallback, treater))
            {
                return fallback;
            }
        }

        return null;
    }

    private bool PartHasDamageHealingRoom(EntityUid patient, EntityUid part)
    {
        if (!IsAttachedPart(patient, part))
            return false;

        if (!TryComp<BodyPartHealthComponent>(part, out var health))
            return false;

        var cap = health.Max;
        if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            cap = health.Max * SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds);

        return health.Current < cap;
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

        foreach (var (childId, childComp) in _body.GetBodyChildren(patient))
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
        TargetBodyZone.Chest,
        TargetBodyZone.LeftArm,
        TargetBodyZone.RightLeg,
        TargetBodyZone.LeftLeg,
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
        for (var i = 0; i < pw.Wounds.Count; i++)
        {
            if (pw.Wounds[i].Treated)
                continue;
            if (i >= pw.Sizes.Count)
                continue;
            var sz = pw.Sizes[i];
            if (worst is null || (byte)sz > (byte)worst.Value)
                worst = sz;
        }

        return worst is { } w ? WoundSizeProfile.BandageDelay(w) : TreatDelay;
    }

    private bool CanApplyInstantWoundTreatment(EntityUid user, WoundTreaterComponent treater)
    {
        return treater.InstantWoundTreatment ||
               (treater.InstantWoundTreatmentSkills.Count > 0 &&
                _skills.HasAllSkills(user, treater.InstantWoundTreatmentSkills));
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
                PickBleedingTarget(medic, patient, treater) is { } bleedingPart)
            {
                part = bleedingPart;
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

            if (!PartHasDamageHealingRoom(patient, part))
            {
                if (PickDamageOnlyTarget(medic, patient, treater) is not { } damagePart)
                {
                    RemComp<CMUBandagePendingComponent>(ent);
                    return;
                }

                part = damagePart;
            }

            treated = true;
            damageOnly = true;
        }

        var treaterDamage = ResolveTreaterDamage(medic, treater);
        var appliedTreaterDamage = _wounds.TryApplyTreaterDamage(patient, medic, treaterUid, treater.Group, treaterDamage, part);
        if (damageOnly && !appliedTreaterDamage)
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        _audio.PlayPvs(treater.TreatEndSound, medic);

        var hasTreater = ConsumeTreater(treaterUid, treater);
        var repeatPart = GetRepeatPart(medic, patient, part, treater);
        args.Repeat = hasTreater && repeatPart != null;
        if (args.Repeat && repeatPart is { } nextPart)
        {
            args.Part = GetNetEntity(nextPart);
            args.Args.Delay = ResolveBandageDelay(medic, patient, nextPart, treaterUid, treater, out var fumblingDelay);

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

    private EntityUid? GetRepeatPart(EntityUid medic, EntityUid patient, EntityUid currentPart, WoundTreaterComponent treater)
    {
        if (treater.CMUTreatsWounds &&
            IsAttachedPart(patient, currentPart) &&
            (PartHasTreatableWound(currentPart, treater, exactMechanism: true) ||
             (treater.CMUMechanisms != WoundMechanismFlags.None && PartHasTreatableWound(currentPart, treater, exactMechanism: false))))
        {
            return currentPart;
        }

        return PickBandageTarget(medic, patient, treater)
            ?? PickBleedingTarget(medic, patient, treater)
            ?? PickDamageOnlyTarget(medic, patient, treater);
    }

    private bool TryTreatOneWoundWithTreater(EntityUid part, WoundTreaterComponent treater, out bool completed)
    {
        if (_wounds.TryTreatWound(
                part,
                treater.Wound,
                out completed,
                mechanismMask: treater.CMUMechanisms,
                quality: treater.CMUTreatmentQuality,
                cleanupClears: treater.CMUCleanupClears,
                stopArterialBleeding: treater.CMUStopsArterialBleeding))
        {
            return true;
        }

        if (treater.CMUMechanisms == WoundMechanismFlags.None ||
            treater.CMUTreatmentQuality != WoundTreatmentQuality.Optimal)
        {
            return false;
        }

        if (treater.CMUCleanupClears != WoundCleanupFlags.None &&
            _wounds.TryTreatWoundCleanup(
                part,
                out completed,
                mechanismMask: treater.CMUMechanisms,
                cleanupClears: treater.CMUCleanupClears,
                stopArterialBleeding: treater.CMUStopsArterialBleeding))
        {
            return true;
        }

        return _wounds.TryTreatWound(
            part,
            treater.Wound,
            out completed,
            quality: WoundTreatmentQuality.Adequate,
            stopArterialBleeding: treater.CMUStopsArterialBleeding);
    }

    private bool TryTreatWoundsWithTreater(EntityUid part, WoundTreaterComponent treater, int maxWounds, out int treated)
    {
        if (_wounds.TryTreatWounds(
                part,
                treater.Wound,
                maxWounds,
                out treated,
                mechanismMask: treater.CMUMechanisms,
                quality: treater.CMUTreatmentQuality,
                cleanupClears: treater.CMUCleanupClears,
                stopArterialBleeding: treater.CMUStopsArterialBleeding))
        {
            return true;
        }

        if (treater.CMUMechanisms == WoundMechanismFlags.None ||
            treater.CMUTreatmentQuality != WoundTreatmentQuality.Optimal)
        {
            return false;
        }

        if (treater.CMUCleanupClears != WoundCleanupFlags.None &&
            TryCleanupWoundsWithTreater(part, treater, maxWounds, out treated))
        {
            return true;
        }

        return _wounds.TryTreatWounds(
            part,
            treater.Wound,
            maxWounds,
            out treated,
            quality: WoundTreatmentQuality.Adequate,
            stopArterialBleeding: treater.CMUStopsArterialBleeding);
    }

    private bool TryCleanupWoundsWithTreater(EntityUid part, WoundTreaterComponent treater, int maxWounds, out int treated)
    {
        treated = 0;
        while (treated < maxWounds &&
               _wounds.TryTreatWoundCleanup(
                   part,
                   out _,
                   mechanismMask: treater.CMUMechanisms,
                   cleanupClears: treater.CMUCleanupClears,
                   stopArterialBleeding: treater.CMUStopsArterialBleeding))
        {
            treated++;
        }

        return treated > 0;
    }

    private bool TryApplyInstantWoundTreatment(
        EntityUid medic,
        EntityUid patient,
        EntityUid firstPart,
        EntityUid treaterUid,
        WoundTreaterComponent treater)
    {
        var maxWounds = Math.Max(1, treater.WoundsTreatedPerUse);
        var treatedWounds = 0;
        var part = firstPart;
        while (treatedWounds < maxWounds)
        {
            if (!TryTreatWoundsWithTreater(part, treater, maxWounds - treatedWounds, out var treatedOnPart))
                break;

            treatedWounds += treatedOnPart;
            if (treatedWounds >= maxWounds)
                break;

            if (PickBandageTarget(medic, patient, treater) is not { } nextPart)
                break;

            part = nextPart;
        }

        if (treatedWounds <= 0)
            return false;

        var treaterDamage = ResolveTreaterDamage(medic, treater);
        _wounds.TryApplyTreaterDamage(patient, medic, treaterUid, treater.Group, treaterDamage, firstPart);

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
