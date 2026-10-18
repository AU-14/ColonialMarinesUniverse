using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.Traits.Phobias;

/// <summary>
///     Slows someone who can't swim while they're in water, more so in deep water.
/// </summary>
public sealed class CMUCantSwimSpeedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCantSwimComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUCantSwimComponent, AfterAutoHandleStateEvent>(OnState);
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
