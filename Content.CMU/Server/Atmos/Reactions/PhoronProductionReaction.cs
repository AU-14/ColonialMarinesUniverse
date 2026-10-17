using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.CMU14.Atmos.Reactions;

/// <summary>
///     Refines plasma into phoron: hot nitrogen fixation inside a narrow temperature window.
///     Below the window nothing fixes; above it a fire burns the feedstock before it converts.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class PhoronProductionReaction : IGasReactionEffect
{
    private const float NitrogenRatio = 0.5f;   // N2 consumed per plasma reacted
    private const float ConversionRate = 40f;   // higher = slower per tick
    private const float YieldRatio = 0.8f;      // losses to unreacted byproducts

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var plasma = mixture.GetMoles(Gas.Plasma);
        var nitrogen = mixture.GetMoles(Gas.Nitrogen);

        var n2Limit = nitrogen / NitrogenRatio;
        var reacting = MathF.Min(plasma, n2Limit) / ConversionRate;

        if (reacting <= 0)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Plasma, -reacting);
        mixture.AdjustMoles(Gas.Nitrogen, -reacting * NitrogenRatio);
        mixture.AdjustMoles(Gas.Phoron, reacting * YieldRatio);

        return ReactionResult.Reacting;
    }
}
