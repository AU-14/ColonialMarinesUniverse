using System.Linq;
using Content.Client.Audio;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.Configuration;
using ServerLightSystem = Content.Server.Light.EntitySystems.ExpendableLightSystem;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMUFlareAudioBudgetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ArrivingNearManyLitFlaresUsesAmbientBudgetAndReleasesSpentSounds()
    {
        var map = await Pair.CreateTestMap();
        var flares = new List<EntityUid>();
        await Client.WaitPost(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            config.SetCVar(CCVars.MaxAmbientSources, 8);
            config.SetCVar(CCVars.AmbientCooldown, 0.1f);
        });
        await Server.WaitAssertion(() =>
        {
            Server.PlayerMan.SetAttachedEntity(ServerSession!, map.Grid.Owner);
            for (var i = 0; i < 32; i++)
            {
                var uid = SEntMan.SpawnEntity("CMFlare", map.GridCoords);
                flares.Add(uid);
                Assert.That(Server.System<ServerLightSystem>().TryActivate((uid,
                    SEntMan.GetComponent<ExpendableLightComponent>(uid))), Is.True);
            }
        });
        await Pair.RunSeconds(2);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Client.System<AmbientSoundSystem>().Update(0.2f);
            var burning = CEntMan.EntityQuery<AudioComponent>()
                .Where(a => a.FileName == "/Audio/Items/Flare/flare_burn.ogg").ToArray();
            Assert.That(burning.Length, Is.InRange(1, 3),
                "A crowd of lit flares must share the ambient per-sound budget instead of allocating one source per flare.");
            Assert.That(burning.All(a => a.Params.Loop), Is.True);
        });
        await Server.WaitPost(() =>
        {
            foreach (var uid in flares)
                Server.System<ServerLightSystem>().ExtinguishFlare((uid,
                    SEntMan.GetComponent<ExpendableLightComponent>(uid)));
        });
        await Pair.RunSeconds(2);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(CEntMan.EntityQuery<AudioComponent>()
            .Any(a => a.FileName == "/Audio/Items/Flare/flare_burn.ogg"), Is.False,
            "Extinguished flares must release their ambient streams."));
    }
}
