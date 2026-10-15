using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Insurgency;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Insurgency;

// Every INSFOR faction gets the ATGM on its points vendor, not only the vanilla CLF (FLOW-12 (d)).
public sealed partial class InsurgencyFactionApplySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    /// <summary>
    ///     The vanilla CLF points rack. Its YAML entries are the single source of the ATGM prices and stock
    ///     guaranteed to every faction, so a balance change there reaches custom factions too.
    /// </summary>
    private static readonly EntProtoId AntiArmorReferenceVendor = "AU14CLFObjectiveWeaponsVendor";

    private static readonly EntProtoId[] AntiArmorItems = { "CMUATGMCase", "CMUATGMMissile" };

    // Section name is a Loc key; the vendor UI localizes section names when a key exists.
    private const string AntiArmorSection = "cmu-insfor-vendor-section-anti-armor";

    /// <summary>
    ///     Custom and DB factions carry their own copy of their vendor sections, so the CLF rack's YAML never
    ///     reaches them. Adds whichever of the ATGM kit and missile the faction does not already stock to its
    ///     main points vendor, at the CLF rack's prices. Free caches (no priced entries) are left alone, and a
    ///     faction that already stocks an item anywhere, including through a base-model vendor, keeps its own.
    ///     Runs on the server-owned runtime copy, so the stored faction and the editor never see the addition.
    /// </summary>
    private void EnsureAntiArmorAccess(FactionDefinition definition)
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

        if (target == null || !TryGetPrototypeVendor(AntiArmorReferenceVendor, out var reference))
            return;

        var missing = new List<CMVendorEntry>();
        foreach (var item in AntiArmorItems)
        {
            if (stocked.Contains(item.Id))
                continue;

            if (FindEntry(reference.Sections, item) is not { } entry)
            {
                Log.Error($"{AntiArmorReferenceVendor} no longer stocks {item}; INSFOR factions will not receive it.");
                continue;
            }

            missing.Add(entry with { LinkedEntries = new(entry.LinkedEntries), Multiplier = null, Max = null });
        }

        if (missing.Count == 0)
            return;

        var section = target.Sections.Find(s => s.Name == AntiArmorSection);
        if (section == null && target.Sections.Count < FactionDefinition.MaxVendorSections)
        {
            section = new CMVendorSection { Name = AntiArmorSection };
            target.Sections.Add(section);
        }

        // At the section cap, the last section takes the entries instead of exceeding it.
        section ??= target.Sections[^1];
        section.Entries.AddRange(missing);
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
