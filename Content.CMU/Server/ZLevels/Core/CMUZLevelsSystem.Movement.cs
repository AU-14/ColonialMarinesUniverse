using System.Threading;
using Content.Shared._CMU14.ZLevels;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Robust.Shared.Timing;
using DiagnosticStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    protected override bool ZPhysicsEnabled => ZLevelsEnabled;

    private int _maxZTransitionsPerTick = 64;
    private long _zTransitionBudgetTicks = TimeSpan.FromMilliseconds(1).Ticks;
    private GameTick _zTransitionBudgetTick;
    private int _zTransitionsThisTick;
    private long _zTransitionBudgetStart;

    private void InitTransitionBudget()
    {
        Subs.CVar(
            _config,
            CMUZLevelsCVars.MaxFallsPerTick,
            value => Interlocked.Exchange(ref _maxZTransitionsPerTick, Math.Max(0, value)),
            true);
        Subs.CVar(
            _config,
            CMUZLevelsCVars.TransitionBudgetMs,
            value => Interlocked.Exchange(
                ref _zTransitionBudgetTicks,
                TimeSpan.FromMilliseconds(Math.Max(0, value)).Ticks),
            true);
    }

    protected override bool CanProcessZLevelTransition(EntityUid ent, int offset)
    {
        var maxTransitionsPerTick = Interlocked.CompareExchange(ref _maxZTransitionsPerTick, 0, 0);
        if (maxTransitionsPerTick <= 0)
            return false;

        var curTick = _gameTiming.CurTick;
        if (_zTransitionBudgetTick != curTick)
        {
            _zTransitionBudgetTick = curTick;
            _zTransitionsThisTick = 0;
            _zTransitionBudgetStart = DiagnosticStopwatch.GetTimestamp();
        }

        if (_zTransitionsThisTick >= maxTransitionsPerTick)
            return false;

        var transitionBudgetTicks = Interlocked.Read(ref _zTransitionBudgetTicks);
        if (transitionBudgetTicks > 0 &&
            DiagnosticStopwatch.GetElapsedTime(_zTransitionBudgetStart) >=
            TimeSpan.FromTicks(transitionBudgetTicks))
        {
            return false;
        }

        return true;
    }

    protected override void RecordZLevelTransition(EntityUid ent, int offset)
    {
        _zTransitionsThisTick++;
    }

    public override bool WakeZPhysics(Entity<CMUZPhysicsComponent?> ent)
    {
        if (!Prof.IsEnabled)
            return WakeZPhysicsCore(ent);

        using var profile = Prof.Group("CMU Z Wake");
        return WakeZPhysicsCore(ent);
    }

    private bool WakeZPhysicsCore(Entity<CMUZPhysicsComponent?> ent)
    {
        if (!ShouldWakeZPhysics(ent))
        {
            RemCompDeferred<CMUZFallingComponent>(ent.Owner);
            return false;
        }

        EnsureComp<CMUZFallingComponent>(ent.Owner);
        return true;
    }
}
