using Content.Shared.Blocking.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameStates;
using Robust.Client.Timing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Networking;

[TestFixture]
public sealed class CMUBlockingPredictionTest
{
    [TestCase(true, TestName = "RejectedBlockingItemDropRestoresHandWithoutMutatingRollbackComponents")]
    [TestCase(false, TestName = "RejectedBlockingItemPickupRestoresHandWithoutMutatingRollbackComponents")]
    public async Task BlockingItemPredictionRestoresAuthoritativeHand(bool serverHolding)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        NetEntity userNet = default;
        NetEntity itemNet = default;

        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = entities.SpawnEntity("CMUYautjaCombistick", map.GridCoords);
            if (serverHolding)
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, item, checkActionBlocker: false), Is.True);

            pair.Server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            userNet = entities.GetNetEntity(user);
            itemNet = entities.GetNetEntity(item);
        });
        await pair.RunUntilSynced();

        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var user = entities.GetEntity(userNet);
            var item = entities.GetEntity(itemNet);
            var hands = entities.System<SharedHandsSystem>();
            var timing = pair.Client.ResolveDependency<IClientGameTiming>();
            var states = pair.Client.ResolveDependency<IClientGameStateManager>();
            Assert.That(timing.InPrediction, Is.True);
            Assert.That(hands.IsHolding(user, item), Is.EqualTo(serverHolding));
            Assert.That(entities.HasComponent<BlockingUserComponent>(user), Is.EqualTo(serverHolding));

            if (serverHolding)
                Assert.That(hands.TryDrop(user, item, checkActionBlocker: false), Is.True);
            else
                Assert.That(hands.TryPickupAnyHand(user, item, checkActionBlocker: false), Is.True);

            Assert.That(hands.IsHolding(user, item), Is.EqualTo(!serverHolding));
            Assert.That(entities.HasComponent<BlockingUserComponent>(user), Is.EqualTo(!serverHolding),
                "Normal predicted interactions must still add or remove blocking ownership.");
            Assert.That(entities.GetComponent<ContainerManagerComponent>(user).LastModifiedTick,
                Is.GreaterThan(timing.LastRealTick), "The real container state must be dirty before rollback.");

            Assert.DoesNotThrow(() => states.ResetPredictedEntities());
            Assert.That(hands.IsHolding(user, item), Is.EqualTo(serverHolding),
                "Rollback must restore the authoritative hand contents, not skip the container.");
        });

        await pair.RunUntilSynced();
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.EntMan;
            var user = entities.GetEntity(userNet);
            var item = entities.GetEntity(itemNet);
            Assert.That(entities.System<SharedHandsSystem>().IsHolding(user, item), Is.EqualTo(serverHolding));
            Assert.That(entities.HasComponent<BlockingUserComponent>(user), Is.EqualTo(serverHolding));
            if (serverHolding)
                Assert.That(entities.GetComponent<BlockingUserComponent>(user).BlockingItem, Is.EqualTo(item));
        });

        await pair.CleanReturnAsync();
    }
}
