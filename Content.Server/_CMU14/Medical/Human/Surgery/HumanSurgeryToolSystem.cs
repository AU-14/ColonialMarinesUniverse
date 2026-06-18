using Content.Server._CMU14.Medical.Machines;
using Content.Server.StatusEffectNew;
using Content.Shared._CMU14.Medical.Targeting;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Equipment;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Rules;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Human.Damage.Infection;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._CMU14.Medical.Human.Damage.Events;
using Content.Shared._CMU14.Medical.Human.Damage.Shrapnel;
using Content.Shared._CMU14.Medical.Machines;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using BodyPartComponent = Content.Shared.Body.Part.BodyPartComponent;
using BodyPartSymmetry = Content.Shared.Body.Part.BodyPartSymmetry;
using BodyPartType = Content.Shared.Body.Part.BodyPartType;

namespace Content.Server._CMU14.Medical.Human.Surgery;

public sealed partial class HumanSurgeryToolSystem : EntitySystem
{
    private const float SurgeryPainSuppressionMinimum = 0.5f;
    private const int SurgeryPainSuppressionTierMinimum = 2;
    private const int SurgerySkillNovice = 1;
    private const int SurgerySkillTrained = 2;
    private const int SurgerySkillExpert = 3;
    private const string SurgerySawmillName = "cmu.medical.surgery";

    private static readonly TimeSpan BaseSurgeryDelay = TimeSpan.FromSeconds(2);
    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";
    private static readonly ProtoId<TagPrototype> CMUFixOVeinTag = "CMUFixOVein";
    private static readonly ProtoId<TagPrototype> CMBurnKitTag = "CMBurnKit";
    private static readonly ProtoId<TagPrototype> CMSynthGraftTag = "CMSynthGraft";
    private static readonly ProtoId<TagPrototype> CMSurgicalLineTag = "CMSurgicalLine";
    private static readonly ProtoId<TagPrototype> CMTraumaKitTag = "CMTraumaKit";
    private static readonly ProtoId<EmotePrototype> ScreamEmote = "Scream";
    private static readonly EntProtoId DeadLarvaItem = "RMCXenoEmbryo";
    private static readonly EntProtoId RoboticLeftHandPrototype = "CMUPartRoboticLeftHand";
    private static readonly EntProtoId RoboticRightHandPrototype = "CMUPartRoboticRightHand";
    private static readonly EntProtoId RoboticLeftFootPrototype = "CMUPartRoboticLeftFoot";
    private static readonly EntProtoId RoboticRightFootPrototype = "CMUPartRoboticRightFoot";
    private static readonly FixedPoint2 ReattachedLimbDamageCapacity = FixedPoint2.New(100);

    private static readonly BodyRegion[] HeadRegions = { BodyRegion.Head };
    private static readonly BodyRegion[] ChestRegions = { BodyRegion.Chest };
    private static readonly BodyRegion[] GroinRegions = { BodyRegion.Groin };
    private static readonly BodyRegion[] LeftArmRegions = { BodyRegion.LeftArm };
    private static readonly BodyRegion[] RightArmRegions = { BodyRegion.RightArm };
    private static readonly BodyRegion[] LeftHandRegions = { BodyRegion.LeftHand };
    private static readonly BodyRegion[] RightHandRegions = { BodyRegion.RightHand };
    private static readonly BodyRegion[] LeftLegRegions = { BodyRegion.LeftLeg };
    private static readonly BodyRegion[] RightLegRegions = { BodyRegion.RightLeg };
    private static readonly BodyRegion[] LeftFootRegions = { BodyRegion.LeftFoot };
    private static readonly BodyRegion[] RightFootRegions = { BodyRegion.RightFoot };

    private readonly HashSet<EntityUid> _nearby = new();

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHumanMedicalSystem _humanMedical = default!;
    [Dependency] private HumanSurgeryModeSystem _humanSurgeryMode = default!;
    [Dependency] private HumanSurgerySystem _humanSurgery = default!;
    [Dependency] private CMUBodyScannerSystem _bodyScanner = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedJitteringSystem _jitter = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedXenoParasiteSystem _parasite = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedCMUShrapnelSystem _shrapnel = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SleepingSystem _sleeping = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private StatusEffectsSystem _status = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedBodyZoneTargetingSystem _zoneTargeting = default!;

    private ISawmill _sawmill = default!;

    private enum SurgeryFailureKind : byte
    {
        None,
        Pain,
        Surgical,
    }

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = _log.GetSawmill(SurgerySawmillName);

