using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Reaction;

/// <summary>
/// Reaction effect that sets the floor around the reacting solution's holder alight. FLOW-22: the fire is the
/// <c>Container</c> fire spec of the whole reacting solution (what is left plus the <see cref="Reagent"/> the reaction
/// consumed), so a fuel burns with the same statistics as in a flamer, in a circle of √units radius capped by the
/// spec's radius. Put on generated <c>{ID}Ignition</c> reactions and on the stock fuels' heat-ignition reactions.
/// Executed server-side by <c>CMUSolutionTileFireSystem</c>.
/// </summary>
public sealed partial class CMUSolutionTileFire : EntityEffectBase<CMUSolutionTileFire>
{
    /// <summary>
    /// The reagent the reaction burns (one unit per unit reaction). It is added back when the fire spec is resolved,
    /// because the reaction has already removed it from the solution.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>Overrides the spec's behaviour entity. Colour always comes from the intensity tier.</summary>
    [DataField]
    public EntProtoId? FireEntity;

    /// <summary>Legacy: the radius now comes from √units and the spec. Kept so older YAML still loads.</summary>
    [DataField]
    public int Range = 1;

    /// <summary>Fallback intensity when the solution resolves to no fire spec (not a fuel). Null: no fire.</summary>
    [DataField]
    public int? Intensity;

    /// <summary>Fallback duration when the solution resolves to no fire spec. Null: the reference duration.</summary>
    [DataField]
    public int? Duration;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-entity-effect-guidebook-solution-tile-fire",
            ("chance", Probability),
            ("range", Range));

    public override LogImpact? Impact => LogImpact.High;
}
