using Content.Server._AU14.Chemistry.Reagents;
using Content.Server.Botany.Components;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Server.Botany;

public sealed partial class MutationSystem : EntitySystem
{
    [Dependency] private ServerReagentGeneratorSystem _gen = default!;


    public void MutateSeed(Entity<PlantHolderComponent> plantHolder, ref SeedData seed, float severity)
    {
        if (!seed.Unique)
        {
            Log.Error($"Attempted to mutate a shared seed");
            return;
        }
        if (severity == 0 || seed.Immutable)
            return;
        _popups.PopupEntity(Loc.GetString("hydro-mutate-quiver", ("PLANT", seed.DisplayName)), plantHolder.Owner);

        var totalMutations = _robustRandom.Next(1, 2 + (int)MathF.Round(severity));
        var MutCon = plantHolder.Comp.MutationController;
        bool MutationEnableCheck = false;
        List<int> SuperAllowedMutations = [];
        List<int> AllowedMutations = [];

        for (int i = 0; i < MutCon.Fields.Count - 1; i++)
        {
            string mutName = MutCon.Fields.GetAt(i).Key;
            if (MutCon.Fields[mutName] > 0)
            {
                SuperAllowedMutations.Add(i);
                MutationEnableCheck = true;
            }
            if ((MutCon.Fields[mutName] == 0 || MutCon.Fields[mutName] == -1) && MutationEnableCheck == false)
            {
                AllowedMutations.Add(i);
                if (MutCon.Fields[mutName] == -1)
                {
                    AllowedMutations.Insert(i, -i);
                }

            }
        }
        if (MutationEnableCheck)
        {
            AllowedMutations = SuperAllowedMutations;
        }

        for (int i = 0; i <= totalMutations + Math.Max(0, (int)MathF.Round(plantHolder.Comp.MutationLevel / 50)); i++)
        {
            int mutNumber = AllowedMutations[_robustRandom.Next(0, AllowedMutations.Count)];

            if (mutNumber < 0)
                return;

            switch (mutNumber)
            {
                case 1: // cancer
                    seed.Lifespan = Math.Max(0, seed.Lifespan - _robustRandom.NextFloat(1, 5));
                    seed.Endurance = Math.Max(0, seed.Endurance - _robustRandom.NextFloat(10, 20));
                    _popups.PopupEntity(Loc.GetString("hydro-mutate-wither", ("PLANT", seed.DisplayName)), plantHolder.Owner);
                    break;
                case 2: // gluttony
                    seed.NutrientConsumption = Math.Max(0, Math.Min(5,
                        seed.NutrientConsumption + _robustRandom.NextFloat(-(severity * 0.1f), (severity * 0.1f))));
                    seed.WaterConsumption = Math.Max(0, Math.Min(50,
                        seed.WaterConsumption + _robustRandom.NextFloat(-severity, severity)));
                    break;
                case 3: // endurance
                    seed.Endurance = Math.Max(10, Math.Min(100, seed.Endurance + (_robustRandom.NextFloat(-5.0f, 5.0f) * severity)));
                    break;
                case 4: // light tolerance
                    seed.IdealLight = Math.Max(0, Math.Min(30, seed.IdealLight + (_robustRandom.NextFloat(-1, 1) * severity)));
                    seed.LightTolerance = Math.Max(0, Math.Min(10, seed.LightTolerance + (_robustRandom.NextFloat(-2, 2) * severity)));
                    break;
                case 5: // tox tolerance
                    //yes it uses weed tolerance in cm13
                    seed.ToxinsTolerance = Math.Max(0, Math.Min(10, seed.WeedTolerance + (_robustRandom.NextFloat(-2, 2) * severity)));
                    break;
                case 6: // weed tolerance
                    seed.WeedTolerance = Math.Max(0, Math.Min(10, seed.WeedTolerance + (_robustRandom.NextFloat(-2, 2) * severity)));
                    if (_robustRandom.Prob((severity * 5) / 100))
                    {
                        //TODO: carnivorous
                    }
                    else if (_robustRandom.Prob((severity * 5) / 100))
                    {
                        //TODO: parasite
                    }
                    break;
                case 7: // production
                    seed.Production = Math.Max(1, Math.Min(10, seed.Production + (_robustRandom.NextFloat(-1, 1) * severity)));
                    break;
                case 8: // lifespan
                    seed.Lifespan = Math.Max(10, Math.Min(30, seed.Lifespan + (_robustRandom.NextFloat(-2, 2) * severity)));
                    if (seed.Yield != -1)
                    {
                        seed.Yield = Math.Max(0, Math.Min(10, seed.Yield +
                            (int)MathF.Round(_robustRandom.NextFloat(-2, 2) * severity)));
                    }
                    break;
                case 9: // potency
                    seed.Potency = Math.Max(0, Math.Min(200, seed.Potency + (_robustRandom.NextFloat(-20, 20) * severity)));
                    break;
                case 10: // maturity
                    seed.Maturation = Math.Max(0, Math.Min(30, seed.Maturation + (_robustRandom.NextFloat(-1, 1) * severity)));
                    if (_robustRandom.Prob((severity * 5) / 100f))
                    {
                        if (seed.HarvestRepeat == HarvestType.NoRepeat)
                            seed.HarvestRepeat = HarvestType.Repeat;
                        else if (seed.HarvestRepeat == HarvestType.Repeat)
                            seed.HarvestRepeat = HarvestType.NoRepeat;
                    }
                    break;
                case 11: // biolum
                    if (_robustRandom.Prob((severity * 2) / 100))
                    {
                        //TODO: bioluminescence
                    }
                    break;
                case 12: // flowers
                    // TODO: flowers
                    break;
                default:
                    string c1pick = _robustRandom.Pick(_gen.ChemicalGenClassesList["C1"]);
                    string c2pick = _robustRandom.Pick(_gen.ChemicalGenClassesList["C2"]);
                    string c3pick = _robustRandom.Pick(_gen.ChemicalGenClassesList["C3"]);
                    string c4pick = _robustRandom.Pick(_gen.ChemicalGenClassesList["C4"]);
                    Dictionary<string, float> weights = new()
                    {
                        {c1pick, 10f },
                        {c2pick, 15f },
                        {c3pick, 25f },
                        {c4pick, 30f }
                    };
                    SeedChemQuantity quant = new()
                    {
                        Min = 1,
                        Max = _robustRandom.Next(1, 3),
                        PotencyDivisor = 1,
                        Inherent = false
                    };
                    string pick = _robustRandom.Pick(weights);
                    if (_robustRandom.Prob(0.4f) && seed.SpecialChemicals.Count > 0)
                    {
                        pick = _robustRandom.Pick(seed.SpecialChemicals.Keys);
                        quant.Min = 7;
                        quant.Max = 7 + _robustRandom.Next(5, 9);
                    }
                    seed.Chemicals.TryAdd(pick, quant);
                    break;
            }
        }




    }
}
