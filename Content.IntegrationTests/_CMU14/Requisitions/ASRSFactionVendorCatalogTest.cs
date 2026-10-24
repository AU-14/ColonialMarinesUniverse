using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CMU14.util;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class ASRSFactionVendorCatalogTest : GameTest
{
    private static readonly ProtoId<PlatoonPrototype> Usmc = "USCM";
    private static readonly ProtoId<PlatoonPrototype> Lacn = "LACN";
    private static readonly ProtoId<PlatoonPrototype> Upp = "UPP";
    private static readonly ProtoId<PlatoonPrototype> Hazops = "HAZOPS";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("govfor", "CMASRSConsoleGovfor")]
    [TestCase("opfor", "CMASRSConsoleOpfor")]
    public async Task SelectedPlatoonVendorGearSurvivesCatalogRefresh(string faction, string consolePrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var platoons = Server.System<PlatoonSpawnRuleSystem>();
            var previous = faction == "govfor" ? platoons.SelectedGovforPlatoon : platoons.SelectedOpforPlatoon;
            EntityUid? console = null;

            void SelectPlatoon(PlatoonPrototype? selected)
            {
                if (faction == "govfor")
                    platoons.SelectedGovforPlatoon = selected;
                else
                    platoons.SelectedOpforPlatoon = selected;
            }

            try
            {
                SelectPlatoon(null);
                console = SEntMan.SpawnEntity(consolePrototype, map.GridCoords);
                var computer = SEntMan.GetComponent<RequisitionsComputerComponent>(console.Value);
                Assert.That(computer.Categories.Any(category => category.Name == "Faction Requisitions"), Is.False);

                SelectPlatoon(SProtoMan.Index(Usmc));
                var uscm = computer.Categories.Single(category => category.Name == "Faction Requisitions");
                Assert.That(uscm.Entries.Any(entry => entry.Crate.Id == "RMCWeaponRifleM54CMK1Unloaded"), Is.True);
                var uscmAmmo = computer.Categories.Single(category => category.Name == "Faction Specialist Ammo");
                foreach (var ammunition in new[]
                         {
                             "RMCMagazineSmartGun", // smart gunner
                             "RMCBoxAGMI", // grenadier
                             "RMCMagazineRifleSharpExplosive", // SHARP
                             "RMCMagazineRifleM4SPRA19", // scout
                             "CMMagazineSniperM96S", // sniper
                             "RMCMagazineSniperXM43E1AntiMateriel", // anti-materiel
                             "RMCMagazineSniperM707Vulture", // Vulture
                             "RMCBoxShotgunHDSG", // shotgunner
                             "RMCTankFlamerLarge", // pyrotechnician
                         })
                    Assert.That(uscmAmmo.Entries.Any(entry => entry.Crate.Id == ammunition), Is.True, ammunition);
                Assert.That(uscmAmmo.Entries.All(entry => entry.CMUFactionVendorItem), Is.True);
                Assert.That(uscmAmmo.Entries.Any(entry => entry.Crate.Id == "RMCSharpSpecEquipmentCase" ||
                                                          entry.Crate.Id == "RMCWeaponRifleSharp" ||
                                                          entry.Crate.Id == "RMCBeltGrenadeLargeFilled"), Is.False);

                SelectPlatoon(SProtoMan.Index(Lacn));
                var lacn = computer.Categories.Single(category => category.Name == "Faction Requisitions");
                Assert.That(lacn.Entries.Any(entry => entry.Crate.Id == "AU14WeaponRifleKramerAR"), Is.True);
                Assert.That(lacn.Entries.Any(entry => entry.Crate.Id == "RMCWeaponRifleM54CMK1Unloaded"), Is.False);
                Assert.That(computer.ItemCatalog.Any(item => item.Prototype.Id == "AU14WeaponRifleKramerAR"), Is.True);
                var lacnAmmo = computer.Categories.Single(category => category.Name == "Faction Specialist Ammo");
                Assert.That(lacnAmmo.Entries.Any(entry => entry.Crate.Id == "AU14WeaponRifleKramerMagazineLSWStandard"), Is.True);
                Assert.That(lacnAmmo.Entries.Any(entry => entry.Crate.Id == "CMULACNBeltMagazine" ||
                                                          entry.Crate.Id == "RMCMagazineSmartGun"), Is.False);

                SelectPlatoon(SProtoMan.Index(Upp));
                var uppAmmo = computer.Categories.Single(category => category.Name == "Faction Specialist Ammo");
                Assert.That(uppAmmo.Entries.Any(entry => entry.Crate.Id == "AU14MagazineSmartGunUPP"), Is.True);
                Assert.That(uppAmmo.Entries.Any(entry => entry.Crate.Id == "RMCRocketHJRA12HE"), Is.True);
                Assert.That(uppAmmo.Entries.Any(entry => entry.Crate.Id == "RMCMagazineSniperType88"), Is.True);
                Assert.That(uppAmmo.Entries.Any(entry => entry.Crate.Id == "RMCTankFlamerLargeUPP"), Is.True);
                Assert.That(uppAmmo.Entries.Any(entry => entry.Crate.Id == "AU14PowerCellSmartgunUPP"), Is.False);

                SelectPlatoon(SProtoMan.Index(Hazops));
                var hazopsAmmo = computer.Categories.Single(category => category.Name == "Faction Specialist Ammo");
                Assert.That(hazopsAmmo.Entries.Any(entry => entry.Crate.Id == "AU14MagazineMinigunHAZOPS"), Is.True);
                Assert.That(hazopsAmmo.Entries.Any(entry => entry.Crate.Id == "AU14TankFlamerM240A2HAZOPS"), Is.True);
                Assert.That(hazopsAmmo.Entries.Any(entry => entry.Crate.Id == "RMCMagazineSniperXM43E1AntiMateriel"), Is.False);

                SelectPlatoon(SProtoMan.Index(Lacn));
                Assert.That(computer.Categories.Count(category => category.Name == "Faction Requisitions"), Is.EqualTo(1));
                Assert.That(computer.Categories.Count(category => category.Name == "Faction Specialist Ammo"), Is.EqualTo(1));
            }
            finally
            {
                if (console != null)
                    SEntMan.DeleteEntity(console.Value);
                SelectPlatoon(previous);
            }
        });
    }
}
