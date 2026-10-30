using Content.Shared.CMU14.Pulling;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaThrallHeroSystem : EntitySystem
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RMCStatusEffectSystem _statusEffects = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaThrallHeroComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<YautjaThrallHeroComponent, YautjaMarkForHuntActionEvent>(OnMarkForHunt);
    }

    /// <summary>
    /// Turns a linked thrall into the cmss13 Thrall species. Server only, the component carries the result.
    /// </summary>
    public void Apply(EntityUid thrall)
    {
        if (_net.IsClient ||
            !HasComp<HumanoidProfileComponent>(thrall) ||
            HasComp<YautjaComponent>(thrall) ||
            HasComp<YautjaThrallHeroComponent>(thrall))
        {
            return;
        }

        var hero = AddComp<YautjaThrallHeroComponent>(thrall);

        if (_mobThreshold.TryGetThresholdForState(thrall, MobState.Critical, out var crit) &&
            _mobThreshold.TryGetThresholdForState(thrall, MobState.Dead, out var dead))
        {
            // dead first, so crit never sits above it for a moment
            _mobThreshold.SetMobStateThreshold(thrall, dead.Value + hero.ExtraHealth, MobState.Dead);
            _mobThreshold.SetMobStateThreshold(thrall, crit.Value + hero.ExtraHealth, MobState.Critical);
            hero.AppliedExtraHealth = hero.ExtraHealth;
        }

        if (TryComp(thrall, out RMCStunResistanceComponent? stunResistance))
            hero.PreviousStunResistance = stunResistance.Resistance;

        if (hero.PreviousStunResistance is not { } previous || previous < hero.StunResistance)
        {
            _statusEffects.GiveStunResistance(thrall, hero.StunResistance);
            hero.AddedStunResistance = true;
        }

        // EnsureComp answers true when the component was already there
        hero.AddedNoPullSlowdown = !EnsureComp<CMUNoPullSlowdownComponent>(thrall, out _);
        hero.AddedIgnoreWeeds = !EnsureComp<IgnoreXenoWeedsSlowdownComponent>(thrall, out _);

        if (!HasComp<YautjaButcherAbilityComponent>(thrall))
        {
            var butcher = AddComp<YautjaButcherAbilityComponent>(thrall);
            butcher.RequiresBlade = false;
            hero.AddedButcher = true;
        }

        _actions.AddAction(thrall, ref hero.MarkForHuntAction, hero.MarkForHuntActionId);
        _movement.RefreshMovementSpeedModifiers(thrall);
    }

    public void Remove(EntityUid thrall)
    {
        if (_net.IsClient || TerminatingOrDeleted(thrall) || !TryComp(thrall, out YautjaThrallHeroComponent? hero))
            return;

        if (hero.AppliedExtraHealth > FixedPoint2.Zero &&
            _mobThreshold.TryGetThresholdForState(thrall, MobState.Critical, out var crit) &&
            _mobThreshold.TryGetThresholdForState(thrall, MobState.Dead, out var dead))
        {
            _mobThreshold.SetMobStateThreshold(thrall, crit.Value - hero.AppliedExtraHealth, MobState.Critical);
            _mobThreshold.SetMobStateThreshold(thrall, dead.Value - hero.AppliedExtraHealth, MobState.Dead);
        }

        if (hero.AddedStunResistance)
        {
            if (hero.PreviousStunResistance is { } previous)
                _statusEffects.GiveStunResistance(thrall, previous);
            else
                RemComp<RMCStunResistanceComponent>(thrall);
        }

        if (hero.AddedNoPullSlowdown)
            RemComp<CMUNoPullSlowdownComponent>(thrall);

        if (hero.AddedIgnoreWeeds)
            RemComp<IgnoreXenoWeedsSlowdownComponent>(thrall);

        if (hero.AddedButcher)
            RemComp<YautjaButcherAbilityComponent>(thrall);

        _actions.RemoveAction(thrall, hero.MarkForHuntAction);
        _marks.ClearPreyMarks(thrall);
        RemComp<YautjaThrallHeroComponent>(thrall);
        _movement.RefreshMovementSpeedModifiers(thrall);
    }

    private void OnDamageModify(Entity<YautjaThrallHeroComponent> ent, ref DamageModifyEvent args)
    {
        var brute = _prototypes.Index(BruteGroup);
        var burn = _prototypes.Index(BurnGroup);
        DamageSpecifier? modified = null;
        foreach (var (type, amount) in args.Damage.DamageDict)
        {
            // only incoming damage, healing goes through untouched
            if (amount <= FixedPoint2.Zero)
                continue;

            float multiplier;
            if (brute.DamageTypes.Contains(type))
                multiplier = ent.Comp.BruteMultiplier;
            else if (burn.DamageTypes.Contains(type))
                multiplier = ent.Comp.BurnMultiplier;
            else
                continue;

            modified ??= new DamageSpecifier(args.Damage);
            modified.DamageDict[type] = amount * multiplier;
        }

        if (modified != null)
            args.Damage = modified;
    }

    private void OnMarkForHunt(Entity<YautjaThrallHeroComponent> ent, ref YautjaMarkForHuntActionEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner)
            return;

        args.Handled = _marks.TryToggleThrallPrey(ent.Owner, args.Target);
    }
}
