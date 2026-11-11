using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared.Chemistry.Components; // CMU14: reaction fallback below
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.Maps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server.EntityEffects.Effects.Solution;

/// <summary>
/// This effect creates smoke at this solution's position.
/// The amount of smoke created is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class AreaReactionEntityEffectsSystem : EntityEffectSystem<TransformComponent, AreaReactionEffect>
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SpreaderSystem _spreader = default!;
    [Dependency] private TurfSystem _turf = default!;

    // TODO: A sane way to make Smoke without a solution.
    protected override void Effect(Entity<TransformComponent> entity, ref EntityEffectEvent<AreaReactionEffect> args)
    {
        // CMU14: reactions never pass a reagent context, so fall back to the reacting solution itself.
        // without this every smoke/foam reaction (mfhs, weedkiller, chem smoke) silently did nothing
        var source = args.ReagentContext?.Source;
        if (source == null && TryComp<SolutionComponent>(entity, out var reacting))
            source = reacting.Solution;

        if (source == null)
            return;

        var quantity = args.ReagentContext is { } context ? context.Quantity.Quantity.Float() : args.Scale; // CMU14: reaction scale = units reacted

        var xform = entity.Comp;
        var mapCoords = _xform.GetMapCoordinates(entity);
        var spreadAmount = (int) Math.Max(
            0,
            Math.Ceiling(quantity / args.Effect.OverflowThreshold));
        var effect = args.Effect;

        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid) ||
            !_map.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef))
            return;

        if (_spreader.RequiresFloorToSpread(effect.PrototypeId.ToString()) && _turf.IsSpace(tileRef))
            return;

        var coords = _map.MapToGrid(gridUid, mapCoords);
        var ent = Spawn(args.Effect.PrototypeId, coords.SnapToGrid());
        var splitSolution = source.SplitSolution(source.Volume);

        _smoke.StartSmoke(ent, splitSolution, args.Effect.Duration, spreadAmount);

        _audio.PlayPvs(args.Effect.Sound, entity, AudioParams.Default.WithVariation(0.25f));
    }
}
