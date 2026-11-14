using Content.Server.Administration.Logs;
using Content.Shared._RMC14.OnCollide;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids.Leap;
using Content.Shared._RMC14.Xenonids.Lunge;
using Content.Shared._RMC14.Xenonids.Pheromones;
using Content.Shared._RMC14.Xenonids.Projectile;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared._RMC14.Xenonids.Spray;
using Content.Shared._RMC14.Xenonids.Sweep;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// ECS-03 (node N10): the anti-xeno kit's passives and the xeno halves of Consecrate, Unwind, THE LIGHT and
/// Saturate. Acid-Rated Plating (0B), Overclock: Steady (3B), Purge Ground (4A), Unwind Recoil (4B),
/// Searing Light (5A) and Saturate's xeno branch (5B) live here; the Cutting Torch (1A) is a Scour kind
/// in <see cref="EWJoeScourSystem"/> and Rending Edge (3A) is on the machete in <see cref="EWJoeArsenalSystem"/>.
/// Every effect is xenofauna-only (<see cref="EWJoeTargetRules.IsXenofauna"/>). The tunables are the
/// components the level table grants (<c>EWJoeXenoKitComponents.cs</c>).
/// </summary>
public sealed class EWJoeXenoKitSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EWJoeArmorShredSystem _shred = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private RMCDazedSystem _daze = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DamageTypePrototype> HeatDamage = "Heat";
    private static readonly ProtoId<DamageTypePrototype> CausticDamage = "Caustic";
    private static readonly ProtoId<DamageTypePrototype>[] AcidTypes = { HeatDamage, CausticDamage };

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();
    private readonly HashSet<Entity<XenoWeedsComponent>> _weeds = new();
    private readonly HashSet<Entity<XenoSweepingComponent>> _sweeping = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        // pheromone receivers are re-applied every frame, so the seared strip runs right after them
        UpdatesAfter.Add(typeof(SharedXenoPheromonesSystem));

        SubscribeLocalEvent<EWJoeAcidPlatingComponent, BeforeDamageChangedEvent>(OnPlatingBeforeDamage);

        SubscribeLocalEvent<EWJoeSteadyComponent, XenoLeapHitAttempt>(OnSteadyLeapHit);
        SubscribeLocalEvent<EWJoeSteadyComponent, XenoLungeHitAttempt>(OnSteadyLungeHit);
        SubscribeLocalEvent<EWJoeSteadyComponent, KnockDownAttemptEvent>(OnSteadyKnockdownAttempt);
        SubscribeLocalEvent<EWJoeSteadyComponent, DamageChangedEvent>(OnSteadyDamageChanged);

        SubscribeLocalEvent<EWJoeUnwindRecoilComponent, DamageDealtEvent>(OnRecoilDamageDealt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        UpdatePlating(now);
        UpdateSearingLight(now);
        UpdateSeared(now);
    }

    #region 0B Acid-Rated Plating

    private void OnPlatingBeforeDamage(Entity<EWJoeAcidPlatingComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || !IsXenoAcid(ent, args.Origin, args.Source))
            return;

        DamageSpecifier? reduced = null;
        foreach (var type in AcidTypes)
        {
            if (!args.Damage.DamageDict.TryGetValue(type, out var amount) || amount <= FixedPoint2.Zero)
                continue;

            reduced ??= new DamageSpecifier(args.Damage);
            reduced.DamageDict[type] = amount * ent.Comp.AcidDamageMultiplier;
        }

        if (reduced != null)
            args.Damage = reduced;
    }

    /// <summary>
    /// Damage from xeno acid: anything a xenofauna, a xeno projectile or an acidic splash deals, and the
    /// acid already on the casing (spit acid ticks with no origin, sprayed acid names the Joe itself).
    /// Only the heat and caustic part of such damage is reduced.
    /// </summary>
    public bool IsXenoAcid(EntityUid joe, EntityUid? origin, EntityUid? source)
    {
        if (IsAcidSource(joe, origin) || IsAcidSource(joe, source))
            return true;

        return (origin == null || origin == joe)
            && (HasComp<UserAcidedComponent>(joe) || HasComp<SprayAcidedComponent>(joe));
    }

    private bool IsAcidSource(EntityUid joe, EntityUid? uid)
    {
        if (uid is not { } source || source == joe || !Exists(source))
            return false;

        return EWJoeTargetRules.IsXenofauna(EntityManager, source)
            || HasComp<XenoProjectileComponent>(source)
            || TryComp(source, out DamageOnCollideComponent? collide) && collide.Acidic;
    }

    /// <summary>Acid on a plated casing is shed after the plating's clear time.</summary>
    private void UpdatePlating(TimeSpan now)
    {
        var query = EntityQueryEnumerator<EWJoeAcidPlatingComponent>();
        while (query.MoveNext(out var uid, out var plating))
        {
            var acided = HasComp<UserAcidedComponent>(uid);
            var sprayed = HasComp<SprayAcidedComponent>(uid);
            if (!acided && !sprayed)
            {
                plating.AcidedSince = null;
                continue;
            }

            plating.AcidedSince ??= now;
            if (now < plating.AcidedSince.Value + plating.AcidClearTime)
                continue;

            plating.AcidedSince = null;
            if (acided)
                RemCompDeferred<UserAcidedComponent>(uid);
            if (sprayed)
                RemCompDeferred<SprayAcidedComponent>(uid);

            _popup.PopupEntity(Loc.GetString("cmu-ew-joe-xenokit-acid-shed"), uid, uid);
        }
    }

    #endregion

    #region 3B Overclock: Steady

    /// <summary>The Joe is running Overclock and has the Steady upgrade.</summary>
    public bool IsSteady(EntityUid joe)
    {
        return HasComp<EWJoeSteadyComponent>(joe)
            && TryComp(joe, out EWJoeOverclockComponent? overclock)
            && overclock.LifeStage <= ComponentLifeStage.Running;
    }

    private void OnSteadyLeapHit(Entity<EWJoeSteadyComponent> ent, ref XenoLeapHitAttempt args)
    {
        // passes through without the bonk a riot shield gives: the Joe simply does not go down
        if (!args.Cancelled && IsSteady(ent))
            args.Cancelled = true;
    }

    private void OnSteadyLungeHit(Entity<EWJoeSteadyComponent> ent, ref XenoLungeHitAttempt args)
    {
        if (!args.Cancelled && IsSteady(ent))
            args.Cancelled = true;
    }

    /// <summary>FLOW-18: records the last damage living xenofauna dealt the Joe, the tell for a xeno knockdown.</summary>
    private void OnSteadyDamageChanged(Entity<EWJoeSteadyComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin is not { } origin || origin == ent.Owner || !Exists(origin))
            return;

        if (!EWJoeTargetRules.IsXenofauna(EntityManager, origin) || !_mobState.IsAlive(origin))
            return;

        ent.Comp.LastXenoHit = _timing.CurTime;
    }

    /// <summary>
    /// FLOW-18 / RB-27: the attempt event carries no source, so a knockdown (or paralyze) is refused only
    /// while Steady and within the hit window of the last xeno damage. Leap and lunge are refused above.
    /// </summary>
    private void OnSteadyKnockdownAttempt(Entity<EWJoeSteadyComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (!args.Cancelled && IsSteady(ent) && (XenoHitRecently(ent) || XenoSweepingNear(ent)))
            args.Cancelled = true;
    }

    /// <summary>Living xenofauna damaged the Joe within <see cref="EWJoeSteadyComponent.XenoHitWindow"/>.</summary>
    public bool XenoHitRecently(Entity<EWJoeSteadyComponent> ent)
    {
        return ent.Comp.LastXenoHit is { } hit && _timing.CurTime - hit <= ent.Comp.XenoHitWindow;
    }

    /// <summary>
    /// A living xenofauna within <see cref="EWJoeSteadyComponent.SweepAttributionRange"/> is mid tail sweep,
    /// trip or lash: RMC adds <see cref="XenoSweepingComponent"/> right before those paralyze, and trip and lash
    /// deal no damage for the hit window to see.
    /// </summary>
    public bool XenoSweepingNear(Entity<EWJoeSteadyComponent> ent)
    {
        _sweeping.Clear();
        _lookup.GetEntitiesInRange(Transform(ent).Coordinates, ent.Comp.SweepAttributionRange, _sweeping, LookupFlags.Uncontained);
        foreach (var xeno in _sweeping)
        {
            if (xeno.Owner != ent.Owner && _mobState.IsAlive(xeno) && EWJoeTargetRules.IsXenofauna(EntityManager, xeno))
                return true;
        }

        return false;
    }

    #endregion

    #region 4A Purge Ground

    /// <summary>
    /// After a landed Consecrate: xeno weeds within the radius are burnt away and a weedkiller zone (the
    /// CAS weedkiller fallout, <see cref="CMUCasFalloutSystem.IsWeedKilled"/>) keeps them from spreading
    /// or being planted there for the block duration. Returns how many weeds were cleared, or -1 when the
    /// Joe has no Purge Ground.
    /// </summary>
    public int TryPurgeGround(EntityUid joe)
    {
        if (!TryComp(joe, out EWJoePurgeGroundComponent? purge))
            return -1;

        var coordinates = Transform(joe).Coordinates;
        _weeds.Clear();
        _lookup.GetEntitiesInRange(coordinates, purge.Radius, _weeds);
        foreach (var weeds in _weeds)
        {
            QueueDel(weeds.Owner);
        }

        var blocker = Spawn(null, _transform.ToMapCoordinates(coordinates));
        AddComp(blocker, new CMUCasFalloutComponent { Type = CMUCasFalloutType.WeedKiller, Radius = purge.Radius });
        AddComp(blocker, new TimedDespawnComponent { Lifetime = (float) purge.BlockDuration.TotalSeconds });

        _adminLogger.Add(LogType.Action, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(joe):joe} purged {_weeds.Count} weed(s) around itself");
        return _weeds.Count;
    }

    #endregion

    #region 4B Unwind Recoil

    /// <summary>Opens the recoil window after Unwind. False when the Joe has no Unwind Recoil.</summary>
    public bool StartUnwindRecoil(EntityUid joe)
    {
        if (!TryComp(joe, out EWJoeUnwindRecoilComponent? recoil))
            return false;

        recoil.ActiveUntil = _timing.CurTime + recoil.Duration;
        _popup.PopupEntity(Loc.GetString("cmu-ew-joe-xenokit-recoil-start"), joe, joe);
        return true;
    }

    private void OnRecoilDamageDealt(Entity<EWJoeUnwindRecoilComponent> ent, ref DamageDealtEvent args)
    {
        if (_timing.CurTime >= ent.Comp.ActiveUntil || args.Origin is not { } attacker || attacker == ent.Owner)
            return;

        if (!Exists(attacker) || !EWJoeTargetRules.IsXenofauna(EntityManager, attacker))
            return;

        // a claw or body attack: melee delivery, or a xeno hitting with itself
        if (args.Impact.Delivery != DamageImpactDelivery.Melee && args.Tool != attacker)
            return;

        var reflected = new DamageSpecifier();
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            if (amount > FixedPoint2.Zero)
                reflected.DamageDict[type] = amount * ent.Comp.ReflectFraction;
        }

        if (reflected.Empty)
            return;

        // the recoil is the xeno's own force turned back: its armor already had its say on the way in
        _damageable.TryChangeDamage(attacker, reflected, ignoreResistances: true, origin: ent.Owner, tool: ent.Owner);
    }

    #endregion

    #region 5A Searing Light

    private void UpdateSearingLight(TimeSpan now)
    {
        var query = EntityQueryEnumerator<EWJoeSearingLightComponent, EWJoeLightComponent>();
        while (query.MoveNext(out var uid, out var searing, out var light))
        {
            if (now < searing.NextBurn)
                continue;

            searing.NextBurn = now + searing.Interval;
            if (!_mobState.IsAlive(uid))
                continue;

            Sear((uid, searing), light.Range, now);
        }
    }

    /// <summary>
    /// Every living xenofauna within <paramref name="range"/> that the Joe can see takes one interval's burn
    /// and is cut off from pheromones for the sear duration. Returns how many were seared.
    /// </summary>
    public int Sear(Entity<EWJoeSearingLightComponent> joe, float range, TimeSpan now)
    {
        var burn = new DamageSpecifier();
        burn.DamageDict[HeatDamage] = joe.Comp.BurnPerSecond * (float) joe.Comp.Interval.TotalSeconds;

        _mobs.Clear();
        _lookup.GetEntitiesInRange(Transform(joe).Coordinates, range, _mobs, LookupFlags.Uncontained);

        var seared = 0;
        foreach (var target in _mobs)
        {
            if (target.Owner == joe.Owner || !_mobState.IsAlive(target, target.Comp))
                continue;

            if (!EWJoeTargetRules.IsXenofauna(EntityManager, target) || !_examine.InRangeUnOccluded(joe.Owner, target.Owner, range))
                continue;

            _damageable.TryChangeDamage(target.Owner, burn, origin: joe.Owner, tool: joe.Owner);
            var mark = EnsureComp<EWJoeSearedComponent>(target.Owner);
            var until = now + joe.Comp.SearDuration;
            if (until > mark.Until)
                mark.Until = until;

            StripPheromones(target.Owner);
            seared++;
        }

        return seared;
    }

    private void UpdateSeared(TimeSpan now)
    {
        _expired.Clear();
        var query = EntityQueryEnumerator<EWJoeSearedComponent>();
        while (query.MoveNext(out var uid, out var seared))
        {
            if (now >= seared.Until)
            {
                _expired.Add(uid);
                continue;
            }

            StripPheromones(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<EWJoeSearedComponent>(uid);
        }
    }

    /// <summary>The receiver halves of recovery, warding and frenzy; the emitters re-apply them each frame.</summary>
    private void StripPheromones(EntityUid uid)
    {
        if (HasComp<XenoRecoveryPheromonesComponent>(uid))
            RemComp<XenoRecoveryPheromonesComponent>(uid);
        if (HasComp<XenoWardingPheromonesComponent>(uid))
            RemComp<XenoWardingPheromonesComponent>(uid);
        if (HasComp<XenoFrenzyPheromonesComponent>(uid))
            RemComp<XenoFrenzyPheromonesComponent>(uid);
    }

    #endregion

    #region 5B Saturate (xeno)

    /// <summary>
    /// Saturate's xeno branch: the xenofauna is dazed and its xeno armor is shredded for a while
    /// (<see cref="EWJoeArmorShredSystem"/>). False when the Joe has no
    /// Saturate (xeno) or the target is not xenofauna.
    /// </summary>
    public bool SaturateXeno(EntityUid joe, EntityUid target)
    {
        if (!TryComp(joe, out EWJoeSaturateXenoComponent? saturate) || !EWJoeTargetRules.IsXenofauna(EntityManager, target))
            return false;

        _daze.TryDaze(target, saturate.DazeTime, refresh: true);

        // the shared armor shred (2A Chitin Crack) carries the strip: the larger and the later win, never stacked
        _shred.Shred(target, saturate.ArmorStrip, saturate.ArmorStripDuration);

        return true;
    }

    #endregion
}
