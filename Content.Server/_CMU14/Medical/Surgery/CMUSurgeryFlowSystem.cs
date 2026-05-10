using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Markers;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Repairable;
using Content.Shared.Body.Part;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUSurgeryFlowSystem : SharedCMUSurgeryFlowSystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly CMUSurgeryDispatchSystem _dispatch = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly CMUBodyScannerSystem _bodyScanner = default!;

    private const float StepDoAfterSeconds = 2f;
    private const float PostOpCastWindowMinutes = 5f;
    private const float PostOpMalunionChance = 0.3f;
    private const string OpenIncisionScalpelStep = "CMSurgeryStepOpenIncisionScalpel";
    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";
    private static readonly float[] SurgeryStepDelayMultipliers = { 1.25f, 1f, 0.75f, 0.55f, 0.4f };

    private static readonly HashSet<string> ClosureStepIds = new()
    {
        "CMSurgeryStepCloseBones",
        "CMSurgeryStepMendRibcage",
        "CMSurgeryStepCloseIncision",
        "CMUSurgeryStepCloseIncision",
        "CMUSurgeryStepCloseReattach",
    };

    private static readonly SoundSpecifier WelderStepSound = new SoundCollectionSpecifier("Welder");

    private static readonly HashSet<string> SurfaceExemptStepIds = new()
    {
        // Pre-op and close-up access steps are allowed to be rougher; the
        // actual repair/extraction/transplant work is where the surface matters.
        "CMSurgeryStepOpenIncisionScalpel",
        "CMSurgeryStepClampBleeders",
        "CMSurgeryStepRetractSkin",
        "CMSurgeryStepSawBones",
        "CMSurgeryStepPriseOpenBones",
        "CMSurgeryStepCloseIncision",
        "CMUSurgeryStepCloseIncision",
        "CMSurgeryStepCloseBones",
        "CMSurgeryStepMendRibcage",
    };

    private static readonly Dictionary<string, SoundSpecifier> ToolCategorySounds = new()
    {
        ["scalpel"] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
        ["hemostat"] = new SoundCollectionSpecifier("RMCSurgeryHemostat"),
        ["retractor"] = new SoundCollectionSpecifier("RMCSurgeryRetractor"),
        ["cautery"] = new SoundCollectionSpecifier("RMCSurgeryCautery"),
        ["bone_saw"] = new SoundCollectionSpecifier("RMCSurgerySaw"),
        ["bone_setter"] = new SoundCollectionSpecifier("RMCSurgerySplint"),
        ["organ_clamp"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
        ["scalpel_or_burn_kit"] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
    };

    protected override bool StartStepDoAfter(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid tool, EntityUid targetPart)
    {
        var delay = ResolveStepDoAfterDelay(surgeon, patient);
        if (TryComp<CMUImprovisedSurgeryToolComponent>(tool, out var improvised))
            delay = TimeSpan.FromSeconds(delay.TotalSeconds * MathF.Max(1f, improvised.DelayMultiplier));

        var ev = new CMUSurgeryStepDoAfterEvent(
            armed.SurgeryId,
            armed.LeafSurgeryId,
            armed.StepIndex,
            armed.TargetPartType,
            armed.TargetSymmetry);
        var doAfter = new DoAfterArgs(EntityManager, surgeon, delay, ev, patient, targetPart, tool)
        {
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            NeedHand = true,
            CancelDuplicate = false,
        };
        if (!DoAfter.TryStartDoAfter(doAfter))
            return false;

        if (HasComp<BlowtorchComponent>(tool))
        {
            _audio.PlayPvs(WelderStepSound, tool);
            return true;
        }

        if (armed.RequiredToolCategory is { } category
            && ToolCategorySounds.TryGetValue(category, out var sound))
        {
            _audio.PlayPvs(sound, patient);
        }

        return true;
    }

    private TimeSpan ResolveStepDoAfterDelay(EntityUid surgeon, EntityUid patient)
    {
        var multiplier = _skills.GetSkillDelayMultiplier(surgeon, SurgerySkill, SurgeryStepDelayMultipliers);
        multiplier *= _bodyScanner.GetSurgeryDelayMultiplier(surgeon, patient);
        return TimeSpan.FromSeconds(StepDoAfterSeconds * multiplier);
    }

    protected override void ApplyWrongToolDamage(EntityUid surgeon, EntityUid patient, EntityUid tool, string damageType, float amount)
    {
        var multiplier = Cfg.GetCVar(CMUMedicalCCVars.SurgeryWrongToolDamageMultiplier);
        var scaled = amount * multiplier;
        if (scaled <= 0f)
        {
            // CCVar = 0 collapses Strict back to Lenient: no damage, just
            // a popup so the medic still gets the "wrong tool" feedback.
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-tool"), patient, surgeon, PopupType.SmallCaution);
            return;
        }

        var spec = CMUWrongToolDamageTable.MakeSpec(damageType, scaled);
        _damage.TryChangeDamage(patient, spec, ignoreResistances: false, origin: surgeon);

        Popup.PopupEntity(
            Loc.GetString("cmu-medical-surgery-wrong-tool-damage", ("tool", Name(tool))),
            patient,
            surgeon,
            PopupType.MediumCaution);
    }

    protected override void RunStepEffect(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid? tool, EntityUid? targetPart)
    {
        // Resolve the step proto id from the CURRENTLY RESOLVED surgery
        // (which may be a prereq like CMSurgeryOpenIncision, not the leaf
        // the medic picked) so V1 SharedCMUSurgerySystem applies the
        // organ remove / bone set / cauterize / reattach side effects.
        var stepProtoId = ResolveStepPrototypeId(armed.SurgeryId, armed.StepIndex);
        if (stepProtoId is null)
        {
            ClearArmed(patient, armed);
            return;
        }

        if (RmcSurgery.GetSingleton(stepProtoId) is not { } stepEnt)
        {
            ClearArmed(patient, armed);
            return;
        }

        var leafId = string.IsNullOrEmpty(armed.LeafSurgeryId) ? armed.SurgeryId : armed.LeafSurgeryId;
        EntityUid stepPart = patient;
        if (targetPart is { } part
            && TryComp<BodyPartComponent>(part, out var targetPartComp)
            && targetPartComp.PartType == armed.TargetPartType
            && targetPartComp.Symmetry == armed.TargetSymmetry)
        {
            stepPart = part;
        }
        else if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(leafId)
                 && targetPart is { } reattachAnchor
                 && HasComp<BodyPartComponent>(reattachAnchor))
        {
            stepPart = reattachAnchor;
        }
        else if (TryFindClickedPart(patient, null, armed.TargetPartType, armed.TargetSymmetry, out var foundPart))
        {
            stepPart = foundPart;
        }

        if (TryFailSurgeryStep(patient, stepProtoId, armed.RequiredToolCategory, surgeon, tool))
        {
            RemComp<CMUSurgeryArmedStepComponent>(patient);
            _dispatch.RefreshUiForPatient(patient);
            return;
        }

        var tools = new List<EntityUid>();
        if (tool is { } usedTool && Exists(usedTool))
            tools.Add(usedTool);

        foreach (var held in Hands.EnumerateHeld(surgeon))
        {
            if (!tools.Contains(held))
                tools.Add(held);
        }

        if (!TryApplyIncisionManagementSystemOpening(stepProtoId, stepPart, tool))
        {
            var stepEvent = new CMSurgeryStepEvent(surgeon, patient, stepPart, tools);
            RaiseLocalEvent(stepEnt, ref stepEvent);
        }

        if (IsReattachLimbStep(stepProtoId)
            && TryFindClickedPart(patient, null, armed.TargetPartType, armed.TargetSymmetry, out var reattachedPart))
        {
            MoveReattachSurgeryStateToLimb(stepPart, reattachedPart);
            stepPart = reattachedPart;
        }

        // Idempotent on subsequent steps, but EnsureSurgeryInFlight
        // refreshes the surgeon snapshot each time so a fresh surgeon
        // picking up an abandoned-but-armed surgery is credited as the
        // new operator.
        var leafDisplay = ResolveLeafDisplayName(leafId);
        EnsureSurgeryInFlight(patient, stepPart, surgeon, leafId, leafDisplay, armed.TargetPartType, armed.TargetSymmetry);

        if (RmcSurgery.GetSingleton(leafId) is { } leafEnt
            && TryComp<CMSurgeryComponent>(leafEnt, out var leafComp)
            && armed.SurgeryId == leafId)
        {
            if (armed.StepIndex >= leafComp.Steps.Count - 1)
            {
                MarkFracturePostOpIfNeeded(patient, stepPart, surgeon, leafId);
                var completeEvLast = new CMSurgeryCompleteEvent(patient, surgeon, leafId);
                RaiseLocalEvent(patient, ref completeEvLast);
                RemComp<CMUSurgeryArmedStepComponent>(patient);
                ClearSurgeryInFlight(patient);
                _dispatch.RefreshUiForPatient(patient);
                return;
            }

            if (TryResolveStepAt(leafId, armed.StepIndex + 1, out var nextLinear, stepPart))
            {
                if (!IsCloseUpSurgeryId(leafId)
                    && IsClosureStep(nextLinear.ResolvedSurgeryId, nextLinear.StepIndex))
                {
                    MarkFracturePostOpIfNeeded(patient, stepPart, surgeon, leafId);
                    var completeEvFunctional = new CMSurgeryCompleteEvent(patient, surgeon, leafId);
                    RaiseLocalEvent(patient, ref completeEvFunctional);

                    RemComp<CMUSurgeryArmedStepComponent>(patient);
                    SetAwaitingClosureChoice(patient, stepPart);
                    Popup.PopupEntity(
                        Loc.GetString("cmu-medical-surgery-choose-repair-or-close"),
                        patient,
                        surgeon,
                        PopupType.Medium);
                    _dispatch.RefreshUiForPatient(patient);
                    return;
                }

                armed.SurgeryId = nextLinear.ResolvedSurgeryId;
                armed.StepIndex = nextLinear.StepIndex;
                armed.RequiredToolCategory = nextLinear.ToolCategory;
                armed.StepLabel = nextLinear.StepLabel;
                armed.ArmedAt = Timing.CurTime;
                Dirty(patient, armed);
                _dispatch.RefreshUiForPatient(patient);
                return;
            }
        }

        if (TryResolveNextStep(patient, stepPart, leafId, out var next))
        {
            armed.SurgeryId = next.ResolvedSurgeryId;
            armed.StepIndex = next.StepIndex;
            armed.RequiredToolCategory = next.ToolCategory;
            armed.StepLabel = next.StepLabel;
            armed.ArmedAt = Timing.CurTime;
            Dirty(patient, armed);
            _dispatch.RefreshUiForPatient(patient);
            return;
        }

        var completeEv = new CMSurgeryCompleteEvent(patient, surgeon, leafId);
        MarkFracturePostOpIfNeeded(patient, stepPart, surgeon, leafId);
        RaiseLocalEvent(patient, ref completeEv);
        RemComp<CMUSurgeryArmedStepComponent>(patient);
        ClearSurgeryInFlight(patient);
        _dispatch.RefreshUiForPatient(patient);
    }

    private void MarkFracturePostOpIfNeeded(EntityUid patient, EntityUid part, EntityUid surgeon, string leafId)
    {
        if (!IsFractureSurgeryId(leafId))
            return;
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return;
        if (partComp.PartType is not (BodyPartType.Arm or BodyPartType.Leg))
            return;
        if (HasComp<FractureComponent>(part) || HasComp<CMUCastComponent>(part))
            return;

        var postOp = EnsureComp<CMUPostOpBoneSetComponent>(part);
        postOp.MalunionCheckAt = Timing.CurTime + TimeSpan.FromMinutes(PostOpCastWindowMinutes);
        postOp.MalunionChance = PostOpMalunionChance;
        Dirty(part, postOp);

        Popup.PopupEntity(
            Loc.GetString("cmu-medical-cast-needed"),
            patient,
            surgeon,
            PopupType.SmallCaution);
    }

    private static bool IsFractureSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgerySetSimpleFracture"
            or "CMUSurgerySetSimpleFractureCavity"
            or "CMUSurgerySetCompoundFracture"
            or "CMUSurgerySetCompoundFractureCavity"
            or "CMUSurgerySetComminutedFracture"
            or "CMUSurgerySetComminutedFractureCavity";
    }

    private bool ShouldOfferRepairOrClose(EntityUid patient, EntityUid surgeon, EntityUid stepPart, string currentLeafId)
    {
        if (!TryComp<BodyPartComponent>(stepPart, out var partComp))
            return false;

        var entries = _dispatch.BuildEligibleSurgeries(
            patient,
            partComp.PartType,
            partComp.Symmetry,
            surgeon,
            stepPart,
            ignoreInProgressLock: true);

        foreach (var entry in entries)
        {
            if (entry.SurgeryId == currentLeafId)
                continue;
            if (!IsOrganRepairChoiceCategory(entry.Category))
                continue;
            if (IsClosureStep(entry.SurgeryId, entry.NextStepIndex))
                continue;

            return true;
        }

        return false;
    }

    private bool TryArmSamePartContinuation(
        EntityUid patient,
        CMUSurgeryArmedStepComponent armed,
        EntityUid surgeon,
        EntityUid stepPart,
        string currentLeafId)
    {
        if (!TryComp<BodyPartComponent>(stepPart, out var partComp))
            return false;

        var entries = _dispatch.BuildEligibleSurgeries(
            patient,
            partComp.PartType,
            partComp.Symmetry,
            surgeon,
            stepPart,
            ignoreInProgressLock: true);

        var candidates = new List<CMUSurgeryEntry>();
        foreach (var entry in entries)
        {
            if (entry.SurgeryId == currentLeafId)
                continue;
            if (!CanAutoContinueCategory(entry.Category))
                continue;
            if (IsClosureStep(entry.SurgeryId, entry.NextStepIndex))
                continue;

            candidates.Add(entry);
        }

        if (candidates.Count == 0)
            return false;

        candidates.Sort((a, b) => AutoContinuationPriority(b.Category).CompareTo(AutoContinuationPriority(a.Category)));
        var best = candidates[0];
        if (candidates.Count > 1
            && AutoContinuationPriority(candidates[1].Category) == AutoContinuationPriority(best.Category))
        {
            return false;
        }

        var next = TryArmStep(
            surgeon,
            patient,
            stepPart,
            best.SurgeryId,
            best.NextStepIndex,
            partComp.PartType,
            partComp.Symmetry,
            allowSamePartInFlightSwitch: true);

        if (next is null)
            return false;

        var display = ResolveLeafDisplayName(best.SurgeryId);
        EnsureSurgeryInFlight(patient, stepPart, surgeon, best.SurgeryId, display, armed.TargetPartType, armed.TargetSymmetry);
        Popup.PopupEntity(
            Loc.GetString("cmu-medical-surgery-auto-continue", ("surgery", display)),
            patient,
            surgeon,
            PopupType.Medium);
        _dispatch.RefreshUiForPatient(patient);
        return true;
    }

    private bool IsClosureStep(string surgeryId, int stepIndex)
    {
        var stepId = ResolveStepPrototypeId(surgeryId, stepIndex);
        return stepId is not null && ClosureStepIds.Contains(stepId);
    }

    private static bool IsReattachLimbStep(string stepProtoId)
    {
        return stepProtoId is "CMUSurgeryStepReattachLimb"
            or "RMCSynthSurgeryStepReattachLimb";
    }

    private void MoveReattachSurgeryStateToLimb(EntityUid source, EntityUid limb)
    {
        if (source == limb)
            return;

        MoveMarker<CMIncisionOpenComponent>(source, limb);
        MoveMarker<CMBleedersClampedComponent>(source, limb);
        MoveMarker<CMSkinRetractedComponent>(source, limb);
        MoveMarker<CMUStumpRemovedComponent>(source, limb);
        MoveMarker<CMUReattachPreppedComponent>(source, limb);
        MoveMarker<CMUReattachCompleteComponent>(source, limb);
    }

    private void MoveMarker<T>(EntityUid source, EntityUid target) where T : Component, new()
    {
        if (!HasComp<T>(source))
            return;

        EnsureComp<T>(target);
        RemComp<T>(source);
    }

    private static bool IsCloseUpSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgeryCloseIncision"
            or "CMUSurgeryCloseBoneCavity"
            or "CMSurgeryCloseIncision"
            or "CMSurgeryCloseRibcage";
    }

    private static bool CanAutoContinueCategory(string category)
    {
        return category is "bleed" or "fracture" or "burn" or "parasite";
    }

    private static int AutoContinuationPriority(string category) => category switch
    {
        "bleed" => 90,
        "fracture" => 80,
        "burn" => 70,
        "parasite" => 50,
        _ => 0,
    };

    private static bool IsOrganRepairChoiceCategory(string category)
    {
        return category is "suture" or "head_organ";
    }

    private string ResolveLeafDisplayName(string leafId)
    {
        if (TryGetMetadata(leafId, out var metadata))
            return metadata.DisplayName ?? leafId;
        if (Prototypes.TryIndex<EntityPrototype>(leafId, out var proto))
            return proto.Name;
        return leafId;
    }

    private bool TryFailSurgeryStep(EntityUid patient, string stepProtoId, string? toolCategory, EntityUid surgeon, EntityUid? tool)
    {
        var chance = GetSurgeryFailureChance(patient, stepProtoId, toolCategory, surgeon, tool);
        if (chance <= 0f || !_random.Prob(chance))
            return false;

        ApplySurgeryFailure(patient, surgeon, tool);
        return true;
    }

    private float GetSurgeryFailureChance(EntityUid patient, string stepProtoId, string? toolCategory, EntityUid surgeon, EntityUid? tool)
    {
        var penalties = GetToolFailurePenalty(tool, toolCategory);
        if (!SurfaceExemptStepIds.Contains(stepProtoId))
            penalties += GetSurfaceFailurePenalty(patient, surgeon);

        if (patient == surgeon)
            penalties += 1;

        penalties += GetSkillFailureCompensation(surgeon);

        return penalties switch
        {
            <= 0 => 0f,
            1 => 0.05f,
            2 => 0.25f,
            _ => 0.5f,
        };
    }

    private int GetToolFailurePenalty(EntityUid? tool, string? toolCategory)
    {
        if (tool is not { } toolUid
            || !TryComp<CMUImprovisedSurgeryToolComponent>(toolUid, out var improvised))
        {
            return 0;
        }

        return Math.Clamp(improvised.GetFailurePenalty(toolCategory), 0, 2);
    }

    private int GetSurfaceFailurePenalty(EntityUid patient, EntityUid surgeon)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle)
            || buckle.BuckledTo is not { } surface)
        {
            return 2;
        }

        if (HasComp<CMOperatingTableComponent>(surface))
            return 0;

        if (IsUnsuitedSurgerySurface(surface))
            return 1;

        if (!TryComp<StrapComponent>(surface, out var strap))
            return 2;

        if (strap.Position == StrapPosition.Down)
            return 0;

        // Self-surgery is allowed while strapped into a chair/seat so the
        // surgeon can still use their hands. It is still rough field surgery,
        // matching roller/stretcher style surface penalty.
        return patient == surgeon ? 1 : 2;
    }

    private bool IsUnsuitedSurgerySurface(EntityUid surface)
    {
        var protoId = MetaData(surface).EntityPrototype?.ID;
        if (ContainsSurfaceKeyword(protoId))
            return true;

        return ContainsSurfaceKeyword(Name(surface));
    }

    private static bool ContainsSurfaceKeyword(string? value)
    {
        return value?.Contains("Roller", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("Stretcher", StringComparison.OrdinalIgnoreCase) == true
            || value?.Contains("Bedroll", StringComparison.OrdinalIgnoreCase) == true;
    }

    private int GetSkillFailureCompensation(EntityUid surgeon)
    {
        if (HasComp<BypassSkillChecksComponent>(surgeon))
            return -3;

        var skill = _skills.GetSkill(surgeon, SurgerySkill);
        return skill switch
        {
            >= 3 => -3,
            >= 2 => -1,
            _ => 0,
        };
    }

    private void ApplySurgeryFailure(EntityUid patient, EntityUid surgeon, EntityUid? tool)
    {
        if (tool is not { } toolUid
            || !TryComp<CMUImprovisedSurgeryToolComponent>(toolUid, out var improvised))
        {
            var defaultSpec = CMUWrongToolDamageTable.MakeSpec("Slash", 3f);
            _damage.TryChangeDamage(patient, defaultSpec, ignoreResistances: false, origin: surgeon);
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-step-failed"), patient, surgeon, PopupType.MediumCaution);
            return;
        }

        var damageAmount = MathF.Max(1f, improvised.MishapDamageAmount);
        var spec = CMUWrongToolDamageTable.MakeSpec(improvised.MishapDamageType, damageAmount);
        _damage.TryChangeDamage(patient, spec, ignoreResistances: false, origin: surgeon);

        Popup.PopupEntity(
            Loc.GetString("cmu-medical-surgery-step-failed-with-tool", ("tool", Name(toolUid))),
            patient,
            surgeon,
            PopupType.MediumCaution);
    }

    private bool TryApplyIncisionManagementSystemOpening(string stepProtoId, EntityUid stepPart, EntityUid? tool)
    {
        if (stepProtoId != OpenIncisionScalpelStep
            || tool is not { } toolUid
            || !HasComp<CMUIncisionManagementSystemComponent>(toolUid))
        {
            return false;
        }

        EnsureComp<CMIncisionOpenComponent>(stepPart);
        EnsureComp<CMBleedersClampedComponent>(stepPart);
        EnsureComp<CMSkinRetractedComponent>(stepPart);
        return true;
    }

    private string? ResolveStepPrototypeId(string surgeryId, int stepIndex)
    {
        if (!Prototypes.TryIndex<EntityPrototype>(surgeryId, out var proto))
            return null;
        if (!proto.TryGetComponent<CMSurgeryComponent>(out var surgeryComp, _compFactory))
            return null;
        if (stepIndex < 0 || stepIndex >= surgeryComp.Steps.Count)
            return null;
        return surgeryComp.Steps[stepIndex];
    }
}
