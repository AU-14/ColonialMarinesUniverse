using System.Linq;
using Content.Client.CMU14.Vehicle;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Every jeep crate leaves the jeep's field manual beside it, and the manual can point at every part
/// it has a page for on the jeep's own art.
/// </summary>
[TestFixture]
public sealed class CMUJeepManualTest : GameTest
{
    [Test]
    public async Task EveryJeepCrateComesWithTheManual()
    {
        var map = await Pair.CreateTestMap();
        var crates = new List<string>();
        await Server.WaitPost(() =>
        {
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!proto.Abstract && proto.TryComp<CMUVehicleCrateComponent>(out _, SEntMan.ComponentFactory))
                    crates.Add(proto.ID);
            }
        });
        Assert.That(crates, Is.Not.Empty);

        foreach (var id in crates)
        {
            var manuals = 0;
            await Server.WaitPost(() =>
            {
                var crate = SEntMan.SpawnEntity(id, new EntityCoordinates(map.Grid, 0.5f, 0.5f));
                var before = SEntMan.EntityQuery<CMUVehicleManualComponent>(true).Count();
                SEntMan.System<CMUVehicleCrateSystem>().Open((crate, SEntMan.GetComponent<CMUVehicleCrateComponent>(crate)));
                manuals = SEntMan.EntityQuery<CMUVehicleManualComponent>(true).Count() - before;
            });
            Assert.That(manuals, Is.EqualTo(1), $"{id} should leave one manual beside its jeep.");
        }
    }

    [Test]
    public async Task EveryPartOfTheManualShowsOnTheJeep()
    {
        await Client.WaitAssertion(() =>
        {
            foreach (var manual in CProtoMan.EnumeratePrototypes<CMUVehicleManualPrototype>())
            {
                Assert.That(manual.Entries.Select(e => e.Id), Is.Unique, manual.ID);
                var diagram = new CMUVehicleManualDiagram(manual);
                foreach (var entry in manual.Entries)
                {
                    if (entry.Masks.Count == 0 && entry.OpenMasks is not { Count: > 0 })
                        continue;

                    var shown = false;
                    foreach (var open in new[] { false, true })
                    {
                        diagram.Open = open;
                        for (var facing = 0; facing < CMUVehicleManualDiagram.Facings.Length && !shown; facing++)
                        {
                            diagram.Facing = facing;
                            shown = diagram.Shows(entry);
                        }
                    }

                    Assert.That(shown, Is.True, $"{manual.ID}: {entry.Id} should be outlined on the art from some side.");
                }
            }
        });
    }
}
