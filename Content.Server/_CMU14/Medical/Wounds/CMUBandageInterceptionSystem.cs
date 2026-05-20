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

        if (ShouldYieldToArmedSurgeryTool(args.User, patient, used))
            return;

        var woundTarget = PickBandageTarget(args.User, patient, treater.Wound);
        if (woundTarget is not { } targetPart)
        {
            if (PickDamageOnlyTarget(args.User, patient, treater) is not { } damageTarget)
            {
                _popup.PopupEntity(Loc.GetString("cmu-medical-bandage-no-wounds"), patient, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            targetPart = damageTarget;
        }

        if (woundTarget != null
            && treater.InstantWoundTreatment
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

    private EntityUid? PickBandageTarget(EntityUid medic, EntityUid patient, WoundType woundType)
    {
        var aimed = _zoneTargeting.TryGetFreshSelection(medic);

        if (aimed is { } zone && PartForZone(patient, zone) is { } aimedPart && PartHasUntreatedWound(aimedPart, woundType))
            return aimedPart;

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback && PartHasUntreatedWound(fallback, woundType))
                return fallback;
        }
        return null;
    }

    private bool PartHasUntreatedWound(EntityUid part, WoundType woundType)
    {
        if (HasComp<CMUEscharComponent>(part))
            return false;
        if (!TryComp<BodyPartWoundComponent>(part, out var pw))
            return false;
        foreach (var wound in pw.Wounds)
        {
            if (!wound.Treated && wound.Type == woundType)
                return true;
        }

        return false;
    }

    private bool ShouldYieldToArmedSurgeryTool(EntityUid medic, EntityUid patient, EntityUid used)
    {
        if (!TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed))
            return false;

        return armed.Surgeon == medic
            && armed.RequiredToolCategory is { } category
            && _surgery.ToolMatchesCategory(used, category);
    }

    private EntityUid? PickDamageOnlyTarget(EntityUid medic, EntityUid patient, WoundTreaterComponent treater)
    {
        if (!HasTreatableDamage(medic, patient, treater))
            return null;

        var aimed = _zoneTargeting.TryGetFreshSelection(medic);
        if (aimed is { } zone && PartForZone(patient, zone) is { } aimedPart)
            return aimedPart;

        EntityUid? fallback = null;
        foreach (var (partUid, _) in _body.GetBodyChildren(patient))
        {
            fallback ??= partUid;
            if (TryComp<BodyPartHealthComponent>(partUid, out var health) && health.Current < health.Max)
                return partUid;
        }

        return fallback;
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
        if (HasComp<BodyPartComponent>(part))
            treated = _wounds.TryTreatWound(part, treater.Wound, out _);

        if (!treated)
        {
            if (!HasTreatableDamage(medic, patient, treater))
            {
                RemComp<CMUBandagePendingComponent>(ent);
                return;
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
        if (PartHasUntreatedWound(currentPart, treater.Wound))
            return currentPart;

        return PickBandageTarget(medic, patient, treater.Wound)
            ?? PickDamageOnlyTarget(medic, patient, treater);
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
            if (!_wounds.TryTreatWounds(part, treater.Wound, maxWounds - treatedWounds, out var treatedOnPart))
                break;

            treatedWounds += treatedOnPart;
            if (treatedWounds >= maxWounds)
                break;

            if (PickBandageTarget(medic, patient, treater.Wound) is not { } nextPart)
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

