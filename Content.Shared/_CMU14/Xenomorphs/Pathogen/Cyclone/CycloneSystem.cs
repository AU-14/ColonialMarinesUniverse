using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Stun;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Plasma;
using Content.Shared._RMC14.Xenonids.Rotate;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Content.Shared.Coordinates;

namespace Content.Shared._CMU14.Xenomorphs.Pathogen.Cyclone;

public sealed partial class CMUXenoCycloneSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly RMCCameraShakeSystem _cameraShake = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _colorFlash = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly RMCSizeStunSystem _size = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly XenoPlasmaSystem _xenoPlasma = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;
    [Dependency] private readonly XenoRotateSystem _rotate = default!;

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

        if (!_xenoPlasma.TryRemovePlasmaPopup((xeno.Owner, null), xeno.Comp.PlasmaCost))
            return;

        args.Handled = true;

        _audio.PlayPredicted(xeno.Comp.WindupSound, xeno, xeno);

        _popup.PopupPredicted(
            Loc.GetString("cmu14-xeno-cyclone-charge"),
            Loc.GetString("cmu14-xeno-cyclone-charge-others", ("xeno", xeno.Owner)),
            xeno, xeno, PopupType.MediumCaution);

        var doAfter = new DoAfterArgs(EntityManager, xeno, xeno.Comp.ActivationDelay,
            new CMUXenoCycloneDoAfterEvent(), xeno)
        {
            BreakOnMove = false,
            BreakOnDamage = false,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<CMUXenoCycloneComponent> xeno, ref CMUXenoCycloneDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        // Execute the first cycle as a series of rapid spin ticks
        ExecuteCycleSpins(xeno, xeno.Comp.BaseRange, xeno.Comp.BaseDamage,
            xeno.Comp.SpinsPerCycle, firstCycle: true);
    }

    /// <summary>
    /// Schedules SpinCount spin ticks spaced SpinInterval apart.
    /// After the last tick, checks if extra cycles should fire.
    /// </summary>
    private void ExecuteCycleSpins(
        Entity<CMUXenoCycloneComponent> xeno,
        float range,
        float totalDamage,
        int spinCount,
        bool firstCycle)
    {
        var damagePerSpin = totalDamage / spinCount;
        var totalHits = 0;

        for (var i = 0; i < spinCount; i++)
        {
            var spinIndex = i;
            var isLastSpin = spinIndex == spinCount - 1;
            var delay = xeno.Comp.SpinInterval * spinIndex;

            Timer.Spawn(delay, () =>
            {
                if (TerminatingOrDeleted(xeno))
                    return;

                var hits = SpinHit(xeno, range, damagePerSpin,
                    knockdown: firstCycle,
                    isFirst: spinIndex == 0,
                    spinIndex: spinIndex,
                    spinCount: spinCount);

                totalHits += hits;

                // After last spin of the first cycle, decide on extra cycles
                if (firstCycle && isLastSpin && _net.IsServer)
                {
                    if (totalHits >= xeno.Comp.MinHitsForCycles)
                        ScheduleExtraCycles(xeno, 0, range);
                }
            });
        }
    }

    private int SpinHit(
        Entity<CMUXenoCycloneComponent> xeno,
        float range,
        float damage,
        bool knockdown,
        bool isFirst,
        int spinIndex,
        int spinCount)
    {
        if (_net.IsServer)
        {
            // Spin the xeno's facing a fixed amount per tick so it visibly rotates
            var anglePerSpin = Angle.FromDegrees(360.0 / spinCount);
            var currentAngle = _transform.GetWorldRotation(xeno) + anglePerSpin;
            _rotate.RotateXeno(xeno, currentAngle.GetDir());

            // Play a spin/slash animation on the xeno itself, not just on targets hit
            if (xeno.Comp.SpinAnimationId != null)
                SpawnAttachedTo(xeno.Comp.SpinAnimationId, xeno.Owner.ToCoordinates());
        }

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

            // Client: just flash
            var filter = Filter.Pvs(target, entityManager: EntityManager);
            _colorFlash.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
            _cameraShake.ShakeCamera(target, 2, 1);

            if (!_net.IsServer)
                continue;

            _damageable.TryChangeDamage(target, new DamageSpecifier
            {
                DamageDict = new Dictionary<string, FixedPoint2>
                {
                    { "Blunt", damage }
                }
            }, origin: xeno);

            if (knockdown && (!_size.TryGetSize(target, out var size) || size < RMCSizes.Big))
                _stun.TryParalyze(target, xeno.Comp.KnockdownTime, true);

            _audio.PlayPvs(xeno.Comp.SpinHitSound, target);
            SpawnAttachedTo(xeno.Comp.HitEffect, target.Owner.ToCoordinates());
        }

        // Play spin popup text on each spin tick, not just the first
        _popup.PopupPredicted(
            Loc.GetString("cmu14-xeno-cyclone-spin"),
            Loc.GetString("cmu14-xeno-cyclone-spin-others", ("xeno", xeno.Owner)),
            xeno, xeno, PopupType.LargeCaution);

        if (_net.IsServer)
            _audio.PlayPvs(xeno.Comp.SpinHitSound, xeno);

        return count;
    }

    private void ScheduleExtraCycles(Entity<CMUXenoCycloneComponent> xeno, int cycleIndex, float prevRange)
    {
        if (cycleIndex >= xeno.Comp.ExtraCycles)
            return;

        // Each subsequent cycle fires a bit faster and wider
        var delay = xeno.Comp.CycleDelay - TimeSpan.FromSeconds(0.4 * cycleIndex);
        if (delay < TimeSpan.FromSeconds(1.5))
            delay = TimeSpan.FromSeconds(1.5);

        var nextRange = Math.Min(prevRange + xeno.Comp.RangeGrowthPerCycle, xeno.Comp.MaxRange);
        var nextDamage = xeno.Comp.BaseDamage * xeno.Comp.CycleDamageMultiplier;

        Timer.Spawn(delay, () =>
        {
            if (TerminatingOrDeleted(xeno))
                return;

            ExecuteCycleSpins(xeno, nextRange, nextDamage,
                xeno.Comp.SpinsPerCycle, firstCycle: false);

            ScheduleExtraCycles(xeno, cycleIndex + 1, nextRange);
        });
    }
}