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
