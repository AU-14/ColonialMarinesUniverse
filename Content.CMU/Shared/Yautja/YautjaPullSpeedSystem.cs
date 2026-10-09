using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaPullSpeedSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<YautjaComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // upstream PullerComponent hardcodes 0.95x on anyone pulling anything, on top of SlowOnPull.
        // preds drag at full speed in cmss13, so cancel it back out. held-speed pulls (crates etc) keep their own math
        if (!TryComp(ent, out PullerComponent? puller) ||
            puller.Pulling is not { } pulling ||
            HasComp<HeldSpeedModifierComponent>(pulling) ||
            puller.WalkSpeedModifier <= 0f ||
            puller.SprintSpeedModifier <= 0f)
        {
            return;
        }

        args.ModifySpeed(1f / puller.WalkSpeedModifier, 1f / puller.SprintSpeedModifier);
    }
}
