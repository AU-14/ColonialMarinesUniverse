using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.CMU14.Systems;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Actions;
using Content.Shared.CMU14.Round.Antags.StrikeOrganizer;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

public sealed partial class StrikePetitionSystem : EntitySystem
{
    public static readonly EntProtoId PetitionPrototype = "CMUStrikePetition";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private RoleSystem _role = default!;
    [Dependency] private StrikeOrganizerRuleSystem _rule = default!;
    [Dependency] private SuspectDescriptionSystem _suspectDescription = default!;
    [Dependency] private WantedSystem _wanted = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StrikePetitionComponent, UseInHandEvent>(OnPetitionUsed);
        SubscribeLocalEvent<StrikePetitionComponent, ExaminedEvent>(OnPetitionExamined);
        SubscribeLocalEvent<StrikePetitionComponent, EntityTerminatingEvent>(OnPetitionTerminating);
        SubscribeLocalEvent<StrikeSignPendingComponent, StrikeSignAcceptEvent>(OnSignAccepted);
        SubscribeLocalEvent<StrikeSignPendingComponent, StrikeSignCancelEvent>(OnSignCancelled);
        SubscribeLocalEvent<StrikePetitionComponent, StrikeSignDoAfterEvent>(OnSignDoAfter);
        SubscribeLocalEvent<StrikeOrganizerPackComponent, GetItemActionsEvent>(OnPackGetItemActions);
        SubscribeLocalEvent<StrikeOrganizerPackComponent, StrikeRedraftActionEvent>(OnPackRedraft);
        SubscribeLocalEvent<StrikeOrganizerPackComponent, StrikeFaxTransmitActionEvent>(OnPackTransmit);
        SubscribeLocalEvent<StrikeOrganizerPackComponent, StrikeFaxSendEvent>(OnPackFaxSend);
    }

    private bool IsDeadOrCrit(EntityUid target)
        => _mobState.IsDead(target) || _mobState.IsCritical(target);

    /// <summary>
    /// Returns true if the target's job belongs to any department blocked on the petition.
    /// Same enumeration pattern as the CLF tattoo gun's department check.
    /// </summary>
    private bool IsInBlockedDepartment(EntityUid target, StrikePetitionComponent comp)
    {
        if (comp.BlockedDepartments.Count == 0)
            return false;

        if (!_mind.TryGetMind(target, out EntityUid mindId, out _))
            return false;

        if (!_jobs.MindTryGetJob(mindId, out JobPrototype? job))
            return false;

        foreach (DepartmentPrototype department in _proto.EnumeratePrototypes<DepartmentPrototype>())
        {
            if (!department.Roles.Contains(job.ID))
                continue;

            foreach (ProtoId<DepartmentPrototype> blocked in comp.BlockedDepartments)
            {
                if (blocked.Id == department.ID)
                    return true;
            }
        }

        return false;
    }

    private void OnPetitionUsed(EntityUid uid, StrikePetitionComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var signer = args.User;

        if (_rule.ResolveVenture() is not { } venture)
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-no-venture"), signer, signer);
            return;
        }

        if (HasComp<StrikeParticipantComponent>(signer) || venture.Participants.Contains(signer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-already"), signer, signer);
            return;
        }

        if (IsDeadOrCrit(signer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-dead"), signer, signer);
            return;
        }

        if (!_mind.TryGetMind(signer, out EntityUid mindId, out var mind))
            return;

        // One rebellion per mind: exclusive antags (CLF recruits, the organizer himself) cannot sign
        if (_role.MindIsExclusiveAntagonist(mindId))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-refused-antag"), signer, signer);
            return;
        }

        if (IsInBlockedDepartment(signer, comp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-refused-dept"), signer, signer);
            return;
        }

        // Consent needs a player behind the pen
        if (!_player.TryGetSessionById(mind.UserId, out _))
            return;

        var (grievance, clf, govfor) = StrikeVentureText.Compose(Loc, venture.Grievance, venture.ClfStance, venture.GovforStance);
        var pending = EnsureComp<StrikeSignPendingComponent>(signer);
        pending.Petition = uid;

        _dialog.OpenConfirmation(
            signer,
            signer,
            Loc.GetString("cmu-strike-sign-dialog-title"),
            Loc.GetString("cmu-strike-sign-dialog-message",
                ("grievance", grievance), ("clfStance", clf), ("govforStance", govfor)),
            new StrikeSignAcceptEvent(),
            new StrikeSignCancelEvent());
    }

    private void OnSignAccepted(EntityUid uid, StrikeSignPendingComponent comp, StrikeSignAcceptEvent args)
    {
        var petition = comp.Petition;
        RemComp<StrikeSignPendingComponent>(uid);

        if (Deleted(petition) || !TryComp(petition, out StrikePetitionComponent? petitionComp))
            return;

        if (IsDeadOrCrit(uid) || HasComp<StrikeParticipantComponent>(uid))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, uid,
            TimeSpan.FromSeconds(petitionComp.SignDoAfterDuration),
            new StrikeSignDoAfterEvent(), petition, uid, petition)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BreakOnHandChange = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-strike-sign-begin"), uid, uid);
    }

    private void OnSignCancelled(EntityUid uid, StrikeSignPendingComponent comp, StrikeSignCancelEvent args)
        => RemComp<StrikeSignPendingComponent>(uid);

    private void OnSignDoAfter(EntityUid uid, StrikePetitionComponent comp, StrikeSignDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        var signer = args.User;

        args.Handled = true;

        if (IsDeadOrCrit(signer) || HasComp<StrikeParticipantComponent>(signer))
            return;

        if (_rule.ResolveVenture() is not { } venture)
            return;

        // Offer-time gates can be stale by conversion: the dialog and do-after leave
        // time for the signer to be recruited by another exclusive antag
        if (_mind.TryGetMind(signer, out EntityUid mindId, out _)
            && _role.MindIsExclusiveAntagonist(mindId))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-refused-antag"), signer, signer);
            return;
        }

        if (IsInBlockedDepartment(signer, comp))
        {
            _popup.PopupEntity(Loc.GetString("cmu-strike-sign-refused-dept"), signer, signer);
            return;
        }

        ConvertSigner(venture, signer);
    }

    private void ConvertSigner(StrikeOrganizerRuleComponent venture, EntityUid signer)
    {
        venture.Participants.Add(signer);
        EnsureComp<StrikeParticipantComponent>(signer);

        // Succession: the first signer after the organizer's fall carries the strike forward.
        // The pack is the physical press; whoever holds it prints.
        var previousOrganizer = venture.Organizer;
        var succeeded = false;
        if (previousOrganizer is { } former && (TerminatingOrDeleted(former) || _mobState.IsDead(former)))
        {
            venture.Organizer = signer;
            succeeded = true;
        }

        var (grievance, clf, govfor) = StrikeVentureText.Compose(Loc, venture.Grievance, venture.ClfStance, venture.GovforStance);

        if (_mind.TryGetMind(signer, out EntityUid mindId, out _))
            _role.MindAddRole(mindId, venture.ParticipantMindRole);

        var briefing = Loc.GetString(venture.ParticipantBriefing,
            ("grievance", grievance), ("clfStance", clf), ("govforStance", govfor));
        if (succeeded)
            briefing += "\n\n" + Loc.GetString("cmu-strike-succession-briefing");

        _antag.SendBriefing(signer, briefing, venture.BriefingColor, venture.BriefingSound);

        var organizer = venture.Organizer is { } org && !TerminatingOrDeleted(org)
            ? ToPrettyString(org)
            : "an unattributed petition";

        var suffix = succeeded ? $". Succeeded {previousOrganizer} as organizer" : string.Empty;
        _adminLog.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(signer)} signed the strike petition ({venture.Participants.Count}/{venture.Goal}) " +
            $"and became a strike participant. Venture of {organizer}: grievance '{venture.Grievance}', " +
            $"CLF {venture.ClfStance}, GOVFOR {venture.GovforStance}{suffix}");

        _popup.PopupEntity(Loc.GetString("cmu-strike-sign-success",
            ("count", venture.Participants.Count), ("goal", venture.Goal)), signer, signer);

        CheckMilestones(venture);
    }

    private void CheckMilestones(StrikeOrganizerRuleComponent venture)
    {
        var (grievance, clf, govfor) = StrikeVentureText.Compose(Loc, venture.Grievance, venture.ClfStance, venture.GovforStance);

        if (!venture.FaxedHalf && venture.Participants.Count >= (venture.Goal + 1) / 2)
        {
            venture.FaxedHalf = true;
            SendUnrestFax("cmu-strike-fax-half-title",
                Loc.GetString("cmu-strike-fax-half", ("grievance", grievance)),
                venture.Organizer);
        }

        if (!venture.FaxedFull && venture.Participants.Count >= venture.Goal)
        {
            venture.FaxedFull = true;
            SendUnrestFax("cmu-strike-fax-full-title",
                Loc.GetString("cmu-strike-fax-full",
                    ("goal", venture.Goal), ("grievance", grievance),
                    ("clfStance", clf), ("govforStance", govfor)),
                venture.Organizer);

            AnnounceVotePassed(venture);
        }
    }

    // CMB faxes reach the marshals, never the strikers: the roster needs its own green light
    private void AnnounceVotePassed(StrikeOrganizerRuleComponent venture)
    {
        var told = new HashSet<EntityUid>();
        if (venture.Organizer is { } organizer && !TerminatingOrDeleted(organizer))
            told.Add(organizer);

        foreach (var participant in venture.Participants)
        {
            if (!TerminatingOrDeleted(participant))
                told.Add(participant);
        }

        var briefing = Loc.GetString("cmu-strike-vote-passed-briefing");
        foreach (var striker in told)
            _antag.SendBriefing(striker, briefing, venture.BriefingColor, venture.BriefingSound);

        _adminLog.Add(LogType.AntagSelection, LogImpact.Medium,
            $"Strike vote passed: {venture.Participants.Count}/{venture.Goal} signatures, briefed {told.Count} strikers");
    }

    private void OnPetitionExamined(EntityUid uid, StrikePetitionComponent comp, ExaminedEvent args)
    {
        if (_rule.ResolveVenture() is not { } venture)
        {
            args.PushText(Loc.GetString("cmu-petition-examine-relic"));
            return;
        }

        var grievance = StrikeVentureText.Compose(Loc, venture.Grievance, venture.ClfStance, venture.GovforStance).Grievance;

        args.PushText(venture.Participants.Count == 0
            ? Loc.GetString("cmu-petition-examine-empty", ("goal", venture.Goal), ("grievance", grievance))
            : Loc.GetString("cmu-petition-examine",
                ("count", venture.Participants.Count), ("goal", venture.Goal),
                ("grievance", grievance), ("names", JoinNames(venture.Participants))));
    }

    private string JoinNames(List<EntityUid> participants)
    {
        var names = new List<string>();
        foreach (var participant in participants)
        {
            if (Exists(participant))
                names.Add(MetaData(participant).EntityName);
        }

        return string.Join(", ", names);
    }

    private void OnPetitionTerminating(EntityUid uid, StrikePetitionComponent comp, ref EntityTerminatingEvent args)
    {
        // Paper burns; the strike does not. Make sure the organizer knows to redraft.
        if (_rule.ResolveVenture() is not { } venture || venture.Organizer is not { } organizer)
            return;

        if (TerminatingOrDeleted(organizer))
            return;

        _popup.PopupEntity(Loc.GetString("cmu-strike-petition-destroyed"), organizer, organizer);
    }

    private void OnPackGetItemActions(Entity<StrikeOrganizerPackComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.RedraftAction, ent.Comp.RedraftActionId);
        args.AddAction(ref ent.Comp.TransmitAction, ent.Comp.TransmitActionId);
    }

    private void OnPackRedraft(Entity<StrikeOrganizerPackComponent> ent, ref StrikeRedraftActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Spawn(PetitionPrototype, Transform(args.Performer).Coordinates);
        _popup.PopupEntity(Loc.GetString("cmu-strike-redraft-done"), args.Performer, args.Performer);
        _adminLog.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(args.Performer)} redrafted a strike petition");
    }

    private void OnPackTransmit(Entity<StrikeOrganizerPackComponent> ent, ref StrikeFaxTransmitActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        // bind the dialog to the pack so the reply event raises where the handler listens
        _dialog.OpenInput(
            ent.Owner,
            args.Performer,
            Loc.GetString("cmu-strike-fax-input"),
            new StrikeFaxSendEvent(),
            largeInput: true,
            characterLimit: 1000,
            title: Loc.GetString("cmu-strike-fax-input-title"));
    }

    // One-way wire: the pack is not a fax machine on any network, so nothing can ever
    // route a reply to it. Sends land at the CMB through the same group as the faxes above.
    private void OnPackFaxSend(Entity<StrikeOrganizerPackComponent> ent, ref StrikeFaxSendEvent args)
    {
        if (args.Message is not { } message || string.IsNullOrWhiteSpace(message))
            return;

        var holder = Transform(ent).ParentUid;
        _wanted.SendFaxToGroup(ColonyCmbFax.MarshalBureauFaxGroup,
            Loc.GetString("cmu-strike-fax-communique-title"),
            message,
            paperPrototype: ColonyCmbFax.CmbPaperPrototype);
        _popup.PopupEntity(Loc.GetString("cmu-strike-fax-sent"), ent, holder);
        _adminLog.Add(LogType.Mind, LogImpact.Medium,
            $"{ToPrettyString(holder)} transmitted a strike communique to the CMB");
    }

    private void SendUnrestFax(string headingId, string body, EntityUid? organizer)
    {
        // A striker talked: the organizer becomes describable once the strike actually escalates
        if (organizer is { } org && !TerminatingOrDeleted(org))
            body += " " + Loc.GetString("cmu-strike-fax-witness",
                ("description", _suspectDescription.Describe(org, _suspectDescription.RandomWitness(org, null))));

        var heading = Loc.GetString(headingId);
        _wanted.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            heading,
            ColonyCmbFax.Build(heading, body),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            }, ColonyCmbFax.CmbPaperPrototype);
    }
}
