using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Robust.Shared.Timing;
using DiagnosticStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    private const float FallWakeDistance = 0.05f;

    private int _maxZTransitionsPerTick = 64;
    private TimeSpan _zTransitionBudget = TimeSpan.FromMilliseconds(1);
    private GameTick _zTransitionBudgetTick;
    private int _zTransitionsThisTick;
    private long _zTransitionBudgetStart;

    private void InitTransitionBudget()
    {
        Subs.CVar(_config, CMUZLevelsCVars.MaxFallsPerTick, value => _maxZTransitionsPerTick = Math.Max(0, value), true);
        Subs.CVar(_config, CMUZLevelsCVars.TransitionBudgetMs, value => _zTransitionBudget = TimeSpan.FromMilliseconds(Math.Max(0, value)), true);
    }

    protected override bool CanProcessZLevelTransition(EntityUid ent, int offset)
    {
        if (_maxZTransitionsPerTick <= 0)
            return false;

        var curTick = _gameTiming.CurTick;
        if (_zTransitionBudgetTick != curTick)
        {
            _zTransitionBudgetTick = curTick;
            _zTransitionsThisTick = 0;
            _zTransitionBudgetStart = DiagnosticStopwatch.GetTimestamp();
        }

        if (_zTransitionsThisTick >= _maxZTransitionsPerTick)
            return false;

        if (_zTransitionBudget > TimeSpan.Zero &&
            DiagnosticStopwatch.GetElapsedTime(_zTransitionBudgetStart) >= _zTransitionBudget)
        {
            return false;
        }

        _zTransitionsThisTick++;
        return true;
    }

    public override void WakeZPhysics(Entity<CMUZPhysicsComponent?> ent)
    {
        if (!_zLevelsEnabled ||
            !Resolve(ent, ref ent.Comp, false))
        {
            return;
        }

        var resolved = new Entity<CMUZPhysicsComponent>(ent.Owner, ent.Comp);
        if (!CanUseZPhysics(resolved))
        {
            RemCompDeferred<CMUZFallingComponent>(ent.Owner);
            return;
        }

        Entity<CMUZPhysicsComponent?> distanceEnt = (ent.Owner, ent.Comp);
        var distance = DistanceToGround(distanceEnt, out var stickyGround);
        if (!stickyGround &&
            MathF.Abs(distance) <= FallWakeDistance &&
            MathF.Abs(ent.Comp.LocalPosition) <= FallWakeDistance &&
            MathF.Abs(ent.Comp.Velocity) <= MinActiveZVelocity)
        {
            RemCompDeferred<CMUZFallingComponent>(ent.Owner);
            return;
        }

        EnsureComp<CMUZFallingComponent>(ent.Owner);
    }
}
