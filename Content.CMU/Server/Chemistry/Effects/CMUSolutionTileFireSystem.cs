using System.Linq;
using Content.Shared.CMU14.Chemistry.Effects.Reaction;
using Content.Shared.CMU14.Chemistry.Fuel;
using Content.Shared.Chemistry.Components;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Content.Shared._RMC14.Atmos;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Chemistry.Effects;

/// <summary>
/// FLOW-22: sets the floor around a reacting solution's holder alight with the solution's own fire spec. The effect
/// runs on the solution entity (it lives in its holder's container, so its mover coordinates are the holder's tile).
/// The reaction has already removed the reagent it burns, so that reagent is added back to resolve the
/// <see cref="CMUFireSource.Container"/> spec of the whole reacting solution; the fire fills a circle of
/// √units radius capped by the spec. Every other fuel in the solution burns away with it, so a blend lights once.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class CMUSolutionTileFireSystem : EntityEffectSystem<TransformComponent, CMUSolutionTileFire>
{
    [Dependency] private CMUFuelProfileSystem _fuel = default!;
    [Dependency] private SharedRMCFlammableSystem _rmcFlammable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<CMUSolutionTileFire> args)
    {
        var effect = args.Effect;
        TryComp(entity, out SolutionComponent? solutionComp);
        if (entity.Comp.MapID == MapId.Nullspace)
        {
            LeaveResidue(entity, solutionComp, effect, args.Scale, popup: false);
            return;
        }

        var whole = solutionComp?.Solution.Clone() ?? new Solution();
        whole.MaxVolume = FixedPoint2.MaxValue;
        if (effect.Reagent is { } reagent && args.Scale > 0)
            whole.AddReagent(reagent.Id, FixedPoint2.New(args.Scale));

        if (!_fuel.TryResolveFire(whole, CMUFireSource.Container, out var spec))
        {
            // Not a fuel (an oxidizer on its own, say): only a legacy fixed-stat fire, if the YAML asks for one.
            if (effect.Intensity is not { } intensity || intensity <= 0)
            {
                // FLOW-02: too diluted to burn. The reaction already removed the reagent; show where it went.
                LeaveResidue(entity, solutionComp, effect, args.Scale, popup: true);
                return;
            }

            var duration = effect.Duration ?? CMUFuelProfileSystem.ReferenceDuration;
            spec = CMUFuelProfileSystem.CreateSpec(Math.Min(intensity, CMUFuelProfileSystem.NonSpecialistMaxIntensity),
                Math.Min(duration, CMUFuelProfileSystem.ContainerMaxDuration),
                Math.Max(1, effect.Range),
                effect.FireEntity ?? CMUFuelProfileSystem.GenericFire,
                0);
        }

        if (effect.FireEntity is { } fireEntity)
            spec = spec with { BehaviourEntity = fireEntity };

        var center = _transform.GetMoverCoordinates(entity, entity.Comp).SnapToGrid(EntityManager);
        var radius = CMUFuelProfileSystem.CircleRadius(whole.Volume, spec);
        _rmcFlammable.SpawnFireCircle(spec.BehaviourEntity, center, radius, spec);

        // The fire consumed the fuel: burn off the rest of the blend so its own ignition reactions do not light it again.
        if (solutionComp == null)
            return;

        var solution = solutionComp.Solution;
        foreach (var quantity in solution.Contents.ToArray())
        {
            if (_fuel.IsFuel(quantity.Reagent.Prototype))
                solution.RemoveReagent(quantity.Reagent, quantity.Quantity);
        }
    }

    /// <summary>
    /// FLOW-02: an ignition that forms no fire turns the burnt reagent into <see cref="CMUSolutionTileFire.Residue"/>
    /// instead of deleting it silently. The residue is no fuel and no reactant of the ignition, so it cannot loop.
    /// </summary>
    private void LeaveResidue(Entity<TransformComponent> entity, SolutionComponent? solutionComp, CMUSolutionTileFire effect,
        float scale, bool popup)
    {
        if (solutionComp == null || effect.Residue is not { } residue || scale <= 0)
            return;

        solutionComp.Solution.AddReagent(residue.Id, FixedPoint2.New(scale));
        Dirty(entity.Owner, solutionComp);
        if (popup)
        {
            _popup.PopupCoordinates(Loc.GetString("cmu-solution-ignition-fizzle"),
                _transform.GetMoverCoordinates(entity, entity.Comp), PopupType.MediumCaution);
        }
    }
}
