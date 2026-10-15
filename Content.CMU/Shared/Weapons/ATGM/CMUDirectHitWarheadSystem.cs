using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.ATGM;

/// <summary>
///     Applies <see cref="CMUDirectHitWarheadComponent"/>: extra direct-hit damage against xenonids, a chance to
///     bring a xenonid the hit left standing down to crit (and spare it the warhead's own blast, so the result is
///     crit rather than death), and gibbing of whitelisted bodies struck directly.
/// </summary>
public sealed partial class CMUDirectHitWarheadSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> CritDamageType = "Blunt";

    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDestructibleSystem _destructible = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private RMCGibSystem _rmcGib = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDirectHitWarheadComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<CMUDirectHitWarheadComponent, AfterProjectileHitEvent>(OnAfterProjectileHit);
        SubscribeLocalEvent<CMUDirectHitBlastShieldComponent, GetExplosionResistanceEvent>(OnBlastShieldResistance);
        SubscribeLocalEvent<CMUDirectHitBlastShieldComponent, ExplosionReceivedEvent>(OnBlastShieldConsumed);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUDirectHitBlastShieldComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (now >= shield.ExpiresAt)
                RemCompDeferred<CMUDirectHitBlastShieldComponent>(uid);
        }
    }

    private void OnBlastShieldResistance(Entity<CMUDirectHitBlastShieldComponent> ent, ref GetExplosionResistanceEvent args)
    {
        if (args.ExplosionPrototype == ent.Comp.ExplosionType.Id && _timing.CurTime < ent.Comp.ExpiresAt)
            args.DamageCoefficient = 0;
    }

    /// <summary>
    ///     The shield covers one blast: it is spent when the matching explosion reaches the xeno, so a busy
    ///     explosion queue delaying that blast cannot outlast it. The expiry is only a cleanup fallback.
    /// </summary>
    private void OnBlastShieldConsumed(Entity<CMUDirectHitBlastShieldComponent> ent, ref ExplosionReceivedEvent args)
    {
        if (args.Explosion == ent.Comp.ExplosionType)
            RemCompDeferred<CMUDirectHitBlastShieldComponent>(ent);
    }

    private void OnProjectileHit(Entity<CMUDirectHitWarheadComponent> ent, ref ProjectileHitEvent args)
    {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (ent.Comp.XenoDamageMultiplier == 1f || !HasComp<XenoComponent>(args.Target))
            return;

        args.Damage = args.Damage * ent.Comp.XenoDamageMultiplier;
    }

    private void OnAfterProjectileHit(Entity<CMUDirectHitWarheadComponent> ent, ref AfterProjectileHitEvent args)
    {
        // Damage is only applied on the server, and gibbing is server-only (GibbingSystem does nothing on the client).
        if (_net.IsClient)
            return;

        var target = args.Target;
        if (TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target))
            return;

        if (HasComp<XenoComponent>(target))
        {
            TryCritXeno(ent, target, args.Projectile.Comp.Shooter);
            return;
        }

        if (!TryComp(target, out BodyComponent? body) ||
            !_whitelist.IsWhitelistPass(ent.Comp.GibWhitelist, target) ||
            _whitelist.IsWhitelistPass(ent.Comp.GibBlacklist, target) ||
            !_destructible.CanDestroy(target))
        {
            return;
        }

        // Gear survives the blast instead of vanishing with the body. The body itself is queued for deletion,
        // so the rest of the collision and the warhead's explosion can still run this tick.
        _rmcGib.ScatterInventoryItems(target);
        _body.GibBody(target, true, body);
    }

    /// <summary>
    ///     Rolls <see cref="CMUDirectHitWarheadComponent.XenoCritChance"/> for a xenonid the direct hit left below its
    ///     critical threshold. On success its damage is topped up to exactly that threshold and it is shielded from
    ///     this warhead's own explosion, which is queued by the same impact: without the shield the blast lands on
    ///     top of the threshold and kills thinly armoured T3s outright. The top-up bypasses armour (the hit already
    ///     went through it) but not shields or invulnerability. RMC's critical grace still applies, so the xeno
    ///     drops a moment later. A xeno the hit already took past crit is left to the blast.
    /// </summary>
    private void TryCritXeno(Entity<CMUDirectHitWarheadComponent> ent, EntityUid target, EntityUid? shooter)
    {
        if (ent.Comp.XenoCritChance <= 0 ||
            !_mobState.IsAlive(target) ||
            !TryComp(target, out DamageableComponent? damageable) ||
            !_mobThreshold.TryGetThresholdForState(target, MobState.Critical, out var threshold))
        {
            return;
        }

        var needed = threshold.Value - _damageable.GetTotalDamage((target, damageable));
        if (needed <= 0 || !_random.Prob(Math.Clamp(ent.Comp.XenoCritChance, 0f, 1f)))
            return;

        var damage = new DamageSpecifier(_prototype.Index(CritDamageType), needed);
        _damageable.TryChangeDamage(target,
            damage,
            ignoreResistances: true,
            damageable: damageable,
            origin: shooter,
            tool: ent.Owner,
            ignoreGlobalModifiers: true);

        // A xeno shield or invulnerability can soak or cancel the top-up; only a xeno actually brought to crit
        // earns the blast shield, otherwise the roll would leave it better off than losing it.
        if (_damageable.GetTotalDamage((target, damageable)) < threshold.Value)
            return;

        if (TryComp(ent, out ExplosiveComponent? explosive))
        {
            var shield = EnsureComp<CMUDirectHitBlastShieldComponent>(target);
            shield.ExplosionType = explosive.ExplosionType;
            shield.ExpiresAt = _timing.CurTime + ent.Comp.XenoCritBlastShield;
        }
    }
}
