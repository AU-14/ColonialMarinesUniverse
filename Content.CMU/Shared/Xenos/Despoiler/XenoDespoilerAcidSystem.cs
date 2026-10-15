using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Stab;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._RMC14.Xenonids.Despoiler;

public sealed partial class XenoDespoilerAcidSystem : SharedXenoDespoilerAcidSystem
{
    [Dependency] private XenoDespoilerHypertensionSystem _hyper = default!;
    [Dependency] private XenoSpitSystem _xenoSpit = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private XenoSystem _xeno = default!; // CMU14

    private EntityQuery<XenoComponent> _xenoQuery;
    private EntityQuery<XenoDespoilerComponent> _despoilerQuery;
    private EntityQuery<XenoDespoilerHypertensionComponent> _hyperQuery;
    // private EntityQuery<MarineComponent> _marineQuery; // CMU14: use the shared reward eligibility check
    private EntityQuery<UserAcidedComponent> _userAcidedQuery;
    private EntityQuery<XenoDespoilerAcidTierComponent> _tierQuery;

    public override void Initialize()
    {
        _xenoQuery = GetEntityQuery<XenoComponent>();
        _despoilerQuery = GetEntityQuery<XenoDespoilerComponent>();
        _hyperQuery = GetEntityQuery<XenoDespoilerHypertensionComponent>();
        // _marineQuery = GetEntityQuery<MarineComponent>(); // CMU14
        _userAcidedQuery = GetEntityQuery<UserAcidedComponent>();
        _tierQuery = GetEntityQuery<XenoDespoilerAcidTierComponent>();

        SubscribeLocalEvent<XenoDespoilerSlashOnHitComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<XenoDespoilerSlashOnHitComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<XenoDespoilerSlashOnHitComponent, RMCGetTailStabBonusDamageEvent>(OnGetTailStabBonusDamage);
        SubscribeLocalEvent<XenoDespoilerHypertensionComponent, DamageChangedEvent>(OnDamageTaken);
        SubscribeLocalEvent<UserAcidedComponent, ComponentShutdown>(OnAcidShutdown);
    }

    private void OnAcidShutdown(Entity<UserAcidedComponent> ent, ref ComponentShutdown args)
    {
        RemComp<XenoDespoilerAcidTierComponent>(ent);
    }

    private void OnGetMeleeDamage(EntityUid uid, XenoDespoilerSlashOnHitComponent comp, ref GetMeleeDamageEvent args)
    {
        if (TryGetHyperBurn(uid, out var burn))
            args.Damage += burn;
    }

    private void OnGetTailStabBonusDamage(EntityUid uid, XenoDespoilerSlashOnHitComponent comp, ref RMCGetTailStabBonusDamageEvent args)
    {
        if (TryGetHyperBurn(uid, out var burn))
            args.Damage += burn;
    }

    private void OnDamageTaken(EntityUid uid, XenoDespoilerHypertensionComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta is not { } delta)
            return;

        _hyper.AddPoints(uid, comp, (float) delta.GetTotal() * comp.PointsPerDamageTaken);
    }

    private bool TryGetHyperBurn(EntityUid uid, out DamageSpecifier burn)
    {
        burn = default!;
        if (!_hyperQuery.TryComp(uid, out var hyper) || hyper.Stacks <= 0)
            return false;

        var bonus = hyper.Stacks * hyper.BonusBurnPerStack;
        if (bonus <= 0)
            return false;

        burn = new DamageSpecifier();
        burn.DamageDict["Heat"] = FixedPoint2.New(bonus);
        return true;
    }

    // CMU14: enemy hive rewards share the existing target rules; acid still excludes xenos.
    private void OnMeleeHit(EntityUid uid, XenoDespoilerSlashOnHitComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        if (!_xenoQuery.HasComp(uid))
            return;

        _hyperQuery.TryComp(uid, out var hyper);

        foreach (var hit in args.HitEntities)
        {
            // Resolve this hit's acid before the reward can increase its stacks.
            if (hit != uid && !_xenoQuery.HasComp(hit) &&
                hyper != null && hyper.Stacks >= comp.EnhanceStacksThreshold)
                ApplyAcid(hit, uid);

            if (hyper != null && _xeno.CanGainRewardsFromTarget(uid, hit))
                _hyper.AddSlashPoints(uid, hyper);
        }
    }

    /// <summary>
    /// Apply the ability's acid tier. XenoSpitSystem owns its damage, duration and visuals;
    /// the separate Despoiler counter tracks the remaining finishing-stab bonus.
    /// </summary>
    public void ApplyAcid(EntityUid target, EntityUid caster, int acidTier = 1)
    {
        if (!_despoilerQuery.TryComp(caster, out var despoilerComp))
            return;

        if (TerminatingOrDeleted(target) || _mobState.IsDead(target) ||
            acidTier < 1 || acidTier > despoilerComp.AcidTiers.Count)
            return;

        _xenoSpit.ApplyAcidTier(target, acidTier, despoilerComp.AcidTiers[acidTier - 1]);

        var tier = EnsureComp<XenoDespoilerAcidTierComponent>(target);
        // The finishing-stab table includes its base bonus before tiers 1 through 3.
        tier.Tier = Math.Max(tier.Tier, _userAcidedQuery.Comp(target).Tier + 1);
        Dirty(target, tier);
    }

    /// <summary>
    /// Decrement the Despoiler acid tier on <paramref name="target"/> by one.
    /// Returns the tier value <em>before</em> the decrement, or 0 if no tier
    /// component was present.
    /// </summary>
    public int ConsumeAcidTier(EntityUid target)
    {
        if (!_tierQuery.TryComp(target, out var tier) || tier.Tier <= 0)
            return 0;

        var previous = tier.Tier;
        tier.Tier--;
        if (tier.Tier <= 0)
            RemComp<XenoDespoilerAcidTierComponent>(target);
        else
            Dirty(target, tier);

        return previous;
    }
}
