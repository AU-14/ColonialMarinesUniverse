using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Mutiny;
using Content.Server.Popups;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Mutiny;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared.CMU14.Mutiny;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

// ReSharper disable once CheckNamespace
namespace Content.Server._RMC14.Marines.Mutiny;

// CMU14: the leader's two abilities as cmss13 has them. recruit swears people in quietly, begin mutiny
// reveals them and makes everyone else on the side pick one
public sealed partial class MutinySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> PoliceSkill = "RMCSkillPolice";
    private static readonly ProtoId<TagPrototype> WorkingJoeTag = "WorkingJoe";
    private const int PoliceSkillMax = 2;

    private readonly Dictionary<EntityUid, CMUMutinyPromptEui> _prompts = new();
    private readonly Dictionary<(EntityUid Leader, EntityUid Target), TimeSpan> _declinedUntil = new();
    private readonly Dictionary<CMUMutinyPromptEui, EntityUid> _promptLeaders = new();
    private readonly List<CMUMutinyPromptEui> _expired = new();

    private void InitializeCMU()
    {
        SubscribeLocalEvent<MutineerComponent, ComponentStartup>(OnMutineerStartupCMU);
        SubscribeLocalEvent<MutineerComponent, ComponentShutdown>(OnMutineerShutdownCMU);
        SubscribeLocalEvent<CMUMutinyLeaderComponent, CMUMutinyBeginActionEvent>(OnBeginAction);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_prompts.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var prompt in _prompts.Values)
        {
            if (prompt.PromptState.Deadline is { } deadline && now >= deadline ||
                TerminatingOrDeleted(prompt.Recipient))
            {
                _expired.Add(prompt);
            }
        }

        foreach (var prompt in _expired)
        {
            prompt.Expire();
        }

        _expired.Clear();
    }

    private void OnMutineerStartupCMU(Entity<MutineerComponent> ent, ref ComponentStartup args)
    {
        RemComp<CMUMutinySwornComponent>(ent);
        SetSide(ent, HasComp<MutineerLeaderComponent>(ent) ? CMUMutinySide.Leader : CMUMutinySide.Mutineer);
    }

    private void OnMutineerShutdownCMU(Entity<MutineerComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<CMUMutinyParticipantComponent>(ent, out var participant) &&
            participant.Side is CMUMutinySide.Mutineer or CMUMutinySide.Leader)
        {
            RemComp<CMUMutinyParticipantComponent>(ent);
        }
    }

    private void CMULeaderStartup(Entity<MutineerLeaderComponent> ent)
    {
        var leader = EnsureComp<CMUMutinyLeaderComponent>(ent);
        if (!leader.Begun)
            _actions.AddAction(ent, ref leader.BeginActionEntity, leader.BeginAction);

        SetSide(ent, CMUMutinySide.Leader);
    }

    private void CMULeaderShutdown(Entity<MutineerLeaderComponent> ent)
    {
        // RemoveAction only detaches non-temporary actions, so the entities would leak
        QueueDel(ent.Comp.RecruitActionEntity);
        ent.Comp.RecruitActionEntity = null;

        if (!TryComp<CMUMutinyLeaderComponent>(ent, out var leader))
            return;

        _actions.RemoveAction(ent.Owner, leader.BeginActionEntity);
        QueueDel(leader.BeginActionEntity);
        leader.BeginActionEntity = null;

        // nobody left to start it, so anyone sworn to this leader is off the hook
        var sworn = EntityQueryEnumerator<CMUMutinySwornComponent>();
        while (sworn.MoveNext(out var uid, out var comp))
        {
            if (comp.Leader != ent.Owner)
                continue;

            RemCompDeferred<CMUMutinySwornComponent>(uid);
            ChatTo(uid, Loc.GetString("cmu-mutiny-sworn-released"));
        }

        foreach (var prompt in _prompts.Values.ToList())
        {
            if (prompt.PromptState.Kind != CMUMutinyPromptKind.ChooseSide && IsFromLeader(prompt, ent))
                prompt.Expire();
        }

        _declinedUntil.Keys.Where(k => k.Leader == ent.Owner).ToList().ForEach(k => _declinedUntil.Remove(k));

        if (!TerminatingOrDeleted(ent))
        {
            RemComp<CMUMutinyLeaderComponent>(ent);
            if (TryComp<CMUMutinyParticipantComponent>(ent, out var participant) && participant.Side == CMUMutinySide.Leader)
                SetSide(ent, CMUMutinySide.Mutineer);
        }
    }

    private bool IsFromLeader(CMUMutinyPromptEui prompt, EntityUid leader)
    {
        return _promptLeaders.TryGetValue(prompt, out var from) && from == leader;
    }

    private void CMURecruit(Entity<MutineerLeaderComponent> ent, ref MutineerRecruitActionEvent args)
    {
        if (args.Handled)
            return;

        var leader = ent.Owner;
        var target = args.Target;
        if (!CanRecruit(leader, target, out var reason))
        {
            _popup.PopupEntity(reason, leader, leader, PopupType.SmallCaution);
            return;
        }

        if (_declinedUntil.TryGetValue((leader, target), out var until) && _timing.CurTime < until)
        {
            _popup.PopupEntity(Loc.GetString("cmu-mutiny-recruit-fail-declined", ("target", Identity.Name(target, EntityManager, leader))), leader, leader, PopupType.SmallCaution);
            return;
        }

        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        var leaderComp = EnsureComp<CMUMutinyLeaderComponent>(leader);
        args.Handled = true;

        var now = _timing.CurTime;
        var state = new CMUMutinyPromptEuiState(
            CMUMutinyPromptKind.Invite,
            Identity.Name(leader, EntityManager, target),
            GetJobName(leader),
            leaderComp.Begun,
            new List<CMUMutinyRosterEntry>(),
            new List<CMUMutinyChoice>(),
            now + leaderComp.InviteTime,
            leaderComp.InviteTime);

        var prompt = new CMUMutinyPromptEui(target, state, [CMUMutinyChoice.Accept], CMUMutinyChoice.Decline,
            (eui, choice) => OnInviteResolved(eui, leader, choice));

        OpenPrompt(prompt, actor.PlayerSession, leader);
        _popup.PopupEntity(Loc.GetString("cmu-mutiny-recruit-sent", ("target", Identity.Name(target, EntityManager, leader))), leader, leader);
    }

    private bool CanRecruit(EntityUid leader, EntityUid target, out string reason)
    {
        var name = Identity.Name(target, EntityManager, leader);
        reason = string.Empty;

        if (target == leader)
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-self");
            return false;
        }

        if (!TryComp<MarineComponent>(target, out var targetMarine) ||
            !TryComp<MarineComponent>(leader, out var leaderMarine) ||
            targetMarine.Faction != leaderMarine.Faction)
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-faction", ("target", name));
            return false;
        }

        if (HasComp<MutineerComponent>(target) ||
            TryComp<CMUMutinySwornComponent>(target, out var sworn) && sworn.Leader == leader)
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-already", ("target", name));
            return false;
        }

        if (HasComp<CMUMutinySwornComponent>(target))
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-other-leader", ("target", name));
            return false;
        }

        if (HasComp<SynthComponent>(target) || _tags.HasTag(target, WorkingJoeTag))
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-synth", ("target", name));
            return false;
        }

        if (IsLoyalistBound(target) ||
            TryComp<CMUMutinyParticipantComponent>(target, out var participant) && participant.Side == CMUMutinySide.Loyalist)
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-loyalist", ("target", name));
            return false;
        }

        if (_mobState.IsIncapacitated(target) ||
            !HasComp<ActorComponent>(target) ||
            !_mind.TryGetMind(target, out _, out _))
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-unresponsive", ("target", name));
            return false;
        }

        if (_prompts.ContainsKey(target))
        {
            reason = Loc.GetString("cmu-mutiny-recruit-fail-pending", ("target", name));
            return false;
        }

        return true;
    }

    private bool IsLoyalistBound(EntityUid uid)
    {
        return HasComp<CMUMutinyLoyalistComponent>(uid) || _skills.HasSkill(uid, PoliceSkill, PoliceSkillMax);
    }

    private void OnInviteResolved(CMUMutinyPromptEui prompt, EntityUid leader, CMUMutinyChoice choice)
    {
        ForgetPrompt(prompt);
        var target = prompt.Recipient;
        if (TerminatingOrDeleted(target))
            return;

        var leaderAlive = !TerminatingOrDeleted(leader) && HasComp<MutineerLeaderComponent>(leader);
        var targetName = Identity.Name(target, EntityManager, leader);

        if (choice != CMUMutinyChoice.Accept)
        {
            if (!leaderAlive)
                return;

            if (TryComp<CMUMutinyLeaderComponent>(leader, out var leaderComp))
                _declinedUntil[(leader, target)] = _timing.CurTime + leaderComp.DeclineCooldown;

            _popup.PopupEntity(Loc.GetString("cmu-mutiny-recruit-declined", ("target", targetName)), leader, leader, PopupType.MediumCaution);
            return;
        }

        // the world moved on while the window was open
        if (!leaderAlive || !CanRecruitOnAccept(leader, target))
        {
            ChatTo(target, Loc.GetString("cmu-mutiny-invite-void"));
            return;
        }

        var begun = CompOrNull<CMUMutinyLeaderComponent>(leader)?.Begun ?? false;
        if (begun)
        {
            MakeMutineer(target);
        }
        else
        {
            EnsureComp<CMUMutinySwornComponent>(target).Leader = leader;
            ChatTo(target, Loc.GetString("cmu-mutiny-sworn", ("leader", Identity.Name(leader, EntityManager, target))));
        }

        _popup.PopupEntity(Loc.GetString(begun ? "cmu-mutiny-recruit-joined" : "cmu-mutiny-recruit-sworn", ("target", targetName)), leader, leader, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(target):player} {(begun ? "joined" : "was sworn into")} the mutiny of {ToPrettyString(leader):player}");
        if (TryComp<ActorComponent>(target, out var actor))
            _chatManager.SendAdminAnnouncement($"Player {actor.PlayerSession.Name} {(begun ? "joined" : "was sworn into")} the mutiny of {ToPrettyString(leader)}.");
    }

    private bool CanRecruitOnAccept(EntityUid leader, EntityUid target)
    {
        // the pending check would trip on this very prompt, which is already forgotten by now
        return CanRecruit(leader, target, out _);
    }

    private void OnBeginAction(Entity<CMUMutinyLeaderComponent> ent, ref CMUMutinyBeginActionEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Begun || !HasComp<MutineerLeaderComponent>(ent))
            return;

        if (!TryComp<ActorComponent>(ent, out var actor) || _prompts.ContainsKey(ent))
            return;

        args.Handled = true;

        var roster = new List<CMUMutinyRosterEntry>();
        var sworn = EntityQueryEnumerator<CMUMutinySwornComponent>();
        while (sworn.MoveNext(out var uid, out var comp))
        {
            if (comp.Leader != ent.Owner)
                continue;

            roster.Add(new CMUMutinyRosterEntry(Name(uid), GetJobName(uid), !_mobState.IsDead(uid)));
        }

        roster.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));

        var state = new CMUMutinyPromptEuiState(
            CMUMutinyPromptKind.Begin,
            Name(ent),
            GetJobName(ent),
            false,
            roster,
            new List<CMUMutinyChoice>(),
            null,
            TimeSpan.Zero);

        var prompt = new CMUMutinyPromptEui(ent, state, [CMUMutinyChoice.Accept], CMUMutinyChoice.Decline,
            (eui, choice) =>
            {
                ForgetPrompt(eui);
                if (choice == CMUMutinyChoice.Accept)
                    BeginMutiny(ent);
            });

        OpenPrompt(prompt, actor.PlayerSession, ent);
    }

    public void BeginMutiny(EntityUid leaderUid)
    {
        if (TerminatingOrDeleted(leaderUid) ||
            !HasComp<MutineerLeaderComponent>(leaderUid) ||
            !TryComp<CMUMutinyLeaderComponent>(leaderUid, out var leader) ||
            leader.Begun)
        {
            return;
        }

        leader.Begun = true;
        _actions.RemoveAction(leaderUid, leader.BeginActionEntity);
        QueueDel(leader.BeginActionEntity);
        leader.BeginActionEntity = null;

        var sworn = EntityQueryEnumerator<CMUMutinySwornComponent>();
        var reveal = new List<EntityUid>();
        while (sworn.MoveNext(out var uid, out var comp))
        {
            if (comp.Leader == leaderUid)
                reveal.Add(uid);
        }

        foreach (var uid in reveal)
        {
            MakeMutineer(uid);
        }

        var faction = CompOrNull<MarineComponent>(leaderUid)?.Faction;
        var now = _timing.CurTime;
        var sessions = new List<ICommonSession>();
        var marines = EntityQueryEnumerator<MarineComponent>();
        while (marines.MoveNext(out var uid, out var marine))
        {
            if (marine.Faction != faction)
                continue;

            TryComp<ActorComponent>(uid, out var actor);
            if (actor != null)
                sessions.Add(actor.PlayerSession);

            if (HasComp<MutineerComponent>(uid) ||
                HasComp<CMUMutinyParticipantComponent>(uid) ||
                _mobState.IsDead(uid) ||
                _tags.HasTag(uid, WorkingJoeTag) ||
                _prompts.ContainsKey(uid))
            {
                continue;
            }

            // no choice to make here, so SSD MPs and command get their side too
            if (IsLoyalistBound(uid))
            {
                SetSide(uid, CMUMutinySide.Loyalist);
                ChatTo(uid, Loc.GetString("cmu-mutiny-side-loyalist-forced"));
                continue;
            }

            if (actor == null)
                continue;

            var disabled = new List<CMUMutinyChoice>();
            if (HasComp<SynthComponent>(uid))
                disabled.Add(CMUMutinyChoice.Mutineer);

            var state = new CMUMutinyPromptEuiState(
                CMUMutinyPromptKind.ChooseSide,
                Identity.Name(leaderUid, EntityManager, uid),
                GetJobName(leaderUid),
                true,
                new List<CMUMutinyRosterEntry>(),
                disabled,
                now + leader.ChooseSideTime,
                leader.ChooseSideTime);

            var prompt = new CMUMutinyPromptEui(uid, state,
                [CMUMutinyChoice.Mutineer, CMUMutinyChoice.Loyalist, CMUMutinyChoice.NonCombatant],
                CMUMutinyChoice.NonCombatant,
                OnSideChosen);

            OpenPrompt(prompt, actor.PlayerSession, leaderUid);
        }

        _chat.DispatchFilteredAnnouncement(
            Filter.Empty().AddPlayers(sessions),
            Loc.GetString("cmu-mutiny-announcement"),
            sender: Loc.GetString("cmu-mutiny-announcement-sender"),
            announcementSound: leader.BeginSound,
            colorOverride: Color.FromHex("#E0524A"));

        _adminLog.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(leaderUid):player} began a mutiny with {reveal.Count} sworn mutineers");
        _chatManager.SendAdminAnnouncement($"{ToPrettyString(leaderUid)} has begun the mutiny with {reveal.Count} sworn mutineers.");
    }

    private void OnSideChosen(CMUMutinyPromptEui prompt, CMUMutinyChoice choice)
    {
        ForgetPrompt(prompt);
        var uid = prompt.Recipient;
        if (TerminatingOrDeleted(uid) || HasComp<MutineerComponent>(uid))
            return;

        switch (choice)
        {
            case CMUMutinyChoice.Mutineer:
                MakeMutineer(uid);
                break;
            case CMUMutinyChoice.Loyalist:
                SetSide(uid, CMUMutinySide.Loyalist);
                ChatTo(uid, Loc.GetString("cmu-mutiny-side-loyalist"));
                break;
            default:
                SetSide(uid, CMUMutinySide.NonCombatant);
                ChatTo(uid, Loc.GetString("cmu-mutiny-side-noncombatant"));
                break;
        }

        _adminLog.Add(LogType.Action, LogImpact.Medium, $"{ToPrettyString(uid):player} sided with {choice} in the mutiny");
    }

    // ends every side of every mutiny. the RMC end command only cleared the mutineers
    public void EndMutinyCMU()
    {
        foreach (var prompt in _prompts.Values.ToList())
        {
            prompt.Expire();
        }

        var participants = EntityQueryEnumerator<CMUMutinyParticipantComponent>();
        while (participants.MoveNext(out var uid, out _))
        {
            RemCompDeferred<CMUMutinyParticipantComponent>(uid);
        }

        var sworn = EntityQueryEnumerator<CMUMutinySwornComponent>();
        while (sworn.MoveNext(out var uid, out _))
        {
            RemCompDeferred<CMUMutinySwornComponent>(uid);
        }

        _declinedUntil.Clear();
    }

    private void SetSide(EntityUid uid, CMUMutinySide side)
    {
        var participant = EnsureComp<CMUMutinyParticipantComponent>(uid);
        participant.Side = side;
        Dirty(uid, participant);
    }

    public bool TryGetPrompt(EntityUid recipient, [NotNullWhen(true)] out CMUMutinyPromptEui? prompt)
    {
        return _prompts.TryGetValue(recipient, out prompt);
    }

    private void OpenPrompt(CMUMutinyPromptEui prompt, ICommonSession session, EntityUid leader)
    {
        _prompts[prompt.Recipient] = prompt;
        _promptLeaders[prompt] = leader;
        _euis.OpenEui(prompt, session);
    }

    private void ForgetPrompt(CMUMutinyPromptEui prompt)
    {
        if (_prompts.TryGetValue(prompt.Recipient, out var current) && current == prompt)
            _prompts.Remove(prompt.Recipient);

        _promptLeaders.Remove(prompt);
    }

    private void ChatTo(EntityUid uid, string message)
    {
        if (TryComp<ActorComponent>(uid, out var actor))
            _chatManager.DispatchServerMessage(actor.PlayerSession, message);
    }

    private string? GetJobName(EntityUid uid)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _))
            return null;

        return _jobs.MindTryGetJobName(mindId, out var job) ? job : null;
    }
}
