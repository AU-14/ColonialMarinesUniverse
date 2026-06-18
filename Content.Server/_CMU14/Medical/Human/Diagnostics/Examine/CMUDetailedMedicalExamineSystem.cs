using Content.Shared._CMU14.Medical.Human.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Human.Diagnostics.Examine;

public sealed partial class CMUDetailedMedicalExamineSystem : EntitySystem
{
    [Dependency] private CMUMedicalExamineSystem _examine = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly TimeSpan ExamineDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CorpsmanExamineDelay = TimeSpan.FromSeconds(0.4);
    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";
    private const int CorpsmanMedicalSkillLevel = 2;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<HumanMedicalComponent, CMUDetailedPhysicalExamineDoAfterEvent>(OnDetailedExamineDoAfter);
    }

    private void OnGetInteractionVerbs(GetVerbsEvent<InteractionVerb> args)
    {
    }

    public bool TryStartDetailedExamine(EntityUid user, EntityUid target)
    {
        return false;
    }

    public TimeSpan GetExamineDelay(EntityUid user)
    {
        return _skills.HasSkill(user, MedicalSkill, CorpsmanMedicalSkillLevel)
            ? CorpsmanExamineDelay
            : ExamineDelay;
    }

    public CMUInspectInjuriesResponseEvent GetInspectInjuriesResponse(EntityUid patient)
    {
        return new CMUInspectInjuriesResponseEvent(
            GetNetEntity(patient),
            Name(patient),
            _examine.GetInspectInjuriesText(patient),
            _examine.GetWorstExternalBleeding(patient));
    }

    private void OnDetailedExamineDoAfter(Entity<HumanMedicalComponent> patient, ref CMUDetailedPhysicalExamineDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;
        RaiseNetworkEvent(GetInspectInjuriesResponse(patient.Owner), user);
    }
}
