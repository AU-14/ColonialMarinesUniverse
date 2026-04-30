using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUSurgeryFlowSystem : SharedCMUSurgeryFlowSystem
{
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly CMUSurgeryDispatchSystem _dispatch = default!;

    private const float StepDoAfterSeconds = 2f;

    protected override void StartStepDoAfter(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid tool)
    {
        var ev = new CMUSurgeryStepDoAfterEvent(armed.SurgeryId, armed.StepIndex);
        var doAfter = new DoAfterArgs(EntityManager, surgeon, StepDoAfterSeconds, ev, patient, patient, tool)
        {
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            NeedHand = true,
        };
        DoAfter.TryStartDoAfter(doAfter);
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

    protected override void RunStepEffect(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon)
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

        EntityUid stepPart = patient;
        if (TryFindClickedPart(patient, null, armed.TargetPartType, armed.TargetSymmetry, out var foundPart))
            stepPart = foundPart;

        var tools = new List<EntityUid>();
        foreach (var held in Hands.EnumerateHeld(surgeon))
            tools.Add(held);

        var stepEvent = new CMSurgeryStepEvent(surgeon, patient, stepPart, tools);
        RaiseLocalEvent(stepEnt, ref stepEvent);

        // Idempotent on subsequent steps, but EnsureSurgeryInFlight
        // refreshes the surgeon snapshot each time so a fresh surgeon
        // picking up an abandoned-but-armed surgery is credited as the
        // new operator.
        var leafId = string.IsNullOrEmpty(armed.LeafSurgeryId) ? armed.SurgeryId : armed.LeafSurgeryId;
        var leafDisplay = ResolveLeafDisplayName(leafId);
        EnsureSurgeryInFlight(patient, stepPart, surgeon, leafId, leafDisplay);

        if (RmcSurgery.GetSingleton(leafId) is { } leafEnt
            && TryComp<CMSurgeryComponent>(leafEnt, out var leafComp)
            && armed.SurgeryId == leafId)
        {
            if (armed.StepIndex >= leafComp.Steps.Count - 1)
            {
                var completeEvLast = new CMSurgeryCompleteEvent(patient, surgeon, leafId);
                RaiseLocalEvent(patient, ref completeEvLast);
                RemComp<CMUSurgeryArmedStepComponent>(patient);
                ClearSurgeryInFlight(patient);
                _dispatch.RefreshUiForPatient(patient);
                return;
            }

            if (TryResolveStepAt(leafId, armed.StepIndex + 1, out var nextLinear))
            {
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
        RaiseLocalEvent(patient, ref completeEv);
        RemComp<CMUSurgeryArmedStepComponent>(patient);
        ClearSurgeryInFlight(patient);
        _dispatch.RefreshUiForPatient(patient);
    }

    private string ResolveLeafDisplayName(string leafId)
    {
        if (TryGetMetadata(leafId, out var metadata))
            return metadata.DisplayName ?? leafId;
        if (Prototypes.TryIndex<EntityPrototype>(leafId, out var proto))
            return proto.Name;
        return leafId;
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
