using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Insurgency;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Insurgency;

// Every INSFOR faction gets the ATGM (FLOW-12 (d)) and the cell's COMSEC fill cards on its points vendor,
// not only the vanilla CLF.
public sealed partial class InsurgencyFactionApplySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    ///     The vanilla CLF points rack. Its YAML entries are the single source of the prices and stock
    ///     guaranteed to every faction, so a balance change there reaches custom factions too.
    /// </summary>
    private static readonly EntProtoId GuaranteedStockReferenceVendor = "AU14CLFObjectiveWeaponsVendor";

    // section name is a Loc key, the vendor UI localizes section names when a key exists.
    // fill cards: a recrypto is only worth anything if the cell can buy cards for the sets it can't
    // reach over the air, and a faction authored without them had no COMSEC at all
    private static readonly (string Section, EntProtoId[] Items)[] GuaranteedStock =
    {
        ("cmu-insfor-vendor-section-anti-armor", new EntProtoId[] { "CMUATGMCase", "CMUATGMMissile" }),
        ("cmu-insfor-vendor-section-comsec", new EntProtoId[] { "ANPRCFillCardCLF" }),
    };

    /// <summary>
    ///     Custom and DB factions carry their own copy of their vendor sections, so the CLF rack's YAML never
    ///     reaches them. Adds whichever guaranteed item the faction does not already stock to its main points
    ///     vendor, at the CLF rack's prices. Free caches (no priced entries) are left alone, and a faction that
    ///     already stocks an item anywhere, including through a base-model vendor, keeps its own.
    ///     Runs on the server-owned runtime copy, so the stored faction and the editor never see the addition.
    /// </summary>
    private void EnsureGuaranteedStock(FactionDefinition definition)
    {
        var stocked = new HashSet<string>();
        FactionVendorDefinition? target = null;
        var targetPriced = 0;

        foreach (var vendor in definition.CellKit.VendorDefinitions)
        {
            if (vendor.UseBaseModelSections)
            {
                if (TryGetPrototypeVendor(vendor.BaseModel, out var baseVendor))
                    CollectStocked(baseVendor.Sections, stocked);

                continue;
            }

            CollectStocked(vendor.Sections, stocked);

            // Only a vendor spending the cell's intel points with at least one priced entry is a points vendor.
            // Pick the one with the most priced entries as the faction's main arsenal.
            var priced = vendor.UsesIntelPoints ? CountPriced(vendor.Sections) : 0;
            if (priced > targetPriced)
            {
                target = vendor;
                targetPriced = priced;
            }
        }

        if (target == null || !TryGetPrototypeVendor(GuaranteedStockReferenceVendor, out var reference))
            return;

        foreach (var (sectionName, items) in GuaranteedStock)
        {
            var missing = new List<CMVendorEntry>();
            foreach (var item in items)
            {
                if (stocked.Contains(item.Id))
                    continue;

                if (FindEntry(reference.Sections, item) is not { } entry)
                {
                    Log.Error($"{GuaranteedStockReferenceVendor} no longer stocks {item}; INSFOR factions will not receive it.");
                    continue;
                }

                missing.Add(entry with { LinkedEntries = new(entry.LinkedEntries), Multiplier = null, Max = null });
            }

            if (missing.Count == 0)
                continue;

            var section = target.Sections.Find(s => s.Name == sectionName);
            if (section == null && target.Sections.Count < FactionDefinition.MaxVendorSections)
            {
                section = new CMVendorSection { Name = sectionName };
                target.Sections.Add(section);
            }

            // At the section cap, the last section takes the entries instead of exceeding it.
            section ??= target.Sections[^1];
            section.Entries.AddRange(missing);
        }
    }

    private bool TryGetPrototypeVendor(EntProtoId id, out CMAutomatedVendorComponent vendor)
    {
        vendor = default!;
        return _prototype.TryIndex(id, out var proto)
            && proto.TryComp(out vendor!, EntityManager.ComponentFactory);
    }

    private static void CollectStocked(List<CMVendorSection> sections, HashSet<string> stocked)
    {
        foreach (var section in sections)
        {
            foreach (var entry in section.Entries)
                stocked.Add(entry.Id.Id);
        }
    }

    private static int CountPriced(List<CMVendorSection> sections)
    {
        var count = 0;
        foreach (var section in sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Points > 0)
                    count++;
            }
        }

        return count;
    }

    private static CMVendorEntry? FindEntry(List<CMVendorSection> sections, EntProtoId id)
    {
        foreach (var section in sections)
        {
            foreach (var entry in section.Entries)
            {
                if (entry.Id == id)
                    return entry;
            }
        }

        return null;
    }
}
