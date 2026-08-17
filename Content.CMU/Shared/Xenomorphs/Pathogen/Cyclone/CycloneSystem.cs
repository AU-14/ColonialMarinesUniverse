using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;
using Content.Shared.FixedPoint;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Cyclone;

public sealed partial class CMUXenoCycloneSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RMCSlowSystem _slow = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    private readonly HashSet<Entity<MobStateComponent>> _hits = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUXenoCycloneComponent, CMUXenoCycloneActionEvent>(OnAction);
        SubscribeLocalEvent<CMUXenoCycloneComponent, CMUXenoCycloneDoAfterEvent>(OnDoAfter);
    }

    private void OnAction(Entity<CMUXenoCycloneComponent> xeno, ref CMUXenoCycloneActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        _popup.PopupClient(
            Loc.GetString("cmu14-xeno-cyclone-charge"),
            xeno, xeno, PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.ActivationDelay,
            new CMUXenoCycloneDoAfterEvent(), xeno)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            BlockDuplicate = true
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<CMUXenoCycloneComponent> xeno, ref CMUXenoCycloneDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var hitCount = SpinHit(xeno, xeno.Comp.BaseRange, xeno.Comp.BaseDamage, stun: true);

        if (hitCount < xeno.Comp.MinHitsForCycles)
            return;

        // Trigger extra cycles via coroutine-style timer chain
        if (_net.IsServer)
            ScheduleCycles(xeno, 0, xeno.Comp.BaseRange);
    }

    private int SpinHit(Entity<CMUXenoCycloneComponent> xeno, float range, float damage, bool stun)
    {
        _hits.Clear();
        _lookup.GetEntitiesInRange(_transform.GetMapCoordinates(xeno), range, _hits);

        var count = 0;
        foreach (var target in _hits)
        {
            if (!_xeno.CanAbilityAttackTarget(xeno, target))
                continue;

            if (_mobState.IsDead(target))
                continue;

            count++;

            if (!_net.IsServer)
                continue;

            _damageable.TryChangeDamage(target, new DamageSpecifier
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Blunt", damage }
                }
            }, origin: xeno);

            if (stun)
                _stun.TryKnockdown(target, TimeSpan.FromSeconds(1), true);
            else
                _slow.TrySlowdown(target, TimeSpan.FromSeconds(1));
        }

        return count;
    }

    private void ScheduleCycles(Entity<CMUXenoCycloneComponent> xeno, int currentCycle, float range)
    {
        if (currentCycle >= xeno.Comp.Cycles)
            return;

        var delay = xeno.Comp.CycleDelay - TimeSpan.FromSeconds(0.5 * currentCycle);
        if (delay < TimeSpan.FromSeconds(1.5))
            delay = TimeSpan.FromSeconds(1.5);

        Timer.Spawn(delay, () =>
        {
            if (TerminatingOrDeleted(xeno))
                return;

            var nextRange = Math.Min(range + xeno.Comp.RangeGrowthPerCycle, 4f);
            SpinHit(xeno, nextRange, xeno.Comp.CycleDamage, stun: false);
            ScheduleCycles(xeno, currentCycle + 1, nextRange);
        });
    }
}
