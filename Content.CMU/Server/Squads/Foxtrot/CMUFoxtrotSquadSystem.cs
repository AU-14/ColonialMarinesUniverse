using Content.Server.Chat.Managers;
using Content.Server.CMU14.Roles;
using Content.Server.GameTicking;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.CMU14.Squads.Foxtrot;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Squads.Foxtrot;

public enum CMUFoxtrotSlot
{
    /// <summary>
    /// The job has no Foxtrot squad slots on this side.
    /// </summary>
    None,

    /// <summary>
    /// Join a regular squad.
    /// </summary>
    Regular,

    /// <summary>
    /// Join the Foxtrot squad.
    /// </summary>
    Foxtrot,

    /// <summary>
    /// The regular squads are out of slots for this job and only the Foxtrot squad has one left.
    /// </summary>
    OnlyFoxtrotLeft,
}

/// <summary>
/// Runs the opt-in Foxtrot squad of GOVFOR and OPFOR (see <see cref="CMUFoxtrotSquadComponent"/>).
/// Its slots are added to the side's regular job slots when the side's jobs are set up, and squad
/// assignment keeps them for the Foxtrot squad: opted-in characters take them first, and anyone else
/// only lands there once the regular squads have no slot left for their job, after confirming on late join.
/// </summary>
public sealed partial class CMUFoxtrotSquadSystem : EntitySystem
{
    private const double ConfirmValiditySeconds = 60;

    [Dependency] private IChatManager _chat = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IServerNetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RoundJobProfileSystem _roundJobProfiles = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SquadSystem _squad = default!;
    [Dependency] private StationJobsSystem _stationJobs = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendor = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<RoundJobSide, (EntProtoId Id, CMUFoxtrotSquadComponent Comp)> _squads = new();

    /// <summary>
    /// Who has joined the Foxtrot squad, per station and job.
    /// </summary>
    private readonly Dictionary<(EntityUid Station, ProtoId<JobPrototype> Job), HashSet<NetUserId>> _members = new();

    private readonly Dictionary<NetUserId, PendingJoin> _pendingJoins = new();

    private sealed record PendingJoin(EntityUid Station, ProtoId<JobPrototype> Job, TimeSpan ConfirmedAt);

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        _net.RegisterNetMessage<CMUFoxtrotSquadConfirmMessage>(OnConfirmed);

