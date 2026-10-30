using Content.Server.Shuttles.Components;
using Robust.Shared.Timing;

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    [Dependency] private IGameTiming _cmuDockTiming = default!;
    private readonly Dictionary<EntityUid, (EntityUid Other, EntityUid? Grid, GameTick Tick)> _cmuDockTransfers = new();

    private void RememberCmuDockTransfer(Entity<DockingComponent> dock)
    {
        if (dock.Comp.DockedWith is { } other)
            _cmuDockTransfers[dock] = (other, Transform(dock).GridUid, _cmuDockTiming.CurTick);
    }

    private void RestoreCmuDockTransfer(Entity<DockingComponent> dock)
    {
        if (!_cmuDockTransfers.Remove(dock, out var transfer) ||
            transfer.Tick != _cmuDockTiming.CurTick ||
            Transform(dock).GridUid == transfer.Grid ||
            !TryComp(transfer.Other, out DockingComponent? other) ||
            Transform(dock).GridUid == Transform(transfer.Other).GridUid ||
            !CanDock(dock, (transfer.Other, other)))
            return;

        Dock(dock, (transfer.Other, other));
        _console.RefreshShuttleConsoles();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        // An ordinary unanchor must not reconnect a port when it is anchored again later.
        foreach (var (dock, transfer) in _cmuDockTransfers)
        {
            if (transfer.Tick != _cmuDockTiming.CurTick)
                _cmuDockTransfers.Remove(dock);
        }
    }

    public override void Shutdown()
    {
        _cmuDockTransfers.Clear();
        base.Shutdown();
    }
}
