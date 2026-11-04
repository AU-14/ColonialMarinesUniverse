using System.Linq;
using JetBrains.Annotations;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Botany.Systems;

/// <summary>
/// Handles the chemicals of a plant.
/// </summary>
public sealed partial class PlantChemicalsSystem : EntitySystem
{
    [Dependency] private BotanySystem _botany = default!;
    [Dependency] private PlantMutationSystem _mutation = default!;
    [Dependency] private IGameTiming _timing = default!;

    [SubscribeLocalEvent]
    private void OnCrossPollinate(Entity<PlantChemicalsComponent> ent, ref PlantCrossPollinateEvent args)
    {
        if (!_botany.TryGetPlantComponent<PlantChemicalsComponent>(args.PollenData, args.PollenProtoId, out var pollenData))
            return;

        _mutation.CrossChemicals(ent, ref ent.Comp.Chemicals, pollenData.Chemicals);
        Dirty(ent);
    }

    /// <summary>
    /// Adds a random chemical to the plant chemicals.
    /// </summary>
    [PublicAPI]
    public void MutateRandomChemical(
        Entity<PlantChemicalsComponent?> ent,
        WeightedRandomFillSolutionPrototype randomChems,
        IRobustRandom? random = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        random ??= SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(ent));

        ProtoId<ReagentPrototype> chemicalId;
        PlantChemQuantity quantity;
        if (TryComp<PlantSpecialChemicalsComponent>(ent, out var special)
            && special.Chemicals.Count > 0
            && random.Prob(0.4f))
        {
            // HashSet enumeration is not stable across processes. Sort before
            // selecting so client and server consume the same random stream.
            var chemicals = special.Chemicals
                .OrderBy(static reagent => reagent.Id, StringComparer.Ordinal)
                .ToArray();
            chemicalId = chemicals[random.Next(chemicals.Length)];
            quantity = new PlantChemQuantity
            {
                Min = 7,
                Max = 7 + random.Next(5, 9),
                PotencyDivisor = 1,
                Inherent = false,
            };
        }
        else
        {
            (chemicalId, _) = randomChems.Pick(random);
            quantity = new PlantChemQuantity
            {
                Min = 1,
                Max = random.Next(1, 3),
                PotencyDivisor = 1,
                Inherent = false,
            };
        }

        if (ent.Comp.Chemicals.TryAdd(chemicalId, quantity))
            Dirty(ent);
    }
}
