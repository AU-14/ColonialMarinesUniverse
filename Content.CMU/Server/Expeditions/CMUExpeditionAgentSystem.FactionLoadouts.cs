using System.Linq;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private enum SpawnWeaponFamily : byte { Rifle, Compact, Support, Marksman, Scout, Sniper, Shotgun }

    private sealed record SpawnArmament(EntProtoId Weapon, EntProtoId Ammunition, int Reserves, SpawnWeaponFamily Family);
    private sealed record FactionArmaments(SpawnArmament[] Rifles, SpawnArmament[] Assault,
        SpawnArmament[] Support, SpawnArmament[] Marksmen, SpawnArmament Compact);

    private static SpawnArmament Rifle(string weapon, string ammo) => new(weapon, ammo, 4, SpawnWeaponFamily.Rifle);
    private static SpawnArmament Compact(string weapon, string ammo) => new(weapon, ammo, 4, SpawnWeaponFamily.Compact);
    private static SpawnArmament Support(string weapon, string ammo) => new(weapon, ammo, 3, SpawnWeaponFamily.Support);
    private static SpawnArmament Marksman(string weapon, string ammo) => new(weapon, ammo, 4, SpawnWeaponFamily.Marksman);
    private static SpawnArmament Scout(string weapon, string ammo) => new(weapon, ammo, 4, SpawnWeaponFamily.Scout);
    private static SpawnArmament Sniper(string weapon, string ammo) => new(weapon, ammo, 4, SpawnWeaponFamily.Sniper);
    private static SpawnArmament Shotgun(string weapon, string ammo) => new(weapon, ammo, 6, SpawnWeaponFamily.Shotgun);

    // These are native firearms and their native compatible magazines/handfuls. The
    // catalog supplies finite reserves; an empty starting slot consumes those same
    // reserves through native insertion before the newly spawned agent can choose a pistol.
    private static readonly Dictionary<string, FactionArmaments> FactionLoadouts = new()
    {
        ["uscm"] = new(
            [Rifle("RMCWeaponRifleM54C", "CMMagazineRifleM54C"), Rifle("RMCWeaponRifleM54C2", "CMMagazineRifleM54CAP")],
            [Compact("WeaponSMGM63", "CMMagazineSMGM63"), Shotgun("WeaponShotgunM890", "CMShellShotgunSlugs")],
            [Support("RMCWeaponRifleM54CE2", "CMMagazineRifleM54CE2"), Support("RMCWeaponLMGM60", "RMCMagazineLMGM60")],
            [Marksman("WeaponRifleM4SPR", "CMMagazineRifleM4SPR"), Sniper("CMM96SSniperRifle", "CMMagazineSniperM96S")],
            Compact("WeaponSMGM63", "CMMagazineSMGM63")),
        ["rmc"] = new(
            [Rifle("RMCWeaponRifleL24", "RMCMagazineRifleL24"), Rifle("AU14WeaponRifleL90", "AU14MagazineRifleL90")],
            [Compact("CMU14WeaponSMGMP5SD", "CMMagazineSMGMP5"), Rifle("RMCWeaponRifleL24B", "RMCMagazineRifleL24")],
            [Support("CMUGPMGTWE", "CMUMagazineGPMGTWE")],
            [Marksman("AU14WeaponRifleL90M", "AU14MagazineRifleL90M"), Sniper("AU14L64A3SniperRifle", "AU14MagazineSniperL64A3")],
            Compact("CMU14WeaponSMGMP5SD", "CMMagazineSMGMP5")),
        ["upp"] = new(
            [Rifle("AU14WeaponRifleAG80", "AU14MagazineRifleAG80"), Rifle("RMCWeaponRifleType71", "RMCMagazineRifleType71")],
            [Compact("RMCWeaponSMGType64", "RMCMagazineSMGType64"), Shotgun("RMCWeaponShotgunType23", "RMCShellShotgunHeavySlugs")],
            [Support("RMCWeaponLMGQYJ72", "RMCMagazineLMGQYJ72")],
            [Sniper("RMCType88SniperRifle", "RMCMagazineSniperType88")],
            Compact("RMCWeaponSMGType64", "RMCMagazineSMGType64")),
        ["pmc"] = new(
            [Rifle("AU14WeaponRifleF44AA", "AU14MagazineRifleF44AA"), Rifle("RMCWeaponRifleM54C2", "CMMagazineRifleM54CAP")],
            [Compact("RMCWeaponSMGM63B2", "CMMagazineSMGM63AP"), Shotgun("WeaponShotgunM890", "CMShellShotgunSlugs")],
            [Support("RMCWeaponRifleM54CE2", "CMMagazineRifleM54CE2")],
            [Scout("WeaponRifleM4SPRCustom", "CMMagazineRifleM4SPRAP")],
            Compact("RMCWeaponSMGM63B2", "CMMagazineSMGM63AP")),
        ["clf"] = new(
            [Rifle("WeaponRifleMAR40", "RMCMagazineRifleMAR40"), Rifle("RMCWeaponRifleMAR30", "RMCMagazineRifleMAR40")],
            [Compact("WeaponSMGMP5", "CMMagazineSMGMP5"), Shotgun("RMCWeaponShotgunType23", "RMCShellShotgunHeavySlugs")],
            [Support("RMCWeaponMar50LMG", "RMCMagazineMar50LMG")],
            [Marksman("WeaponRifleAR10", "RMCMagazineRifleAR10"), Marksman("RMCWeaponRifleL42A", "RMCMagazineRifleL42A")],
            Compact("WeaponSMGMP5", "CMMagazineSMGMP5")),
        ["cmb"] = new(
            [Rifle("RMCWeaponRifleL42A", "RMCMagazineRifleL42A"), Rifle("RMCWeaponRifleM54C", "CMMagazineRifleM54C")],
            [Compact("WeaponSMGMP5", "CMMagazineSMGMP5"), Shotgun("WeaponShotgunM890", "CMShellShotgunSlugs")],
            [Support("RMCWeaponLMGM60", "RMCMagazineLMGM60")],
            [Marksman("WeaponRifleAR10", "RMCMagazineRifleAR10")],
            Compact("WeaponSMGMP5", "CMMagazineSMGMP5")),
        ["lacn"] = new(
            [Rifle("AU14WeaponRifleKramerAR", "AU14WeaponRifleKramerMagazineStandard"), Rifle("CMUWeaponRifleKramerEnforcer", "AU14WeaponRifleKramerMagazineStandard")],
            [Compact("WeaponSMGMP5", "CMMagazineSMGMP5"), Shotgun("CMUWeaponShotgunLACNBreacher", "CMShellShotgunSlugs")],
            [Support("AU14WeaponRifleKramerLSW", "AU14WeaponRifleKramerMagazineLSWStandard")],
            [Sniper("AU14WeaponSniperRifleM42A2", "CMMagazineSniperM96S")],
            Compact("WeaponSMGMP5", "CMMagazineSMGMP5")),
        ["ccaf"] = new(
            [Rifle("AUWeaponRifleM75AMAS", "AU14MagazineRifleM75AMAS"), Rifle("AUWeaponRifleC10A6BR", "AU14MagazineRifleC10A6BR")],
            [Compact("WeaponSMGM63", "CMMagazineSMGM63"), Shotgun("WeaponShotgunM890", "CMShellShotgunSlugs")],
            [Support("RMCWeaponRifleM54CE2", "CMMagazineRifleM54CE2")],
            [Scout("AU14WeaponRifleM49A", "CMMagazineRifleM4SPR")],
            Compact("WeaponSMGM63", "CMMagazineSMGM63")),
        ["uacg"] = new(
            [Rifle("AUWeaponRifleM20A", "AU14MagazineRifleM20A"), Rifle("RMCWeaponRifleM54CMK1", "CMMagazineRifleM54CMK1")],
            [Compact("WeaponSMGM63", "CMMagazineSMGM63"), Shotgun("WeaponShotgunM890", "CMShellShotgunSlugs")],
            [Support("RMCWeaponLMGM60", "RMCMagazineLMGM60")],
            [Marksman("WeaponRifleM4SPR", "CMMagazineRifleM4SPR")],
            Compact("WeaponSMGMP5", "CMMagazineSMGMP5")),
        ["prodigy"] = new(
            [Rifle("AU14WeaponRifleprodigyrifleAR", "AU14WeaponRifleprodigyrifleMagazineStandard")],
            [Compact("CMU14WeaponSMGMP5SD", "CMMagazineSMGMP5"), Rifle("AU14WeaponRifleprodigyrifleAR", "AU14WeaponRifleprodigyrifleMagazineStandard")],
            [Support("RMCWeaponRifleM54CE2", "CMMagazineRifleM54CE2")],
            [Scout("AU14WeaponRifleM49A", "CMMagazineRifleM4SPR")],
            Compact("CMU14WeaponSMGMP5SD", "CMMagazineSMGMP5")),
    };

    private static SpawnArmament SelectSpawnArmament(FactionArmaments faction, CMUExpeditionAgentComponent agent, int index)
    {
        if (agent.AntiVehicle || agent.CombatRole == CMUExpeditionCombatRole.Medic)
            return faction.Compact;
        var options = agent.CombatRole switch
        {
            CMUExpeditionCombatRole.Support => faction.Support,
            CMUExpeditionCombatRole.Marksman => faction.Marksmen,
            CMUExpeditionCombatRole.Breacher => faction.Assault,
            _ => faction.Rifles,
        };
        return options[Math.Abs(index % options.Length)];
    }

    private bool ApplySpawnArmament(EntityUid uid, CMUExpeditionAgentComponent agent, string outfit, int variantIndex)
    {
        if (!FactionLoadouts.TryGetValue(outfit, out var faction) || !_guns.TryGetGun(uid, out var original) ||
            !_hands.IsHolding(uid, original.Owner, out var originalHand))
            return false;
        var armament = SelectSpawnArmament(faction, agent, variantIndex);
        if (!ProtoMan.HasIndex(armament.Weapon) || !ProtoMan.HasIndex(armament.Ammunition))
            return false;
        var weapon = Spawn(armament.Weapon, Transform(uid).Coordinates);
        var replacements = new List<EntityUid> { weapon };
        var ammunition = new List<EntityUid>();
        for (var i = 0; i < armament.Reserves; i++)
        {
            var ammo = Spawn(armament.Ammunition, Transform(uid).Coordinates);
            replacements.Add(ammo);
            ammunition.Add(ammo);
        }
        if (!HasComp<GunComponent>(weapon) || ammunition.Any(ammo => !CompatibleAmmunition(uid, weapon, ammo)) ||
            !PrepareSpawnArmament(uid, weapon, ammunition))
        {
            foreach (var item in replacements)
                QueueDel(item);
            return false;
        }
        // Preserve ammunition shared by the launcher or another carried weapon. Only
        // the original primary's now-obsolete reserves participate in this transaction.
        var otherWeapons = CarriedWeapons(uid).Where(other => other != original.Owner).ToArray();
        var reserves = SupplyStores(uid).SelectMany(store => store.Container.ContainedEntities
            .Where(item => CompatibleAmmunition(uid, original, item) &&
                !otherWeapons.Any(other => CompatibleAmmunition(uid, other, item)))
            .Select(item => (Item: item, Store: store))).ToArray();
        var detached = new List<(EntityUid Item, StorageComponent Store)>();
        var success = true;
        foreach (var reserve in reserves)
        {
            if (!_containers.Remove(reserve.Item, reserve.Store.Container, destination: Transform(uid).Coordinates))
            {
                success = false;
                break;
            }
            detached.Add(reserve);
        }
        if (success)
        {
            _wield.TryUnwield(original.Owner, uid);
            success = _hands.TryDrop(uid, original.Owner) && _hands.TryPickup(uid, weapon, originalHand, animate: false);
        }
        if (success)
            foreach (var ammo in ammunition)
                if (!StoreSupply(uid, ammo))
                {
                    success = false;
                    break;
                }
        if (!success)
        {
            // Deletion is synchronous here so replacement magazines immediately release
            // storage cells before the exact original supplies are restored.
            foreach (var item in replacements)
                if (Exists(item))
                    Del(item);
            foreach (var reserve in detached)
                if (!StoreOwnedItem(uid, reserve.Item, reserve.Store))
                    Log.Error($"Could not restore original expedition ammunition {ToPrettyString(reserve.Item)} to {ToPrettyString(uid)} after rejected faction loadout.");
            if (!_hands.IsHolding(uid, original.Owner) && !_hands.TryPickup(uid, original.Owner, originalHand, animate: false))
                Log.Error($"Could not restore original expedition primary {ToPrettyString(original.Owner)} to {ToPrettyString(uid)} after rejected faction loadout.");
            ActivateWeapon(uid, original.Owner);
            return false;
        }
        foreach (var reserve in detached)
            QueueDel(reserve.Item);
        QueueDel(original.Owner);
        var preference = EnsureComp<CMUExpeditionWeaponRoleComponent>(weapon);
        preference.Priority = 30;
        preference.MaximumRange = armament.Family switch
        {
            SpawnWeaponFamily.Compact => 11, SpawnWeaponFamily.Shotgun => 7,
            SpawnWeaponFamily.Sniper => 18, _ => 14,
        };
        preference.CloseRange = 2;
        preference.ClosePriority = armament.Family switch
        {
            SpawnWeaponFamily.Compact => 8, SpawnWeaponFamily.Shotgun => 15,
            SpawnWeaponFamily.Support => -8, SpawnWeaponFamily.Marksman or SpawnWeaponFamily.Scout => -12,
            SpawnWeaponFamily.Sniper => -20, _ => 0,
        };
        preference.BurstLimit = armament.Family switch
        {
            SpawnWeaponFamily.Support => 8, SpawnWeaponFamily.Marksman or SpawnWeaponFamily.Scout => 2,
            SpawnWeaponFamily.Sniper => 1, SpawnWeaponFamily.Shotgun => 6, _ => int.MaxValue,
        };
        if (armament.Family == SpawnWeaponFamily.Sniper)
            EnsureComp<SniperWhitelistComponent>(uid);
        else if (armament.Family == SpawnWeaponFamily.Scout)
            EnsureComp<ScoutWhitelistComponent>(uid);
        agent.Rifle = weapon;
        agent.WeaponBurstLimit = preference.BurstLimit;
        agent.NextWeaponChoice = _timing.CurTime;
        agent.WeaponDecision = "faction-primary-equipped";
        ActivateWeapon(uid, weapon);
        return true;
    }

    private bool PrepareSpawnArmament(EntityUid uid, EntityUid weapon, List<EntityUid> ammunition)
    {
        if (WeaponAmmo(weapon) > 0)
            return true;
        if (_itemSlots.TryGetSlot(weapon, "gun_magazine", out var slot))
        {
            // Only initialize an empty native slot. Do not replace a native supplied
            // magazine or manufacture rounds to compensate for an invalid prototype.
            if (slot.HasItem || ammunition.Count == 0 || !_itemSlots.TryInsert(weapon, "gun_magazine", ammunition[0], uid))
                return false;
            ammunition.RemoveAt(0);
            return WeaponAmmo(weapon) > 0;
        }
        if (!TryComp<BallisticAmmoProviderComponent>(weapon, out var tube))
            return false;
        foreach (var ammo in ammunition.ToArray())
        {
            for (var i = 0; i < tube.Capacity && Exists(ammo) && !SpawnAmmunitionLoaded(weapon, ammo) &&
                _guns.CanInsertBallistic((weapon, tube), ammo); i++)
            {
                var before = WeaponAmmo(weapon);
                if (!_guns.TryAmmoInsert((weapon, tube), ammo, uid, weapon, 0) || WeaponAmmo(weapon) <= before)
                    break;
            }
            // The last round of a native handful is the original entity, transferred
            // into the tube. It is no longer a reserve to insert or store again.
            if (!Exists(ammo) || SpawnAmmunitionLoaded(weapon, ammo) || SupplyQuantity(ammo) == 0)
                ammunition.Remove(ammo);
            if (WeaponAmmo(weapon) >= tube.Capacity)
                break;
        }
        return WeaponAmmo(weapon) > 0;
    }

    private bool SpawnAmmunitionLoaded(EntityUid weapon, EntityUid ammo) =>
        _containers.TryGetContainingContainer((ammo, null, null), out var container) && container.Owner == weapon;
}
