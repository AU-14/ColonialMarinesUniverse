using System.Collections.Generic;
using System.Linq;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Surgery;

public abstract class SharedCMUSurgeryFlowSystem : EntitySystem
{
    [Dependency] protected readonly IConfigurationManager Cfg = default!;
    [Dependency] protected readonly INetManager Net = default!;
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] protected readonly SharedBodySystem Body = default!;
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected readonly SharedHandsSystem Hands = default!;
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] protected readonly SharedUserInterfaceSystem UserInterface = default!;
    [Dependency] protected readonly SharedCMSurgerySystem RmcSurgery = default!;

    private readonly Dictionary<string, CMUSurgeryStepMetadataPrototype> _bySurgery = new();

    private readonly Dictionary<string, Type[]> _toolCategories = new();

    private const float ArmedStepScanInterval = 0.5f;
    private float _armedStepScanAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        BuildToolCategoryTable();
        IndexMetadata();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, InteractUsingEvent>(OnArmedInteractUsing);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, InteractHandEvent>(OnArmedInteractHand);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, CMUSurgeryStepDoAfterEvent>(OnStepDoAfter);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUSurgeryStepMetadataPrototype>())
            IndexMetadata();
    }

    private void IndexMetadata()
    {
        _bySurgery.Clear();
        foreach (var proto in Prototypes.EnumeratePrototypes<CMUSurgeryStepMetadataPrototype>())
        {
            _bySurgery[proto.Surgery] = proto;
        }
    }

    private void BuildToolCategoryTable()
    {
        _toolCategories.Clear();

        _toolCategories["scalpel"] = new[] { typeof(CMScalpelComponent) };
        _toolCategories["hemostat"] = new[] { typeof(CMHemostatComponent) };
        _toolCategories["retractor"] = new[] { typeof(CMRetractorComponent) };
        _toolCategories["cautery"] = new[] { typeof(CMCauteryComponent) };
        _toolCategories["bone_saw"] = new[] { typeof(CMBoneSawComponent), typeof(CMSurgicalDrillComponent) };
        _toolCategories["bone_setter"] = new[] { typeof(CMBoneSetterComponent) };
        _toolCategories["bone_gel"] = new[] { typeof(CMBoneGelComponent) };
        _toolCategories["bone_graft"] = new[] { typeof(CMUBoneGraftComponent) };
        _toolCategories["organ_clamp"] = new[] { typeof(CMUOrganClampComponent) };
        // Resolver only checks "is this a BodyPart" — the matching-symmetry
        // check (right leg slot ↔ right leg part) lives in
        // OnArmedInteractUsing's reattach-surgery branch.
        _toolCategories["severed_limb"] = new[] { typeof(BodyPartComponent) };
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Server drives expire; the armed component is networked so the
        // server's deletion mirrors over to the client.
        if (!Net.IsServer)
            return;

        _armedStepScanAccumulator += frameTime;
        if (_armedStepScanAccumulator < ArmedStepScanInterval)
            return;
        _armedStepScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<CMUSurgeryArmedStepComponent>();
        while (query.MoveNext(out var uid, out var armed))
        {
            if (now - armed.ArmedAt < armed.ExpireAfter)
                continue;

            ClearArmed(uid, armed, expired: true);
        }
    }

    public CMUSurgeryArmedStepComponent? TryArmStep(EntityUid surgeon, EntityUid patient, EntityUid targetPart, string surgeryId, int stepIndex, BodyPartType? fallbackType = null, BodyPartSymmetry? fallbackSymmetry = null)
    {
        // Reattach targets the patient body — no BodyPartComponent there,
        // so dispatch supplies the slot type/symmetry as a fallback.
        BodyPartType armedType;
        BodyPartSymmetry armedSymmetry;
        if (TryComp<BodyPartComponent>(targetPart, out var partComp))
        {
            armedType = partComp.PartType;
            armedSymmetry = partComp.Symmetry;
        }
        else if (fallbackType is { } t && fallbackSymmetry is { } s)
        {
            armedType = t;
            armedSymmetry = s;
        }
        else
        {
            return null;
        }

        // Patient-level lock: only one in-flight surgery per patient. A
        // mismatch refuses the arm so the BUI can surface "finish or abandon"
        // instead of silently switching surgeries.
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
        {
            if (lockComp.Part != targetPart || lockComp.LeafSurgeryId != surgeryId)
                return null;
        }

        if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var existing)
            && existing.LeafSurgeryId == surgeryId)
        {
            existing.Surgeon = surgeon;
            existing.ArmedAt = Timing.CurTime;
            Dirty(patient, existing);
            return existing;
        }

        // Resolve via the requirement chain so prereqs (open-incision,
        // open-ribcage, etc.) can't be skipped. Legacy RMC prereqs without
        // a CMU metadata entry get a synthesized label from the step proto.
        if (!TryResolveNextStep(patient, targetPart, surgeryId, out var resolved))
            return null;

        var armed = EnsureComp<CMUSurgeryArmedStepComponent>(patient);
        armed.Surgeon = surgeon;
        // SurgeryId = resolved (drives V1 step-event raise in RunStepEffect).
        // LeafSurgeryId = what the medic picked (drives BUI display).
        armed.SurgeryId = resolved.ResolvedSurgeryId;
        armed.StepIndex = resolved.StepIndex;
        armed.TargetPartType = armedType;
        armed.TargetSymmetry = armedSymmetry;
        armed.RequiredToolCategory = resolved.ToolCategory;
        armed.StepLabel = resolved.StepLabel;
        armed.LeafSurgeryId = surgeryId;
        armed.ArmedAt = Timing.CurTime;
        Dirty(patient, armed);
        return armed;
    }

    public void EnsureSurgeryInFlight(EntityUid patient, EntityUid part, EntityUid surgeon, string leafSurgeryId, string leafDisplayName)
    {
        var lockComp = EnsureComp<CMUSurgeryInProgressComponent>(patient);
        var alreadyInFlight = lockComp.LeafSurgeryId == leafSurgeryId && lockComp.Part == part;
        lockComp.Part = part;
        lockComp.LeafSurgeryId = leafSurgeryId;
        Dirty(patient, lockComp);

        var inFlight = EnsureComp<CMUSurgeryInFlightComponent>(part);
        inFlight.LeafSurgeryId = leafSurgeryId;
        inFlight.LeafSurgeryDisplayName = leafDisplayName;
        inFlight.Surgeon = surgeon;
        inFlight.SurgeonName = Name(surgeon);
        if (!alreadyInFlight)
            inFlight.StartedAt = Timing.CurTime;
        Dirty(part, inFlight);
    }

    public void ClearSurgeryInFlight(EntityUid patient)
    {
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
        {
            if (lockComp.Part.IsValid() && HasComp<CMUSurgeryInFlightComponent>(lockComp.Part))
                RemComp<CMUSurgeryInFlightComponent>(lockComp.Part);
            RemComp<CMUSurgeryInProgressComponent>(patient);
        }
    }

    public void ClearArmed(EntityUid patient, CMUSurgeryArmedStepComponent? armed = null, bool expired = false)
    {
        if (!Resolve(patient, ref armed, false))
            return;

        var surgeon = armed.Surgeon;
        RemComp<CMUSurgeryArmedStepComponent>(patient);

        if (Net.IsServer && surgeon.IsValid())
        {
            var msg = expired
                ? "cmu-medical-surgery-armed-expired"
                : "cmu-medical-surgery-armed-cancelled";
            Popup.PopupEntity(Loc.GetString(msg), surgeon, surgeon, PopupType.SmallCaution);
        }
    }

    private void OnArmedInteractUsing(Entity<CMUSurgeryArmedStepComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var (patient, armed) = ent;

        if (args.User != armed.Surgeon)
            return;

        var isRightTool = ToolMatchesCategory(args.Used, armed.RequiredToolCategory);
        var hasWrongDamage = TryGetWrongToolDamage(args.Used, out var damageType, out var amount);

        // Non-surgery items (analyzer, bandage, meds, etc.) pass through
        // so the medic can still treat the patient between steps.
        if (!isRightTool && !hasWrongDamage)
            return;

        if (!TryFindClickedPart(patient, args.Target, armed.TargetPartType, armed.TargetSymmetry, out _)
            && !IsReattachOnPatientBody(patient, args.Target, armed))
        {
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-part"), patient, args.User, PopupType.SmallCaution);
            args.Handled = true;
            return;
        }

        if (isRightTool)
        {
            if (armed.RequiredToolCategory == "severed_limb"
                && !LimbMatchesAnyMissingSlot(patient, args.Used))
            {
                Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-limb"), patient, args.User, PopupType.SmallCaution);
                args.Handled = true;
                return;
            }

            if (Net.IsServer)
                StartStepDoAfter(patient, armed, args.User, args.Used);
            args.Handled = true;
            return;
        }

        ApplyWrongToolDamage(args.User, patient, args.Used, damageType, amount);
        ClearArmed(patient, armed);
        args.Handled = true;
    }

    private bool IsReattachOnPatientBody(EntityUid patient, EntityUid? clickTarget, CMUSurgeryArmedStepComponent armed)
    {
        if (armed.LeafSurgeryId != "CMUSurgeryReattachLimb")
            return false;
        return clickTarget == patient;
    }

    private void OnArmedInteractHand(Entity<CMUSurgeryArmedStepComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var (patient, armed) = ent;

        if (args.User != armed.Surgeon)
            return;

        Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-no-tool"), patient, args.User, PopupType.SmallCaution);
        ClearArmed(patient, armed);
        args.Handled = true;
    }

    /// <summary>
    ///     Override in the sealed server class so prediction rollback can't
    ///     re-raise the step event on the client.
    /// </summary>
    protected virtual void StartStepDoAfter(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid tool)
    {
    }

    protected virtual void ApplyWrongToolDamage(EntityUid surgeon, EntityUid patient, EntityUid tool, string damageType, float amount)
    {
    }

    /// <summary>
    ///     Server-only — raises V1 <c>CMSurgeryStepEvent</c> + either re-arms
    ///     or raises <c>CMSurgeryCompleteEvent</c>. Shared no-ops so
    ///     prediction rollback can't double-apply state mutations.
    /// </summary>
    protected virtual void RunStepEffect(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon)
    {
    }

    private void OnStepDoAfter(Entity<CMUSurgeryArmedStepComponent> ent, ref CMUSurgeryStepDoAfterEvent args)
    {
        var (patient, armed) = ent;

        if (args.Cancelled)
        {
            ClearArmed(patient, armed);
            return;
        }

        if (args.Handled)
            return;
        args.Handled = true;

        // Surgeon may have switched targets via a re-arm before the DoAfter
        // resolved.
        if (armed.Surgeon != args.User)
            return;

        if (armed.SurgeryId != args.SurgeryId || armed.StepIndex != args.StepIndex)
            return;

        if (Net.IsServer)
            RunStepEffect(patient, armed, args.User);
    }

    public bool TryGetMetadata(string surgeryId, out CMUSurgeryStepMetadataPrototype metadata)
    {
        return _bySurgery.TryGetValue(surgeryId, out metadata!);
    }

    public IEnumerable<CMUSurgeryStepMetadataPrototype> EnumerateMetadata()
    {
        return _bySurgery.Values;
    }

    /// <summary>
    ///     Walks RMC's <c>GetNextStep</c> — honours the <c>Requirement</c>
    ///     chain so picking "Set Compound Fracture" arms open-incision first
    ///     when the part isn't yet incised.
    /// </summary>
    public bool TryResolveNextStep(EntityUid patient, EntityUid? targetPart, string surgeryId, out CMUResolvedStep resolved)
    {
        resolved = default!;
        if (targetPart is null)
            return false;
        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;

        var next = RmcSurgery.GetNextStep(patient, targetPart.Value, surgeryEnt);
        if (next is null)
            return false; // surgery already complete on this part — nothing to arm.

        var (resolvedSurgery, stepIdx) = next.Value;
        var resolvedSurgeryProtoId = MetaData(resolvedSurgery.Owner).EntityPrototype?.ID;
        if (resolvedSurgeryProtoId is null)
            return false;

        var totalSteps = resolvedSurgery.Comp.Steps.Count;

        var stepLabel = string.Empty;
        string? toolCategory = null;

        if (TryGetMetadata(resolvedSurgeryProtoId, out var metadata) && stepIdx < metadata.Steps.Count)
        {
            var stepMeta = metadata.Steps[stepIdx];
            stepLabel = stepMeta.Label;
            toolCategory = stepMeta.ToolCategory;
        }
        else
        {
            // Fall back to the step entity's prototype name + a heuristic
            // tool category from the step's tool registry.
            var stepProtoId = resolvedSurgery.Comp.Steps[stepIdx];
            if (RmcSurgery.GetSingleton(stepProtoId) is { } stepEnt)
            {
                stepLabel = MetaData(stepEnt).EntityName;
                toolCategory = ResolveLegacyStepToolCategory(stepEnt);
            }
        }

        resolved = new CMUResolvedStep(
            resolvedSurgeryProtoId,
            stepIdx,
            stepLabel,
            toolCategory,
            totalSteps,
            // Gating prereq id only when the leaf surgery isn't the one
            // being armed — lets the BUI flag "(via Open Incision)".
            resolvedSurgeryProtoId == surgeryId ? null : resolvedSurgeryProtoId);
        return true;
    }

    public bool TryResolveStepAt(string surgeryId, int stepIndex, out CMUResolvedStep resolved)
    {
        resolved = default!;
        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;
        if (!TryComp<CMSurgeryComponent>(surgeryEnt, out var surgeryComp))
            return false;
        if (stepIndex < 0 || stepIndex >= surgeryComp.Steps.Count)
            return false;

        var stepLabel = string.Empty;
        string? toolCategory = null;

        if (TryGetMetadata(surgeryId, out var metadata) && stepIndex < metadata.Steps.Count)
        {
            var stepMeta = metadata.Steps[stepIndex];
            stepLabel = stepMeta.Label;
            toolCategory = stepMeta.ToolCategory;
        }
        else
        {
            var stepProtoId = surgeryComp.Steps[stepIndex];
            if (RmcSurgery.GetSingleton(stepProtoId) is { } stepEnt)
            {
                stepLabel = MetaData(stepEnt).EntityName;
                toolCategory = ResolveLegacyStepToolCategory(stepEnt);
            }
        }

        resolved = new CMUResolvedStep(
            surgeryId,
            stepIndex,
            stepLabel,
            toolCategory,
            surgeryComp.Steps.Count,
            null);
        return true;
    }

    protected string? ResolveLegacyStepToolCategory(EntityUid stepEnt)
    {
        if (!TryComp<CMSurgeryStepComponent>(stepEnt, out var stepComp) || stepComp.Tool is null)
            return null;

        foreach (var (_, reg) in stepComp.Tool)
        {
            if (reg.Component is null)
                continue;
            var componentType = reg.Component.GetType();

            foreach (var (categoryName, categoryTypes) in _toolCategories)
            {
                foreach (var t in categoryTypes)
                {
                    if (t == componentType)
                        return categoryName;
                }
            }
        }
        return null;
    }

    public bool TryFindClickedPart(EntityUid patient, EntityUid? clickTarget, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid part)
    {
        part = default;

        if (clickTarget is { } direct
            && TryComp<BodyPartComponent>(direct, out var directBp)
            && directBp.PartType == type
            && directBp.Symmetry == symmetry)
        {
            part = direct;
            return true;
        }

        foreach (var (childId, childComp) in Body.GetBodyChildren(patient))
        {
            if (childComp.PartType != type || childComp.Symmetry != symmetry)
                continue;
            part = childId;
            return true;
        }

        return false;
    }

    public bool LimbMatchesAnyMissingSlot(EntityUid patient, EntityUid heldLimb)
    {
        if (!TryComp<BodyPartComponent>(heldLimb, out var heldBp))
            return false;
        if (heldBp.PartType is not (BodyPartType.Arm or BodyPartType.Leg))
            return false;

        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return false;
        if (Body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return false;

        var heldSide = heldBp.Symmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => null,
        };
        if (heldSide is null)
            return false;

        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type != heldBp.PartType)
                continue;
            // Slot id encodes side — left_arm / right_leg / etc.
            if (!slotId.Contains(heldSide, System.StringComparison.Ordinal))
                continue;
            // Accept the matching slot — if it's filled, the attach call
            // no-ops with a "slot occupied" popup, which is the right UX.
            return true;
        }

        return false;
    }

    public bool ToolMatchesCategory(EntityUid tool, string? category)
    {
        if (category is null)
            return true;
        if (!_toolCategories.TryGetValue(category, out var componentTypes))
            return false;

        foreach (var ct in componentTypes)
        {
            if (HasComp(tool, ct))
                return true;
        }
        return false;
    }

    public bool TryGetWrongToolDamage(EntityUid tool, out string damageType, out float amount)
    {
        foreach (var (componentType, dmgType, amt) in CMUWrongToolDamageTable.Entries)
        {
            if (!HasComp(tool, componentType))
                continue;
            damageType = dmgType;
            amount = amt;
            return true;
        }
        damageType = string.Empty;
        amount = 0f;
        return false;
    }

    public CMUSurgeryBuiState BuildBuiState(EntityUid patient, string patientName, List<CMUSurgeryPartEntry> parts, CMUSurgeryArmedStepComponent? armed)
    {
        CMUArmedStepInfo? armedInfo = null;
        if (armed is not null)
        {
            // Surface the leaf the medic picked — SurgeryId may differ when
            // a prereq is currently being run.
            var leafId = string.IsNullOrEmpty(armed.LeafSurgeryId) ? armed.SurgeryId : armed.LeafSurgeryId;
            string leafDisplayName = ResolveSurgeryDisplayName(leafId);
            armedInfo = new CMUArmedStepInfo(armed.SurgeryId, leafDisplayName, armed.StepIndex, armed.StepLabel, armed.RequiredToolCategory);
        }

        CMUSurgeryInFlightInfo? inFlight = null;
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp)
            && TryComp<CMUSurgeryInFlightComponent>(lockComp.Part, out var flight))
        {
            var partDisplay = string.Empty;
            if (TryComp<BodyPartComponent>(lockComp.Part, out var partComp))
                partDisplay = FormatPartName(partComp.PartType, partComp.Symmetry);
            inFlight = new CMUSurgeryInFlightInfo(
                GetNetEntity(lockComp.Part),
                partDisplay,
                flight.LeafSurgeryDisplayName,
                flight.SurgeonName,
                flight.StartedAt);
        }

        return new CMUSurgeryBuiState(GetNetEntity(patient), patientName, parts, armedInfo, inFlight);
    }

    public string ResolveSurgeryDisplayName(string surgeryId)
    {
        if (TryGetMetadata(surgeryId, out var metadata))
            return metadata.DisplayName ?? surgeryId;
        if (Prototypes.TryIndex<EntityPrototype>(surgeryId, out var proto))
            return proto.Name;
        return surgeryId;
    }

    public static string FormatPartName(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "Left ",
            BodyPartSymmetry.Right => "Right ",
            _ => string.Empty,
        };
        return side + type;
    }
}
