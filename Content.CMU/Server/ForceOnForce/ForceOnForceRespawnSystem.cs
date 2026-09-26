using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Round.Roles;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.ForceOnForce;

/// <summary>Tracks death on the account, so ghosting or reconnecting cannot reset the wait.</summary>
public sealed partial class ForceOnForceRespawnSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly Dictionary<NetUserId, TimeSpan> _deaths = new();
    private readonly HashSet<NetUserId> _spawned = new();
    private readonly Dictionary<NetUserId, RoundJobSide> _sides = new();
    public static readonly TimeSpan RespawnDelay = TimeSpan.FromMinutes(5);

    public override void Initialize()
    {
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawn);
        SubscribeLocalEvent<GetDisallowedJobsEvent>(OnGetDisallowedJobs);
    }

    private void OnMobStateChanged(Entity<MindContainerComponent> ent, ref MobStateChangedEvent args)
    {
        if (!TryComp<MindComponent>(ent.Comp.Mind, out var mind) || mind.UserId is not { } player)
            return;

        if (args.NewMobState == MobState.Dead)
            _deaths[player] = _timing.CurTime;
        else if (args.OldMobState == MobState.Dead)
            _deaths.Remove(player);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _deaths.Clear();
        _spawned.Clear();
        _sides.Clear();
    }

    private void OnSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.Player is { } player)
        {
            _deaths.Remove(player.UserId);
            _spawned.Add(player.UserId);
            if (IsForceOnForce && TryComp<MarineComponent>(args.Mob, out var marine))
            {
                var side = marine.Faction?.ToLowerInvariant() switch
                {
                    "govfor" => RoundJobSide.Govfor,
                    "opfor" => RoundJobSide.Opfor,
                    _ => RoundJobSide.None,
                };
                if (side != RoundJobSide.None)
                    _sides.TryAdd(player.UserId, side);
            }
        }
    }

    private bool IsForceOnForce => string.Equals(_ticker.CurrentPreset?.ID ?? _ticker.Preset?.ID,
        "ForceOnForce", StringComparison.OrdinalIgnoreCase);

    public bool CanJoinSide(NetUserId player, RoundJobSide side) =>
        !IsForceOnForce || !_sides.TryGetValue(player, out var original) || original == side;

    public bool HasLockedSide(NetUserId player) => IsForceOnForce && _sides.ContainsKey(player);

    private void OnGetDisallowedJobs(ref GetDisallowedJobsEvent args)
    {
        if (!HasLockedSide(args.Player.UserId))
            return;

        foreach (var job in _prototypes.EnumeratePrototypes<JobPrototype>())
        {
            if (!CanJoinSide(args.Player.UserId, job.RoundSide))
                args.Jobs.Add(job.ID);
        }
    }

    public TimeSpan Remaining(NetUserId player)
    {
        return _deaths.TryGetValue(player, out var death)
            ? TimeSpan.FromSeconds(Math.Max(0, (death + RespawnDelay - _timing.CurTime).TotalSeconds))
            : TimeSpan.Zero;
    }

    public bool HasDied(NetUserId player) => _deaths.ContainsKey(player);
    public bool HasSpawned(NetUserId player) => _spawned.Contains(player);
}
