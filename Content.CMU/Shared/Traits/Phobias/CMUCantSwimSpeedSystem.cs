using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;

namespace Content.Shared.CMU14.Traits.Phobias;

/// <summary>
///     Slows someone who can't swim while they're in water, more so in deep water.
/// </summary>
public sealed class CMUCantSwimSpeedSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCantSwimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUCantSwimComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CMUCantSwimComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<CMUCantSwimComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUCantSwimComponent, StandAttemptEvent>(OnStandAttempt);
    }

    private void OnStandAttempt(Entity<CMUCantSwimComponent> ent, ref StandAttemptEvent args)
    {
        if (ent.Comp.ForcedDown)
            args.Cancel();
    }

    public void SetForcedDown(Entity<CMUCantSwimComponent> ent, bool down)
    {
        if (ent.Comp.ForcedDown == down)
            return;

        ent.Comp.ForcedDown = down;
        Dirty(ent);
        if (down)
            _standing.Down(ent);
        else
            _standing.Stand(ent);
    }

    private void OnUpdateCanMove(Entity<CMUCantSwimComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Stuck)
            args.Cancel();
    }

    private void OnShutdown(Entity<CMUCantSwimComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Stuck && !TerminatingOrDeleted(ent))
            _actionBlocker.UpdateCanMove(ent);
    }

    public void SetStuck(Entity<CMUCantSwimComponent> ent, bool stuck)
    {
        if (ent.Comp.Stuck == stuck)
            return;

        ent.Comp.Stuck = stuck;
        Dirty(ent);
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnRefreshSpeed(Entity<CMUCantSwimComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var multiplier = ent.Comp.Water switch
        {
            CMUSwimWater.Shallow => ent.Comp.ShallowSpeed,
            CMUSwimWater.Deep => ent.Comp.DeepSpeed,
            _ => 1f,
        };

        if (multiplier < 1f)
            args.ModifySpeed(multiplier, multiplier);
    }

    private void OnState(Entity<CMUCantSwimComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
        _actionBlocker.UpdateCanMove(ent);
    }

    public void SetWater(Entity<CMUCantSwimComponent> ent, CMUSwimWater water)
    {
        if (ent.Comp.Water == water)
            return;

        ent.Comp.Water = water;
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
