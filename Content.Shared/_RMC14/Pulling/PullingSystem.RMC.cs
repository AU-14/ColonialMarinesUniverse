using Content.Shared._RMC14.Fireman;
using Content.Shared._RMC14.Pulling;
using Content.Shared.Movement.Pulling.Components;

namespace Content.Shared.Movement.Pulling.Systems;

public sealed partial class PullingSystem
{
    [Dependency] private RMCPullingSystem _rmcPulling = default!;

    private bool HandleRMCPullToggle(EntityUid puller)
    {
        var ev = new RMCPullToggleEvent();
        RaiseLocalEvent(puller, ref ev);
        return ev.Handled;
    }

    private void RetargetRMCPull(EntityUid puller, ref EntityUid target, ref PullableComponent? pullable)
    {
        if (_rmcPulling.TryRetargetPull(puller, target) is not { } retarget)
            return;

        target = retarget;
        pullable = CompOrNull<PullableComponent>(retarget);
    }

    private bool TryRetargetBuckledPull(Entity<PullableComponent> pulled)
    {
        if (pulled.Comp.Puller is not { } puller ||
            _rmcPulling.TryRetargetPull(puller, pulled) is not { } retarget)
        {
            return false;
        }

        TryStartPull(puller, retarget);
        return true;
    }
}
