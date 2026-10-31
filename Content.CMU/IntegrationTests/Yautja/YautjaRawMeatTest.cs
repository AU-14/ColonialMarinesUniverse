using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaRawMeatTest
{
    [Test]
    public async Task YautjaDigestRawMeatHumansStillGetSick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var food = entMan.SpawnEntity(null, map.GridCoords);

            var hunterBite = new Solution("UncookedAnimalProteins", FixedPoint2.New(5));
            var hunterEv = new IngestingEvent(food, hunterBite, false);
            entMan.EventBus.RaiseLocalEvent(hunter, ref hunterEv);

            var marineBite = new Solution("UncookedAnimalProteins", FixedPoint2.New(5));
            var marineEv = new IngestingEvent(food, marineBite, false);
            entMan.EventBus.RaiseLocalEvent(marine, ref marineEv);

            Assert.Multiple(() =>
            {
                Assert.That(hunterBite.GetTotalPrototypeQuantity("UncookedAnimalProteins"), Is.EqualTo(FixedPoint2.Zero),
                    "a yautja eating its prey shouldn't get food poisoning");
                Assert.That(hunterBite.GetTotalPrototypeQuantity("Protein"), Is.EqualTo(FixedPoint2.New(5)), "it's still food");
                Assert.That(marineBite.GetTotalPrototypeQuantity("UncookedAnimalProteins"), Is.EqualTo(FixedPoint2.New(5)),
                    "humans keep the raw meat sickness");
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(marine);
            entMan.DeleteEntity(food);
        });

        await pair.CleanReturnAsync();
    }
}
