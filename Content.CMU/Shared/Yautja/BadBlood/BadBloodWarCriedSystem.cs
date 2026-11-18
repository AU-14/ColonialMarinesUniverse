using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-705: the speed of a clanmate who heard the War Cry, on both sides so movement is predicted. The server's
/// BadBloodLeaderPerksSystem adds and ends <see cref="BadBloodWarCriedComponent"/>.
/// </summary>
public sealed class BadBloodWarCriedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodWarCriedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BadBloodWarCriedComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BadBloodWarCriedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnStartup(Entity<BadBloodWarCriedComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<BadBloodWarCriedComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _movement.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnRefreshSpeed(Entity<BadBloodWarCriedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // the refresh on shutdown must already leave the boost out
        if (ent.Comp.LifeStage > ComponentLifeStage.Running)
            return;

        args.ModifySpeed(ent.Comp.SpeedMultiplier, ent.Comp.SpeedMultiplier);
    }
}
