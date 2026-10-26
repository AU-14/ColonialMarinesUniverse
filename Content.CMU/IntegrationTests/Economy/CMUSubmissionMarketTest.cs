using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Ambassador;
using Content.Server.CMU14.ColonyEconomy;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Economy;

// submission points pay through the market: best tag wins, selling more of one thing
// drops its price, and stuff the point wouldn't pay for bounces instead of vanishing
[TestFixture]
public sealed class CMUSubmissionMarketTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private static readonly EntProtoId CropPoint = "AUSubmissionPointCrop";
    private static readonly EntProtoId SteelPoint = "AUSubmissionPointSteel";
    private static readonly ProtoId<CMUCommodityPrototype> Tomato = "CMUCommodityTomato";
    private static readonly ProtoId<CMUCommodityPrototype> Pumpkin = "CMUCommodityPumpkin";

    [Test]
    public async Task SalesGoThroughTheMarket()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var budget = SEntMan.System<ColonyBudgetSystem>();
            var market = SEntMan.System<SubmissionMarketSystem>();
            var containers = SEntMan.System<SharedContainerSystem>();
            var protos = Server.ResolveDependency<IPrototypeManager>();
            var trade = SEntMan.System<AmbassadorConsoleSystem>().GetSubmissionMultiplier();
            var tariff = SEntMan.System<CorporateConsoleSystem>().GetTariff();

            var point = SEntMan.SpawnEntity(CropPoint, map.GridCoords);
            var submission = containers.GetContainer(point, "submission");

            float Sell(string item)
            {
                var before = budget.GetBudget();
                var spawned = SEntMan.SpawnEntity(item, map.GridCoords);
                Assert.That(containers.Insert(spawned, submission), Is.True, $"{item} should go in");
                return budget.GetBudget() - before;
            }

            // tomato is tagged tomato + fruit + vegetable, it has to sell as a tomato
            var tomato = protos.Index(Tomato);
            var expected = 8.5f * market.GetMultiplier(tomato) * trade * (1f - tariff);
            var first = Sell("FoodTomato");
            Assert.That(first, Is.EqualTo(expected).Within(0.01f));

            var second = Sell("FoodTomato");
            Assert.That(second, Is.LessThan(first), "selling more of one thing should drop its price");

            // a different commodity isn't touched by the tomato sales
            var pumpkin = protos.Index(Pumpkin);
            var pumpkinMultiplier = market.GetMultiplier(pumpkin);
            Assert.That(pumpkinMultiplier, Is.GreaterThan(market.GetMultiplier(tomato)));
            var pumpkinPay = Sell("FoodPumpkin");
            Assert.That(pumpkinPay, Is.EqualTo(11.75f * pumpkinMultiplier * trade * (1f - tariff)).Within(0.01f));

            // cooked food carries the fruit tag but the blacklist bounces it
            var slice = SEntMan.SpawnEntity("FoodCakeAppleSlice", map.GridCoords);
            Assert.That(containers.Insert(slice, submission), Is.False, "cake slices shouldn't sell");

            // the steel point doesn't buy food at all
            var steel = SEntMan.SpawnEntity(SteelPoint, map.GridCoords);
            var steelContainer = containers.GetContainer(steel, "submission");
            var stray = SEntMan.SpawnEntity("FoodTomato", map.GridCoords);
            Assert.That(containers.Insert(stray, steelContainer), Is.False, "the steel point shouldn't take a tomato");
        });
    }
}
