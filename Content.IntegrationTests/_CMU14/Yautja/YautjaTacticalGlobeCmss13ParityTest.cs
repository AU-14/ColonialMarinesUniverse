using Content.Shared._RMC14.TacticalMap;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaTacticalGlobeCmss13ParityTest
{
    [Test]
    public async Task HunterGlobeOpensTheSharedTacticalMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var globe = entMan.SpawnEntity("CMUYautjaStructureYautjaMachinesGlobe", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<TacticalMapComputerComponent>(globe), Is.True,
                        "The CMSS13 hunter globe must expose a tactical map computer on the hunter ship.");
                    Assert.That(entMan.HasComponent<ActivatableUIComponent>(globe), Is.True,
                        "The hunter globe must be interactively openable.");
                    Assert.That(entMan.HasComponent<UserInterfaceComponent>(globe), Is.True,
                        "The hunter globe must bind the existing TacticalMapComputerBui.");
                    var computer = entMan.GetComponent<TacticalMapComputerComponent>(globe);
                    Assert.That(computer.Faction, Is.Null,
                        "The CMSS13 globe is not faction-bound and must display every faction's map blip.");
                    Assert.That(computer.AllowCanvas, Is.False,
                        "The hunter globe is a read-only map display, not a canvas editor.");
                });
            }
            finally
            {
                if (!entMan.Deleted(globe))
                    entMan.DeleteEntity(globe);
            }
        });

        await pair.CleanReturnAsync();
    }
}
