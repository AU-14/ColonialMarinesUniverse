using Content.Shared._RMC14.NightVision;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class InnateNightVisionRestoreTest
{
    // the visor used to leave its green filter stuck on a synth's own NV after being turned off
    [Test]
    public async Task VisorNightVisionHandsInnateNightVisionBackUntouched()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var nightVisionSys = server.System<SharedNightVisionSystem>();

            // the real synth NV block, so the test breaks if that ever changes shape
            var synth = entities.SpawnEntity("RMCSynthAddComponents", map.GridCoords);
            var innate = entities.GetComponent<NightVisionComponent>(synth);
            Assert.That(innate.Innate);
            var green = innate.Green;
            var blockScopes = innate.BlockScopes;
            var state = innate.State;

            // any green NV item does it, the visor and the scout sight share the same enable path
            var visor = entities.SpawnEntity("CMGlassesM42ScoutSight", map.GridCoords);
            var item = entities.GetComponent<NightVisionItemComponent>(visor);

            nightVisionSys.EnableNightVisionItem((visor, item), synth);
            Assert.That(innate.Green, Is.True, "visor never took over");

            nightVisionSys.DisableNightVisionItem((visor, item), synth);
            Assert.That(entities.HasComponent<NightVisionComponent>(synth), "innate NV got removed");
            Assert.That(innate.Green, Is.EqualTo(green), "visor's green filter stuck to the synth's own NV");
            Assert.That(innate.BlockScopes, Is.EqualTo(blockScopes));
            Assert.That(innate.State, Is.EqualTo(state));
        });

        await pair.CleanReturnAsync();
    }
}
