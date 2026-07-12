using System;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Part;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgeryStateExamineSystem : EntitySystem
{
    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    [Dependency] private SharedCMUSurgeryFlowSystem _flow = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SkillsSystem _skills = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUMedicalBodyIndexComponent, ExaminedEvent>(OnPatientExamined);
        SubscribeLocalEvent<BodyPartComponent, ExaminedEvent>(OnPartExamined);
    }

    private void OnPatientExamined(Entity<CMUMedicalBodyIndexComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup("cmu-surgery-site"))
        {
            foreach (var (part, bodyPart) in _medicalIndex.GetBodyParts(ent.Owner))
                PushSiteExamine(args, ent.Owner, part, bodyPart);
        }
    }

    private void OnPartExamined(Entity<BodyPartComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        var patient = ent.Comp.Body ?? ent.Owner;
        PushSiteExamine(args, patient, ent.Owner, ent.Comp);
    }

    private void PushSiteExamine(
        ExaminedEvent args,
        EntityUid patient,
        EntityUid part,
        BodyPartComponent bodyPart)
    {
        var site = _flow.GetSiteState(part);
        if (site.Access == CMUSurgicalAccess.Closed)
            return;

        var partName = SharedCMUSurgeryFlowSystem.FormatPartName(bodyPart.PartType, bodyPart.Symmetry);
        if (!HasRequiredSurgerySkill(args.Examiner, patient, part, bodyPart))
        {
            args.PushMarkup(Loc.GetString(
                "cmu-medical-surgery-examine-incision",
                ("part", partName)));
            return;
        }

        args.PushMarkup(Loc.GetString(
            "cmu-medical-surgery-examine-site-details",
            ("part", partName),
            ("access", ResolveAccessLabel(site.Access)),
            ("hemostasis", ResolveHemostasisLabel(site.Hemostasis)),
            ("step", ResolveCurrentStep(patient, part, bodyPart))));
    }

    private bool HasRequiredSurgerySkill(
        EntityUid examiner,
        EntityUid patient,
        EntityUid part,
        BodyPartComponent bodyPart)
    {
        var required = 1;
        if (TryGetActiveProcedure(patient, part, bodyPart, out var surgeryId)
            && _flow.TryGetDefinition(surgeryId, out var surgery))
        {
            required = Math.Max(required, surgery.MinSkill);
        }

        return _skills.HasSkill(examiner, SurgerySkill, required);
    }

    private bool TryGetActiveProcedure(
        EntityUid patient,
        EntityUid part,
        BodyPartComponent bodyPart,
        out string surgeryId)
    {
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var inProgress)
            && inProgress.Part == part)
        {
            surgeryId = inProgress.LeafSurgeryId;
            return true;
        }

        if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed)
            && armed.TargetPartType == bodyPart.PartType
            && armed.TargetSymmetry == bodyPart.Symmetry)
        {
            surgeryId = string.IsNullOrEmpty(armed.LeafSurgeryId)
                ? armed.SurgeryId
                : armed.LeafSurgeryId;
            return true;
        }

        surgeryId = string.Empty;
        return false;
    }

    private string ResolveCurrentStep(EntityUid patient, EntityUid part, BodyPartComponent bodyPart)
    {
        if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed)
            && armed.TargetPartType == bodyPart.PartType
            && armed.TargetSymmetry == bodyPart.Symmetry)
        {
            return armed.StepLabel;
        }

        if (TryComp<CMUSurgeryInFlightComponent>(part, out var inFlight)
            && _flow.TryResolveNextStep(patient, part, inFlight.LeafSurgeryId, out var resolved))
        {
            return resolved.StepLabel;
        }

        return Loc.GetString("cmu-medical-surgery-examine-no-active-step");
    }

    private string ResolveAccessLabel(CMUSurgicalAccess access)
    {
        var key = access switch
        {
            CMUSurgicalAccess.Incised => "cmu-medical-surgery-access-incised",
            CMUSurgicalAccess.Shallow => "cmu-medical-surgery-access-shallow",
            CMUSurgicalAccess.BoneCut => "cmu-medical-surgery-access-bone-cut",
            CMUSurgicalAccess.Deep => "cmu-medical-surgery-access-deep",
            _ => "cmu-medical-surgery-access-closed",
        };
        return Loc.GetString(key);
    }

    private string ResolveHemostasisLabel(CMUSurgicalHemostasis hemostasis)
    {
        var key = hemostasis switch
        {
            CMUSurgicalHemostasis.Clamped => "cmu-medical-surgery-hemostasis-clamped",
            CMUSurgicalHemostasis.Uncontrolled => "cmu-medical-surgery-hemostasis-uncontrolled",
            _ => "cmu-medical-surgery-hemostasis-none",
        };
        return Loc.GetString(key);
    }
}
