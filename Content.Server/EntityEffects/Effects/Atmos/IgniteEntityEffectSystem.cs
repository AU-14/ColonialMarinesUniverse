using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Atmos;

namespace Content.Server.EntityEffects.Effects.Atmos;

/// <summary>
/// Sets this entity on fire.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class IngiteEntityEffectSystem : EntityEffectSystem<FlammableComponent, Ignite>
{
    [Dependency] private FlammableSystem _flammable = default!;

    protected override void Effect(Entity<FlammableComponent> entity, ref EntityEffectEvent<Ignite> args)
    {
        _flammable.Ignite(entity, args.ReagentContext?.Organ ?? entity.Owner, flammable: entity.Comp);
    }
}
