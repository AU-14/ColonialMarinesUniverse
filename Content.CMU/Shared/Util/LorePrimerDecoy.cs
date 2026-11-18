using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.util;

/// <summary>
/// CMU14 FLOW-611: picks the primer whose knowledge levels marines are actually told about this round. With
/// <see cref="LorePrimerPrototype.DecoyChance"/> it is one of <see cref="LorePrimerPrototype.DecoyPrimers"/>, so
/// marine intel can point at xenos or neomorphs while the real threat is something else.
/// </summary>
public static class LorePrimerDecoy
{
    public static LorePrimerPrototype Resolve(LorePrimerPrototype primer, IPrototypeManager prototypes, IRobustRandom random)
    {
        if (primer.DecoyChance <= 0f ||
            primer.DecoyPrimers is not { Count: > 0 } decoys ||
            !random.Prob(Math.Clamp(primer.DecoyChance, 0f, 1f)))
        {
            return primer;
        }

        // a decoy with nothing to say leaves the real intel in place
        return prototypes.TryIndex(random.Pick(decoys), out var decoy) && decoy.KnowledgeLevels is { Count: > 0 }
            ? decoy
            : primer;
    }
}
