using System.Collections.Generic;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.GameTicking;
using Robust.Client.GameStates;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class OrganClientStateResetTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ResettingReplicatedHumanDoesNotSpawnOrganEffects(bool roundCleanupReceived)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        NetEntity humanNet = default;

        await server.WaitPost(() =>
        {
            var human = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, human);
            humanNet = server.EntMan.GetNetEntity(human);
        });
        await pair.RunUntilSynced();

        await client.WaitAssertion(() =>
        {
            var entMan = client.EntMan;
            var human = entMan.GetEntity(humanNet);
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(index.TryGetOrgan<HeartComponent>(human, out _), Is.True);
                Assert.That(index.TryGetOrgan<KidneysComponent>(human, out _), Is.True);
                Assert.That(index.TryGetOrgan<LiverComponent>(human, out _), Is.True);
                Assert.That(index.TryGetOrgan<LungsComponent>(human, out _), Is.True);
                Assert.That(index.TryGetOrgan<CMUStomachComponent>(human, out _), Is.True);
            });

            var spawnedEffects = new List<string>();
            void OnEntityInitialized(Entity<MetaDataComponent> entity)
            {
                if (entity.Comp.EntityPrototype?.ID is { } prototype &&
                    prototype.StartsWith("StatusEffectCMU", StringComparison.Ordinal))
                {
                    spawnedEffects.Add(prototype);
                }
            }

            entMan.EntityInitialized += OnEntityInitialized;
            try
            {
                if (roundCleanupReceived)
                    entMan.EventBus.RaiseEvent(EventSource.Network, new RoundRestartCleanupEvent());

                // Exercise the real detach-before-delete loop. Direct DeleteEntity marks the organs
                // terminating first and cannot reproduce the collection mutation seen by clients.
                var state = new GameState(
                    GameTick.Zero,
                    client.ResolveDependency<IGameTiming>().CurTick,
                    0,
                    Array.Empty<EntityState>(),
                    Array.Empty<SessionState>(),
                    Array.Empty<NetEntity>());
                var gameStates = client.ResolveDependency<IClientGameStateManager>();
                Assert.DoesNotThrow(() => gameStates.PartialStateReset(state, resetAllEntities: true));
                Assert.That(spawnedEffects, Is.Empty);
                Assert.That(entMan.EntityExists(human), Is.False);
            }
            finally
            {
                entMan.EntityInitialized -= OnEntityInitialized;
            }
        });

        await pair.CleanReturnAsync();
    }
}
