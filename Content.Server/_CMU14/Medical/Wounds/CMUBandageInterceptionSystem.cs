using System;
using System.Linq;
using Content.Server._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Medical.Wounds;

public sealed class CMUBandageInterceptionSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;
    [Dependency] private readonly CMUWoundsSystem _wounds = default!;

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

    public void HandleAfterInteract(Entity<CMUHumanMedicalComponent> medic, ref AfterInteractEvent args)
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

        if (PickBandageTarget(args.User, patient) is not { } targetPart)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-bandage-no-wounds"), patient, args.User, PopupType.SmallCaution);
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

    private EntityUid? PickBandageTarget(EntityUid medic, EntityUid patient)
    {
        var aimed = _zoneTargeting.TryGetFreshSelection(medic);

        if (aimed is { } zone && PartForZone(patient, zone) is { } aimedPart && PartHasUntreatedWound(aimedPart))
            return aimedPart;

        foreach (var fallbackZone in BandageFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is { } fallback && PartHasUntreatedWound(fallback))
                return fallback;
        }
        return null;
    }

    private bool PartHasUntreatedWound(EntityUid part)
    {
        if (HasComp<CMUEscharComponent>(part))
            return false;
        if (!TryComp<BodyPartWoundComponent>(part, out var pw))
            return false;
        return pw.Wounds.Any(w => !w.Treated);
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

        var part = GetEntity(args.Part);
        var treated = false;
        if (HasComp<BodyPartComponent>(part))
            treated = _wounds.TryTreatWound(part, out _);

        if (!treated || !TryComp<WoundTreaterComponent>(treaterUid, out var treater))
        {
            RemComp<CMUBandagePendingComponent>(ent);
            return;
        }

        _wounds.TryApplyTreaterDamage(patient, medic, treaterUid, treater.Group, ResolveTreaterDamage(medic, treater));
        _audio.PlayPvs(treater.TreatEndSound, medic);

        var hasTreater = ConsumeTreater(treaterUid, treater);
        var repeatPart = GetRepeatPart(medic, patient, part);
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

    private EntityUid? GetRepeatPart(EntityUid medic, EntityUid patient, EntityUid currentPart)
    {
        if (PartHasUntreatedWound(currentPart))
            return currentPart;

        return PickBandageTarget(medic, patient);
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
}

