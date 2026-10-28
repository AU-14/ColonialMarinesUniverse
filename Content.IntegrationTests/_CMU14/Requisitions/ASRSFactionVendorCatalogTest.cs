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
                Assert.That(uscmAmmo.Entries.Any(entry => entry.Crate.Id == "RMCMagazineSmartGun"), Is.True);
                Assert.That(uscmAmmo.Entries.Any(entry => entry.Crate.Id == "RMCTankFlamerLarge"), Is.True,
                    "Ammunition inside a specialist case must be orderable separately.");
                Assert.That(uscmAmmo.Entries.Any(entry => entry.Crate.Id == "RMCSharpSpecEquipmentCase" ||
                                                          entry.Crate.Id == "RMCWeaponRifleSharp" ||
                                                          entry.Crate.Id == "RMCPouchFuelTank"), Is.False,
                    "A case's weapon and fuel-carrying pouch are not specialist ammunition.");

                SelectPlatoon(SProtoMan.Index(Lacn));
                var lacn = computer.Categories.Single(category => category.Name == "Faction Requisitions");
                Assert.That(lacn.Entries.Any(entry => entry.Crate.Id == "AU14WeaponRifleKramerAR"), Is.True);
                Assert.That(lacn.Entries.Any(entry => entry.Crate.Id == "RMCWeaponRifleM54CMK1Unloaded"), Is.False);
                Assert.That(computer.ItemCatalog.Any(item => item.Prototype.Id == "AU14WeaponRifleKramerAR"), Is.True);
                var lacnAmmo = computer.Categories.Single(category => category.Name == "Faction Specialist Ammo");
                Assert.That(lacnAmmo.Entries.Any(entry => entry.Crate.Id == "AU14WeaponRifleKramerMagazineLSWStandard"), Is.True);
                Assert.That(lacnAmmo.Entries.Any(entry => entry.Crate.Id == "RMCMagazineSmartGun"), Is.False,
                    "Refreshing the selected platoon must remove the previous platoon's ammunition.");

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
