using Content.Server._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Server.GameObjects;
using Content.Shared.Damage;
using Content.Server._RMC14.Announce;

namespace Content.Server._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed partial class CMUBlightCoreSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly XenoEvolutionSystem _evolution = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly XenoAnnounceSystem _xenoAnnounce = default!;

    private const float JoinTimeBonusSeconds = 10f;
    private const float InitialVoteSeconds = 35f;

    private sealed class ActiveVote
    {
        public required Entity<HiveComponent> Hive;
        public TimeSpan EndsAt;
        public readonly Dictionary<EntityUid, int> CandidateVotes = new();
        public readonly Dictionary<EntityUid, EntityUid> Voters = new();
    }

    private readonly Dictionary<EntityUid, ActiveVote> _activeVotes = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUBlightCoreComponent, StepTriggeredOffEvent>(OnStep);
        SubscribeLocalEvent<CMUBlightCoreComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<CMUBlightCoreComponent, BlightCoreAcceptMessage>(OnAccept);
        SubscribeLocalEvent<CMUBlightCoreComponent, BlightCoreDeclineMessage>(OnDecline);
        SubscribeLocalEvent<XenoComponent, BlightCoreVoteMessage>(OnVoteCast);
        SubscribeLocalEvent<CMUXenoOvermindComponent, MobStateChangedEvent>(OnOvermindDeath);
        SubscribeLocalEvent<CMUBlightCoreComponent, EntityTerminatingEvent>(OnCoreDestroyed);
        SubscribeLocalEvent<CMUBlightCoreComponent, DamageChangedEvent>(OnCoreDamaged);
        SubscribeLocalEvent<CMUBlightCoreComponent, ComponentStartup>(OnCoreInit);
        
    }

    private void OnCoreInit(Entity<CMUBlightCoreComponent> core, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        if (core.Comp.CurrentOvermind != null)
            return;

        Timer.Spawn(0, () =>
        {
            if (TerminatingOrDeleted(core))
                return;

            if (core.Comp.CurrentOvermind != null)
                return;

            // Find pathogen hive directly instead of relying on HiveMemberComponent being set
            EntityUid? pathogenHive = null;
            var hiveQuery = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
            while (hiveQuery.MoveNext(out var hiveUid, out _, out var meta))
            {
                if (meta.EntityPrototype?.ID == "CMUPathogenHive")
                {
                    pathogenHive = hiveUid;
                    break;
                }
            }

            if (pathogenHive == null)
                return;

            var query = EntityQueryEnumerator<CMUXenoOvermindComponent, HiveMemberComponent>();
            while (query.MoveNext(out var overmindUid, out var overmindComp, out var member))
            {
                if (member.Hive != pathogenHive.Value)
                    continue;

                if (overmindComp.LinkedCore != null)
                    continue;

                overmindComp.LinkedCore = core.Owner;
                Dirty(overmindUid, overmindComp);
                core.Comp.CurrentOvermind = GetNetEntity(overmindUid);
                Dirty(core);
                break;
            }
        });
    }

    private void OnStep(Entity<CMUBlightCoreComponent> core, ref StepTriggeredOffEvent args)
    {
    Log.Debug($"BlightCore: OnStep odpalony dla {args.Tripper}");
    
    if (_net.IsClient)
    {
        Log.Debug("BlightCore: IsClient, return");
        return;
    }

        var tripper = args.Tripper;

        if (_hive.GetHive(tripper) is not { } hive)
            return;

        if (_activeVotes.TryGetValue(core.Owner, out var vote) &&
            vote.CandidateVotes.ContainsKey(tripper))
            return;

        if (!_player.TryGetSessionByEntity(tripper, out var session))
            return;
            
        _ui.OpenUi(core.Owner, BlightCoreUiKey.Accept, session);
        Log.Debug($"BlightCore: OpenUi Accept dla {tripper}, session={session.Name}");
        _ui.SetUiState(core.Owner, BlightCoreUiKey.Accept, new BlightCoreBuiState
        {
            Core = GetNetEntity(core.Owner),
            EndsAt = _timing.CurTime + TimeSpan.FromSeconds(InitialVoteSeconds)
        });
        Log.Debug($"BlightCore: SetUiState Accept done");
    }

    private void OnStepTriggerAttempt(Entity<CMUBlightCoreComponent> core, ref StepTriggerAttemptEvent args)
    {
        var stepper = args.Tripper;

        if (!TryComp(stepper, out XenoComponent? _))
        {
            return;
        }

        if (_mobState.IsDead(stepper))
        {
            return;
        }

        if (_hive.GetHive(stepper) is not { } xenoHive ||
            _hive.GetHive(core.Owner) is not { } coreHive ||
            xenoHive.Owner != coreHive.Owner)
        {
            return;
        }

        if (_hive.HasHiveQueen(xenoHive))
        {
            return;
        }

        if (core.Comp.CurrentOvermind is not null &&
            !TerminatingOrDeleted(GetEntity(core.Comp.CurrentOvermind.Value)))
        {
            return;
        }

        args.Continue = true;
    }

    private void OnAccept(EntityUid coreUid, CMUBlightCoreComponent comp, BlightCoreAcceptMessage msg)
    {
        var candidate = GetEntity(msg.Candidate);

        if (_hive.GetHive(candidate) is not { } hive)
            return;

        if (_hive.HasHiveQueen(hive))
            return;

        if (_activeVotes.TryGetValue(coreUid, out var vote))
        {
            if (vote.Hive.Owner != hive.Owner)
                return;

            if (!vote.CandidateVotes.TryAdd(candidate, 0))
                return;

            vote.EndsAt += TimeSpan.FromSeconds(JoinTimeBonusSeconds);

            BroadcastVoteState(coreUid, vote);
            OpenVoteUiForHive(coreUid, hive);
            PopupToHive(hive, Loc.GetString("cmu14-blight-core-candidate-joined",
                ("name", Name(candidate))));
        }
        else
        {
            var newVote = new ActiveVote
            {
                Hive = hive,
                EndsAt = _timing.CurTime + TimeSpan.FromSeconds(InitialVoteSeconds)
            };

            newVote.CandidateVotes[candidate] = 0;
            _activeVotes[coreUid] = newVote;

            BroadcastVoteState(coreUid, newVote);
            OpenVoteUiForHive(coreUid, hive);  // ← brakowało tego

            PopupToHive(hive, Loc.GetString("cmu14-blight-core-vote-started",
                ("name", Name(candidate))));
        }
    }

    private void OnDecline(EntityUid coreUid, CMUBlightCoreComponent comp, BlightCoreDeclineMessage msg)
    {
        // no-op
    }

    private void OnVoteCast(EntityUid voterEnt, XenoComponent _, BlightCoreVoteMessage msg)
    {
        EntityUid? coreUid = null;
        ActiveVote? vote = null;

        if (_hive.GetHive(voterEnt) is not { } voterHive)
            return;

        foreach (var (core, v) in _activeVotes)
        {
            if (v.Hive.Owner == voterHive.Owner)
            {
                coreUid = core;
                vote = v;
                break;
            }
        }

        if (coreUid == null || vote == null)
            return;

        var votedFor = GetEntity(msg.VotedFor);

        if (!vote.CandidateVotes.ContainsKey(votedFor))
            return;

        if (_hive.GetHive(voterEnt) is not { } hive ||
            hive.Owner != vote.Hive.Owner)
            return;

        if (vote.Voters.TryGetValue(voterEnt, out var previous))
        {
            if (previous == votedFor)
                return;
            vote.CandidateVotes[previous]--;
        }

        vote.Voters[voterEnt] = votedFor;
        vote.CandidateVotes[votedFor]++;

        BroadcastVoteState(coreUid.Value, vote);
    }

    private void BecomeOvermind(
        Entity<CMUBlightCoreComponent> core,
        EntityUid candidate,
        Entity<HiveComponent> hive)
    {
        var overmindEnt = _evolution.EvolveTo(candidate, core.Comp.OvermindPrototype);

        core.Comp.CurrentOvermind = GetNetEntity(overmindEnt);
        Dirty(core);

        var overmindComp = EnsureComp<CMUXenoOvermindComponent>(overmindEnt);
        overmindComp.LinkedCore = core.Owner;
        Dirty(overmindEnt, overmindComp);

        _hive.SetHiveQueen(overmindEnt, hive);

        _popup.PopupEntity(
            Loc.GetString("cmu14-blight-core-became-overmind"),
            overmindEnt,
            PopupType.LargeCaution);
    }
    
    private void OnCoreDestroyed(Entity<CMUBlightCoreComponent> core, ref EntityTerminatingEvent args)
    {
        if (_net.IsClient)
            return;

        if (core.Comp.CurrentOvermind is not { } netOvermind)
            return;

        var overmind = GetEntity(netOvermind);
        if (TerminatingOrDeleted(overmind))
            return;

        if (_hive.GetHive(overmind) is not { } hive)
            return;

        // Check for another surviving blight core in the same hive
        EntityUid? replacementCore = null;
        var coreQuery = EntityQueryEnumerator<CMUBlightCoreComponent, HiveMemberComponent>();
        while (coreQuery.MoveNext(out var otherCoreUid, out _, out var member))
        {
            if (otherCoreUid == core.Owner || TerminatingOrDeleted(otherCoreUid))
                continue;

            if (member.Hive != hive.Owner)
                continue;

            replacementCore = otherCoreUid;
            break;
        }

        if (replacementCore is { } newCoreUid)
        {
            if (TryComp(overmind, out CMUXenoOvermindComponent? overmindComp))
            {
                overmindComp.LinkedCore = newCoreUid;
                Dirty(overmind, overmindComp);
            }

            if (TryComp(newCoreUid, out CMUBlightCoreComponent? newCoreComp))
            {
                newCoreComp.CurrentOvermind = GetNetEntity(overmind);
                Dirty(newCoreUid, newCoreComp);
            }

            PopupToHive(hive, Loc.GetString("cmu14-blight-core-destroyed-overmind-survives"));
            return;
        }

        PopupToHive(hive, Loc.GetString("cmu14-blight-core-destroyed-overmind-died"));
        QueueDel(overmind);
    }

    private void OnOvermindDeath(Entity<CMUXenoOvermindComponent> overmind, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        if (overmind.Comp.LinkedCore is { } coreUid && TryComp(coreUid, out CMUBlightCoreComponent? core))
        {
            core.CurrentOvermind = null;
            Dirty(coreUid, core);
        }

        if (_hive.GetHive(overmind.Owner) is { } hive)
            PopupToHive(hive, Loc.GetString("cmu14-blight-core-overmind-died"));

        if (!TerminatingOrDeleted(overmind.Owner))
            QueueDel(overmind.Owner);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;

        if (_activeVotes.Count == 0)
            return;

        var expired = new List<EntityUid>();

        foreach (var (coreUid, vote) in _activeVotes)
        {
            if (time >= vote.EndsAt)
                expired.Add(coreUid);
        }

        foreach (var coreUid in expired)
        {
            ResolveVote(coreUid);
        }
    }

    private void ResolveVote(EntityUid coreUid)
    {
        if (!_activeVotes.Remove(coreUid, out var vote))
            return;

        CloseVoteUiForHive(coreUid, vote.Hive);

        if (!TryComp(coreUid, out CMUBlightCoreComponent? core))
            return;

        EntityUid? winner = null;
        var bestVotes = -1;

        foreach (var (candidate, votes) in vote.CandidateVotes)
        {
            if (Deleted(candidate) || _mobState.IsDead(candidate))
                continue;

            if (votes > bestVotes)
            {
                bestVotes = votes;
                winner = candidate;
            }
        }

        if (winner is not { } chosen)
            return;

        BecomeOvermind((coreUid, core), chosen, vote.Hive);
    }

    private void PopupToHive(Entity<HiveComponent> hive, string message)
    {
        _xenoAnnounce.AnnounceToHive(EntityUid.Invalid, hive.Owner, message, hive.Comp.AnnounceSound, PopupType.Medium);
    }

    private void OpenVoteUiForHive(EntityUid coreUid, Entity<HiveComponent> hive)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;

            if (_hive.GetHive(ent) is not { } entHive || entHive.Owner != hive.Owner)
                continue;

            _ui.OpenUi(ent, BlightCoreUiKey.Vote, ent);
        }
    }

    private void BroadcastVoteState(EntityUid coreUid, ActiveVote vote)
    {
        var list = new List<BlightCoreVoteCandidate>();
        foreach (var (candidate, votes) in vote.CandidateVotes)
        {
            var name = Deleted(candidate) ? "???" : Name(candidate);
            list.Add(new BlightCoreVoteCandidate(GetNetEntity(candidate), name, votes));
        }

        var state = new BlightCoreVoteBuiState
        {
            Core = GetNetEntity(coreUid),
            EndsAt = vote.EndsAt,
            Candidates = list
        };

        // Ustaw state na każdym xeno w hive
        var query = EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out _, out var member))
        {
            if (member.Hive != vote.Hive.Owner)
                continue;

            _ui.SetUiState(uid, BlightCoreUiKey.Vote, state);
        }
    }

    private void CloseVoteUiForHive(EntityUid coreUid, Entity<HiveComponent> hive)
    {
        var query = EntityQueryEnumerator<XenoComponent, HiveMemberComponent>();
        while (query.MoveNext(out var uid, out _, out var member))
        {
            if (member.Hive != hive.Owner)
                continue;

            _ui.CloseUi(uid, BlightCoreUiKey.Vote);
        }
    }

    private TimeSpan _lastDamageAnnounce = TimeSpan.Zero;
    private const float DamageAnnounceCoolddownSeconds = 10f;

    private void OnCoreDamaged(Entity<CMUBlightCoreComponent> core, ref DamageChangedEvent args)
    {
        if (_net.IsClient)
            return;

        if (args.DamageDelta == null || args.DamageDelta.GetTotal() <= 0)
            return;

        var now = _timing.CurTime;
        if (now - core.Comp.LastDamageAnnounceAt < TimeSpan.FromSeconds(DamageAnnounceCoolddownSeconds))
            return;

        core.Comp.LastDamageAnnounceAt = now;

        if (_hive.GetHive(core.Owner) is not { } hive)
            return;

        PopupToHive(hive, Loc.GetString("cmu14-blight-core-under-attack"));
    }
}