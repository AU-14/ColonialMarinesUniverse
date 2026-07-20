using Content.Shared._RMC14.DoAfter;
using Robust.Shared.Network;

namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    [Dependency] private RMCDoAfterSystem _rmcDoAfter = default!;
    [Dependency] private INetManager _rmcNetManager = default!;

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

        if (_rmcNetManager.IsServer)
            SpawnAttachedTo(doAfter.Args.TargetEffect, targetTransform.Coordinates);

        doAfter.LastEffectSpawnTime = time;
    }
}
