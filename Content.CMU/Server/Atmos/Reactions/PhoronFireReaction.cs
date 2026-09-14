using Content.Server.Atmos;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;

namespace Content.Server.CMU14.Atmos.Reactions;

/// <summary>
///     Burns phoron with oxygen into CO2. Ignites hotter than plasma and releases more
///     energy per mole (220 kJ vs plasma's 160, hydrogen's 284). Consequence: phoron
///     production chambers run above 673 K and must stay O2-free, or the product flashes
///     the moment it forms - O2 discipline is part of the refinement loop.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class PhoronFireReaction : IGasReactionEffect
{
    private const float OxygenFullburn = 2f;      // O2 moles consumed per phoron burned
    private const float BurnRateDelta = 20f;      // higher = slower burn
    private const float EnergyReleased = 220e3f;  // hotter than plasma, below hydrogen

    public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
    {
        var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        var temperature = mixture.Temperature;
        var location = holder as TileAtmosphere;
        mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;

        var initialPhoron = mixture.GetMoles(Gas.Phoron);
        var initialOxygen = mixture.GetMoles(Gas.Oxygen);

        // Limit by the limiting reactant, then by the burn rate.
        var burnedFuel = MathF.Min(initialPhoron, initialOxygen / OxygenFullburn) / BurnRateDelta;

        if (burnedFuel <= 0)
            return ReactionResult.NoReaction;

        mixture.AdjustMoles(Gas.Phoron, -burnedFuel);
        mixture.AdjustMoles(Gas.Oxygen, -burnedFuel * OxygenFullburn);
        // Conservation of mass.
        mixture.AdjustMoles(Gas.CarbonDioxide, burnedFuel * (1f + OxygenFullburn));
        mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel * (1f + OxygenFullburn);

        var energyReleased = EnergyReleased * burnedFuel / heatScale;
        var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
        if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
            mixture.Temperature = (temperature * oldHeatCapacity + energyReleased) / newHeatCapacity;

        if (location != null && mixture.Temperature > Atmospherics.FireMinimumTemperatureToExist)
            atmosphereSystem.HotspotExpose(location, mixture.Temperature, mixture.Volume);

        return ReactionResult.Reacting;
    }
}
