using Content.Shared._RMC14.DoAfter;
#if !EXCEPTION_TOLERANCE
using Robust.Shared.Network;
#endif

namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    [Dependency] private RMCDoAfterSystem _rmcDoAfter = default!;
#if !EXCEPTION_TOLERANCE
    [Dependency] private INetManager _rmcNetManager = default!;
#endif

    private bool ShouldCancelRMC(DoAfter doAfter)
    {
        return _rmcDoAfter.ShouldCancel(doAfter);
    }

    private void UpdateRMCTargetEffect(DoAfter doAfter, TimeSpan time)
    {
        if (doAfter.Args.TargetEffect == null ||
            doAfter.LastEffectSpawnTime is { } last && time - last < TimeSpan.FromSeconds(1) ||
            !TryComp(doAfter.Args.Target, out TransformComponent? targetTransform))
        {
            return;
        }

        if (IsRMCServer())
            SpawnAttachedTo(doAfter.Args.TargetEffect, targetTransform.Coordinates);

        doAfter.LastEffectSpawnTime = time;
    }

    private bool IsRMCServer()
    {
#if EXCEPTION_TOLERANCE
        return _netManager.IsServer;
#else
        return _rmcNetManager.IsServer;
#endif
    }
}
