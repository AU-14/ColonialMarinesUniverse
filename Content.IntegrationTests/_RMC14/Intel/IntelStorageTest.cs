using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Intel;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Intel;

[TestFixture]
[TestOf(typeof(IntelSystem))]
public sealed class IntelStorageTest : GameTest
{
    [Test]
    public async Task FailedStorageInsertDoesNotFallThroughToEntityStorage()
    {
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelPaperScraps, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelProgressReports, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelFolders, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelTechnicalManuals, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelDisks, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelDataTerminals, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelSafes, 0, sync: false);
        await OverrideCVar(Side.Server, RMCCVars.RMCIntelExperimentalDevices, 1);

        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        EntityUid cabinet = default;
        await server.WaitPost(() =>
        {
            cabinet = server.EntMan.SpawnEntity("CMFilingCabinet", map.GridCoords);
            server.EntMan.SpawnEntity("RMCSpawnerIntelClose", map.GridCoords);
            server.EntMan.SpawnEntity("RMCSpawnerIntelMedium", map.GridCoords);
            server.EntMan.SpawnEntity("RMCSpawnerIntelFar", map.GridCoords);
            server.EntMan.SpawnEntity("RMCSpawnerIntelScience", map.GridCoords);
        });

        await server.WaitAssertion(() => server.System<IntelSystem>().RunSpawners());

        await server.WaitAssertion(() =>
        {
            var storage = server.EntMan.GetComponent<StorageComponent>(cabinet);
            var devices = server.EntMan.EntityQueryEnumerator<IntelRetrieveItemObjectiveComponent>();
            var deviceCount = 0;
            while (devices.MoveNext(out _, out _))
            {
                deviceCount++;
            }

            Assert.Multiple(() =>
            {
                Assert.That(deviceCount, Is.EqualTo(1));
                Assert.That(storage.Container.ContainedEntities, Is.Empty);
            });
        });
    }
}
