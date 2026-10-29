using Content.Shared.CMU14.Chemistry.Effects.Reaction;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Chemistry.Reagent;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Chemistry.Effects;

/// <summary>
/// Spawns a diamond of RMC tile fire around the holder of the reacting solution. The solution entity
/// lives in its holder's container, so its mover coordinates are the holder's tile.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class CMUSolutionTileFireSystem : EntityEffectSystem<TransformComponent, CMUSolutionTileFire>
{
    private static readonly EntProtoId DefaultFire = "RMCTileFire";

    [Dependency] private RMCReagentSystem _reagent = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<CMUSolutionTileFire> args)
    {
        if (entity.Comp.MapID == MapId.Nullspace)
            return;

        var effect = args.Effect;
        var spawn = effect.FireEntity ?? DefaultFire;
        if (effect.FireEntity == null &&
            effect.Reagent is { } reagentId &&
            _reagent.TryIndex(reagentId, out var reagent))
        {
            spawn = reagent.FireEntity;
        }

        var center = _transform.GetMoverCoordinates(entity, entity.Comp).SnapToGrid(EntityManager);
        // A trace of fuel lights its own tile; the full range needs range² units to burn at once.
        var range = Math.Clamp((int) MathF.Sqrt(args.Scale), 1, Math.Max(1, effect.Range));
        _rmcFlammable.SpawnFireDiamond(spawn, center, range, effect.Intensity, effect.Duration);
    }
}
