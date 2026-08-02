using System.Linq;
using Content.Server.Botany.Components;
using Content.Server.Botany.Systems;
using Content.Server.Popups;
using Content.Shared.Atmos;
using Content.Shared.Random;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Botany;

public sealed partial class MutationSystem : EntitySystem
{
    [Dependency] private IRobustRandom _robustRandom = default!;
    [Dependency] private PopupSystem _popups = default!;
    [Dependency] private PlantHolderSystem _plantHolder = default!;
    [Dependency] private IGameTiming _time = default!;

    /// <summary>
    /// For each random mutation, see if it occurs on this plant this check.
    /// </summary>
    /// <param name="seed"></param>
    /// <param name="severity"></param>
    public void CheckRandomMutations(EntityUid plantHolder, ref SeedData seed, float severity)
    {
        // RMC14 disables random seed mutations.
    }

    /// <summary>
    /// Checks all defined mutations against a seed to see which of them are applied.
    /// </summary>
    public void MutateSeed(EntityUid plantHolder, ref SeedData seed, float severity)
    {
        if (!seed.Unique)
        {
            Log.Error($"Attempted to mutate a shared seed");
            return;
        }
        CheckRandomMutations(plantHolder, ref seed, severity);
    }

    public void MutateSpecies(Entity<PlantHolderComponent> plantHolder, ref SeedData seed, float severity)
    {
        if (plantHolder.Comp.Seed is null)
            return;
        string lastname = seed.DisplayName;
        string mvar = string.Empty;
        var seedints = ProtoMan.GetInstances<SeedPrototype>();
        if (seed.MutationPrototypes.Count > 0 && seed.Immutable == false)
            mvar = _robustRandom.Pick(seed.MutationPrototypes);
        if (seedints.ContainsKey(mvar))
            plantHolder.Comp.Seed = seedints[mvar]; //plantHolder.Comp.Seed.SpeciesChange(seedints[mvar]);
        else return;
        plantHolder.Comp.Dead = false;
        _plantHolder.Mutate(plantHolder.Owner, 1, 0, plantHolder.Comp);
        plantHolder.Comp.Age = 0;
        plantHolder.Comp.Health = plantHolder.Comp.Seed.Endurance;
        plantHolder.Comp.LastCycle = _time.CurTime;
        plantHolder.Comp.Harvest = false;
        plantHolder.Comp.WeedLevel = 0;

        _popups.PopupEntity(Loc.GetString("hydro-mutate-species", ("OLD", lastname), ("NEW", plantHolder.Comp.Seed.DisplayName)),
            plantHolder.Owner);
    }

    public SeedData Cross(SeedData a, SeedData b)
    {
        SeedData result = b.Clone();

        CrossChemicals(ref result.Chemicals, a.Chemicals);

        CrossFloat(ref result.NutrientConsumption, a.NutrientConsumption);
        CrossFloat(ref result.WaterConsumption, a.WaterConsumption);
        CrossFloat(ref result.IdealHeat, a.IdealHeat);
        CrossFloat(ref result.HeatTolerance, a.HeatTolerance);
        CrossFloat(ref result.IdealLight, a.IdealLight);
        CrossFloat(ref result.LightTolerance, a.LightTolerance);
        CrossFloat(ref result.ToxinsTolerance, a.ToxinsTolerance);
        CrossFloat(ref result.LowPressureTolerance, a.LowPressureTolerance);
        CrossFloat(ref result.HighPressureTolerance, a.HighPressureTolerance);
        CrossFloat(ref result.PestTolerance, a.PestTolerance);
        CrossFloat(ref result.WeedTolerance, a.WeedTolerance);

        CrossFloat(ref result.Endurance, a.Endurance);
        CrossInt(ref result.Yield, a.Yield);
        CrossFloat(ref result.Lifespan, a.Lifespan);
        CrossFloat(ref result.Maturation, a.Maturation);
        CrossFloat(ref result.Production, a.Production);
        CrossFloat(ref result.Potency, a.Potency);

        CrossBool(ref result.Seedless, a.Seedless);
        CrossBool(ref result.Ligneous, a.Ligneous);
        CrossBool(ref result.TurnIntoKudzu, a.TurnIntoKudzu);
        CrossBool(ref result.CanScream, a.CanScream);

        CrossGasses(ref result.ExudeGasses, a.ExudeGasses);
        CrossGasses(ref result.ConsumeGasses, a.ConsumeGasses);

        // LINQ Explanation
        // For the list of mutation effects on both plants, use a 50% chance to pick each one.
        // Union all of the chosen mutations into one list, and pick ones with a Distinct (unique) name.
        result.Mutations = result.Mutations.Where(m => Random(0.5f)).Union(a.Mutations.Where(m => Random(0.5f))).DistinctBy(m => m.Name).ToList();

        // Hybrids have a high chance of being seedless. Balances very
        // effective hybrid crossings.
        if (a.Name != result.Name && Random(0.7f))
        {
            result.Seedless = true;
        }

        return result;
    }

    private void CrossChemicals(ref Dictionary<string, SeedChemQuantity> val, Dictionary<string, SeedChemQuantity> other)
    {
        // Go through chemicals from the pollen in swab
        foreach (var otherChem in other)
        {
            // if both have same chemical, randomly pick potency ratio from the two.
            if (val.ContainsKey(otherChem.Key))
            {
                val[otherChem.Key] = Random(0.5f) ? otherChem.Value : val[otherChem.Key];
            }
            // if target plant doesn't have this chemical, has 50% chance to add it.
            else
            {
                if (Random(0.5f))
                {
                    var fixedChem = otherChem.Value;
                    fixedChem.Inherent = false;
                    val.Add(otherChem.Key, fixedChem);
                }
            }
        }

        // if the target plant has chemical that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisChem in val)
        {
            if (!other.ContainsKey(thisChem.Key))
            {
                if (Random(0.5f))
                {
                    if (val.Count > 1)
                    {
                        val.Remove(thisChem.Key);
                    }
                }
            }
        }
    }

    private void CrossGasses(ref Dictionary<Gas, float> val, Dictionary<Gas, float> other)
    {
        // Go through gasses from the pollen in swab
        foreach (var otherGas in other)
        {
            // if both have same gas, randomly pick ammount from the two.
            if (val.ContainsKey(otherGas.Key))
            {
                val[otherGas.Key] = Random(0.5f) ? otherGas.Value : val[otherGas.Key];
            }
            // if target plant doesn't have this gas, has 50% chance to add it.
            else
            {
                if (Random(0.5f))
                {
                    val.Add(otherGas.Key, otherGas.Value);
                }
            }
        }
        // if the target plant has gas that the pollen in swab does not, 50% chance to remove it.
        foreach (var thisGas in val)
        {
            if (!other.ContainsKey(thisGas.Key))
            {
                if (Random(0.5f))
                {
                    val.Remove(thisGas.Key);
                }
            }
        }
    }
    private void CrossFloat(ref float val, float other)
    {
        val = Random(0.5f) ? val : other;
    }

    private void CrossInt(ref int val, int other)
    {
        val = Random(0.5f) ? val : other;
    }

    private void CrossBool(ref bool val, bool other)
    {
        val = Random(0.5f) ? val : other;
    }

    private bool Random(float p)
    {
        return _robustRandom.Prob(p);
    }
}
