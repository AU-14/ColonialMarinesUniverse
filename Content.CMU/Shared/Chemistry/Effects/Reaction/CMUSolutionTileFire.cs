using Content.Shared.Chemistry.Reagent;
using Content.Shared.Database;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Reaction;

/// <summary>
/// Reaction effect that sets the tiles around the reacting solution's holder on fire, as a diamond of
/// <see cref="Range"/> tiles. The procgen generator puts it on a fuel or oxidizer's ignition reaction.
/// Executed server-side by <c>CMUSolutionTileFireSystem</c>.
/// </summary>
public sealed partial class CMUSolutionTileFire : EntityEffectBase<CMUSolutionTileFire>
{
    /// <summary>
    /// The reagent whose fire this is. Its <see cref="ReagentPrototype.FireEntity"/> is used when
    /// <see cref="FireEntity"/> is not set.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>The tile fire to spawn. Falls back to the reagent's fire, then to red RMC tile fire.</summary>
    [DataField]
    public EntProtoId? FireEntity;

    /// <summary>Diamond radius in tiles. 1 sets the holder's tile and its four neighbours alight.</summary>
    [DataField]
    public int Range = 1;

    /// <summary>Fire intensity. Null keeps the tile fire's default.</summary>
    [DataField]
    public int? Intensity;

    /// <summary>Fire duration. Null keeps the tile fire's default.</summary>
    [DataField]
    public int? Duration;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-entity-effect-guidebook-solution-tile-fire",
            ("chance", Probability),
            ("range", Range));

    public override LogImpact? Impact => LogImpact.High;
}
