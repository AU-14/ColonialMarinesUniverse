using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Examine;
using Content.Shared._CMU14.Input;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Examine;

public sealed partial class CMUDetailedMedicalExamineSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private CMUMedicalExamineSystem _examine = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
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

        CommandBinds.Builder
            .Bind(CMUKeyFunctions.CMUInspectInjuries, new PointerInputCmdHandler(HandleInspectInjuries))
            .Register<CMUDetailedMedicalExamineSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<CMUDetailedMedicalExamineSystem>();
    }

    private void OnGetInteractionVerbs(GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = args.Target;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-medical-detailed-examine-verb"),
            Act = () => TryStartDetailedExamine(user, target),
            Message = Loc.GetString("cmu-medical-detailed-examine-verb-message"),
        });
    }

    public bool TryStartDetailedExamine(EntityUid user, EntityUid target)
    {
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return false;

        if (!_actionBlocker.CanInteract(user, target) ||
            !_interaction.InRangeAndAccessible(user, target))
        {
            return false;
        }

        return StartDetailedExamine(user, target);
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

    private bool HandleInspectInjuries(ICommonSession? session, EntityCoordinates coordinates, EntityUid target)
    {
        if (session?.AttachedEntity is not { Valid: true } user ||
            !Exists(user) ||
            !Exists(target) ||
            !coordinates.IsValid(EntityManager))
        {
            return false;
        }

        return TryStartDetailedExamine(user, target);
    }

    private bool StartDetailedExamine(EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            GetExamineDelay(user),
            new CMUDetailedPhysicalExamineDoAfterEvent(),
            target,
            target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            RequireCanInteract = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-detailed-examine-start", ("target", target)),
                target,
                user);
            return true;
        }

        return false;
    }

    private void OnDetailedExamineDoAfter(Entity<CMUHumanMedicalComponent> patient, ref CMUDetailedPhysicalExamineDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        var user = args.User;
        RaiseNetworkEvent(GetInspectInjuriesResponse(patient.Owner), user);
    }
}
