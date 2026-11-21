using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship.DirectFire;

namespace Content.Server.CMU14.Dropship;

/// <summary>
/// Re-runs dropship fire control diagnostics whenever a booted dropship lands.
/// </summary>
public sealed class DropshipFireControlLandingSystem : EntitySystem
{
    [Dependency] private DropshipFireControlBootSystem _boot = default!;

    public override void Initialize()
    {
        // broadcast sub, the per-component FTLCompletedEvent slots are already taken
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp(args.Entity, out DropshipComponent? dropship))
            return;
        _boot.OnLanded(args.Entity, dropship.Crashed);
    }
}
