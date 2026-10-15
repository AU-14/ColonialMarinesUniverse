using Content.Server.CMU14.Round;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14;

[TestFixture]
public sealed class ExperimentEggCleanupTest
{
    private static readonly Robust.Shared.Prototypes.ProtoId<GamePresetPrototype> ColonyFallPrototype = "ColonyFall";

    [Test]
    public async Task RoundCleanupPreservesCrateEggsAndRemovesLooseStructures()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid crate = default, looseEgg = default, weeds = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            crate = entities.SpawnEntity("AU14CrateSecureWeYuXenoEggs", map.GridCoords);
            Assert.That(EggCount(crate), Is.EqualTo(2), "The actual crate must be filled before cleanup.");
            looseEgg = entities.SpawnEntity("XenoEgg", map.GridCoords);
            weeds = entities.SpawnEntity("XenoWeeds", map.GridCoords);
            server.System<AuRoundSystem>().SetPreset(server.ResolveDependency<IPrototypeManager>()
                .Index<GamePresetPrototype>(ColonyFallPrototype));
            entities.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
        });
        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(EggCount(crate), Is.EqualTo(2), "Round startup must preserve experiment cargo.");
            Assert.That(server.EntMan.Deleted(looseEgg), Is.True);
            Assert.That(server.EntMan.Deleted(weeds), Is.True);
            var laterCrate = server.EntMan.SpawnEntity("AU14CrateSecureWeYuXenoEggs", map.GridCoords);
            Assert.That(EggCount(laterCrate), Is.EqualTo(2));
        });
        await pair.CleanReturnAsync();

        int EggCount(EntityUid storage)
        {
            return server.EntMan.GetComponent<EntityStorageComponent>(storage).Contents.ContainedEntities
                .Count(uid => server.EntMan.HasComponent<XenoEggComponent>(uid));
        }
    }
}