        SubscribeLocalEvent<CMBoneGelComponent, AfterInteractEvent>(OnBoneGelAfterInteract);
        SubscribeLocalEvent<CMBoneSawComponent, AfterInteractEvent>(OnBoneSawAfterInteract);
        SubscribeLocalEvent<CMBoneSetterComponent, AfterInteractEvent>(OnBoneSetterAfterInteract);
        SubscribeLocalEvent<CMScalpelComponent, AfterInteractEvent>(OnScalpelAfterInteract);
        SubscribeLocalEvent<CMCauteryComponent, AfterInteractEvent>(OnCauteryAfterInteract);
        SubscribeLocalEvent<CMHemostatComponent, AfterInteractEvent>(OnHemostatAfterInteract);
        SubscribeLocalEvent<CMRetractorComponent, AfterInteractEvent>(OnRetractorAfterInteract);
        SubscribeLocalEvent<CMSurgicalDrillComponent, AfterInteractEvent>(OnSurgicalDrillAfterInteract);
        SubscribeLocalEvent<CMUFixOVeinComponent, AfterInteractEvent>(OnFixOVeinAfterInteract);
        SubscribeLocalEvent<CMUBoneGraftComponent, AfterInteractEvent>(OnBoneGraftAfterInteract);
        SubscribeLocalEvent<CMUShrapnelExtractorComponent, AfterInteractEvent>(OnShrapnelExtractorAfterInteract);
        SubscribeLocalEvent<BodyPartComponent, AfterInteractEvent>(OnBodyPartAfterInteract);
        SubscribeLocalEvent<HumanMedicalComponent, HumanSurgeryToolDoAfterEvent>(OnSurgeryDoAfter);
    }

    private bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled) &&
            _cfg.GetCVar(CMUMedicalCCVars.SurgeryEnabled);
    }

    private void OnBoneGelAfterInteract(Entity<CMBoneGelComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnBoneSawAfterInteract(Entity<CMBoneSawComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnBoneSetterAfterInteract(Entity<CMBoneSetterComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnScalpelAfterInteract(Entity<CMScalpelComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnCauteryAfterInteract(Entity<CMCauteryComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnHemostatAfterInteract(Entity<CMHemostatComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnRetractorAfterInteract(Entity<CMRetractorComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnSurgicalDrillAfterInteract(Entity<CMSurgicalDrillComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnFixOVeinAfterInteract(Entity<CMUFixOVeinComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnBoneGraftAfterInteract(Entity<CMUBoneGraftComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnShrapnelExtractorAfterInteract(Entity<CMUShrapnelExtractorComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled &&
            args.CanReach &&
            !_humanSurgeryMode.IsSurgeryModeEnabled(args.User) &&
            args.Target is { } target &&
            _shrapnel.TryStartExtraction(args.User, target, ent.Owner))
        {
            args.Handled = true;
            return;
        }

        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnBodyPartAfterInteract(Entity<BodyPartComponent> ent, ref AfterInteractEvent args)
    {
        OnToolAfterInteract(ent.Owner, ref args);
    }

    private void OnToolAfterInteract(EntityUid tool, ref AfterInteractEvent args)
    {
        if (args.Handled ||
            !args.CanReach ||
            args.Target is not { } patient ||
            !IsLayerEnabled() ||
            HasComp<SynthComponent>(patient) ||
            !TryComp<HumanMedicalComponent>(patient, out var medical) ||
            !IsSurgeryCapableTool(tool))
        {
            return;
        }

        args.Handled = TryHandleSurgeryToolInteraction(args.User, patient, tool, medical, popupNoProcedure: true);
    }

    private void OnSurgeryDoAfter(Entity<HumanMedicalComponent> patient, ref HumanSurgeryToolDoAfterEvent args)
    {
        if (args.Cancelled)
        {
            LogSurgeryDebug(
                "doafter-cancelled",
                patient.Owner,
                args.User,
                args.Used,
                patient.Comp,
                args.Attempt,
                "do-after was cancelled");
            _humanSurgery.CancelUncommittedOperation(patient.Owner, args.Attempt);
            return;
        }

        if (!IsLayerEnabled())
        {
            LogSurgeryDebug(
                "doafter-rejected",
                patient.Owner,
                args.User,
                args.Used,
                patient.Comp,
                args.Attempt,
                "medical or surgery layer disabled");
            return;
        }

        if (args.Used is not { } tool)
        {
            LogSurgeryDebug(
                "doafter-rejected",
                patient.Owner,
                args.User,
                null,
                patient.Comp,
                args.Attempt,
                "used tool missing");
            return;
        }

        if (HasComp<SynthComponent>(patient.Owner))
        {
            LogSurgeryDebug(
                "doafter-rejected",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                args.Attempt,
                "patient is synth");
            return;
        }

        if (!IsSurgeryCapableTool(tool))
        {
            LogSurgeryDebug(
                "doafter-rejected",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                args.Attempt,
                "used entity is no longer a surgery-capable tool");
            return;
        }

        if (!_humanSurgeryMode.IsSurgeryModeEnabled(args.User))
        {
            LogSurgeryDebug(
                "doafter-mode-disabled",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                args.Attempt,
                "surgeon surgery mode disabled before completion");
            _humanSurgery.CancelUncommittedOperation(patient.Owner, args.Attempt);
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-mode-required"),
                patient.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        var painkilled = HasPainSuppressionForSurgery(patient.Owner);
        var attempt = RefreshStoredSurgeryAttempt(patient.Owner, patient.Comp, args.Attempt, painkilled);
        LogSurgeryDebug(
            "doafter-complete",
            patient.Owner,
            args.User,
            tool,
            patient.Comp,
            attempt,
            "completion entered");

        if (!TryValidateStoredSurgeryDoAfterAttempt(patient.Owner, patient.Comp, attempt, out var validationFailure))
        {
            if (IsSurgeryAttemptAlreadyApplied(patient.Comp, args.Attempt))
            {
                LogSurgeryDebug(
                    "doafter-validation-already-applied",
                    patient.Owner,
                    args.User,
                    tool,
                    patient.Comp,
                    args.Attempt,
                    validationFailure);
                return;
            }

            LogSurgeryDebug(
                "doafter-validation-failed",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                attempt,
                validationFailure);
            _humanSurgery.CancelUncommittedOperation(patient.Owner, args.Attempt);
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-cannot-start"),
                patient.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        if (!TryValidateCM13SurgeryConditions(args.User, patient.Owner, tool, patient.Comp, attempt, out var reason))
        {
            LogSurgeryDebug(
                "doafter-condition-failed",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                attempt,
                reason);
            _humanSurgery.CancelUncommittedOperation(patient.Owner, attempt);
            _popup.PopupEntity(reason, patient.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        if (!TryValidateArmorForSurgery(patient.Owner, attempt.Region, out reason))
        {
            LogSurgeryDebug(
                "doafter-armor-failed",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                attempt,
                reason);
            _humanSurgery.CancelUncommittedOperation(patient.Owner, attempt);
            _popup.PopupEntity(reason, patient.Owner, args.User, PopupType.SmallCaution);
            return;
        }

        var surgeryFailure = TryResolveSurgeryOutcome(args.User, patient.Owner, patient.Comp, attempt);
        if (surgeryFailure != SurgeryFailureKind.None)
        {
            if (surgeryFailure == SurgeryFailureKind.Surgical ||
                _humanSurgery.HasCommittedOperation(patient.Owner, attempt))
            {
                ApplySurgeryFailure(patient.Owner, attempt);
            }

            if (!_humanSurgery.HasCommittedOperation(patient.Owner, attempt))
                _humanSurgery.CancelUncommittedOperation(patient.Owner, attempt);

            return;
        }

        var result = TryApplyServerBackedSurgery(patient.Owner, tool, patient.Comp, attempt, args.User);
        if (!result.Applied)
        {
            if (IsSurgeryAttemptAlreadyApplied(patient.Comp, attempt))
            {
                LogSurgeryDebug(
                    "doafter-apply-already-applied",
                    patient.Owner,
                    args.User,
                    tool,
                    patient.Comp,
                    attempt,
                    result.FailureReason);
                return;
            }

            LogSurgeryDebug(
                "doafter-apply-failed",
                patient.Owner,
                args.User,
                tool,
                patient.Comp,
                attempt,
                result.FailureReason);
            _humanSurgery.CancelUncommittedOperation(patient.Owner, attempt);
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-cannot-start"),
                patient.Owner,
                args.User,
                PopupType.SmallCaution);
            return;
        }

        _humanSurgery.MarkOperationApplied(patient.Owner, attempt);
        LogSurgeryDebug(
            "doafter-applied",
            patient.Owner,
            args.User,
            tool,
            patient.Comp,
            attempt,
            "surgery step applied");

        if (TryComp<CMSurgeryToolComponent>(tool, out var surgeryTool) &&
            surgeryTool.EndSound is not null)
        {
            _audio.PlayPvs(surgeryTool.EndSound, patient.Owner);
        }

        PopupSurgeryStepApplied(args.User, patient.Owner, attempt);

        if (attempt.Step == SurgeryStepKind.RepairInternalBleed &&
            _tags.HasTag(tool, CMUFixOVeinTag))
        {
            QueueDel(tool);
        }

        if (result.PainEventRequired &&
            CanFeelSurgeryPain(patient.Owner))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-step-pain-reaction"),
                patient.Owner,
                args.User,
                PopupType.SmallCaution);
        }
    }

    private SurgeryAttempt RefreshStoredSurgeryAttempt(
        EntityUid patient,
        HumanMedicalComponent medical,
        SurgeryAttempt expected,
        bool painkilled)
    {
        return expected with
        {
            PatientAnesthetized = HasAnesthesiaForSurgery(patient, medical),
            PatientPainkilled = painkilled,
        };
    }

    private bool TryValidateStoredSurgeryDoAfterAttempt(
        EntityUid patient,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        out string failure)
    {
        if (attempt.Region == BodyRegion.None)
        {
            failure = "stored attempt has no region";
            return false;
        }

        if (!CanPerformStoredSurgeryStep(medical, attempt))
        {
            var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
            failure = $"stored attempt cannot apply to region presence {region.Presence}";
            return false;
        }

        if (!_humanSurgery.TryGetActiveOperation(patient, attempt.Region, out var operation) ||
            operation.ProcedureId != attempt.ProcedureId)
        {
            failure = operation.ProcedureId == SurgeryProcedureId.None
                ? "no active operation for stored attempt region"
                : $"active operation mismatch: active={FormatSurgeryOperation(operation)}, expectedProcedure={attempt.ProcedureId}";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static bool CanPerformStoredSurgeryStep(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);

        if (IsIncisionManagementStep(attempt.Step) &&
            (region.Incision != IncisionDepth.Closed ||
             HasOpenStump(medical, attempt.Region)))
        {
            return true;
        }

        if (attempt.Step == SurgeryStepKind.RepairStump)
            return true;

        if (attempt.Step is SurgeryStepKind.AttachBiologicalLimb or SurgeryStepKind.FitProsthetic)
            return region.Presence is LimbPresence.Missing or LimbPresence.Detached;

        if (attempt.Step == SurgeryStepKind.RemoveProsthetic)
            return region.Presence == LimbPresence.Prosthetic;

        return region.Presence == LimbPresence.Present;
    }

    private static bool IsIncisionManagementStep(SurgeryStepKind step)
    {
        return step is
            SurgeryStepKind.OpenIncision or
            SurgeryStepKind.PrepareIncision or
            SurgeryStepKind.ClampBleeders or
            SurgeryStepKind.RetractIncision or
            SurgeryStepKind.DeepAccess or
            SurgeryStepKind.MendBoneAccess or
            SurgeryStepKind.CloseIncision;
    }

    private void LogSurgeryDebug(
        string phase,
        EntityUid patient,
        EntityUid surgeon,
        EntityUid? tool,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        string reason)
    {
        var selected = _zoneTargeting.TryGetSelectedZone(surgeon);
        var selectedText = selected?.ToString() ?? "none";
        var activeText = "none";
        if (attempt.Region != BodyRegion.None &&
            _humanSurgery.TryGetActiveOperation(patient, attempt.Region, out var operation))
        {
            activeText = FormatSurgeryOperation(operation);
        }

        var regionText = "none";
        if (attempt.Region != BodyRegion.None)
            regionText = FormatRegion(medical, attempt.Region);

        var message =
            $"[CMU surgery] {phase}: reason='{reason}', " +
            $"surgeon={FormatEntity(surgeon)}, patient={FormatEntity(patient)}, tool={FormatEntity(tool)}, " +
            $"selected={selectedText}, attempt={FormatSurgeryAttempt(attempt)}, active={activeText}, region={regionText}";

        if (phase.Contains("failed", StringComparison.Ordinal) ||
            phase.Contains("rejected", StringComparison.Ordinal) ||
            phase == "start-no-attempt")
        {
            _sawmill.Warning(message);
            return;
        }

        _sawmill.Debug(message);
    }

    private string FormatEntity(EntityUid? uid)
    {
        return uid is { } entity
            ? ToPrettyString(entity)
            : "null";
    }

    private static string FormatSurgeryAttempt(SurgeryAttempt attempt)
    {
        return $"region={attempt.Region}, step={attempt.Step}, proc={attempt.ProcedureId}, " +
            $"idx={attempt.StepIndex}, organ={attempt.OrganSlot}, injury={attempt.InjuryId}, " +
            $"bleed={attempt.BleedSourceId}, anesth={attempt.PatientAnesthetized}, painkilled={attempt.PatientPainkilled}, " +
            $"pain={attempt.PainRequirement}, toolQuality={attempt.ToolQuality}, surface={attempt.SurfaceQuality}, " +
            $"skill={attempt.RequiredSurgerySkill}, lying={attempt.LyingRequired}, self={attempt.SelfOperable}, role={attempt.ToolRole}";
    }

    private static string FormatSurgeryOperation(SurgeryOperationState operation)
    {
        return $"region={operation.Region}, proc={operation.ProcedureId}, idx={operation.StepIndex}, committed={operation.Committed}";
    }

    private static string FormatRegion(
        HumanMedicalComponent medical,
        BodyRegion requestedRegion)
    {
        var index = (int) requestedRegion;
        if (index <= 0 ||
            index >= medical.Regions.Length)
        {
            return $"requested={requestedRegion}, slot=out-of-range, regionCount={medical.Regions.Length}";
        }

        var region = medical.Regions[index];
        if (region.Region != requestedRegion)
        {
            return $"requested={requestedRegion}, slot-mismatch={region.Region}, " +
                $"presence={region.Presence}, incision={region.Incision}, " +
                $"brute={region.BruteDamage}, burn={region.BurnDamage}, broken={region.Skeletal.Broken}, splinted={region.Skeletal.Splinted}";
        }

        return $"region={region.Region}, presence={region.Presence}, incision={region.Incision}, " +
            $"brute={region.BruteDamage}, burn={region.BurnDamage}, broken={region.Skeletal.Broken}, splinted={region.Skeletal.Splinted}";
    }

    private static bool IsSurgeryAttemptAlreadyApplied(
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (attempt.Region == BodyRegion.None)
            return false;

        var region = HumanMedicalLedger.GetRegion(medical, attempt.Region);
        return attempt.Step switch
        {
            SurgeryStepKind.OpenIncision => region.Incision >= IncisionDepth.OpenSkin,
            SurgeryStepKind.PrepareIncision => region.Incision >= GetShortcutIncisionDepth(attempt.Region),
            SurgeryStepKind.ClampBleeders => !HasActiveSurgicalBleed(medical, attempt.Region),
            SurgeryStepKind.RetractIncision => region.Incision >= IncisionDepth.Retracted,
            SurgeryStepKind.DeepAccess => region.Incision >= IncisionDepth.DeepAccess,
            SurgeryStepKind.MendBoneAccess => region.Incision <= IncisionDepth.Retracted,
            SurgeryStepKind.CloseIncision => region.Incision == IncisionDepth.Closed,
            SurgeryStepKind.SutureWound or SurgeryStepKind.RepairStump => IsInjuryClosed(medical, attempt.InjuryId),
            SurgeryStepKind.RepairInternalBleed => IsBleedClosed(medical, attempt.BleedSourceId),
            SurgeryStepKind.RepairFracture => !region.Skeletal.Broken,
            SurgeryStepKind.ApplyBoneGel => region.Skeletal.BoneGelApplied,
            SurgeryStepKind.SetBone => region.Skeletal.BoneSet,
            SurgeryStepKind.ApplyBoneGraft => region.Skeletal.BoneGrafted,
            SurgeryStepKind.SealBoneWithGel or SurgeryStepKind.SetGraftedBone => !region.Skeletal.Broken,
            SurgeryStepKind.RemoveEschar => IsInjuryDebrided(medical, attempt.InjuryId),
            SurgeryStepKind.RepairOrgan or
                SurgeryStepKind.RepairEyes or
                SurgeryStepKind.RepairBrainDamage => IsOrganRepaired(medical, attempt.OrganSlot),
            _ => false,
        };
    }

    private static IncisionDepth GetShortcutIncisionDepth(BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest
            ? IncisionDepth.DeepAccess
            : IncisionDepth.Retracted;
    }

    private static bool IsInjuryClosed(HumanMedicalComponent medical, int injuryId)
    {
        if (injuryId == 0)
            return false;

        foreach (var injury in medical.Injuries)
        {
            if (injury.Id != injuryId)
                continue;

            return injury.Flags.HasFlag(InjuryFlags.Sutured) ||
                injury.Flags.HasFlag(InjuryFlags.Closed);
        }

        return true;
    }

    private static bool IsInjuryDebrided(HumanMedicalComponent medical, int injuryId)
    {
        if (injuryId == 0)
            return false;

        foreach (var injury in medical.Injuries)
        {
            if (injury.Id != injuryId)
                continue;

            return injury.Flags.HasFlag(InjuryFlags.Debrided);
        }

        return true;
    }

    private static bool IsBleedClosed(HumanMedicalComponent medical, int bleedSourceId)
    {
        if (bleedSourceId == 0)
            return false;

        foreach (var source in medical.BleedSources)
        {
            if (source.Id != bleedSourceId)
                continue;

            return !source.Active ||
                source.Treatment.HasFlag(TreatmentFlags.Closed) ||
                source.Treatment.HasFlag(TreatmentFlags.Sutured);
        }

        return true;
    }

    private static bool IsOrganRepaired(HumanMedicalComponent medical, OrganSlot organSlot)
    {
        if (organSlot == OrganSlot.None)
            return false;

        var organ = HumanMedicalLedger.GetOrgan(medical, organSlot);
        return organ.Slot == organSlot && organ.Damage <= FixedPoint2.Zero;
    }

    private SurgeryResult TryApplyServerBackedSurgery(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        EntityUid surgeon)
    {
        if (attempt.Step == SurgeryStepKind.CutEmbryoRoots)
        {
            if (!_parasite.TryCutLarvaRootsForCmuSurgery(patient))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No removable embryo exists in the target region.");

            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.RemoveEmbryo)
        {
            if (!_parasite.TryRemoveLarvaForCmuSurgery(patient, DeadLarvaItem, out _))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No removable embryo exists in the target region.");

            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.RemoveForeignObject)
        {
            if (!_shrapnel.TryExtractSurgicalShrapnelFromRegion(patient, attempt.Region, tool, out _, surgeon))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No surgical foreign object exists in the target region.");

            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.AmputateLimb)
        {
            if (!TryApplySurgicalAmputation(patient, attempt.Region))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No matching limb exists in the target region.");

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-amputation-success"),
                patient,
                surgeon,
                PopupType.SmallCaution);
            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.AttachBiologicalLimb)
        {
            if (!TryApplySurgicalLimbAttachment(patient, tool, medical, attempt, prosthetic: false))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No matching biological limb can be attached.");

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-limb-reattach-success"),
                patient,
                surgeon);
            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.FitProsthetic)
        {
            if (!TryApplySurgicalLimbAttachment(patient, tool, medical, attempt, prosthetic: true))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No matching prosthetic limb can be fitted.");

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-prosthetic-fit-success"),
                patient,
                surgeon);
            return ServerBackedApplied(attempt);
        }

        if (attempt.Step == SurgeryStepKind.RemoveProsthetic)
        {
            if (!TryApplySurgicalProstheticRemoval(patient, medical, attempt, surgeon))
                return new SurgeryResult(false, MedicalDirtyFlags.None, "No prosthetic limb can be removed.");

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-prosthetic-remove-success"),
                patient,
                surgeon,
                PopupType.SmallCaution);
            return ServerBackedApplied(attempt);
        }

        var result = _humanSurgery.TryApplySurgery(patient, attempt, medical, surgeon);
        if (result.Applied && attempt.Step == SurgeryStepKind.RemoveEschar)
            ClearEscharPartMarker(patient, attempt.Region);

        return result;
    }

    private static SurgeryResult ServerBackedApplied(SurgeryAttempt attempt)
    {
        return new SurgeryResult(true, MedicalDirtyFlags.None, string.Empty)
        {
            PainEventRequired = !attempt.PatientAnesthetized && !attempt.PatientPainkilled,
        };
    }

    public bool TryHandleSurgeryToolInteraction(
        EntityUid user,
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        bool popupNoProcedure)
    {
        if (!IsLayerEnabled() ||
            HasComp<SynthComponent>(patient) ||
            !IsSurgeryCapableTool(tool))
        {
            return false;
        }

        if (!_humanSurgeryMode.IsSurgeryModeEnabled(user))
        {
            if (!popupNoProcedure)
                return false;

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-mode-required"),
                patient,
                user,
                PopupType.SmallCaution);
            return true;
        }

        var painkilled = HasPainSuppressionForSurgery(patient);
        if (!TryCreateSurgeryAttempt(user, patient, tool, medical, painkilled, out var attempt))
        {
            LogSurgeryDebug(
                "start-no-attempt",
                patient,
                user,
                tool,
                medical,
                attempt,
                "could not create surgery attempt from selected/current region");

            if (!popupNoProcedure)
                return false;

            _popup.PopupEntity(
                GetNoSurgeryAttemptMessage(user, medical),
                patient,
                user,
                PopupType.SmallCaution);
            return true;
        }

        if (!_humanSurgery.TryReserveOperation(patient, attempt, out var reserveFailure))
        {
            LogSurgeryDebug(
                "start-reserve-failed",
                patient,
                user,
                tool,
                medical,
                attempt,
                reserveFailure);
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-region-locked"),
                patient,
                user,
                PopupType.SmallCaution);
            return true;
        }

        if (!TryValidateCM13SurgeryConditions(user, patient, tool, medical, attempt, out var reason) ||
            !TryValidateArmorForSurgery(patient, attempt.Region, out reason))
        {
            LogSurgeryDebug(
                "start-condition-failed",
                patient,
                user,
                tool,
                medical,
                attempt,
                reason);
            _humanSurgery.CancelUncommittedOperation(patient, attempt);
            _popup.PopupEntity(reason, patient, user, PopupType.SmallCaution);
            return true;
        }

        var skillMultiplier = _skills.GetSkillDelayMultiplier(user, SurgerySkill);
        var delay = HumanSurgeryRules.GetStepDuration(
            attempt.BaseDelay > TimeSpan.Zero ? attempt.BaseDelay : BaseSurgeryDelay,
            attempt.ToolQuality,
            attempt.SurfaceQuality,
            skillMultiplier,
            attempt.LyingRequired,
            user == patient);
        delay += _skills.GetDelay(user, tool);
        delay *= _bodyScanner.GetSurgeryDelayMultiplier(user, patient);

        var ev = new HumanSurgeryToolDoAfterEvent(attempt);
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            delay,
            ev,
            patient,
            target: patient,
            used: tool)
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
        {
            LogSurgeryDebug(
                "start-doafter-failed",
                patient,
                user,
                tool,
                medical,
                attempt,
                "TryStartDoAfter returned false");
            _humanSurgery.CancelUncommittedOperation(patient, attempt);
            return true;
        }

        LogSurgeryDebug(
            "start-doafter",
            patient,
            user,
            tool,
            medical,
            attempt,
            "do-after started");

        if (TryComp<CMSurgeryToolComponent>(tool, out var surgeryTool) &&
            surgeryTool.StartSound is not null)
        {
            _audio.PlayPvs(surgeryTool.StartSound, user);
        }

        return true;
    }

    private void PopupSurgeryStepApplied(
        EntityUid surgeon,
        EntityUid patient,
        SurgeryAttempt attempt)
    {
        if (attempt.Region == BodyRegion.None)
            return;

        var part = Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(attempt.Region));
        var message = attempt.Step switch
        {
            SurgeryStepKind.OpenIncision => "cmu-medical-surgery-success-open-incision",
            SurgeryStepKind.PrepareIncision => "cmu-medical-surgery-success-prepare-incision",
            SurgeryStepKind.ClampBleeders => "cmu-medical-surgery-success-clamp-bleeders",
            SurgeryStepKind.RetractIncision => "cmu-medical-surgery-success-retract-incision",
            SurgeryStepKind.DeepAccess => "cmu-medical-surgery-success-deep-access",
            SurgeryStepKind.MendBoneAccess => "cmu-medical-surgery-success-mend-bone-access",
            SurgeryStepKind.CloseIncision => "cmu-medical-surgery-success-close-incision",
            SurgeryStepKind.ApplyBoneGel => "cmu-medical-surgery-success-apply-bone-gel",
            SurgeryStepKind.SetBone => "cmu-medical-surgery-success-set-bone",
            SurgeryStepKind.SealBoneWithGel => "cmu-medical-surgery-success-seal-bone",
            SurgeryStepKind.ApplyBoneGraft => "cmu-medical-surgery-success-apply-bone-graft",
            SurgeryStepKind.SetGraftedBone => "cmu-medical-surgery-success-set-grafted-bone",
            _ => "cmu-medical-surgery-success-generic",
        };

        _popup.PopupEntity(
            Loc.GetString(message, ("part", part)),
            patient,
            surgeon,
            PopupType.Small);
    }

    private string GetNoSurgeryAttemptMessage(
        EntityUid surgeon,
        HumanMedicalComponent medical)
    {
        if (_zoneTargeting.TryGetSelectedZone(surgeon) is not { } zone)
            return Loc.GetString("cmu-medical-surgery-select-region");

        foreach (var region in RegionsForZone(zone))
        {
            var state = HumanMedicalLedger.GetRegion(medical, region);
            if (state.Region != region)
                continue;

            var part = Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(region));
            if (state.Presence != LimbPresence.Present)
            {
                if (!TryGetLinkedStumpSurgeryRegion(medical, region, out var stumpRegion))
                {
                    return Loc.GetString(
                        "cmu-medical-surgery-missing-region",
                        ("part", part));
                }

                state = HumanMedicalLedger.GetRegion(medical, stumpRegion);
                part = Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(stumpRegion));
            }

            return state.Incision switch
            {
                IncisionDepth.Closed => Loc.GetString(
                    "cmu-medical-surgery-needs-incision",
                    ("part", part)),
                IncisionDepth.OpenSkin => Loc.GetString(
                    HasActiveSurgicalBleed(medical, region)
                        ? "cmu-medical-surgery-needs-clamp"
                        : "cmu-medical-surgery-needs-retraction",
                    ("part", part)),
                IncisionDepth.Retracted when IsEncasedRegion(region) && !HasFracturedBoneAccess(region, state) => Loc.GetString(
                    "cmu-medical-surgery-needs-bone-access",
                    ("part", part)),
                IncisionDepth.Retracted => Loc.GetString(
                    "cmu-medical-surgery-no-exposed-repair",
                    ("part", part)),
                IncisionDepth.DeepAccess => Loc.GetString(
                    "cmu-medical-surgery-deep-access-ready",
                    ("part", part)),
                _ => Loc.GetString("cmu-medical-surgery-cannot-start"),
            };
        }

        return Loc.GetString("cmu-medical-surgery-cannot-start");
    }

    private bool TryCreateSurgeryAttempt(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        bool painkilled,
        out SurgeryAttempt attempt)
    {
        if (_zoneTargeting.TryGetSelectedZone(surgeon) is { } zone)
        {
            foreach (var region in RegionsForZone(zone))
            {
                if (TryCreateSurgeryAttempt(patient, tool, medical, region, painkilled, out attempt))
                    return true;

                if (TryGetLinkedStumpSurgeryRegion(medical, region, out var stumpRegion) &&
                    TryCreateSurgeryAttempt(patient, tool, medical, stumpRegion, painkilled, out attempt))
                {
                    return true;
                }
            }
        }

        if (_humanSurgery.TryGetSingleActiveOperationRegion(patient, out var activeRegion))
        {
            if (TryCreateSurgeryAttempt(patient, tool, medical, activeRegion, painkilled, out attempt))
                return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateSurgeryAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        bool painkilled,
        out SurgeryAttempt attempt)
    {
        attempt = default;
        if (region == BodyRegion.None)
            return false;

        var anesthetized = HasAnesthesiaForSurgery(patient, medical);
        var lockedProcedure = _humanSurgery.TryGetActiveOperation(patient, region, out var operation)
            ? operation.ProcedureId
            : SurgeryProcedureId.None;

        if (TryCreateStumpRepairAttempt(tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateLimbAttachmentAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateProstheticRemovalAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (!CanPerformNonStumpSurgeryOnRegion(medical, region))
            return false;

        if (TryCreateSutureWoundAttempt(tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateForeignObjectRemovalAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateEscharRemovalAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateIncisionAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateAmputationAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateRepairAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        return false;
    }

    private static bool CanPerformNonStumpSurgeryOnRegion(
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        return regionState.Presence == LimbPresence.Present ||
            TryFindOpenStump(medical, region);
    }

    private bool TryCreateSutureWoundAttempt(
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.SutureWound) ||
            !IsSutureWoundTool(tool) ||
            !HumanSurgeryProcedureRules.TryFindSuturableWound(medical, region, out var wound))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.SutureWound,
            injuryId: wound.Id,
            bleedSourceId: FindBleedSourceForInjury(medical, wound),
            toolQuality: GetSutureWoundToolQuality(tool),
            painRequirement: SurgeryPainRequirement.Light,
            requiredSurgerySkill: SurgerySkillNovice,
            selfOperable: true,
            baseDelay: TimeSpan.FromSeconds(2),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.SutureWound,
            stepIndex: 0,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateForeignObjectRemovalAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.RemoveForeignObject) ||
            !IsForeignObjectRemovalTool(tool) ||
            !_shrapnel.TryFindSurgicalShrapnelInRegion(patient, region, out var depth) ||
            !HasForeignObjectAccess(medical, region, depth))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RemoveForeignObject,
            toolQuality: GetForeignObjectRemovalToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: depth == ForeignObjectDepth.Surgical
                ? SurgerySkillTrained
                : SurgerySkillNovice,
            lyingRequired: depth == ForeignObjectDepth.Surgical,
            baseDelay: depth == ForeignObjectDepth.Surgical
                ? TimeSpan.FromSeconds(4)
                : TimeSpan.FromSeconds(3),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.RemoveForeignObject,
            stepIndex: depth == ForeignObjectDepth.Surgical && IsEncasedRegion(region) ? 3 : 2,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateEscharRemovalAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.RemoveEschar) ||
            !IsEscharRemovalTool(tool) ||
            !HasShallowAccess(medical, region) ||
            !HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, region, out var burn))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RemoveEschar,
            injuryId: burn.Id,
            toolQuality: GetEscharRemovalToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Medium,
            requiredSurgerySkill: SurgerySkillNovice,
            selfOperable: true,
            baseDelay: TimeSpan.FromSeconds(3),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.RemoveEschar,
            stepIndex: 0,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateIncisionAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        var procedureId = lockedProcedure;
        if (procedureId == SurgeryProcedureId.None &&
            !TryGetAccessProcedureForRegion(patient, medical, region, out procedureId))
        {
            procedureId = SurgeryProcedureId.SurgicalAccess;
        }

        if (!ProcedureUsesIncision(procedureId))
        {
            attempt = default;
            return false;
        }

        if (HasComp<CMUIncisionManagementSystemComponent>(tool))
        {
            var targetDepth = IsEncasedRegion(region)
                ? IncisionDepth.DeepAccess
                : IncisionDepth.Retracted;
            if (regionState.Incision < targetDepth)
            {
                attempt = BuildAttempt(
                    region,
                    SurgeryStepKind.PrepareIncision,
                    toolQuality: SurgeryToolQuality.Ideal,
                    surfaceQuality: GetSurgerySurfaceQuality(patient),
                    painRequirement: IsEncasedRegion(region)
                        ? SurgeryPainRequirement.Heavy
                        : SurgeryPainRequirement.Medium,
                    requiredSurgerySkill: IsEncasedRegion(region)
                        ? SurgerySkillTrained
                        : SurgerySkillNovice,
                    lyingRequired: IsEncasedRegion(region),
                    selfOperable: !IsEncasedRegion(region),
                    baseDelay: TimeSpan.FromSeconds(1.2),
                    anesthetized: anesthetized,
                    painkilled: painkilled,
                    procedureId: procedureId,
                    stepIndex: 0,
                    toolRole: SurgeryToolRole.InitialIncisionShortcut);
                return true;
            }
        }

        if (HasComp<CMScalpelComponent>(tool) &&
            regionState.Incision == IncisionDepth.Closed)
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.OpenIncision,
                toolQuality: GetIncisionToolQuality(tool),
                painRequirement: SurgeryPainRequirement.Medium,
                requiredSurgerySkill: SurgerySkillNovice,
                selfOperable: true,
                baseDelay: TimeSpan.FromSeconds(2),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: procedureId,
                stepIndex: 0,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (HasComp<CMHemostatComponent>(tool) &&
            regionState.Incision == IncisionDepth.OpenSkin &&
            HasActiveSurgicalBleed(medical, region))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.ClampBleeders,
                toolQuality: SurgeryToolQuality.Ideal,
                painRequirement: SurgeryPainRequirement.Light,
                requiredSurgerySkill: SurgerySkillNovice,
                selfOperable: true,
                baseDelay: TimeSpan.FromSeconds(2),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: procedureId,
                stepIndex: 1,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (IsRetractTool(tool))
        {
            if (regionState.Incision == IncisionDepth.OpenSkin)
            {
                attempt = BuildAttempt(
                    region,
                    SurgeryStepKind.RetractIncision,
                    toolQuality: GetRetractToolQuality(tool),
                    painRequirement: SurgeryPainRequirement.Medium,
                    requiredSurgerySkill: SurgerySkillNovice,
                    selfOperable: true,
                    baseDelay: TimeSpan.FromSeconds(2),
                    anesthetized: anesthetized,
                    painkilled: painkilled,
                    procedureId: procedureId,
                    stepIndex: 1,
                    toolRole: GetToolRole(tool));
                return true;
            }
        }

        if (IsDeepAccessTool(tool) &&
            regionState.Incision == IncisionDepth.Retracted &&
            IsEncasedRegion(region) &&
            !HasFracturedBoneAccess(region, regionState))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.DeepAccess,
                toolQuality: GetDeepAccessToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Heavy,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(3),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: procedureId,
                stepIndex: 2,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (IsBoneAccessClosureTool(tool) &&
            regionState.Incision == IncisionDepth.DeepAccess &&
            IsEncasedRegion(region) &&
            lockedProcedure == SurgeryProcedureId.None &&
            !HasRepairableProblem(patient, medical, region))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.MendBoneAccess,
                toolQuality: GetBoneRepairToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Heavy,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(3),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.SurgicalAccess,
                stepIndex: 3,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (HasComp<CMCauteryComponent>(tool) &&
            regionState.Incision != IncisionDepth.Closed &&
            (regionState.Incision != IncisionDepth.DeepAccess || !IsEncasedRegion(region)) &&
            lockedProcedure == SurgeryProcedureId.None &&
            !HasRepairableProblem(patient, medical, region))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.CloseIncision,
                toolQuality: GetCloseIncisionToolQuality(tool),
                painRequirement: SurgeryPainRequirement.Medium,
                requiredSurgerySkill: SurgerySkillNovice,
                selfOperable: true,
                baseDelay: TimeSpan.FromSeconds(2.5),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.CloseIncision,
                stepIndex: 0,
                toolRole: GetToolRole(tool));
            return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateAmputationAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        var regionState = HumanMedicalLedger.GetRegion(medical, region);
        if (!IsAmputatableRegion(region) ||
            regionState.Presence != LimbPresence.Present ||
            !HasShallowAccess(medical, region) ||
            TryFindOpenStump(medical, region))
        {
            attempt = default;
            return false;
        }

        if (lockedProcedure == SurgeryProcedureId.None &&
            HasComp<CMScalpelComponent>(tool))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.SeverMuscles,
                toolQuality: GetIncisionToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Heavy,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.Amputation,
                stepIndex: 2,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (lockedProcedure == SurgeryProcedureId.Amputation &&
            IsAmputationCancelTool(tool))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.CancelAmputation,
                toolQuality: GetInternalBleedRepairToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Medium,
                requiredSurgerySkill: SurgerySkillNovice,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(3),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.Amputation,
                stepIndex: 3,
                toolRole: SurgeryToolRole.RepairVessel);
            return true;
        }

        if (lockedProcedure == SurgeryProcedureId.Amputation &&
            HasComp<CMBoneSawComponent>(tool))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.AmputateLimb,
                toolQuality: GetDeepAccessToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Full,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.Amputation,
                stepIndex: 3,
                toolRole: GetToolRole(tool));
            return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateRepairAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (TryCreateAlienEmbryoRemovalAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateBrainDamageSurgeryAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateEyeSurgeryAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (TryCreateInternalBleedRepairAttempt(patient, tool, medical, region, lockedProcedure, painkilled, anesthetized, out attempt))
            return true;

        if (ProcedureMatches(lockedProcedure, SurgeryProcedureId.RepairOrgan) &&
            (HasComp<CMUOrganClampComponent>(tool) ||
             _tags.HasTag(tool, CMTraumaKitTag)) &&
            TryFindDamagedOrgan(medical, region, out var organ))
        {
            if (!HasOrganAccess(medical, organ))
            {
                attempt = default;
                return false;
            }

            attempt = BuildAttempt(
                region,
                SurgeryStepKind.RepairOrgan,
                organSlot: organ.Slot,
                toolQuality: GetOrganRepairToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Heavy,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(3),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.RepairOrgan,
                stepIndex: IsEncasedRegion(region) ? 3 : 2,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (TryCreateFractureRepairAttempt(
                patient,
                tool,
                medical,
                region,
                lockedProcedure,
                painkilled,
                anesthetized,
                out attempt))
        {
            return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateFractureRepairAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        attempt = default;
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.RepairFracture) ||
            !HasBoneRepairTool(tool) ||
            !HasRequiredRepairAccess(medical, region))
        {
            return false;
        }

        var skeletal = HumanMedicalLedger.GetRegion(medical, region).Skeletal;
        if (!skeletal.Broken)
            return false;

        if (!TryGetNextFractureStep(tool, skeletal, out var step))
            return false;

        attempt = BuildAttempt(
            region,
            step,
            toolQuality: GetBoneRepairToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: SurgerySkillTrained,
            lyingRequired: true,
            baseDelay: GetBoneRepairDelay(step),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.RepairFracture,
            stepIndex: GetRepairStepIndex(region),
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateAlienEmbryoRemovalAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (region != BodyRegion.Chest ||
            !ProcedureMatches(lockedProcedure, SurgeryProcedureId.AlienEmbryoRemoval) ||
            !TryComp<VictimInfectedComponent>(patient, out var infected) ||
            infected.IsBursting ||
            !HasRequiredRepairAccess(medical, region))
        {
            attempt = default;
            return false;
        }

        if (!infected.RootsCut && HasComp<CMScalpelComponent>(tool))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.CutEmbryoRoots,
                toolQuality: GetIncisionToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Full,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.AlienEmbryoRemoval,
                stepIndex: 3,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (infected.RootsCut && HasComp<CMHemostatComponent>(tool))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.RemoveEmbryo,
                toolQuality: GetForeignObjectRemovalToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Full,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.AlienEmbryoRemoval,
                stepIndex: 4,
                toolRole: GetToolRole(tool));
            return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateBrainDamageSurgeryAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (region != BodyRegion.Head ||
            !ProcedureMatches(lockedProcedure, SurgeryProcedureId.BrainDamageSurgery) ||
            !HasRequiredRepairAccess(medical, region))
        {
            attempt = default;
            return false;
        }

        if (IsForeignObjectRemovalTool(tool) &&
            _shrapnel.TryFindSurgicalShrapnelInRegion(patient, region, out var depth) &&
            HasForeignObjectAccess(medical, region, depth))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.RemoveForeignObject,
                toolQuality: GetForeignObjectRemovalToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Heavy,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.BrainDamageSurgery,
                stepIndex: 3,
                toolRole: GetToolRole(tool));
            return true;
        }

        if (_tags.HasTag(tool, CMUFixOVeinTag) &&
            TryFindDamagedOrganSlot(medical, region, OrganSlot.Brain, out _))
        {
            attempt = BuildAttempt(
                region,
                SurgeryStepKind.RepairBrainDamage,
                organSlot: OrganSlot.Brain,
                toolQuality: GetInternalBleedRepairToolQuality(tool),
                surfaceQuality: GetSurgerySurfaceQuality(patient),
                painRequirement: SurgeryPainRequirement.Full,
                requiredSurgerySkill: SurgerySkillTrained,
                lyingRequired: true,
                baseDelay: TimeSpan.FromSeconds(4),
                anesthetized: anesthetized,
                painkilled: painkilled,
                procedureId: SurgeryProcedureId.BrainDamageSurgery,
                stepIndex: 3,
                toolRole: GetToolRole(tool));
            return true;
        }

        attempt = default;
        return false;
    }

    private bool TryCreateEyeSurgeryAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (region != BodyRegion.Head ||
            !ProcedureMatches(lockedProcedure, SurgeryProcedureId.EyeSurgery) ||
            !_tags.HasTag(tool, CMUFixOVeinTag) ||
            !HasShallowAccess(medical, region) ||
            !TryFindDamagedOrganSlot(medical, region, OrganSlot.Eyes, out _))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RepairEyes,
            organSlot: OrganSlot.Eyes,
            toolQuality: GetInternalBleedRepairToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: SurgerySkillTrained,
            lyingRequired: true,
            baseDelay: TimeSpan.FromSeconds(3),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.EyeSurgery,
            stepIndex: 2,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateInternalBleedRepairAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.RepairInternalBleeding) ||
            !IsInternalBleedRepairTool(tool) ||
            !HasShallowAccess(medical, region) ||
            !TryFindInternalBleed(medical, region, out var bleed))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RepairInternalBleed,
            bleedSourceId: bleed.Id,
            toolQuality: GetInternalBleedRepairToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: SurgerySkillNovice,
            lyingRequired: true,
            baseDelay: TimeSpan.FromSeconds(5),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.RepairInternalBleeding,
            stepIndex: 2,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateStumpRepairAttempt(
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.SealStump) ||
            !IsStumpSealTool(tool) ||
            !HasShallowAccess(medical, region) ||
            !TryFindOpenStump(medical, region, out var stump))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RepairStump,
            injuryId: stump.Id,
            bleedSourceId: FindBleedSourceForInjury(medical, stump),
            toolQuality: SurgeryToolQuality.Ideal,
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: SurgerySkillNovice,
            baseDelay: TimeSpan.FromSeconds(2),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.SealStump,
            stepIndex: 0,
            toolRole: GetToolRole(tool));
        return true;
    }

    private bool TryCreateLimbAttachmentAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!IsAmputatableRegion(region) ||
            !TryResolveSurgeryLimbTool(tool, region, out _, out var prosthetic) ||
            HumanMedicalLedger.GetRegion(medical, region).Presence is not (LimbPresence.Missing or LimbPresence.Detached) ||
            !TryGetAttachmentTarget(patient, region, out _, out _, out _) ||
            HasOpenStumpForMissingRegion(medical, region))
        {
            attempt = default;
            return false;
        }

        var procedureId = prosthetic
            ? SurgeryProcedureId.FitProsthetic
            : SurgeryProcedureId.ReattachLimb;
        if (!ProcedureMatches(lockedProcedure, procedureId))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            prosthetic ? SurgeryStepKind.FitProsthetic : SurgeryStepKind.AttachBiologicalLimb,
            toolQuality: SurgeryToolQuality.Ideal,
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: prosthetic ? SurgeryPainRequirement.Heavy : SurgeryPainRequirement.Full,
            requiredSurgerySkill: SurgerySkillTrained,
            lyingRequired: true,
            baseDelay: prosthetic ? TimeSpan.FromSeconds(5) : TimeSpan.FromSeconds(6),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: procedureId,
            stepIndex: 0,
            toolRole: prosthetic ? SurgeryToolRole.FitProsthetic : SurgeryToolRole.AttachLimb);
        return true;
    }

    private bool TryCreateProstheticRemovalAttempt(
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        BodyRegion region,
        SurgeryProcedureId lockedProcedure,
        bool painkilled,
        bool anesthetized,
        out SurgeryAttempt attempt)
    {
        if (!ProcedureMatches(lockedProcedure, SurgeryProcedureId.RemoveProsthetic) ||
            !IsAmputatableRegion(region) ||
            !IsProstheticRemovalTool(tool) ||
            HumanMedicalLedger.GetRegion(medical, region).Presence != LimbPresence.Prosthetic ||
            !TryGetBodyPartForRegion(patient, region, out var partUid, out _) ||
            !HasComp<CMUProstheticLimbComponent>(partUid))
        {
            attempt = default;
            return false;
        }

        attempt = BuildAttempt(
            region,
            SurgeryStepKind.RemoveProsthetic,
            toolQuality: GetProstheticRemovalToolQuality(tool),
            surfaceQuality: GetSurgerySurfaceQuality(patient),
            painRequirement: SurgeryPainRequirement.Heavy,
            requiredSurgerySkill: SurgerySkillTrained,
            lyingRequired: true,
            baseDelay: TimeSpan.FromSeconds(4),
            anesthetized: anesthetized,
            painkilled: painkilled,
            procedureId: SurgeryProcedureId.RemoveProsthetic,
            stepIndex: 0,
            toolRole: SurgeryToolRole.RemoveProsthetic);
        return true;
    }

    private bool TryValidateArmorForSurgery(
        EntityUid patient,
        BodyRegion region,
        out string failure)
    {
        var slotFlags = SurgeryArmorSlotsForRegion(region);
        if (slotFlags == SlotFlags.NONE)
        {
            failure = string.Empty;
            return true;
        }

        var slots = _inventory.GetSlotEnumerator(patient, slotFlags);
        while (slots.NextItem(out var item))
        {
            if (!HasComp<CMHardArmorComponent>(item))
                continue;

            failure = region == BodyRegion.Head
                ? Loc.GetString("cmu-medical-surgery-remove-helmet")
                : Loc.GetString("cmu-medical-surgery-remove-armor");
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static SlotFlags SurgeryArmorSlotsForRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => SlotFlags.HEAD,
            BodyRegion.Chest or BodyRegion.Groin =>
                SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyRegion.LeftArm or BodyRegion.RightArm =>
                SlotFlags.OUTERCLOTHING | SlotFlags.INNERCLOTHING,
            BodyRegion.LeftHand or BodyRegion.RightHand =>
                SlotFlags.GLOVES,
            BodyRegion.LeftLeg or BodyRegion.RightLeg =>
                SlotFlags.OUTERCLOTHING | SlotFlags.LEGS,
            BodyRegion.LeftFoot or BodyRegion.RightFoot =>
                SlotFlags.FEET,
            _ => SlotFlags.NONE,
        };
    }

    private bool TryValidateCM13SurgeryConditions(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid tool,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        out string failure)
    {
        _ = tool;
        _ = medical;

        if (surgeon == patient && !attempt.SelfOperable)
        {
            failure = Loc.GetString("cmu-medical-surgery-self-not-allowed");
            return false;
        }

        if (surgeon == patient && !attempt.PatientPainkilled)
        {
            failure = Loc.GetString("cmu-medical-surgery-self-pain-control");
            return false;
        }

        if (surgeon == patient && !IsSelfSurgerySecured(patient))
        {
            failure = Loc.GetString("cmu-medical-surgery-self-not-secured");
            return false;
        }

        if (!_skills.HasSkill(surgeon, SurgerySkill, attempt.RequiredSurgerySkill))
        {
            failure = Loc.GetString("cmu-medical-surgery-missing-skills");
            return false;
        }

        if (attempt.LyingRequired && !IsLyingDownForSurgery(patient))
        {
            failure = Loc.GetString("cmu-medical-surgery-patient-not-lying");
            return false;
        }

        if (attempt.ProcedureId == SurgeryProcedureId.AlienEmbryoRemoval &&
            !IsOnOperatingTable(patient))
        {
            failure = Loc.GetString("cmu-medical-surgery-needs-operating-table");
            return false;
        }

        if (attempt.LyingRequired && !HasSurgerySpace(surgeon, patient))
        {
            failure = Loc.GetString("cmu-medical-surgery-cannot-start");
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private SurgeryFailureKind TryResolveSurgeryOutcome(
        EntityUid surgeon,
        EntityUid patient,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt)
    {
        if (CanFeelSurgeryPain(patient))
        {
            var painReduction = GetPainReduction(attempt);
            var painFailureChance = HumanSurgeryRules.GetPainFailureChance(
                attempt.PainRequirement,
                attempt.PatientAnesthetized,
                !HasAnesthesiaForSurgery(patient, medical),
                painReduction);

            if (painFailureChance > 0 && _random.Prob(painFailureChance / 100f))
            {
                LogSurgeryDebug(
                    "outcome-pain-failed",
                    patient,
                    surgeon,
                    null,
                    medical,
                    attempt,
                    $"pain failure roll hit, chance={painFailureChance}");
                ApplySurgeryPainFeedback(patient);

                _popup.PopupEntity(
                    Loc.GetString("cmu-medical-surgery-step-pain-interrupted"),
                    patient,
                    surgeon,
                    PopupType.SmallCaution);
                return SurgeryFailureKind.Pain;
            }
        }

        var surgerySkill = _skills.GetSkill(surgeon, SurgerySkill);
        var surgeryFailureChance = HumanSurgeryRules.GetFailureChance(
            attempt.ToolQuality,
            attempt.SurfaceQuality,
            surgerySkill,
            attempt.LyingRequired);

        if (surgeryFailureChance > 0 && _random.Prob(surgeryFailureChance / 100f))
        {
            LogSurgeryDebug(
                "outcome-surgery-failed",
                patient,
                surgeon,
                null,
                medical,
                attempt,
                $"surgery failure roll hit, chance={surgeryFailureChance}");
            if (ShouldAgitatePatientOnSurgeryFailure(patient, attempt))
                ApplySurgeryPainFeedback(patient);

            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-step-failed"),
                patient,
                surgeon,
                PopupType.SmallCaution);
            return SurgeryFailureKind.Surgical;
        }

        return SurgeryFailureKind.None;
    }

    private bool ShouldAgitatePatientOnSurgeryFailure(
        EntityUid patient,
        SurgeryAttempt attempt)
    {
        return !attempt.PatientAnesthetized &&
            !attempt.PatientPainkilled &&
            CanFeelSurgeryPain(patient);
    }

    private void ApplySurgeryPainFeedback(EntityUid patient)
    {
        if (!CanFeelSurgeryPain(patient))
            return;

        _jitter.DoJitter(patient, TimeSpan.FromSeconds(1.25), true, 14f, 5f, true);

        _emote.TryEmoteWithChat(patient, ScreamEmote, forceEmote: true, cooldown: TimeSpan.Zero);
    }

    private bool CanFeelSurgeryPain(EntityUid patient)
    {
        if (TryComp<MobStateComponent>(patient, out var mobState) &&
            mobState.CurrentState is MobState.Critical or MobState.Dead)
        {
            return false;
        }

        return !HasComp<RMCUnconsciousComponent>(patient) &&
            !HasComp<SleepingComponent>(patient);
    }

    private void ApplySurgeryFailure(EntityUid patient, SurgeryAttempt attempt)
    {
        if (!TryComp<HumanMedicalComponent>(patient, out var medical))
            return;

        var transaction = new MedicalTransaction(attempt.Region);
        switch (attempt.Step)
        {
            case SurgeryStepKind.OpenIncision:
            case SurgeryStepKind.PrepareIncision:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(10),
                    FixedPoint2.Zero));
                break;
            case SurgeryStepKind.RetractIncision:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(15),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.SetIncisionDepth(attempt.Region, IncisionDepth.Retracted));
                break;
            case SurgeryStepKind.ClampBleeders:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(5),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.AddBleedSource(
                    attempt.Region,
                    BleedKind.External,
                    FixedPoint2.New(1)));
                break;
            case SurgeryStepKind.DeepAccess:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(15),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.SetSkeletalState(
                    attempt.Region,
                    broken: true,
                    splinted: HumanMedicalLedger.GetRegion(medical, attempt.Region).Skeletal.Splinted));
                break;
            case SurgeryStepKind.MendBoneAccess:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(8),
                    FixedPoint2.Zero));
                break;
            case SurgeryStepKind.CloseIncision:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.Zero,
                    FixedPoint2.New(3)));
                break;
            case SurgeryStepKind.RepairInternalBleed:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(10),
                    FixedPoint2.Zero));
                break;
            case SurgeryStepKind.RepairOrgan:
            case SurgeryStepKind.RepairEyes:
            case SurgeryStepKind.RepairBrainDamage:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(5),
                    FixedPoint2.Zero));
                if (attempt.OrganSlot != OrganSlot.None)
                    transaction.Add(MedicalEffect.AddOrganDamage(attempt.OrganSlot, FixedPoint2.New(2)));
                break;
            case SurgeryStepKind.CutEmbryoRoots:
            case SurgeryStepKind.RemoveEmbryo:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(12),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.AddBleedSource(
                    attempt.Region,
                    BleedKind.Internal,
                    FixedPoint2.New(2)));
                break;
            case SurgeryStepKind.RepairFracture:
            case SurgeryStepKind.ApplyBoneGel:
            case SurgeryStepKind.SetBone:
            case SurgeryStepKind.SealBoneWithGel:
            case SurgeryStepKind.ApplyBoneGraft:
            case SurgeryStepKind.SetGraftedBone:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(10),
                    FixedPoint2.Zero));
                break;
            case SurgeryStepKind.RepairStump:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(4),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.AddBleedSource(
                    attempt.Region,
                    BleedKind.Stump,
                    FixedPoint2.New(2),
                    attempt.InjuryId));
                break;
            case SurgeryStepKind.RemoveForeignObject:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(6),
                    FixedPoint2.Zero));
                break;
            case SurgeryStepKind.RemoveEschar:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.Zero,
                    FixedPoint2.New(6)));
                break;
            case SurgeryStepKind.SeverMuscles:
            case SurgeryStepKind.AmputateLimb:
                transaction.Add(MedicalEffect.AddRegionDamage(
                    attempt.Region,
                    FixedPoint2.New(18),
                    FixedPoint2.Zero));
                transaction.Add(MedicalEffect.AddBleedSource(
                    attempt.Region,
                    BleedKind.External,
                    FixedPoint2.New(3)));
                break;
            default:
                return;
        }

        _humanMedical.ApplyTransaction((patient, medical), transaction);
    }

    private static int GetPainReduction(SurgeryAttempt attempt)
    {
        if (attempt.PatientAnesthetized)
            return (int) SurgeryPainRequirement.Full;
        if (attempt.PatientPainkilled)
            return (int) SurgeryPainRequirement.Medium;

        return (int) SurgeryPainRequirement.None;
    }

    private bool IsLyingDownForSurgery(EntityUid patient)
    {
        if (_standing.IsDown(patient))
            return true;

        if (TryComp<BuckleComponent>(patient, out var buckle) &&
            buckle.BuckledTo is { } buckledTo &&
            TryComp<StrapComponent>(buckledTo, out var strap))
        {
            return strap.Position == StrapPosition.Down;
        }

        return false;
    }

    private bool IsOnOperatingTable(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle) &&
            buckle.BuckledTo is { } buckledTo &&
            HasComp<CMOperatingTableComponent>(buckledTo);
    }

    private bool IsSelfSurgerySecured(EntityUid patient)
    {
        if (_standing.IsDown(patient))
            return true;

        return TryComp<BuckleComponent>(patient, out var buckle) &&
            buckle.BuckledTo != null;
    }

    private SurgerySurfaceQuality GetSurgerySurfaceQuality(EntityUid patient)
    {
        if (patient == default ||
            !TryComp<BuckleComponent>(patient, out var buckle) ||
            buckle.BuckledTo is not { } buckledTo)
        {
            return SurgerySurfaceQuality.Awful;
        }

        if (HasComp<CMOperatingTableComponent>(buckledTo))
            return SurgerySurfaceQuality.Ideal;

        if (TryComp<StrapComponent>(buckledTo, out var strap) &&
            strap.Position == StrapPosition.Down)
        {
            return SurgerySurfaceQuality.Adequate;
        }

        return SurgerySurfaceQuality.Awful;
    }

    private bool HasSurgerySpace(EntityUid surgeon, EntityUid patient)
    {
        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(patient).Coordinates, 0.4f, _nearby);

        foreach (var nearby in _nearby)
        {
            if (nearby == surgeon ||
                nearby == patient ||
                !HasComp<MobStateComponent>(nearby))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool HasAnesthesiaForSurgery(
        EntityUid patient,
        HumanMedicalComponent? medical = null)
    {
        if (TryGetInhaledAnesthesia(patient, out _))
        {
            if (medical != null && HasRupturedLung(medical))
                return false;

            if (!HasComp<SleepingComponent>(patient) &&
                !HasComp<RMCUnconsciousComponent>(patient))
            {
                _sleeping.TrySleeping((patient, null));
            }

            return HasComp<SleepingComponent>(patient) ||
                HasComp<RMCUnconsciousComponent>(patient);
        }

        if (!HasComp<SleepingComponent>(patient) &&
            !HasComp<RMCUnconsciousComponent>(patient))
        {
            return false;
        }

        return true;
    }

    private bool TryGetInhaledAnesthesia(
        EntityUid patient,
        out EntityUid tank)
    {
        tank = default;
        if (!TryComp<InternalsComponent>(patient, out var internals) ||
            !_internals.AreInternalsWorking(patient, internals) ||
            internals.GasTankEntity is not { } tankUid ||
            !TryComp<GasTankComponent>(tankUid, out var gasTank))
        {
            return false;
        }

        if (gasTank.Air.GetMoles(Gas.NitrousOxide) <= 0.01f)
            return false;

        tank = tankUid;
        return true;
    }

    private static bool HasRupturedLung(HumanMedicalComponent medical)
    {
        return HumanMedicalLedger.GetOrgan(medical, OrganSlot.LeftLung).Status == OrganDamageStatus.Broken ||
            HumanMedicalLedger.GetOrgan(medical, OrganSlot.RightLung).Status == OrganDamageStatus.Broken;
    }

    private bool HasPainSuppressionForSurgery(EntityUid patient)
    {
        return _pain.GetAccumulationSuppression(patient) >= SurgeryPainSuppressionMinimum ||
            _pain.GetTierSuppression(patient) >= SurgeryPainSuppressionTierMinimum;
    }

    private bool IsSurgeryCapableTool(EntityUid tool)
    {
        return HasComp<CMSurgeryToolComponent>(tool) ||
            IsInternalBleedRepairTool(tool) ||
            IsSutureWoundTool(tool) ||
            IsBurnSurgeryTool(tool) ||
            IsForeignObjectRemovalTool(tool) ||
            IsEscharRemovalTool(tool) ||
            IsAttachableLimbSurgeryTool(tool) ||
            IsProstheticRemovalTool(tool) ||
            _tags.HasTag(tool, CMTraumaKitTag);
    }

    private static SurgeryAttempt BuildAttempt(
        BodyRegion region,
        SurgeryStepKind step,
        OrganSlot organSlot = OrganSlot.None,
        int injuryId = 0,
        int bleedSourceId = 0,
        SurgeryPainRequirement painRequirement = SurgeryPainRequirement.Medium,
        SurgeryToolQuality toolQuality = SurgeryToolQuality.Ideal,
        SurgerySurfaceQuality surfaceQuality = SurgerySurfaceQuality.Ideal,
        int requiredSurgerySkill = SurgerySkillNovice,
        bool lyingRequired = false,
        bool selfOperable = false,
        TimeSpan baseDelay = default,
        bool anesthetized = false,
        bool painkilled = false,
        SurgeryProcedureId procedureId = SurgeryProcedureId.None,
        int stepIndex = 0,
        SurgeryToolRole toolRole = SurgeryToolRole.None)
    {
        return new SurgeryAttempt(
            region,
            step,
            organSlot,
            injuryId,
            bleedSourceId,
            anesthetized,
            painkilled,
            painRequirement,
            toolQuality,
            surfaceQuality,
            requiredSurgerySkill,
            lyingRequired,
            selfOperable,
            baseDelay,
            procedureId,
            stepIndex,
            toolRole);
    }

    private static bool ProcedureMatches(
        SurgeryProcedureId lockedProcedure,
        SurgeryProcedureId procedureId)
    {
        return lockedProcedure == SurgeryProcedureId.None ||
            lockedProcedure == procedureId;
    }

    private static bool ProcedureUsesIncision(SurgeryProcedureId procedureId)
    {
        return procedureId is
            SurgeryProcedureId.SurgicalAccess or
            SurgeryProcedureId.SealStump or
            SurgeryProcedureId.RemoveForeignObject or
            SurgeryProcedureId.RepairInternalBleeding or
            SurgeryProcedureId.RemoveEschar or
            SurgeryProcedureId.RepairOrgan or
            SurgeryProcedureId.RepairFracture or
            SurgeryProcedureId.AlienEmbryoRemoval or
            SurgeryProcedureId.EyeSurgery or
            SurgeryProcedureId.BrainDamageSurgery or
            SurgeryProcedureId.Amputation;
    }

    private bool TryGetAccessProcedureForRegion(
        EntityUid patient,
        HumanMedicalComponent medical,
        BodyRegion region,
        out SurgeryProcedureId procedureId)
    {
        if (HasOpenStump(medical, region))
        {
            procedureId = SurgeryProcedureId.SealStump;
            return true;
        }

        if (region == BodyRegion.Chest &&
            TryComp<VictimInfectedComponent>(patient, out var infected) &&
            !infected.IsBursting)
        {
            procedureId = SurgeryProcedureId.AlienEmbryoRemoval;
            return true;
        }

        if (region == BodyRegion.Head &&
            TryFindDamagedOrganSlot(medical, region, OrganSlot.Brain, out _))
        {
            procedureId = SurgeryProcedureId.BrainDamageSurgery;
            return true;
        }

        if (region == BodyRegion.Head &&
            TryFindDamagedOrganSlot(medical, region, OrganSlot.Eyes, out _))
        {
            procedureId = SurgeryProcedureId.EyeSurgery;
            return true;
        }

        if (_shrapnel.TryFindSurgicalShrapnelInRegion(patient, region, out _))
        {
            procedureId = SurgeryProcedureId.RemoveForeignObject;
            return true;
        }

        if (TryFindInternalBleed(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RepairInternalBleeding;
            return true;
        }

        if (HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RemoveEschar;
            return true;
        }

        if (TryFindDamagedOrgan(medical, region, out _))
        {
            procedureId = SurgeryProcedureId.RepairOrgan;
            return true;
        }

        if (HumanMedicalLedger.GetRegion(medical, region).Skeletal.Broken)
        {
            procedureId = SurgeryProcedureId.RepairFracture;
            return true;
        }

        procedureId = SurgeryProcedureId.None;
        return false;
    }

    private bool HasBoneRepairTool(EntityUid tool)
    {
        return HasComp<CMBoneSetterComponent>(tool) ||
            HasComp<CMBoneGelComponent>(tool) ||
            HasComp<CMUBoneGraftComponent>(tool) ||
            HasComp<CMSurgicalDrillComponent>(tool) ||
            HasComp<CMBoneSawComponent>(tool);
    }

    private bool TryGetNextFractureStep(
        EntityUid tool,
        SkeletalState skeletal,
        out SurgeryStepKind step)
    {
        if (HasComp<CMBoneGelComponent>(tool))
        {
            if (!skeletal.BoneGelApplied)
            {
                step = SurgeryStepKind.ApplyBoneGel;
                return true;
            }

            if (skeletal.BoneSet && !RequiresBoneGraft(skeletal))
            {
                step = SurgeryStepKind.SealBoneWithGel;
                return true;
            }
        }

        if (HasComp<CMBoneSetterComponent>(tool))
        {
            if (skeletal.BoneGelApplied && !skeletal.BoneSet)
            {
                step = SurgeryStepKind.SetBone;
                return true;
            }

            if (skeletal.BoneGelApplied &&
                skeletal.BoneSet &&
                skeletal.BoneGrafted &&
                RequiresBoneGraft(skeletal))
            {
                step = SurgeryStepKind.SetGraftedBone;
                return true;
            }
        }

        if (HasComp<CMUBoneGraftComponent>(tool) &&
            skeletal.BoneGelApplied &&
            skeletal.BoneSet &&
            !skeletal.BoneGrafted &&
            RequiresBoneGraft(skeletal))
        {
            step = SurgeryStepKind.ApplyBoneGraft;
            return true;
        }

        step = SurgeryStepKind.RepairFracture;
        return false;
    }

    private static TimeSpan GetBoneRepairDelay(SurgeryStepKind step)
    {
        return step is SurgeryStepKind.SetBone or SurgeryStepKind.SetGraftedBone
            ? TimeSpan.FromSeconds(4)
            : TimeSpan.FromSeconds(3);
    }

    private bool IsInternalBleedRepairTool(EntityUid tool)
    {
        return _tags.HasTag(tool, CMUFixOVeinTag) ||
            _tags.HasTag(tool, CMSurgicalLineTag) ||
            HasComp<RMCCableCoilComponent>(tool);
    }

    private bool IsAmputationCancelTool(EntityUid tool)
    {
        return _tags.HasTag(tool, CMUFixOVeinTag) ||
            _tags.HasTag(tool, CMSurgicalLineTag);
    }

    private bool IsSutureWoundTool(EntityUid tool)
    {
        return _tags.HasTag(tool, CMSurgicalLineTag) ||
            _tags.HasTag(tool, CMUFixOVeinTag) ||
            HasComp<RMCCableCoilComponent>(tool);
    }

    private bool IsStumpSealTool(EntityUid tool)
    {
        return HasComp<CMCauteryComponent>(tool) ||
            HasComp<CMHemostatComponent>(tool) ||
            _tags.HasTag(tool, CMUFixOVeinTag) ||
            _tags.HasTag(tool, CMSurgicalLineTag);
    }

    private bool IsBurnSurgeryTool(EntityUid tool)
    {
        return _tags.HasTag(tool, CMBurnKitTag) ||
            _tags.HasTag(tool, CMSynthGraftTag);
    }

    private bool IsForeignObjectRemovalTool(EntityUid tool)
    {
        return HasComp<CMHemostatComponent>(tool) ||
            HasComp<CMUShrapnelExtractorComponent>(tool);
    }

    private bool IsEscharRemovalTool(EntityUid tool)
    {
        return HasComp<CMScalpelComponent>(tool) ||
            HasComp<CMCauteryComponent>(tool) ||
            IsBurnSurgeryTool(tool);
    }

    private bool IsAttachableLimbSurgeryTool(EntityUid tool)
    {
        return TryComp<BodyPartComponent>(tool, out var part) &&
            part.Body == null &&
            IsAmputatableRegion(ResolvePartRegion(tool, part));
    }

    private bool IsProstheticRemovalTool(EntityUid tool)
    {
        return HasComp<CMBoneSawComponent>(tool) ||
            HasComp<CMSurgicalDrillComponent>(tool);
    }

    private bool IsCloseIncisionTool(EntityUid tool)
    {
        return HasComp<CMCauteryComponent>(tool) ||
            _tags.HasTag(tool, CMSurgicalLineTag);
    }

    private bool IsRetractTool(EntityUid tool)
    {
        return HasComp<CMRetractorComponent>(tool) ||
            HasComp<CMHemostatComponent>(tool) ||
            HasComp<CMScalpelComponent>(tool);
    }

    private bool IsDeepAccessTool(EntityUid tool)
    {
        return HasComp<CMBoneSawComponent>(tool) ||
            HasComp<CMSurgicalDrillComponent>(tool);
    }

    private bool IsBoneAccessClosureTool(EntityUid tool)
    {
        return HasComp<CMBoneGelComponent>(tool) ||
            HasComp<CMBoneSetterComponent>(tool) ||
            HasComp<CMUBoneGraftComponent>(tool);
    }

    private SurgeryToolRole GetToolRole(EntityUid tool)
    {
        if (TryComp<BodyPartComponent>(tool, out var bodyPart) &&
            bodyPart.Body == null)
        {
            return HasComp<CMUProstheticLimbComponent>(tool)
                ? SurgeryToolRole.FitProsthetic
                : SurgeryToolRole.AttachLimb;
        }
        if (HasComp<CMUIncisionManagementSystemComponent>(tool))
            return SurgeryToolRole.InitialIncisionShortcut;
        if (HasComp<CMScalpelComponent>(tool))
            return SurgeryToolRole.CutFlesh;
        if (HasComp<CMHemostatComponent>(tool))
            return SurgeryToolRole.ClampOrExtract;
        if (HasComp<CMUShrapnelExtractorComponent>(tool))
            return SurgeryToolRole.ClampOrExtract;
        if (HasComp<CMRetractorComponent>(tool))
            return SurgeryToolRole.Retract;
        if (HasComp<CMCauteryComponent>(tool))
            return SurgeryToolRole.CloseIncision;
        if (HasComp<CMBoneSawComponent>(tool))
            return SurgeryToolRole.CutBone;
        if (HasComp<CMBoneSetterComponent>(tool))
            return SurgeryToolRole.SetBone;
        if (HasComp<CMBoneGelComponent>(tool))
            return SurgeryToolRole.SealBone;
        if (HasComp<CMSurgicalDrillComponent>(tool))
            return SurgeryToolRole.Drill;
        if (HasComp<CMUBoneGraftComponent>(tool))
            return SurgeryToolRole.SealBone;
        if (_tags.HasTag(tool, CMUFixOVeinTag) ||
            HasComp<RMCCableCoilComponent>(tool))
        {
            return SurgeryToolRole.RepairVessel;
        }
        if (_tags.HasTag(tool, CMSurgicalLineTag))
            return SurgeryToolRole.SutureWound;
        if (_tags.HasTag(tool, CMTraumaKitTag) ||
            HasComp<CMUOrganClampComponent>(tool))
        {
            return SurgeryToolRole.RepairOrgan;
        }
        if (_tags.HasTag(tool, CMBurnKitTag))
            return SurgeryToolRole.TreatBurn;
        if (_tags.HasTag(tool, CMSynthGraftTag))
            return SurgeryToolRole.GraftBurn;

        return SurgeryToolRole.None;
    }

    private SurgeryToolQuality GetProstheticRemovalToolQuality(EntityUid tool)
    {
        if (HasComp<CMBoneSawComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMSurgicalDrillComponent>(tool))
            return SurgeryToolQuality.Suboptimal;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetIncisionToolQuality(EntityUid tool)
    {
        if (HasComp<CMScalpelComponent>(tool))
            return SurgeryToolQuality.Ideal;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetRetractToolQuality(EntityUid tool)
    {
        if (HasComp<CMRetractorComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMHemostatComponent>(tool))
            return SurgeryToolQuality.Suboptimal;
        if (HasComp<CMScalpelComponent>(tool))
            return SurgeryToolQuality.Awful;

        return SurgeryToolQuality.Substitute;
    }

    private SurgeryToolQuality GetDeepAccessToolQuality(EntityUid tool)
    {
        if (HasComp<CMBoneSawComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMSurgicalDrillComponent>(tool))
            return SurgeryToolQuality.Substitute;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetCloseIncisionToolQuality(EntityUid tool)
    {
        if (HasComp<CMCauteryComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMScalpelComponent>(tool))
            return SurgeryToolQuality.Suboptimal;

        return SurgeryToolQuality.BadSubstitute;
    }

    private SurgeryToolQuality GetInternalBleedRepairToolQuality(EntityUid tool)
    {
        if (_tags.HasTag(tool, CMUFixOVeinTag))
            return SurgeryToolQuality.Ideal;
        if (_tags.HasTag(tool, CMSurgicalLineTag))
            return SurgeryToolQuality.Substitute;
        if (HasComp<RMCCableCoilComponent>(tool))
            return SurgeryToolQuality.BadSubstitute;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetSutureWoundToolQuality(EntityUid tool)
    {
        if (_tags.HasTag(tool, CMSurgicalLineTag))
            return SurgeryToolQuality.Ideal;
        if (_tags.HasTag(tool, CMUFixOVeinTag))
            return SurgeryToolQuality.Suboptimal;
        if (HasComp<RMCCableCoilComponent>(tool))
            return SurgeryToolQuality.Substitute;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetForeignObjectRemovalToolQuality(EntityUid tool)
    {
        if (HasComp<CMHemostatComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMUShrapnelExtractorComponent>(tool))
            return SurgeryToolQuality.Suboptimal;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetEscharRemovalToolQuality(EntityUid tool)
    {
        if (HasComp<CMScalpelComponent>(tool))
            return SurgeryToolQuality.Ideal;
        if (_tags.HasTag(tool, CMBurnKitTag) ||
            _tags.HasTag(tool, CMSynthGraftTag))
        {
            return SurgeryToolQuality.Suboptimal;
        }
        if (HasComp<CMCauteryComponent>(tool))
            return SurgeryToolQuality.Substitute;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetOrganRepairToolQuality(EntityUid tool)
    {
        if (_tags.HasTag(tool, CMTraumaKitTag))
            return SurgeryToolQuality.Ideal;
        if (HasComp<CMUOrganClampComponent>(tool))
            return SurgeryToolQuality.Suboptimal;

        return SurgeryToolQuality.Awful;
    }

    private SurgeryToolQuality GetBoneRepairToolQuality(EntityUid tool)
    {
        if (HasComp<CMBoneGelComponent>(tool) ||
            HasComp<CMBoneSetterComponent>(tool) ||
            HasComp<CMUBoneGraftComponent>(tool))
        {
            return SurgeryToolQuality.Ideal;
        }

        if (HasComp<CMSurgicalDrillComponent>(tool))
            return SurgeryToolQuality.Substitute;
        if (HasComp<CMBoneSawComponent>(tool))
            return SurgeryToolQuality.BadSubstitute;

        return SurgeryToolQuality.Awful;
    }

    private static bool IsEncasedRegion(BodyRegion region)
    {
        return region is BodyRegion.Head or BodyRegion.Chest;
    }

    private static bool HasShallowAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return HumanMedicalLedger.GetRegion(medical, region).Incision >= IncisionDepth.Retracted;
    }

    private static bool HasRequiredRepairAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        return IsEncasedRegion(region)
            ? HasDeepAccess(medical, region)
            : HasShallowAccess(medical, region);
    }

    private static int GetRepairStepIndex(BodyRegion region)
    {
        return IsEncasedRegion(region) ? 3 : 2;
    }

    private static bool HasForeignObjectAccess(
        HumanMedicalComponent medical,
        BodyRegion region,
        ForeignObjectDepth depth)
    {
        if (depth < ForeignObjectDepth.Deep)
            return false;

        if (depth == ForeignObjectDepth.Surgical && IsEncasedRegion(region))
            return HasDeepAccess(medical, region);

        return HasShallowAccess(medical, region);
    }

    private static bool HasOrganAccess(HumanMedicalComponent medical, OrganState organ)
    {
        return organ.Region switch
        {
            BodyRegion.Head or BodyRegion.Chest =>
                HasDeepAccess(medical, organ.Region),
            BodyRegion.Groin =>
                HasShallowAccess(medical, organ.Region),
            _ => HasShallowAccess(medical, organ.Region),
        };
    }

    private static bool HasDeepAccess(HumanMedicalComponent medical, BodyRegion region)
    {
        var state = HumanMedicalLedger.GetRegion(medical, region);
        return state.Incision == IncisionDepth.DeepAccess ||
            HasFracturedBoneAccess(region, state);
    }

    private static bool HasFracturedBoneAccess(BodyRegion region, RegionState state)
    {
        return IsEncasedRegion(region) &&
            state.Incision >= IncisionDepth.Retracted &&
            state.Skeletal.Broken &&
            state.Skeletal.Severity.IsAtLeast(FractureSeverity.Compound);
    }

    private static bool RequiresBoneGraft(SkeletalState skeletal)
    {
        return skeletal.Severity.IsAtLeast(FractureSeverity.Shattered);
    }

    private bool HasRepairableProblem(
        EntityUid patient,
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        if (TryFindInternalBleed(medical, region, out _) ||
            TryFindDamagedOrgan(medical, region, out _) ||
            TryFindDamagedOrganSlot(medical, region, OrganSlot.Brain, out _) ||
            TryFindDamagedOrganSlot(medical, region, OrganSlot.Eyes, out _) ||
            TryFindOpenStump(medical, region, out _) ||
            HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, region, out _) ||
            region == BodyRegion.Chest && TryComp<VictimInfectedComponent>(patient, out var infected) && !infected.IsBursting ||
            _shrapnel.TryFindSurgicalShrapnelInRegion(patient, region, out _))
        {
            return true;
        }

        return HumanMedicalLedger.GetRegion(medical, region).Skeletal.Broken;
    }

    private static bool TryFindInternalBleed(
        HumanMedicalComponent medical,
        BodyRegion region,
        out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region != region ||
                source.Kind != BleedKind.Internal ||
                source.Treatment.HasFlag(TreatmentFlags.Closed) ||
                source.Treatment.HasFlag(TreatmentFlags.Sutured))
            {
                continue;
            }

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool HasActiveSurgicalBleed(
        HumanMedicalComponent medical,
        BodyRegion region)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region == region &&
                source.Kind == BleedKind.External &&
                source.Flags.HasFlag(BleedFlags.Surgical) &&
                source.Active)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindDamagedOrgan(
        HumanMedicalComponent medical,
        BodyRegion region,
        out OrganState organ)
    {
        for (var i = 1; i < medical.Organs.Length; i++)
        {
            organ = medical.Organs[i];
            if (organ.Region == region &&
                organ.Damage > 0 &&
                !organ.Missing &&
                organ.Slot is not OrganSlot.Brain and not OrganSlot.Eyes)
            {
                return true;
            }
        }

        organ = default;
        return false;
    }

    private static bool TryFindDamagedOrganSlot(
        HumanMedicalComponent medical,
        BodyRegion region,
        OrganSlot slot,
        out OrganState organ)
    {
        organ = HumanMedicalLedger.GetOrgan(medical, slot);
        return organ.Slot == slot &&
            organ.Region == region &&
            organ.Damage > 0 &&
            !organ.Missing;
    }

    private static bool TryFindOpenStump(
        HumanMedicalComponent medical,
        BodyRegion region,
        out InjuryRecord stump)
    {
        foreach (var injury in medical.Injuries)
        {
            if (injury.Region != region ||
                !injury.IsOpenStump)
            {
                continue;
            }

            stump = injury;
            return true;
        }

        stump = default;
        return false;
    }

    private static bool TryFindOpenStump(HumanMedicalComponent medical, BodyRegion region)
    {
        return TryFindOpenStump(medical, region, out _);
    }

    private static bool HasOpenStump(HumanMedicalComponent medical, BodyRegion region)
    {
        return TryFindOpenStump(medical, region, out _);
    }

    private static bool TryGetLinkedStumpSurgeryRegion(
        HumanMedicalComponent medical,
        BodyRegion selectedRegion,
        out BodyRegion stumpRegion)
    {
        stumpRegion = LimbLossRules.GetStumpAnchorRegion(selectedRegion);
        if (stumpRegion == BodyRegion.None ||
            stumpRegion == selectedRegion)
        {
            stumpRegion = BodyRegion.None;
            return false;
        }

        var state = HumanMedicalLedger.GetRegion(medical, stumpRegion);
        if (state.Incision != IncisionDepth.Closed ||
            HasOpenStump(medical, stumpRegion))
        {
            return true;
        }

        stumpRegion = BodyRegion.None;
        return false;
    }

    private static bool HasOpenStumpForMissingRegion(
        HumanMedicalComponent medical,
        BodyRegion missingRegion)
    {
        var stumpRegion = LimbLossRules.GetStumpAnchorRegion(missingRegion);
        return stumpRegion != BodyRegion.None &&
            HasOpenStump(medical, stumpRegion);
    }

    private static int FindBleedSourceForInjury(HumanMedicalComponent medical, InjuryRecord injury)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.SourceInjuryId == injury.Id ||
                source.Region == injury.Region && source.Kind == BleedKind.Stump)
            {
                return source.Id;
            }
        }

        return 0;
    }

    private bool TryApplySurgicalAmputation(EntityUid patient, BodyRegion region)
    {
        if (!TryGetBodyPartForRegion(patient, region, out var partUid, out var part))
            return false;

        var ev = new BodyPartSeveredEvent(patient, partUid, part.PartType);
        RaiseLocalEvent(partUid, ref ev);

        return TryComp<HumanMedicalComponent>(patient, out var medical) &&
            HumanMedicalLedger.GetRegion(medical, region).Presence == LimbPresence.Missing;
    }

    private bool TryApplySurgicalLimbAttachment(
        EntityUid patient,
        EntityUid limb,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        bool prosthetic)
    {
        if (!TryResolveSurgeryLimbTool(limb, attempt.Region, out var limbPart, out var limbIsProsthetic) ||
            limbIsProsthetic != prosthetic ||
            !TryGetAttachmentTarget(patient, attempt.Region, out var parentUid, out var slotId, out var parentPart))
        {
            return false;
        }

        if (prosthetic && !EnsureProstheticChildExtremity(limb, limbPart))
            return false;

        var affectedRegions = new List<BodyRegion>(4);
        var transaction = new MedicalTransaction(attempt.Region);
        var startingHpFraction = Math.Clamp(
            _cfg.GetCVar(CMUMedicalCCVars.SurgeryLimbReattachStartingHpFraction),
            0f,
            1f);
        var startingDamage = ReattachedLimbDamageCapacity * FixedPoint2.New(1f - startingHpFraction);

        foreach (var (attachedPartUid, attachedPart) in _body.GetBodyPartChildren(limb, limbPart))
        {
            var attachedRegion = ResolvePartRegion(attachedPartUid, attachedPart);
            if (!IsAmputatableRegion(attachedRegion))
                continue;

            var attachedProsthetic = HasComp<CMUProstheticLimbComponent>(attachedPartUid);
            transaction.Add(MedicalEffect.SetRegionPresence(
                attachedRegion,
                attachedProsthetic ? LimbPresence.Prosthetic : LimbPresence.Present));

            var stumpRegion = LimbLossRules.GetStumpAnchorRegion(attachedRegion);
            if (stumpRegion != BodyRegion.None)
                transaction.Add(MedicalEffect.CloseStumpRecords(stumpRegion));

            if (!attachedProsthetic)
            {
                transaction.Add(MedicalEffect.MarkDetachedLimbReattached(attachedRegion));
                if (startingDamage > FixedPoint2.Zero)
                    transaction.Add(MedicalEffect.AddRegionDamage(attachedRegion, startingDamage, FixedPoint2.Zero));
            }

            affectedRegions.Add(attachedRegion);
        }

        if (affectedRegions.Count == 0)
            return false;

        if (!_body.AttachPart(parentUid, slotId, limb, parentPart, limbPart))
            return false;

        var result = _humanMedical.ApplyTransaction((patient, medical), transaction);
        if (!result.Applied)
            return false;

        foreach (var region in affectedRegions)
        {
            RemoveMissingLimbStatus(patient, region);
        }

        return true;
    }

    private bool EnsureProstheticChildExtremity(EntityUid limb, BodyPartComponent limbPart)
    {
        if (ProstheticChildExtremity(limbPart.PartType, limbPart.Symmetry) is not { } childInfo)
            return true;

        if (TryGetChildBodyPart(limb, childInfo.Slot, childInfo.Type, out var existingChild, out _))
        {
            EnsureComp<CMUProstheticLimbComponent>(existingChild);
            return true;
        }

        var childUid = Spawn(childInfo.Prototype, Transform(limb).Coordinates);
        if (!TryComp<BodyPartComponent>(childUid, out var childPart))
        {
            QueueDel(childUid);
            return false;
        }

        EnsureComp<CMUProstheticLimbComponent>(childUid);

        var attached = _body.AttachPart(limb, childInfo.Slot, childUid, limbPart, childPart) ||
            _body.TryCreatePartSlotAndAttach(limb, childInfo.Slot, childUid, childInfo.Type, limbPart, childPart);

        if (!attached)
            QueueDel(childUid);

        return attached;
    }

    private bool TryGetChildBodyPart(
        EntityUid parent,
        string slotId,
        BodyPartType expectedType,
        out EntityUid child,
        out BodyPartComponent childPart)
    {
        child = default;
        childPart = default!;

        if (!_containers.TryGetContainer(parent, SharedBodySystem.GetPartSlotContainerId(slotId), out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (!TryComp<BodyPartComponent>(contained, out var containedPart) ||
                containedPart.PartType != expectedType)
            {
                continue;
            }

            child = contained;
            childPart = containedPart;
            return true;
        }

        return false;
    }

    private static (string Slot, BodyPartType Type, EntProtoId Prototype)? ProstheticChildExtremity(
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Arm, BodyPartSymmetry.Left) =>
                (Slot: "left_hand", Type: BodyPartType.Hand, Prototype: RoboticLeftHandPrototype),
            (BodyPartType.Arm, BodyPartSymmetry.Right) =>
                (Slot: "right_hand", Type: BodyPartType.Hand, Prototype: RoboticRightHandPrototype),
            (BodyPartType.Leg, BodyPartSymmetry.Left) =>
                (Slot: "left_foot", Type: BodyPartType.Foot, Prototype: RoboticLeftFootPrototype),
            (BodyPartType.Leg, BodyPartSymmetry.Right) =>
                (Slot: "right_foot", Type: BodyPartType.Foot, Prototype: RoboticRightFootPrototype),
            _ => null,
        };
    }

    private bool TryApplySurgicalProstheticRemoval(
        EntityUid patient,
        HumanMedicalComponent medical,
        SurgeryAttempt attempt,
        EntityUid surgeon)
    {
        _ = surgeon;

        if (!TryGetBodyPartForRegion(patient, attempt.Region, out var partUid, out _) ||
            !HasComp<CMUProstheticLimbComponent>(partUid) ||
            !_containers.TryGetContainingContainer((partUid, null, null), out var container))
        {
            return false;
        }

        var transaction = new MedicalTransaction(attempt.Region);
        transaction.Add(MedicalEffect.SetRegionPresence(attempt.Region, LimbPresence.Missing));
        var result = _humanMedical.ApplyTransaction((patient, medical), transaction);
        if (!result.Applied)
            return false;

        if (!_containers.Remove(partUid, container))
        {
            var rollback = new MedicalTransaction(attempt.Region);
            rollback.Add(MedicalEffect.SetRegionPresence(attempt.Region, LimbPresence.Prosthetic));
            _humanMedical.ApplyTransaction((patient, medical), rollback);
            return false;
        }

        _transform.SetCoordinates(partUid, Transform(patient).Coordinates);
        _transform.AttachToGridOrMap(partUid);
        ApplyMissingLimbStatus(patient, attempt.Region);
        return true;
    }

    private bool TryResolveSurgeryLimbTool(
        EntityUid limb,
        BodyRegion region,
        out BodyPartComponent part,
        out bool prosthetic)
    {
        part = default!;
        prosthetic = false;
        if (!TryComp<BodyPartComponent>(limb, out var limbPart) ||
            limbPart.Body != null ||
            ResolvePartRegion(limb, limbPart) != region)
        {
            return false;
        }

        part = limbPart;
        prosthetic = HasComp<CMUProstheticLimbComponent>(limb);
        return true;
    }

    private bool TryGetAttachmentTarget(
        EntityUid patient,
        BodyRegion region,
        out EntityUid parentUid,
        out string slotId,
        out BodyPartComponent parentPart)
    {
        parentUid = default;
        slotId = string.Empty;
        parentPart = default!;

        var parentRegion = ParentRegionForAttachedRegion(region);
        if (parentRegion == BodyRegion.None ||
            SlotForAttachedRegion(region) is not { } targetSlot ||
            !TryGetBodyPartForRegion(patient, parentRegion, out parentUid, out parentPart))
        {
            return false;
        }

        slotId = targetSlot;
        return true;
    }

    private bool TryGetBodyPartForRegion(
        EntityUid patient,
        BodyRegion region,
        out EntityUid partUid,
        out BodyPartComponent part)
    {
        foreach (var (candidateUid, candidatePart) in _body.GetBodyChildren(patient))
        {
            if (ResolvePartRegion(candidateUid, candidatePart) != region)
                continue;

            partUid = candidateUid;
            part = candidatePart;
            return true;
        }

        partUid = default;
        part = default!;
        return false;
    }

    private static bool IsAmputatableRegion(BodyRegion region)
    {
        return region is
            BodyRegion.LeftArm or
            BodyRegion.RightArm or
            BodyRegion.LeftHand or
            BodyRegion.RightHand or
            BodyRegion.LeftLeg or
            BodyRegion.RightLeg or
            BodyRegion.LeftFoot or
            BodyRegion.RightFoot;
    }

    private void ClearEscharPartMarker(EntityUid patient, BodyRegion region)
    {
        foreach (var (partUid, part) in _body.GetBodyChildren(patient))
        {
            if (ResolvePartRegion(partUid, part) != region)
                continue;

            RemComp<CMUEscharComponent>(partUid);
        }
    }

    private BodyRegion ResolvePartRegion(EntityUid partUid, BodyPartComponent part)
    {
        if (TryComp<AnatomyRegionComponent>(partUid, out var anatomy) &&
            anatomy.Region != BodyRegion.None)
        {
            return anatomy.Region;
        }

        return RegionForPart(part.PartType, part.Symmetry);
    }

    private static BodyRegion RegionForPart(BodyPartType type, BodyPartSymmetry symmetry)
    {
        return (type, symmetry) switch
        {
            (BodyPartType.Head, _) => BodyRegion.Head,
            (BodyPartType.Torso, _) => BodyRegion.Chest,
            (BodyPartType.Arm, BodyPartSymmetry.Left) => BodyRegion.LeftArm,
            (BodyPartType.Arm, BodyPartSymmetry.Right) => BodyRegion.RightArm,
            (BodyPartType.Hand, BodyPartSymmetry.Left) => BodyRegion.LeftHand,
            (BodyPartType.Hand, BodyPartSymmetry.Right) => BodyRegion.RightHand,
            (BodyPartType.Leg, BodyPartSymmetry.Left) => BodyRegion.LeftLeg,
            (BodyPartType.Leg, BodyPartSymmetry.Right) => BodyRegion.RightLeg,
            (BodyPartType.Foot, BodyPartSymmetry.Left) => BodyRegion.LeftFoot,
            (BodyPartType.Foot, BodyPartSymmetry.Right) => BodyRegion.RightFoot,
            _ => BodyRegion.None,
        };
    }

    private static BodyRegion ParentRegionForAttachedRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.LeftArm or BodyRegion.RightArm => BodyRegion.Chest,
            BodyRegion.LeftHand => BodyRegion.LeftArm,
            BodyRegion.RightHand => BodyRegion.RightArm,
            BodyRegion.LeftLeg or BodyRegion.RightLeg => BodyRegion.Chest,
            BodyRegion.LeftFoot => BodyRegion.LeftLeg,
            BodyRegion.RightFoot => BodyRegion.RightLeg,
            _ => BodyRegion.None,
        };
    }

    private static string? SlotForAttachedRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.LeftArm => "left_arm",
            BodyRegion.RightArm => "right_arm",
            BodyRegion.LeftHand => "left_hand",
            BodyRegion.RightHand => "right_hand",
            BodyRegion.LeftLeg => "left_leg",
            BodyRegion.RightLeg => "right_leg",
            BodyRegion.LeftFoot => "left_foot",
            BodyRegion.RightFoot => "right_foot",
            _ => null,
        };
    }

    private void ApplyMissingLimbStatus(EntityUid body, BodyRegion region)
    {
        if (StatusForRegion(region) is { } statusProto)
            _status.TrySetStatusEffectDuration(body, statusProto, duration: null);
    }

    private void RemoveMissingLimbStatus(EntityUid body, BodyRegion region)
    {
        if (StatusForRegion(region) is { } statusProto)
            _status.TryRemoveStatusEffect(body, statusProto);
    }

    private static EntProtoId? StatusForRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.LeftArm => "StatusEffectCMUMissingArmLeft",
            BodyRegion.RightArm => "StatusEffectCMUMissingArmRight",
            BodyRegion.LeftHand => "StatusEffectCMUMissingHandLeft",
            BodyRegion.RightHand => "StatusEffectCMUMissingHandRight",
            BodyRegion.LeftLeg => "StatusEffectCMUMissingLegLeft",
            BodyRegion.RightLeg => "StatusEffectCMUMissingLegRight",
            BodyRegion.LeftFoot => "StatusEffectCMUMissingFootLeft",
            BodyRegion.RightFoot => "StatusEffectCMUMissingFootRight",
            _ => null,
        };
    }

    private static BodyRegion[] RegionsForZone(TargetBodyZone zone)
    {
        return zone switch
        {
            TargetBodyZone.Head => HeadRegions,
            TargetBodyZone.Chest => ChestRegions,
            TargetBodyZone.GroinPelvis => GroinRegions,
            TargetBodyZone.LeftArm => LeftArmRegions,
            TargetBodyZone.RightArm => RightArmRegions,
            TargetBodyZone.LeftHand => LeftHandRegions,
            TargetBodyZone.RightHand => RightHandRegions,
            TargetBodyZone.LeftLeg => LeftLegRegions,
            TargetBodyZone.RightLeg => RightLegRegions,
            TargetBodyZone.LeftFoot => LeftFootRegions,
            TargetBodyZone.RightFoot => RightFootRegions,
            _ => ChestRegions,
        };
    }
}
