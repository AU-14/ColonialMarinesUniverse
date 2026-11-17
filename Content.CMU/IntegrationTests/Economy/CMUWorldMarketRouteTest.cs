using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.WorldMarket;
using Content.Shared.CMU14.WorldMarket;

namespace Content.IntegrationTests.CMU14.Economy;

[TestFixture]
public sealed class CMUWorldMarketRouteTest : GameTest
{
    [Test]
    public async Task RoadClosureChangesGiftsAndActionsOnlyForUnreachableDestinations()
    {
        const string faction = "UPP";
        const float budget = 10000;
        const string festival = "RUMCActionFestival";
        string site = default!;
        string origin = default!;

        await Server.WaitAssertion(() =>
        {
            var world = Server.System<WorldMarketSystem>();
            var market = world.EnsureMarket();
            market.Comp.MapSeed = 3137;
            var cities = world.BuildCities(faction, budget, out _, out _);
            var destination = cities.First(c => c.Site);
            site = destination.Id;
            origin = cities.First(c => !c.Site).Id;
            world.AddCityInfluence(market, faction, origin, 50);
            market.Comp.Hazards.Add(new MapHazard
            {
                Proto = "RUMCHazardBandits",
                Latitude = destination.Latitude,
                Longitude = destination.Longitude,
                EndsAt = TimeSpan.MaxValue,
            });
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(2));
        await Server.WaitAssertion(() =>
        {
            var world = Server.System<WorldMarketSystem>();
            var cities = world.BuildCities(faction, budget, out _, out _);
            var isolated = cities.Single(c => c.Id == site);
            Assert.That(isolated.CanAfford, Is.False);
            Assert.That(isolated.Actions.Single(a => a.Id == festival).Blocked, Is.Not.Empty);
            Assert.That(cities.Single(c => c.Id == origin).CanAfford, Is.True);
            Assert.That(world.DoCityAction(faction, site, festival, budget), Is.Null);
            var market = world.EnsureMarket();
            // If the strongest city is the destination, the runner-up must be able to reach it.
            world.AddCityInfluence(market, faction, site, 50);
            Assert.That(world.BuildCities(faction, budget, out _, out _).Single(c => c.Id == origin).CanAfford, Is.False);
            Assert.That(world.GiftCity(faction, origin, budget), Is.Null);
            market.Comp.Hazards.Clear();
        });

        await Pair.RunTicksSync(Pair.SecondsToTicks(2));
        await Server.WaitAssertion(() =>
        {
            var world = Server.System<WorldMarketSystem>();
            var reopened = world.BuildCities(faction, budget, out _, out _).Single(c => c.Id == site);
            Assert.That(reopened.CanAfford, Is.True);
            Assert.That(reopened.Actions.Single(a => a.Id == festival).Blocked, Is.Empty);
            Assert.That(world.DoCityAction(faction, site, festival, budget), Is.Not.Null);
            SEntMan.DeleteEntity(world.EnsureMarket().Owner);
        });
    }
}
