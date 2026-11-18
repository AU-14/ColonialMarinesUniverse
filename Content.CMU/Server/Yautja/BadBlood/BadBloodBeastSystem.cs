using System.Linq;
using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.K9;
using Content.Shared._RMC14.K9.Components;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ARCH-702 (docs/GraphAudit-BadBlood-Playtest5Plan.md): the Bad Blood pack layer. Every clan beast (kennel
/// hound, thrall) carries <see cref="BadBloodBeastComponent"/> and answers to one master: the least-loaded living
/// Beast-Master, else the living Hunt Leader (<see cref="GetMasterFor"/>). <see cref="Bind"/> keeps the Beast-Master's
/// pack and, for a hound, RMC's K9 handler bond in step. ARCH-706: kennel beasts re-bind when a Beast-Master takes or
/// drops the caste, when the Hunt Leader changes and when their master dies. Landing hounds keep the Bad Blood who
/// woke them while that one lives. ARCH-705: the Beast-Master's armour counts for less. ECS-707 (the pack actions)
/// is in BadBloodBeastSystem.Pack.cs.
/// </summary>
public sealed partial class BadBloodBeastSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private K9System _k9 = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _rebindScratch = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodBeastComponent, ComponentShutdown>(OnBeastShutdown);

        SubscribeLocalEvent<BadBloodBeastMasterComponent, CMGetArmorEvent>(OnMasterGetArmor);
        SubscribeLocalEvent<BadBloodBeastMasterComponent, MobStateChangedEvent>(OnMasterMobStateChanged);

        SubscribeLocalEvent<BadBloodCasteTakenEvent>(OnCasteTaken);
        SubscribeLocalEvent<BadBloodCasteDroppedEvent>(OnCasteDropped);
        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);

        InitializePack();
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        UpdatePackEffects(now);

        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;
        UpdateBonds(now);
        UpdatePackBond();
    }

    #region Queries

    /// <summary>
    /// Whether <paramref name="uid"/> is one of the clan's beasts: a kennel or landing hound, or a thrall.
    /// </summary>
    public bool IsClanBeast(EntityUid uid)
    {
        return HasComp<BadBloodBeastComponent>(uid);
    }

    /// <summary>
    /// The master <paramref name="beast"/> should answer to now: the living Beast-Master with the fewest beasts, else
    /// the living Hunt Leader, else nobody. <paramref name="except"/> is never picked (a master that is going).
    /// </summary>
    public EntityUid? GetMasterFor(EntityUid beast, EntityUid? except = null)
    {
        EntityUid? best = null;
        var bestLoad = int.MaxValue;
        var masters = EntityQueryEnumerator<BadBloodBeastMasterComponent>();
        while (masters.MoveNext(out var uid, out var master))
        {
            if (uid == except || master.LifeStage > ComponentLifeStage.Running || !IsLivingMaster(uid))
                continue;

            // its own bond does not count against the master it already has
            var load = master.Beasts.Count - (master.Beasts.Contains(beast) ? 1 : 0);
            if (load >= bestLoad)
                continue;

            best = uid;
            bestLoad = load;
        }

        if (best != null)
            return best;

        if (_rule.GetLeader() is { } leader && leader != except && IsLivingMaster(leader))
            return leader;

        return null;
    }

    /// <summary>
    /// Living Bad Bloods who hold the Beast-Master caste.
    /// </summary>
    public int CountLivingBeastMasters()
    {
        var count = 0;
        var masters = EntityQueryEnumerator<BadBloodBeastMasterComponent>();
        while (masters.MoveNext(out var uid, out var master))
        {
            if (master.LifeStage <= ComponentLifeStage.Running && IsLivingMaster(uid))
                count++;
        }

        return count;
    }

    private bool IsLivingMaster(EntityUid uid)
    {
        return !TerminatingOrDeleted(uid) && !_mobState.IsDead(uid);
    }

    // a beast whose master is the one that matters to it: the pack master, or the K9 bond a landing hound got from
    // the Bad Blood who woke it
    private EntityUid? GetEffectiveMaster(EntityUid beast, BadBloodBeastComponent comp)
    {
        if (comp.Master != null)
            return comp.Master;

        // a kennel hound answers to the pack layer only: its old K9 bond (say a deposed Hunt Leader's) is not a master
        if (TryComp(beast, out BadBloodHoundComponent? hound) && hound.FromKennel)
            return null;

        return TryComp(beast, out K9DogComponent? k9) ? k9.Master : null;
    }

    #endregion

    #region Binding

    /// <summary>
    /// Binds <paramref name="beast"/> to <paramref name="master"/>: the pack layer, the Beast-Master's pack list, the
    /// K9 handler bond for a hound and the thrall's master. A null master only clears the pack layer (the K9 bond of
    /// a dead master is left, it commands nothing). Returns false for something that is not a clan beast.
    /// </summary>
    public bool Bind(EntityUid beast, EntityUid? master)
    {
        if (!TryComp(beast, out BadBloodBeastComponent? comp))
            return false;

        var old = comp.Master;
        if (old is { } previous && TryComp(previous, out BadBloodBeastMasterComponent? oldPack))
            oldPack.Beasts.Remove(beast);

        comp.Master = master;
        comp.RebindAt = null;
        Dirty(beast, comp);

        if (master is not { } newMaster)
            return true;

        if (TryComp(newMaster, out BadBloodBeastMasterComponent? pack))
            pack.Beasts.Add(beast);

        if (TryComp(beast, out K9DogComponent? k9) && (k9.Master != newMaster || !k9.HandlerBonded))
            _k9.BindMaster(beast, k9, newMaster, asHandler: true);

        if (TryComp(beast, out YautjaThrallComponent? thrall) && thrall.Master != newMaster)
        {
            thrall.Master = newMaster;
            Dirty(beast, thrall);
        }

        if (old is { } before && before != newMaster)
            DropIdleHandler(before);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood beast: {ToPrettyString(beast):beast} now answers to {ToPrettyString(newMaster):player}");
        return true;
    }

    /// <summary>
    /// Binds <paramref name="beast"/> to whoever <see cref="GetMasterFor"/> picks.
    /// </summary>
    public bool BindToBestMaster(EntityUid beast, EntityUid? except = null)
    {
        return Bind(beast, GetMasterFor(beast, except));
    }

    // RMC's handler kit stays on a Bad Blood only while it still has a dog to command
    private void DropIdleHandler(EntityUid master)
    {
        if (TerminatingOrDeleted(master) ||
            !TryComp(master, out K9HandlerComponent? handler) ||
            handler.Dogs.Any(dog => !TerminatingOrDeleted(dog)))
        {
            return;
        }

        RemComp<K9HandlerComponent>(master);
    }

    // kennel hounds and thralls follow the pack; a landing hound keeps the Bad Blood who woke it
    private bool FollowsThePack(EntityUid beast)
    {
        return !TryComp(beast, out BadBloodHoundComponent? hound) || hound.FromKennel;
    }

    private void OnBeastShutdown(Entity<BadBloodBeastComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Master is { } master && TryComp(master, out BadBloodBeastMasterComponent? pack))
            pack.Beasts.Remove(ent.Owner);
    }

    // ARCH-706: a new Beast-Master takes over the kennel beasts that have no Beast-Master of their own
    private void OnCasteTaken(ref BadBloodCasteTakenEvent ev)
    {
        if (!HasComp<BadBloodBeastMasterComponent>(ev.User))
            return;

        _rebindScratch.Clear();
        var beasts = EntityQueryEnumerator<BadBloodBeastComponent>();
        while (beasts.MoveNext(out var uid, out var comp))
        {
            if (!FollowsThePack(uid) || _mobState.IsDead(uid))
                continue;

            if (comp.Master is { } master && HasComp<BadBloodBeastMasterComponent>(master) && IsLivingMaster(master))
                continue;

            _rebindScratch.Add(uid);
        }

        foreach (var beast in _rebindScratch)
        {
            BindToBestMaster(beast);
        }

        _rebindScratch.Clear();
    }

    // ARCH-706: the caste's components are already gone, so the beasts it held go to the next master
    private void OnCasteDropped(ref BadBloodCasteDroppedEvent ev)
    {
        RebindBeastsOf(ev.User, except: null);
    }

    // the Hunt Leader changed: beasts that answered to the old one, or to nobody, go to the next master
    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent ev)
    {
        _rebindScratch.Clear();
        var beasts = EntityQueryEnumerator<BadBloodBeastComponent>();
        while (beasts.MoveNext(out var uid, out var comp))
        {
            if (comp.RebindAt != null || _mobState.IsDead(uid))
                continue;

            var master = GetEffectiveMaster(uid, comp);
            var orphan = master == null || !IsLivingMaster(master.Value);
            var oldLeader = master != null && master == ev.Old && !HasComp<BadBloodBeastMasterComponent>(master.Value);
            if (orphan || (oldLeader && FollowsThePack(uid)))
                _rebindScratch.Add(uid);
        }

        foreach (var beast in _rebindScratch)
        {
            BindToBestMaster(beast);
        }

        _rebindScratch.Clear();
    }

    private void RebindBeastsOf(EntityUid master, EntityUid? except)
    {
        _rebindScratch.Clear();
        var beasts = EntityQueryEnumerator<BadBloodBeastComponent>();
        while (beasts.MoveNext(out var uid, out var comp))
        {
            if (comp.Master == master && !_mobState.IsDead(uid))
                _rebindScratch.Add(uid);
        }

        foreach (var beast in _rebindScratch)
        {
            BindToBestMaster(beast, except);
        }

        _rebindScratch.Clear();
    }

    // once a second: Last Order runs out, and a beast whose master is dead or gone finds another one
    private void UpdateBonds(TimeSpan now)
    {
        _rebindScratch.Clear();
        var beasts = EntityQueryEnumerator<BadBloodBeastComponent>();
        while (beasts.MoveNext(out var uid, out var comp))
        {
            if (_mobState.IsDead(uid))
                continue;

            if (comp.RebindAt is { } at)
            {
                if (now >= at)
                    _rebindScratch.Add(uid);

                continue;
            }

            var master = GetEffectiveMaster(uid, comp);
            if (master is { } living && IsLivingMaster(living))
            {
                // a landing hound bound through the wake path: the pack layer learns its master
                if (comp.Master == null)
                {
                    comp.Master = living;
                    Dirty(uid, comp);
                    if (TryComp(living, out BadBloodBeastMasterComponent? pack))
                        pack.Beasts.Add(uid);
                }

                continue;
            }

            // nobody to answer to: only worth a re-bind once someone can take it
            if (GetMasterFor(uid) != null)
                _rebindScratch.Add(uid);
        }

        foreach (var beast in _rebindScratch)
        {
            BindToBestMaster(beast);
        }

        _rebindScratch.Clear();
    }

    #endregion

    #region The Beast-Master

    // ARCH-705: the Tracker pattern, the caste's cost
    private void OnMasterGetArmor(Entity<BadBloodBeastMasterComponent> ent, ref CMGetArmorEvent args)
    {
        args.ArmorModifier *= ent.Comp.ArmorMultiplier;
    }

    // ECS-707 Last Order: the pack rages over its dead master, then finds another
    private void OnMasterMobStateChanged(Entity<BadBloodBeastMasterComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var now = _timing.CurTime;
        var until = now + ent.Comp.LastOrderDuration;
        foreach (var beast in ent.Comp.Beasts.ToArray())
        {
            if (TerminatingOrDeleted(beast) || _mobState.IsDead(beast) ||
                !TryComp(beast, out BadBloodBeastComponent? comp))
            {
                ent.Comp.Beasts.Remove(beast);
                continue;
            }

            var rage = EnsureComp<K9ProtectorRageComponent>(beast);
            rage.ExpiresAt = until;
            Dirty(beast, rage);
            _speed.RefreshMovementSpeedModifiers(beast);

            comp.RebindAt = until;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-beast-last-order"), beast, beast, PopupType.LargeCaution);
        }

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood beast: Beast-Master {ToPrettyString(ent.Owner):player} died, {ent.Comp.Beasts.Count} beasts rage (Last Order)");
    }

    #endregion
}
