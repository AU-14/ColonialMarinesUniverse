using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.CMU14.Traits.DrugAllergy;

namespace Content.Server.CMU14.Traits.DrugAllergy;

/// <summary>
///     A health scanner picks up an allergic reaction while it's happening, and what it's a reaction to.
/// </summary>
public sealed class CMUAllergyScannerSystem : EntitySystem
{
    [Dependency] private RMCReagentSystem _reagents = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<DrugAllergyComponent, CMUHealthScannerReadingEvent>(OnScannerReading);
    }

    private void OnScannerReading(Entity<DrugAllergyComponent> ent, ref CMUHealthScannerReadingEvent args)
    {
        if (!ent.Comp.ReactionActive)
            return;

        args.State.CMUAllergicReaction = ent.Comp.Allergen is { } allergen &&
                                        _reagents.TryIndex(allergen, out var reagent)
            ? reagent.LocalizedName
            : string.Empty;
    }
}
