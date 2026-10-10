using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Atmos.Reactions; // CMU14: gas fires spawn RMC tile fires
using Content.Shared.Atmos;
using Content.Shared.Atmos.Reactions;
using JetBrains.Annotations;
using Robust.Shared.Prototypes; // CMU14

namespace Content.Server.Atmos.Reactions
{
    [UsedImplicitly]
    [DataDefinition]
    public sealed partial class TritiumFireReaction : IGasReactionEffect
    {
        private static readonly EntProtoId TileFire = "CMUTileFireTritium"; // CMU14: green fire, the anti-xeno variant, and radioactive while it burns

        public ReactionResult React(GasMixture mixture, IGasMixtureHolder? holder, AtmosphereSystem atmosphereSystem, float heatScale)
        {
            var energyReleased = 0f;
            var oldHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
            var temperature = mixture.Temperature;
            var location = holder as TileAtmosphere;
            mixture.ReactionResults[(byte)GasReaction.Fire] = 0f;
            var burnedFuel = 0f;
            var initialTrit = mixture.GetMoles(Gas.Tritium);

            if (mixture.GetMoles(Gas.Oxygen) < initialTrit ||
                Atmospherics.MinimumTritiumOxyburnEnergy > (temperature * oldHeatCapacity * heatScale))
            {
                burnedFuel = mixture.GetMoles(Gas.Oxygen) / Atmospherics.TritiumBurnOxyFactor;
                if (burnedFuel > initialTrit)
                    burnedFuel = initialTrit;

                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
                mixture.AdjustMoles(Gas.Oxygen, -burnedFuel / Atmospherics.TritiumBurnFuelRatio);
            }
            else
            {
                // Limit the amount of fuel burned by the limiting reactant, either our initial tritium or the amount of oxygen available given the burn ratio.
                burnedFuel = Math.Min(initialTrit, mixture.GetMoles(Gas.Oxygen) / Atmospherics.TritiumBurnFuelRatio) / Atmospherics.TritiumBurnTritFactor;
                mixture.AdjustMoles(Gas.Tritium, -burnedFuel);
                mixture.AdjustMoles(Gas.Oxygen, -burnedFuel / Atmospherics.TritiumBurnFuelRatio);
                energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel * (Atmospherics.TritiumBurnTritFactor - 1));
            }

            if (burnedFuel > 0)
            {
                energyReleased += (Atmospherics.FireHydrogenEnergyReleased * burnedFuel);

                // TODO ATMOS Radiation pulse here!

                // Conservation of mass is important.
                mixture.AdjustMoles(Gas.WaterVapor, burnedFuel);

                mixture.ReactionResults[(byte)GasReaction.Fire] += burnedFuel;
            }

            energyReleased /= heatScale; // adjust energy to make sure speedup doesn't cause mega temperature rise
            if (energyReleased > 0)
            {
                var newHeatCapacity = atmosphereSystem.GetHeatCapacity(mixture, true);
                if (newHeatCapacity > Atmospherics.MinimumHeatCapacity)
                    mixture.Temperature = ((temperature * oldHeatCapacity + energyReleased) / newHeatCapacity);
            }

            if (location != null)
            {
                temperature = mixture.Temperature;
                if (temperature > Atmospherics.FireMinimumTemperatureToExist)
                {
                    atmosphereSystem.HotspotExpose(location, temperature, mixture.Volume);
                    CMUGasFireBridge.SpawnTileFire(location, TileFire); // CMU14
                }
            }

            return mixture.ReactionResults[(byte)GasReaction.Fire] != 0 ? ReactionResult.Reacting : ReactionResult.NoReaction;
        }
    }
}
