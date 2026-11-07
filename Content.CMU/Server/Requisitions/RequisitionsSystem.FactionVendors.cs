using System;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Weapons.Ranged.Flamer;
using Content.Shared.CMU14.util;
using Content.Shared.Storage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    private void AppendFactionVendorGear(EntityUid consoleUid, RequisitionsComputerComponent comp)
    {
        var platoons = EntityManager.EntitySysManager.GetEntitySystem<PlatoonSpawnRuleSystem>();
        var platoon = comp.Faction switch
        {
            "govfor" => platoons.SelectedGovforPlatoon,
            "opfor" => platoons.SelectedOpforPlatoon,
            _ => null,
        };
        if (platoon == null)
            return;

        var sources = new List<RequisitionsPriceSource>();
        foreach (var category in comp.Categories)
        foreach (var order in category.Entries)
        {
            if (TryGetDeterministicManifest(order, out var manifest))
                sources.Add(new RequisitionsPriceSource(order.Cost, manifest));
        }

        var knownPrices = RequisitionsPriceCalculator.Calculate(sources);
        var comparablePrices = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in comp.Categories)
        {
            var prices = new List<int>();
            foreach (var order in category.Entries)
            {
                if (!TryGetDeterministicManifest(order, out var manifest))
                    continue;
                foreach (var item in manifest.Keys)
                {
                    if (knownPrices.TryGetValue(item, out var price))
                        prices.Add(price);
                }
            }

            if (prices.Count > 0)
                comparablePrices[category.Name] = prices;
        }
        var seen = new HashSet<EntProtoId>();
        var categoryEntries = new List<RequisitionsEntry>();
        foreach (var markerClass in new[] { PlatoonMarkerClass.ReqVend, PlatoonMarkerClass.ReqEquipment })
        {
            if (!platoons.TryResolvePlatoonVendor(platoon, markerClass, out var vendorId) ||
                !_prototypeManager.TryIndex<EntityPrototype>(vendorId, out var vendor) ||
                !vendor.TryComp<CMAutomatedVendorComponent>(
                    CompName.Get<CMAutomatedVendorComponent>(EntityManager.ComponentFactory), out var stock))
                continue;

            foreach (var section in stock.Sections)
            foreach (var entry in section.Entries)
            {
                if (!seen.Add(entry.Id) || !_prototypeManager.TryIndex<EntityPrototype>(entry.Id, out var itemPrototype))
                    continue;

                var price = knownPrices.TryGetValue(entry.Id, out var existing)
                    ? existing
                    : EstimateVendorItemCost(section.Name, entry.Points, comparablePrices);
                categoryEntries.Add(new RequisitionsEntry
                {
                    Name = itemPrototype.Name,
                    Crate = entry.Id,
                    Cost = price,
                    CMUFactionVendorItem = true,
                });
            }
        }

        if (categoryEntries.Count > 0)
        {
            comp.Categories.Add(new RequisitionsCategory
            {
                Name = Loc.GetString("cmu-asrs-category-faction-requisitions"),
                Entries = categoryEntries,
            });
        }

        // CMU14: gunner racks list ammunition directly; specialist racks issue cases.
        // Use the selected platoon's case contents so each specialist class receives
        // only its own ammunition, without also selling the case or its weapon.
        var ammoSeen = new HashSet<EntProtoId>();
        var specialistAmmo = new List<RequisitionsEntry>();
        foreach (var markerClass in new[] { PlatoonMarkerClass.Arifleman, PlatoonMarkerClass.SWeapons })
        {
            if (!platoons.TryResolvePlatoonVendor(platoon, markerClass, out var vendorId) ||
                !_prototypeManager.TryIndex<EntityPrototype>(vendorId, out var vendor) ||
                !vendor.TryComp<CMAutomatedVendorComponent>(
                    CompName.Get<CMAutomatedVendorComponent>(EntityManager.ComponentFactory), out var stock))
                continue;

            foreach (var section in stock.Sections)
            foreach (var entry in section.Entries)
            {
                AddSpecialistAmmo(entry.Id, section.Name);
                if (markerClass != PlatoonMarkerClass.SWeapons ||
                    !_prototypeManager.TryIndex<EntityPrototype>(entry.Id, out var casePrototype) ||
                    !casePrototype.TryComp<StorageFillComponent>(
                        CompName.Get<StorageFillComponent>(EntityManager.ComponentFactory), out var fill))
                    continue;

                foreach (var content in fill.Contents)
                {
                    if (content.PrototypeId is { } ammoId)
                        AddSpecialistAmmo(ammoId, "Specialist Ammo");
                }
            }
        }

        if (specialistAmmo.Count > 0)
        {
            comp.Categories.Add(new RequisitionsCategory
            {
                Name = Loc.GetString("cmu-asrs-category-faction-specialist-ammo"),
                Entries = specialistAmmo,
            });
        }

        Dirty(consoleUid, comp);

        void AddSpecialistAmmo(EntProtoId id, string section)
        {
            if (!_prototypeManager.TryIndex<EntityPrototype>(id, out var prototype) ||
                !IsSpecialistAmmunition(id, prototype) ||
                !ammoSeen.Add(id))
                return;

            var price = knownPrices.TryGetValue(id, out var existing)
                ? existing
                : EstimateVendorItemCost(section, null, comparablePrices);
            specialistAmmo.Add(new RequisitionsEntry
            {
                Name = prototype.Name,
                Crate = id,
                Cost = price,
                CMUFactionVendorItem = true,
            });
        }
    }

    // CMU14: recognize ammunition, including specialist rockets, grenades, shotgun
    // boxes and fuel tanks, while excluding filled belts, pouches and weapons.
    private bool IsSpecialistAmmunition(EntProtoId id, EntityPrototype prototype)
    {
        // Fuel-carrying pouches also contain "FuelTank" in their IDs; only actual
        // flamer tanks supply ammunition to the weapon.
        if (prototype.TryComp<RMCFlamerTankComponent>(
                CompName.Get<RMCFlamerTankComponent>(EntityManager.ComponentFactory), out _))
            return true;

        var name = id.Id;
        if (name.Contains("Magazine", StringComparison.OrdinalIgnoreCase) &&
            prototype.TryComp<BallisticAmmoProviderComponent>(
                CompName.Get<BallisticAmmoProviderComponent>(EntityManager.ComponentFactory), out _))
            return true;

        return name.Contains("Rocket", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("BoxShotgun", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("BoxAGM", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("PacketGrenade", StringComparison.OrdinalIgnoreCase);
    }

    private static int EstimateVendorItemCost(
        string section,
        int? vendorPoints,
        Dictionary<string, List<int>> comparablePrices)
    {
        var name = section.ToLowerInvariant();
        var category = name.Contains("ammun") || name.Contains("munition") || name.Contains("magazine") || name.Contains("ammo") ? "Ammo"
            : name.Contains("weapon") || name.Contains("firearm") || name.Contains("primar") || name.Contains("secondar") ? "Weapons"
            : name.Contains("explosive") || name.Contains("mine") ? "Explosives"
            : name.Contains("material") ? "Materials"
            : name.Contains("tool") || name.Contains("engineering") ? "Engineering"
            : "Gear";
        var fallback = category switch
        {
            "Weapons" => 600,
            "Ammo" => 150,
            "Explosives" => 350,
            "Engineering" => 300,
            "Materials" => 200,
            _ => 250,
        };
        var comparable = fallback;
        if (comparablePrices.TryGetValue(category, out var candidates) && candidates.Count > 0)
        {
            candidates.Sort();
            comparable = candidates[candidates.Count / 2];
        }

        var rarity = name.Contains("special") || name.Contains("heavy") ? 2 : 1;
        return Math.Max(comparable * rarity, (vendorPoints ?? 0) * 100);
    }
}
