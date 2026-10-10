using System.Linq;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static readonly Dictionary<string, ProtoId<StartingGearPrototype>> OutfitCatalog = new()
    {
        ["uscm"] = "CMUExpeditionOutfitUSCM", ["rmc"] = "CMUExpeditionOutfitRMC",
        ["upp"] = "CMUExpeditionOutfitUPP", ["pmc"] = "CMUExpeditionOutfitPMC",
        ["clf"] = "CMUExpeditionOutfitCLF", ["cmb"] = "CMUExpeditionOutfitCMB",
        ["lacn"] = "CMUExpeditionOutfitLACN", ["ccaf"] = "CMUExpeditionOutfitCCAF",
        ["uacg"] = "CMUExpeditionOutfitUACG", ["prodigy"] = "CMUExpeditionOutfitProdigy",
    };

    private static readonly Dictionary<string, (ProtoId<StartingGearPrototype> Mobile, ProtoId<StartingGearPrototype> Heavy)> OutfitRoles = new()
    {
        ["uscm"] = ("CMUExpeditionOutfitUSCMMobile", "CMUExpeditionOutfitUSCMHeavy"),
        ["rmc"] = ("CMUExpeditionOutfitRMCMobile", "CMUExpeditionOutfitRMCHeavy"),
        ["upp"] = ("CMUExpeditionOutfitUPPMobile", "CMUExpeditionOutfitUPPHeavy"),
        ["pmc"] = ("CMUExpeditionOutfitPMCMobile", "CMUExpeditionOutfitPMC"),
        ["clf"] = ("CMUExpeditionOutfitCLFMobile", "CMUExpeditionOutfitCLFHeavy"),
        ["cmb"] = ("CMUExpeditionOutfitCMBMobile", "CMUExpeditionOutfitCMB"),
        ["lacn"] = ("CMUExpeditionOutfitLACNMobile", "CMUExpeditionOutfitLACNHeavy"),
        ["ccaf"] = ("CMUExpeditionOutfitCCAFMobile", "CMUExpeditionOutfitCCAF"),
        ["uacg"] = ("CMUExpeditionOutfitUACGMobile", "CMUExpeditionOutfitUACG"),
        ["prodigy"] = ("CMUExpeditionOutfitProdigyMobile", "CMUExpeditionOutfitProdigy"),
    };

    public static IEnumerable<string> OutfitNames => OutfitCatalog.Keys.Prepend("scavenger");
    public static bool IsOutfit(string outfit) => outfit == "scavenger" || OutfitCatalog.ContainsKey(outfit);

    // Spawn-time faction clothing and armament. Existing medicine, tools, launchers
    // and faction/IFF orders remain intact; admins set team relations explicitly.
    private bool ApplySpawnOutfit(EntityUid uid, string outfit, int variantIndex = 0)
    {
        if (outfit == "scavenger")
            return true;
        if (!OutfitCatalog.TryGetValue(outfit, out var id))
            return false;
        var agent = Comp<CMUExpeditionAgentComponent>(uid);
        if (OutfitRoles.TryGetValue(outfit, out var roles))
        {
            if (agent.AntiVehicle || agent.CombatRole is CMUExpeditionCombatRole.Flanker or CMUExpeditionCombatRole.Medic)
                id = roles.Mobile;
            else if (agent.CombatRole is CMUExpeditionCombatRole.Support or CMUExpeditionCombatRole.Breacher)
                id = roles.Heavy;
        }
        if (!ProtoMan.TryIndex(id, out var gear))
            return false;
        var original = new Dictionary<string, EntityUid>();
        var slots = _inventory.GetSlotEnumerator(uid);
        while (slots.MoveNext(out var slot))
            if (slot.ContainedEntity is { } item)
                original[slot.ID] = item;
        var replacements = new Dictionary<string, EntityUid>();
        foreach (var (slot, prototype) in gear.Equipment)
        {
            var item = Spawn(prototype, Transform(uid).Coordinates);
            replacements[slot] = item;
            if (!_inventory.CanEquip(uid, item, slot, out _, assumeEmpty: true))
            {
                foreach (var staged in replacements.Values)
                    QueueDel(staged);
                return false;
            }
        }
        var success = true;
        foreach (var (slot, item) in replacements)
        {
            if (original.ContainsKey(slot) && !_inventory.TryUnequip(uid, slot, silent: true) ||
                !_inventory.TryEquip(uid, item, slot, silent: true))
            {
                success = false;
                break;
            }
        }
        // Uniform/vest replacement can dislodge dependent slots. Restore their exact
        // containers before the armament transaction checks native storage capacity.
        if (success)
            foreach (var (slot, item) in original)
                if (!replacements.ContainsKey(slot) && !_inventory.TryGetSlotEntity(uid, slot, out _) &&
                    !_inventory.TryEquip(uid, item, slot, silent: true))
                    success = false;
        if (success)
            success = ApplySpawnArmament(uid, agent, outfit, variantIndex);
        if (!success)
        {
            foreach (var (slot, item) in replacements)
            {
                if (_inventory.TryGetSlotEntity(uid, slot, out var worn) && worn == item)
                    _inventory.TryUnequip(uid, slot, silent: true);
                Del(item);
            }
        }
        // Removing a uniform/vest may dislodge dependent slots. Restore the very same
        // belt, pockets and slung weapon after the new clothing is fitted.
        foreach (var (slot, item) in original)
        {
            if (success && replacements.ContainsKey(slot))
                QueueDel(item);
            else if (!_inventory.TryGetSlotEntity(uid, slot, out _) && !_inventory.TryEquip(uid, item, slot, silent: true))
                Log.Error($"Could not restore original expedition slot {slot} on {ToPrettyString(uid)} after rejected faction outfit.");
        }
        if (success)
            agent.Outfit = outfit;
        return success;
    }
}
