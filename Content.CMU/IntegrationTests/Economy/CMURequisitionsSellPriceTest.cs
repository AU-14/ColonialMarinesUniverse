using Content.IntegrationTests.Fixtures;
using Content.Server.Cargo.Systems;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Economy;

/// <summary>
///     Requisitions crates can't be sold back for more than they cost.
/// </summary>
[TestFixture]
public sealed class CMURequisitionsSellPriceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMUCrateElectronicHearingProtection", 110)]
    // Colony ASRS guns, priced down so colonists can afford to arm themselves.
    [TestCase("CMUWeaponShotgunM37A1Colony", 650)]
    [TestCase("CMUCrateBoxAR10Colony", 1000)]
    [TestCase("CMUCrateBoxM16Colony", 900)]
    [TestCase("CMUCrateBoxKramerColony", 950)]
    [TestCase("AU14CrateBoxMod88", 550)]
    [TestCase("AU14CrateBoxRevolverM2019", 550)]
    [TestCase("AU14CrateBoxRSH9", 600)]
    [TestCase("AU14CrateBoxHuntingRifleCrate", 650)]
    [TestCase("CMUCrateBoxTPR9Colony", 450)]
    [TestCase("AU14CrateBoxM4A3", 450)]
    [TestCase("CMUCrateBoxGlock75Colony", 500)]
    [TestCase("AU14CrateBoxSkeetShootingCrate", 650)]
    [TestCase("AU14KitWeaponRXFM", 600)]
    [TestCase("AU14CrateBoxM44", 550)]
    [TestCase("AU14CrateBoxHuntingRifleABRCrate", 750)]
    [TestCase("CMUCrateBoxNailgun", 450)]
    [TestCase("CMUCrateFlamerColony", 2000)]
    // Colony ASRS ammo
    [TestCase("CMUCrateBoxM16ColonyMagazines", 240)]
    [TestCase("CMUCrateBoxAR10ColonyMagazines", 300)]
    [TestCase("CMUCrateBoxKramerColonyMagazines", 260)]
    [TestCase("AU14CrateBox357HP", 240)]
    [TestCase("AU14CrateBox357", 200)]
    [TestCase("AU14CrateBoxMod88Mags", 200)]
    [TestCase("AU14CrateBoxRSH9SL", 200)]
    [TestCase("AU14CrateBoxBasiraMagazines", 220)]
    [TestCase("AU14CrateBoxShotgunShells", 250)]
    [TestCase("AU14CrateBoxTPR23BMag", 180)]
    [TestCase("CMUCrateBoxGlock75ColonyMagazines", 180)]
    [TestCase("AU14CrateBoxM44SL", 200)]
    [TestCase("AU14CrateBoxM44SLM", 230)]
    [TestCase("AU14CrateBoxD18Magazines", 180)]
    [TestCase("AU14CrateBoxABR40Magazines", 240)]
    [TestCase("CMUCrateBoxNailgunMagazines", 180)]
    public async Task CrateSellsForLessThanItCosts(string crate, int cost)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var spawned = SEntMan.SpawnEntity(crate, map.GridCoords);
            var price = SEntMan.System<PricingSystem>().GetPrice(spawned);
            if (SEntMan.TryGetComponent(spawned, out RequisitionsCrateComponent? reward))
                price += reward.Reward;

            Assert.That(price, Is.LessThan(cost));
        });
    }
}
