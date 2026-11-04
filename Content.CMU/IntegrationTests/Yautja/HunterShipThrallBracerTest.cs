using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class HunterShipThrallBracerTest : GameTest
{
    // cmss13 keeps two closets of thrall kits in the middle deck corridors. the port turned the kits into cardboard
    // boxes, so there was no plain thrall bracer anywhere on the ship
    [TestCase("CMUHunterShipMeatLockerThrallGearBoxx5Offset0x1")]
    [TestCase("CMUHunterShipMeatLockerThrallGearBoxx5Offset1x1")]
    public async Task CorridorClosetHoldsThrallBracers(string closet)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var locker = SSpawnAtPosition(closet, map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            var bracers = 0;
            foreach (var box in AllContained(containers, locker).ToList())
            {
                if (SEntMan.TryGetComponent(box, out YautjaThrallBracerComponent? _) &&
                    SEntMan.GetComponent<MetaDataComponent>(box).EntityPrototype?.ID == "CMUYautjaThrallBracer")
                    bracers++;
            }

            Assert.That(bracers, Is.EqualTo(5));
        });
    }

    private IEnumerable<EntityUid> AllContained(SharedContainerSystem containers, EntityUid root)
    {
        if (!SEntMan.HasComponent<ContainerManagerComponent>(root))
            yield break;

        foreach (var container in containers.GetAllContainers(root).ToArray())
        {
            foreach (var child in container.ContainedEntities.ToArray())
            {
                yield return child;
                foreach (var nested in AllContained(containers, child))
                    yield return nested;
            }
        }
    }
}
