using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Botany;

/// <summary>
/// CMU14 (COUPLING-02): a gene-aware seed filter. AutoPorts keys items by prototype, so every packet of one species looks the
/// same to it; this reads the genes from the seed's plant snapshot (falling back to the species prototype) instead.
/// An empty filter accepts every seed.
/// </summary>
[DataDefinition]
public sealed partial class CMUSeedFilter
{
    /// <summary>Plant prototypes (species) accepted. Empty accepts every species.</summary>
    [DataField]
    public List<EntProtoId> Species = new();

    [DataField]
    public float? MinPotency;

    [DataField]
    public int? MinYield;

    [DataField]
    public bool RejectSeedless;

    public bool IsEmpty => Species.Count == 0 && MinPotency == null && MinYield == null && !RejectSeedless;
}

public sealed class CMUSeedGenesSystem : EntitySystem
{
    [Dependency] private readonly BotanySystem _botany = default!;

    /// <summary>The plant genes a seed packet carries: its snapshot, or the species prototype when it has none.</summary>
    public bool TryGetGenes(SeedComponent seed, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PlantComponent? genes)
    {
        return _botany.TryGetPlantComponent(seed.PlantData, seed.PlantProtoId, out genes);
    }

    public bool Matches(CMUSeedFilter filter, SeedComponent seed)
    {
        if (filter.Species.Count > 0 && !filter.Species.Contains(seed.PlantProtoId))
            return false;

        if (filter.MinPotency != null || filter.MinYield != null)
        {
            if (!TryGetGenes(seed, out var genes))
                return false;

            if (filter.MinPotency is { } potency && genes.Potency < potency)
                return false;

            if (filter.MinYield is { } yield && genes.Yield < yield)
                return false;
        }

        if (filter.RejectSeedless &&
            _botany.TryGetPlantComponent<PlantTraitSeedlessComponent>(seed.PlantData, seed.PlantProtoId, out _))
        {
            return false;
        }

        return true;
    }
}
