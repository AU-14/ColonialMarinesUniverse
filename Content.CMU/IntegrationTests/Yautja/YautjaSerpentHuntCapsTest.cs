using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

// cmss13 /datum/emergency_call/pred/xeno caps how many big xenos a serpent hunt releases
[TestFixture]
public sealed class YautjaSerpentHuntCapsTest
{
    private static readonly string[] TierThree = { "CMXenoPraetorian", "CMXenoRavager", "RMCXenoDespoiler" };

    [Test]
    public async Task SmallSerpentHuntReleasesOneTierThreeAtMost()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var flightConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipFlightConsoleOverwatchSouthOffset0x13", map.GridCoords);
            var huntsmasterConsole = entMan.SpawnEntity("CMUHunterShipPlacedCMUHunterShipHuntsmastersConsoleOverwatchSouthOffsetNeg1x13", map.GridCoords);
            entMan.SpawnEntity("CMUHunterShipMarkerPredatorSpawn", map.GridCoords.Offset(new Vector2(4, 0)));
            entMan.EnsureComponent<YautjaComponent>(hunter);

            var flight = entMan.GetComponent<YautjaHuntConsoleComponent>(flightConsole);
            var jungle = flight.AvailableDestinations.Single(candidate => candidate.Id == "jungle_moon");
            entMan.EventBus.RaiseLocalEvent(flightConsole, new YautjaHuntingGroundSelectedEvent(entMan.GetNetEntity(hunter), jungle.Id));

            entMan.EventBus.RaiseLocalEvent(huntsmasterConsole,
                new YautjaHuntCallSelectedEvent(entMan.GetNetEntity(hunter), "serpents_small"));

            var released = entMan.EntityQuery<YautjaHuntPreyComponent>(true)
                .Select(prey => entMan.GetComponent<MetaDataComponent>(prey.Owner).EntityPrototype?.ID)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(released, Has.Count.EqualTo(4), "mob_max = 4");
                Assert.That(released.Count(TierThree.Contains), Is.EqualTo(1), "max_xeno_t3 = 1");
                Assert.That(released.Count(id => id == "CMXenoLurker"), Is.AtMost(1), "max_xeno_t2 = 1");
                Assert.That(released.Count(id => id == "CMXenoWarrior"), Is.AtLeast(2), "everyone past the caps is a warrior");
            });
        });

        await pair.CleanReturnAsync();
    }
}
