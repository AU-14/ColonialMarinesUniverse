using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DoAfter;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Medical.Diagnostics.Examine;

public sealed partial class CMUDetailedMedicalExamineSystem : EntitySystem
{
    [Dependency] private CMUMedicalExamineSystem _examine = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly TimeSpan ExamineDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CorpsmanExamineDelay = TimeSpan.FromSeconds(0.4);
    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";
    private const int CorpsmanMedicalSkillLevel = 2;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUDetailedPhysicalExamineDoAfterEvent>(OnDetailedExamineDoAfter);
    }

    public bool TryStartDetailedExamine(EntityUid user, EntityUid target)
    {
        if (TerminatingOrDeleted(user) ||
            TerminatingOrDeleted(target) ||
            !HasComp<CMUHumanMedicalComponent>(target))
        {
            return false;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            GetExamineDelay(user),
            new CMUDetailedPhysicalExamineDoAfterEvent(),
            target,
            target: target)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        });
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

    private void OnGetInteractionVerbs(GetVerbsEvent<InteractionVerb> args)
    {
        var patient = args.Target;
        if (!args.CanAccess ||
            !args.CanInteract ||
            !HasComp<CMUHumanMedicalComponent>(patient))
        {
            return;
        }

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Act = () => TryStartDetailedExamine(user, patient),
            Text = Loc.GetString("cmu-medical-detailed-examine-verb"),
            Message = Loc.GetString("cmu-medical-detailed-examine-verb-message"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/examine.svg.192dpi.png")),
        });
    }

    private void OnDetailedExamineDoAfter(Entity<CMUHumanMedicalComponent> patient, ref CMUDetailedPhysicalExamineDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        RaiseNetworkEvent(GetInspectInjuriesResponse(patient.Owner), user);
    }
}
