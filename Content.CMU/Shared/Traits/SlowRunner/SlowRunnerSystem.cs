using Content.Shared.Movement.Systems;

namespace Content.Shared._CMU14.Traits.SlowRunner;

public sealed class SlowRunnerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlowRunnerComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<SlowRunnerComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(1f, ent.Comp.SprintSpeedModifier);
    }
}
