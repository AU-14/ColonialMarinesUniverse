using Content.Shared.FixedPoint;
using Content.Shared.Nutrition;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaRawMeatTest
{
    private const string RawMeat = "UncookedAnimalProteins";
    private const string Protein = "Protein";

    [Test]
    public async Task YautjaDigestRawMeatHumansAndBadBloodsStillGetSick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);
            var badBlood = entMan.SpawnEntity("CMUMobYautjaBadBloodGrunt", map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var food = entMan.SpawnEntity(null, map.GridCoords);

            var hunterBite = new Solution(RawMeat, FixedPoint2.New(5));
            var hunterEv = new IngestingEvent(food, hunterBite, false);
            entMan.EventBus.RaiseLocalEvent(hunter, ref hunterEv);

            var badBloodBite = new Solution(RawMeat, FixedPoint2.New(5));
            var badBloodEv = new IngestingEvent(food, badBloodBite, false);
            entMan.EventBus.RaiseLocalEvent(badBlood, ref badBloodEv);

            var marineBite = new Solution(RawMeat, FixedPoint2.New(5));
            var marineEv = new IngestingEvent(food, marineBite, false);
            entMan.EventBus.RaiseLocalEvent(marine, ref marineEv);

            Assert.Multiple(() =>
            {
                Assert.That(hunterBite.GetTotalPrototypeQuantity(RawMeat), Is.EqualTo(FixedPoint2.Zero),
                    "a yautja eating its prey shouldn't get food poisoning");
                Assert.That(hunterBite.GetTotalPrototypeQuantity(Protein), Is.EqualTo(FixedPoint2.New(5)), "it's still food");
                Assert.That(badBloodBite.GetTotalPrototypeQuantity(RawMeat), Is.EqualTo(FixedPoint2.New(5)),
                    "a Bad Blood has to cook its prey, raw meat stays raw");
                Assert.That(badBloodBite.GetTotalPrototypeQuantity(Protein), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(marineBite.GetTotalPrototypeQuantity(RawMeat), Is.EqualTo(FixedPoint2.New(5)),
                    "humans keep the raw meat sickness");
            });

            entMan.DeleteEntity(hunter);
            entMan.DeleteEntity(badBlood);
            entMan.DeleteEntity(marine);
            entMan.DeleteEntity(food);
        });

        await pair.CleanReturnAsync();
    }
}
