using Content.Server._RMC14.Xenonids.Construction;
using Content.Server._RMC14.Xenonids.JoinXeno;
using Content.Server.Administration.Logs;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._RMC14.Gibbing;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-707 (docs/GraphAudit-BadBlood-Playtest5Plan.md): thralled xenos for the clan. The Beast-Master buys a
/// cage at the kennel (BadBloodCampSystem.Armory); a ghost takes the thrall from the cage's GhostRoleMobSpawner, and
/// <see cref="YautjaThrallSystem.HivebreakXeno"/> converts it with the cage's own YautjaHivebreaker as the settings:
/// Bad Blood hive, the master's factions, hivebroken. It then answers to the Beast-Master who bought the cage
/// (BadBloodBeastSystem.Bind). Each Beast-Master keeps at most MaxThralls; thralls count against the clan's threat
/// budget (D11, <see cref="CountClanThralls"/>). FLOW-709: cages and abandoned thralls are taken in the hound's order
/// (BadBloodHoundSystem.CanTakeBeast), an abandoned thrall never goes to the larva queue, and a thrall nobody takes
/// back goes into a cage of its kind. Thralls are not hounds: nothing here touches BadBloodHoundComponent.
/// </summary>
public sealed class BadBloodThrallBeastSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodBeastSystem _beasts = default!;
    [Dependency] private BadBloodHoundSystem _hounds = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private YautjaThrallSystem _thralls = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _caged = new();
    private readonly HashSet<EntityUid> _abandoned = new();

    public override void Initialize()
    {
        // after RMC's pylon handler, which puts any spawned xeno in its spawner's (here: no) hive
        SubscribeLocalEvent<BadBloodThrallBeastComponent, GhostRoleSpawnerUsedEvent>(OnThrallSpawned,
            after: new[] { typeof(XenoPylonSystem) });
        SubscribeLocalEvent<BadBloodThrallCageComponent, GhostRoleRequestAttemptEvent>(OnCageRequestAttempt);
        SubscribeLocalEvent<BadBloodThrallBeastComponent, GhostRoleRequestAttemptEvent>(OnThrallRequestAttempt);
        // the dead thrall's mind is read before the gib sends it to a ghost
        SubscribeLocalEvent<BadBloodThrallBeastComponent, MobStateChangedEvent>(OnThrallMobStateChanged,
            before: new[] { typeof(RMCGibSystem) });
        // FLOW-709: an abandoned clan beast is never offered to the larva queue
        SubscribeLocalEvent<AbandonedXenoQueueableComponent, ComponentAdd>(OnAbandonedAdded);
    }

    public override void Update(float frameTime)
    {
        if (_abandoned.Count > 0)
        {
            foreach (var uid in _abandoned)
            {
                if (!TerminatingOrDeleted(uid))
                    RemComp<AbandonedXenoQueueableComponent>(uid);
            }

            _abandoned.Clear();
        }

        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        _caged.Clear();
        var thralls = EntityQueryEnumerator<BadBloodThrallBeastComponent>();
        while (thralls.MoveNext(out var uid, out var thrall))
        {
            if (_mobState.IsDead(uid))
                continue;

            // a player who only lost their connection keeps the body: their mind is still in it
            if (HasComp<ActorComponent>(uid) || _mind.TryGetMind(uid, out _, out _))
            {
                thrall.AbandonedAt = null;
                continue;
            }

            // somebody is about to win it
            if (HasComp<GhostRoleRaffleComponent>(uid))
                continue;

            thrall.AbandonedAt ??= now;
            if (now >= thrall.AbandonedAt + thrall.UnclaimedTimeout)
                _caged.Add(uid);
        }

        foreach (var uid in _caged)
        {
            ReturnToCage(uid);
        }

        _caged.Clear();
    }

    #region Counting

    /// <summary>
    /// <paramref name="master"/>'s thralls: the living ones bound to them and the cages they bought that nobody has
    /// taken yet. Bounded by <see cref="BadBloodBeastMasterComponent.MaxThralls"/>.
    /// </summary>
    public int CountThralls(EntityUid master)
    {
        var count = 0;
        var thralls = EntityQueryEnumerator<BadBloodThrallBeastComponent, BadBloodBeastComponent>();
        while (thralls.MoveNext(out var uid, out _, out var beast))
        {
            if (beast.Master == master && !_mobState.IsDead(uid))
                count++;
        }

        var cages = EntityQueryEnumerator<BadBloodThrallCageComponent>();
        while (cages.MoveNext(out var uid, out var cage))
        {
            if (cage.Master == master && !TerminatingOrDeleted(uid))
                count++;
        }

        return count;
    }

    /// <summary>
    /// D11: the clan's thralls against its threat budget, living thralls and untaken cages alike. Each is a full
    /// player, unlike a hound.
    /// </summary>
    public int CountClanThralls()
    {
        var count = 0;
        var thralls = EntityQueryEnumerator<BadBloodThrallBeastComponent>();
        while (thralls.MoveNext(out var uid, out _))
        {
            if (!_mobState.IsDead(uid))
                count++;
        }

        var cages = EntityQueryEnumerator<BadBloodThrallCageComponent>();
        while (cages.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                count++;
        }

        return count;
    }

    #endregion

    #region Taking a thrall

    // the cage's ghost took the thrall: convert it with the cage's settings and give it to the cage's buyer
    private void OnThrallSpawned(Entity<BadBloodThrallBeastComponent> ent, ref GhostRoleSpawnerUsedEvent args)
    {
        if (!TryComp(args.Spawner, out BadBloodThrallCageComponent? cage))
            return;

        ent.Comp.CagePrototype = MetaData(args.Spawner).EntityPrototype?.ID;

        var master = cage.Master is { } buyer && !TerminatingOrDeleted(buyer) && !_mobState.IsDead(buyer)
            ? buyer
            : _beasts.GetMasterFor(ent.Owner);

        // without anyone to answer to, the cage stands in as the master: the conversion falls back to the cage's own
        // factions (thrallNpcFaction), and the pack layer binds the thrall once a master appears
        if (TryComp(args.Spawner, out YautjaHivebreakerComponent? settings))
            _thralls.HivebreakXeno(master ?? args.Spawner, ent.Owner, args.Spawner, settings);

        _beasts.Bind(ent.Owner, master);

        // the cage is used up (GhostRoleSystem deletes it next): from now on the thrall counts, not the cage
        RemComp<BadBloodThrallCageComponent>(args.Spawner);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood beast: thrall {ToPrettyString(ent.Owner):thrall} taken from {ToPrettyString(args.Spawner):cage}");
    }

    private void OnCageRequestAttempt(Entity<BadBloodThrallCageComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled ||
            _hounds.CanTakeBeast(args.Player, ent.Comp.StockedAt + ent.Comp.ReservePriority, out var reason))
        {
            return;
        }

        _hounds.RefuseRequest(ref args, reason);
    }

    // FLOW-709: an abandoned thrall offered again (BadBloodGhostRoleSystem) is gated like a cage
    private void OnThrallRequestAttempt(Entity<BadBloodThrallBeastComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        var offeredAt = ent.Comp.AbandonedAt ?? _timing.CurTime;
        if (args.Cancelled || _hounds.CanTakeBeast(args.Player, offeredAt + ent.Comp.ReservePriority, out var reason))
            return;

        _hounds.RefuseRequest(ref args, reason);
    }

    // the player who drove a thrall that just died waits out the beast respawn delay
    private void OnThrallMobStateChanged(Entity<BadBloodThrallBeastComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !_mind.TryGetMind(ent.Owner, out _, out var mind) ||
            mind.UserId is not { } user)
        {
            return;
        }

        _hounds.RecordBeastDeath(user);
    }

    // the component is not yet added while ComponentAdd runs, so it cannot be removed here: strip it next tick,
    // before the larva queue hands the body out (LarvaQueueSystem only offers bodies on its own update)
    private void OnAbandonedAdded(Entity<AbandonedXenoQueueableComponent> ent, ref ComponentAdd args)
    {
        if (HasComp<BadBloodBeastComponent>(ent))
            _abandoned.Add(ent.Owner);
    }

    #endregion

    #region Unclaimed

    /// <summary>
    /// Puts an unclaimed thrall back into a cage of its kind where it stands, for the same master. Nothing is lost.
    /// </summary>
    public bool ReturnToCage(EntityUid thrall)
    {
        if (TerminatingOrDeleted(thrall) ||
            !TryComp(thrall, out BadBloodThrallBeastComponent? comp) ||
            comp.CagePrototype is not { } cageProto)
        {
            return false;
        }

        var xform = Transform(thrall);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map))
            return false;

        var cage = Spawn(cageProto, xform.Coordinates);
        var cageComp = EnsureComp<BadBloodThrallCageComponent>(cage);
        cageComp.Master = CompOrNull<BadBloodBeastComponent>(thrall)?.Master;
        cageComp.StockedAt = _timing.CurTime;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood beast: unclaimed thrall {ToPrettyString(thrall):thrall} went back into {ToPrettyString(cage):cage}");
        QueueDel(thrall);
        return true;
    }

    #endregion
}