        RefreshSquads();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _members.Clear();
        _pendingJoins.Clear();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs ev)
    {
        if (ev.WasModified<EntityPrototype>())
            RefreshSquads();
    }

    private void RefreshSquads()
    {
        _squads.Clear();
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract ||
                !proto.TryGetComponent(out CMUFoxtrotSquadComponent? foxtrot, EntityManager.ComponentFactory) ||
                !proto.TryGetComponent(out SquadTeamComponent? team, EntityManager.ComponentFactory))
            {
                continue;
            }

            var side = team.Group switch
            {
                "GOVFOR" => RoundJobSide.Govfor,
                "OPFOR" => RoundJobSide.Opfor,
                _ => RoundJobSide.None,
            };

            if (side != RoundJobSide.None)
                _squads[side] = (proto.ID, foxtrot);
        }
    }

    /// <summary>
    /// Adds the Foxtrot squad's slots on top of a side's job slots, for each reserved role the side has.
    /// </summary>
    public void AddReservedSlots(Dictionary<ProtoId<JobPrototype>, int> jobs, RoundJobSide side)
    {
        if (!_squads.TryGetValue(side, out var squad))
            return;

        foreach (var (jobId, slots) in jobs)
        {
            if (slots <= 0 ||
                !_prototypes.TryIndex(jobId, out var job) ||
                _roundJobProfiles.GetRoundSide(job) != side ||
                job.RoundRole is not { } role ||
                !squad.Comp.Slots.TryGetValue(role, out var reserved))
            {
                continue;
            }

            jobs[jobId] = slots + reserved;
            Log.Info($"Added {reserved} {squad.Id} slots to {jobId} ({slots} -> {slots + reserved})");
        }
    }

    public bool TryGetSquad(RoundJobSide side, out EntProtoId squad)
    {
        squad = default;
        if (!_squads.TryGetValue(side, out var found))
            return false;

        squad = found.Id;
        return true;
    }

    /// <summary>
    /// Works out whether a player joining as <paramref name="jobId"/> goes to the Foxtrot squad.
    /// Slots the player already holds count as theirs, so respawning into the same job is not counted twice.
    /// </summary>
    public CMUFoxtrotSlot Pick(EntityUid station, ProtoId<JobPrototype> jobId, NetUserId player, bool optedIn)
    {
        if (!_prototypes.TryIndex(jobId, out var job) ||
            job.RoundRole is not { } role ||
            !_squads.TryGetValue(_roundJobProfiles.GetRoundSide(job), out var squad) ||
            !squad.Comp.Slots.TryGetValue(role, out var reserved) ||
            reserved <= 0 ||
            !TryComp(station, out StationJobsComponent? stationJobs) ||
            !_stationJobs.TryGetJobSlot(station, jobId, out var remaining, stationJobs))
        {
            return CMUFoxtrotSlot.None;
        }

        _members.TryGetValue((station, jobId), out var members);

        var holdsSlot = false;
        var others = 0;
        var securityOthers = 0;
        foreach (var (user, jobs) in stationJobs.PlayerJobs)
        {
            if (!jobs.Contains(jobId))
                continue;

            if (user == player)
            {
                holdsSlot = true;
                continue;
            }

            others++;
            if (members?.Contains(user) == true)
                securityOthers++;
        }

        if (optedIn && securityOthers < reserved)
            return CMUFoxtrotSlot.Foxtrot;

        // Unlimited jobs never run out of regular slots.
        if (remaining is not { } open)
            return CMUFoxtrotSlot.Regular;

        var total = others + (holdsSlot ? 1 : 0) + open;
        var regularSlots = total - reserved;
        if (others - securityOthers < regularSlots)
            return CMUFoxtrotSlot.Regular;

        return securityOthers < reserved ? CMUFoxtrotSlot.OnlyFoxtrotLeft : CMUFoxtrotSlot.Regular;
    }

    /// <summary>
    /// Picks the squad for a player spawning into a GOVFOR/OPFOR squad. Returns false to leave them in
    /// the regular squad rotation.
    /// </summary>
    public bool TryPickFoxtrotSquad(
        EntityUid? station,
        ProtoId<JobPrototype>? jobId,
        ICommonSession? player,
        HumanoidCharacterProfile? profile,
        out EntProtoId squad)
    {
        squad = default;
        if (jobId is not { } job ||
            !_prototypes.TryIndex(job, out var jobProto))
        {
            return false;
        }

        // Create the squad with the regular ones so overwatch, the callsign directory and the ID
        // console list it before anyone has joined it.
        if (TryGetSquad(_roundJobProfiles.GetRoundSide(jobProto), out var sideSquad))
            _squad.TryEnsureSquad(sideSquad, out _);

        if (station is not { } stationUid || player == null)
            return false;

        var optedIn = profile?.FoxtrotSquadOptIn == true;
        var slot = Pick(stationUid, job, player.UserId, optedIn);
        if (slot is not (CMUFoxtrotSlot.Foxtrot or CMUFoxtrotSlot.OnlyFoxtrotLeft) ||
            !TryGetSquad(_roundJobProfiles.GetRoundSide(jobProto), out squad))
        {
            return false;
        }

        AddMember(stationUid, job, player.UserId);

        // Round start joiners never saw the confirm popup, so tell them why they are here.
        if (!optedIn)
            _chat.DispatchServerMessage(player,
                Loc.GetString("cmu-foxtrot-squad-only-slot-assigned", ("squad", _prototypes.Index(squad).Name)));

        return true;
    }

    /// <summary>
    /// Counts <paramref name="player"/> against the Foxtrot squad's <paramref name="job"/> slots.
    /// </summary>
    public void AddMember(EntityUid station, ProtoId<JobPrototype> job, NetUserId player)
    {
        if (!_members.TryGetValue((station, job), out var members))
            _members[(station, job)] = members = new HashSet<NetUserId>();

        members.Add(player);
    }

    /// <summary>
    /// Gives a new Foxtrot squad member the squad's skills and vendor points.
    /// </summary>
    public void ApplyPerks(EntityUid member, EntityUid squad, JobPrototype? job)
    {
        if (job?.RoundRole is not { } role ||
            !TryComp(squad, out CMUFoxtrotSquadComponent? foxtrot))
        {
            return;
        }

        if (foxtrot.SkillRoles.Contains(role))
        {
            foreach (var (skill, level) in foxtrot.Skills)
            {
                if (_skills.GetSkill(member, skill) < level)
                    _skills.SetSkill(member, skill, level);
            }
        }

        if (foxtrot.ExtraVendorPoints.TryGetValue(role, out var points) &&
            TryComp(member, out CMVendorUserComponent? vendor))
        {
            _vendor.SetPoints((member, vendor), vendor.Points + points);
        }
    }

    /// <summary>
    /// Opens the confirm popup when a late join would take a slot only the Foxtrot squad has left.
    /// Returns true when the popup was sent and the spawn must wait.
    /// </summary>
    public bool TryOpenReservedConfirm(
        ICommonSession player,
        HumanoidCharacterProfile profile,
        EntityUid station,
        string? jobId)
    {
        if (jobId == null ||
            !_prototypes.TryIndex<JobPrototype>(jobId, out var job) ||
            Pick(station, job.ID, player.UserId, profile.FoxtrotSquadOptIn) != CMUFoxtrotSlot.OnlyFoxtrotLeft ||
            !TryGetSquad(_roundJobProfiles.GetRoundSide(job), out var squadId))
        {
            _pendingJoins.Remove(player.UserId);
            return false;
        }

        if (_pendingJoins.TryGetValue(player.UserId, out var pending) &&
            pending.Station == station &&
            pending.Job.Id == jobId &&
            pending.ConfirmedAt != TimeSpan.MinValue &&
            _timing.RealTime - pending.ConfirmedAt <= TimeSpan.FromSeconds(ConfirmValiditySeconds))
        {
            return false;
        }

        _pendingJoins[player.UserId] = new PendingJoin(station, job.ID, TimeSpan.MinValue);
        var squadName = _prototypes.Index(squadId).Name;
        RaiseNetworkEvent(new CMUFoxtrotSquadConfirmEvent(job.LocalizedName, squadName), player.Channel);
        return true;
    }

    private void OnConfirmed(CMUFoxtrotSquadConfirmMessage message)
    {
        var userId = message.MsgChannel.UserId;
        if (!_pendingJoins.TryGetValue(userId, out var pending) ||
            pending.ConfirmedAt != TimeSpan.MinValue ||
            !_player.TryGetSessionById(userId, out var session))
        {
            return;
        }

        if (_gameTicker.PlayerGameStatuses.TryGetValue(userId, out var status) &&
            status == PlayerGameStatus.JoinedGame)
        {
            _pendingJoins.Remove(userId);
            return;
        }

        _pendingJoins[userId] = pending with { ConfirmedAt = _timing.RealTime };
        _gameTicker.MakeJoinGame(session, pending.Station, pending.Job.Id);
        _pendingJoins.Remove(userId);
    }
}
