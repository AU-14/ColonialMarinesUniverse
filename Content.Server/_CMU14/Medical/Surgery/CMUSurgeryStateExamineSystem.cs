using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Examine;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUSurgeryStateExamineSystem : EntitySystem
{
    [Dependency] private readonly SharedCMSurgerySystem _rmcSurgery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUHumanMedicalComponent, ExaminedEvent>(OnPatientExamined);
        SubscribeLocalEvent<BodyPartComponent, ExaminedEvent>(OnPartExamined);
    }

    private void OnPatientExamined(Entity<CMUHumanMedicalComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (!TryComp<CMUSurgeryInProgressComponent>(ent, out var lockComp))
            return;
        if (!TryComp<CMUSurgeryInFlightComponent>(lockComp.Part, out var flight))
            return;

        var nextStep = ResolveNextStepLabel(ent, lockComp.Part, flight.LeafSurgeryId);
        args.PushMarkup(Loc.GetString(
            "cmu-medical-surgery-examine-patient-in-progress",
            ("surgery", flight.LeafSurgeryDisplayName),
            ("surgeon", flight.SurgeonName),
            ("next", nextStep)));
    }

    private void OnPartExamined(Entity<BodyPartComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (TryComp<CMUSurgeryInFlightComponent>(ent, out var flight))
        {
            var nextStep = ResolveNextStepLabel(default, ent, flight.LeafSurgeryId);
            args.PushMarkup(Loc.GetString(
                "cmu-medical-surgery-examine-part-in-progress",
                ("surgery", flight.LeafSurgeryDisplayName),
                ("surgeon", flight.SurgeonName),
                ("next", nextStep)));
            return;
        }

        if (HasComp<CMRibcageOpenComponent>(ent) || HasComp<CMIncisionOpenComponent>(ent))
            args.PushMarkup(Loc.GetString("cmu-medical-surgery-examine-part-abandoned"));
    }

    private string ResolveNextStepLabel(EntityUid body, EntityUid part, string leafSurgeryId)
    {
        if (_rmcSurgery.GetSingleton(leafSurgeryId) is not { } surgeryEnt)
            return "—";
        var bodyForLookup = body.IsValid() ? body : part;
        var next = _rmcSurgery.GetNextStep(bodyForLookup, part, surgeryEnt);
        if (next is null)
            return "—";

        var (nextSurgery, stepIdx) = next.Value;
        if (stepIdx < 0 || stepIdx >= nextSurgery.Comp.Steps.Count)
            return "—";
        var stepProtoId = nextSurgery.Comp.Steps[stepIdx];
        if (_rmcSurgery.GetSingleton(stepProtoId) is not { } stepEnt)
            return "—";
        return MetaData(stepEnt).EntityName;
    }
}
