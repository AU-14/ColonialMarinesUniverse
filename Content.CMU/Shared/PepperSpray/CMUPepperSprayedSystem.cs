using Content.Shared.Movement.Systems;

namespace Content.Shared.CMU14.PepperSpray;

/// <summary>
///     Being pepper sprayed slows you down, more with every spray you take.
/// </summary>
public sealed class CMUPepperSprayedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPepperSprayedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CMUPepperSprayedComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<CMUPepperSprayedComponent, ComponentShutdown>(OnShutdown);
    }

    public float GetSpeedMultiplier(CMUPepperSprayedComponent comp)
    {
        return 1f - Math.Clamp(comp.Intensity * comp.SlowPerIntensity, 0f, comp.MaxSlow);
    }

    private void OnRefreshSpeed(Entity<CMUPepperSprayedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var multiplier = GetSpeedMultiplier(ent.Comp);
        args.ModifySpeed(multiplier, multiplier);
    }

    private void OnState(Entity<CMUPepperSprayedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    private void OnShutdown(Entity<CMUPepperSprayedComponent> ent, ref ComponentShutdown args)
    {
        if (!TerminatingOrDeleted(ent))
            _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }

    public void SetIntensity(Entity<CMUPepperSprayedComponent> ent, float intensity)
    {
        ent.Comp.Intensity = Math.Max(0f, intensity);
        Dirty(ent);
        _speed.RefreshMovementSpeedModifiers(ent.Owner);
    }
}
