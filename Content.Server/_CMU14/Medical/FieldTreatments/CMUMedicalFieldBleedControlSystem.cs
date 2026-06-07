using Content.Server._CMU14.Medical.Wounds;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.FieldTreatments;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Server._CMU14.Medical.FieldTreatments;

public sealed partial class CMUMedicalFieldBleedControlSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stacks = default!;
    [Dependency] private CMUWoundsSystem _wounds = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMedicalMixingBaseComponent, AfterInteractEvent>(OnBaseAfterInteract);
        SubscribeLocalEvent<CMUMedicalMixingBaseComponent, CMUFieldBleedControlDoAfterEvent>(OnBleedControlDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.WoundsEnabled);
    }

    private void OnBaseAfterInteract(Entity<CMUMedicalMixingBaseComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } patient)
            return;
        if (!ent.Comp.ControlsBleeding || !IsLayerEnabled())
            return;
        if (!HasComp<CMUHumanMedicalComponent>(patient) || HasComp<SynthComponent>(patient))
            return;

        if (!TryPickBleedingPart(args.User, patient, ent.Comp.StopsArterialBleeding, out var part, out var blockedByArterial))
        {
            var loc = blockedByArterial
                ? "cmu-field-treatment-arterial-requires-trauma"
                : "cmu-field-treatment-no-bleeding";
            _popup.PopupEntity(Loc.GetString(loc), patient, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        var delay = ResolveBleedControlDelay(args.User, ent.Comp);
        if (delay <= TimeSpan.Zero)
        {
            if (TryApplyBleedControl(args.User, patient, ent, part))
                args.Handled = true;
            return;
        }

        var ev = new CMUFieldBleedControlDoAfterEvent(GetNetEntity(part));
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, ev,
            ent.Owner, target: patient, used: ent.Owner)
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

        _popup.PopupEntity(Loc.GetString("cmu-field-treatment-bleed-control-start"), patient, args.User);
        args.Handled = true;
    }

    private TimeSpan ResolveBleedControlDelay(EntityUid user, CMUMedicalMixingBaseComponent comp)
    {
        if (comp.BleedControlDelay <= TimeSpan.Zero)
            return TimeSpan.Zero;

        if (comp.InstantBleedControlSkills.Count > 0 &&
            _skills.HasAllSkills(user, comp.InstantBleedControlSkills))
        {
            return TimeSpan.Zero;
        }

        return comp.BleedControlDelay;
    }

    private void OnBleedControlDoAfter(Entity<CMUMedicalMixingBaseComponent> ent, ref CMUFieldBleedControlDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } patient)
            return;
        if (!IsLayerEnabled())
            return;
        if (!HasComp<CMUHumanMedicalComponent>(patient) || HasComp<SynthComponent>(patient))
            return;

        var part = GetEntity(args.Part);
        TryApplyBleedControl(args.User, patient, ent, part);
    }

    private bool TryApplyBleedControl(
        EntityUid user,
        EntityUid patient,
        Entity<CMUMedicalMixingBaseComponent> item,
        EntityUid part)
    {
        if (!TryComp<BodyPartWoundComponent>(part, out var wounds) ||
            wounds.ExternalBleeding == ExternalBleedTier.None)
        {
            _popup.PopupEntity(Loc.GetString("cmu-field-treatment-no-bleeding"), patient, user, PopupType.SmallCaution);
            return false;
        }

        if (wounds.ExternalBleeding == ExternalBleedTier.Arterial && !item.Comp.StopsArterialBleeding)
        {
            _popup.PopupEntity(Loc.GetString("cmu-field-treatment-arterial-requires-trauma"), patient, user, PopupType.SmallCaution);
            return false;
        }

        if (_net.IsServer && _stacks.GetCount(item.Owner) < 1)
            return false;

        if (!_wounds.StopSurfaceBleedingOnPart(part, wounds))
            return false;

        if (_net.IsServer && !_stacks.Use(item.Owner, 1))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-field-treatment-bleed-control-finish"), patient, user);
        return true;
    }

    private bool TryPickBleedingPart(
        EntityUid user,
        EntityUid patient,
        bool stopsArterial,
        out EntityUid part,
        out bool blockedByArterial)
    {
        part = default;
        blockedByArterial = false;

        if (_zoneTargeting.TryGetFreshSelection(user) is { } zone &&
            PartForZone(patient, zone) is { } aimed)
        {
            if (CanUseOnPart(aimed, stopsArterial, out blockedByArterial))
            {
                part = aimed;
                return true;
            }

            if (blockedByArterial)
                return false;
        }

        foreach (var fallbackZone in BleedControlFallbackOrder)
        {
            if (PartForZone(patient, fallbackZone) is not { } candidate)
                continue;
            if (!CanUseOnPart(candidate, stopsArterial, out var arterialBlocked))
            {
                blockedByArterial |= arterialBlocked;
                continue;
            }

            part = candidate;
            return true;
        }

        return false;
    }

    private bool CanUseOnPart(EntityUid part, bool stopsArterial, out bool blockedByArterial)
    {
        blockedByArterial = false;
        if (!TryComp<BodyPartWoundComponent>(part, out var wounds) ||
            wounds.ExternalBleeding == ExternalBleedTier.None)
        {
            return false;
        }

        if (wounds.ExternalBleeding != ExternalBleedTier.Arterial || stopsArterial)
            return true;

        blockedByArterial = true;
        return false;
    }

    private EntityUid? PartForZone(EntityUid patient, TargetBodyZone zone)
    {
        var (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(zone);

        foreach (var (partUid, part) in _body.GetBodyChildren(patient))
        {
            if (part.PartType != type)
                continue;
            if (symmetry != BodyPartSymmetry.None && part.Symmetry != symmetry)
                continue;
            return partUid;
        }

        return null;
    }

    private static readonly TargetBodyZone[] BleedControlFallbackOrder =
    {
        TargetBodyZone.Head,
        TargetBodyZone.RightArm,
        TargetBodyZone.Chest,
        TargetBodyZone.LeftArm,
        TargetBodyZone.RightLeg,
        TargetBodyZone.LeftLeg,
    };
}
