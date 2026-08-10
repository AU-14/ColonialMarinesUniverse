#nullable enable

using System.Linq;
using Content.Shared.CMU.Round;

namespace Content.Shared._RMC14.Vendors;

public abstract partial class SharedCMAutomatedVendorSystem
{
    /// <summary>
    /// Replaces one live vendor's inventory from detached round-plan data and rebuilds all runtime indexes.
    /// </summary>
    public void ApplyRoundVendorProfile(
        Entity<CMAutomatedVendorComponent> vendor,
        ResolvedRoundVendorProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var sections = new List<CMVendorSection>(profile.Sections.Length);
        foreach (var resolvedSection in profile.Sections)
        {
            var entries = new List<CMVendorEntry>(resolvedSection.Entries.Length);
            foreach (var resolvedEntry in resolvedSection.Entries)
            {
                entries.Add(new CMVendorEntry
                {
                    Id = resolvedEntry.Product,
                    Amount = resolvedEntry.Amount,
                });
            }

            sections.Add(new CMVendorSection
            {
                Name = resolvedSection.Name,
                Choices = resolvedSection.Choice is { } choice
                    ? (choice.Id, choice.Amount)
                    : null,
                TakeAll = resolvedSection.TakeAll,
                Entries = entries,
            });
        }

        vendor.Comp.Jobs = profile.Jobs.ToList();
        vendor.Comp.Sections = sections;
        RebuildVendorInventory(vendor);
    }
}
