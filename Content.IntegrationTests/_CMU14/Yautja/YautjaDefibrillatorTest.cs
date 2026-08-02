using Content.Server.Medical;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaDefibrillatorTest
{
    [Test]
    public async Task StandardDefibrillatorRejectsYautjaButAcceptsHuman()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var defibrillatorSystem = entMan.System<DefibrillatorSystem>();
            var inventory = entMan.System<InventorySystem>();
            var mobState = entMan.System<MobStateSystem>();
            var toggle = entMan.System<ItemToggleSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var yautja = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var defibrillator = entMan.SpawnEntity("CMDefibrillator", MapCoordinates.Nullspace);

            try
            {
                if (inventory.TryGetSlotEntity(yautja, "outerClothing", out var outerClothing))
                    entMan.DeleteEntity(outerClothing.Value);

                mobState.ChangeMobState(yautja, MobState.Dead);
                mobState.ChangeMobState(human, MobState.Dead);

                Assert.That(toggle.TryActivate((defibrillator, null), user, predicted: false), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<YautjaComponent>(yautja), Is.True);
                    Assert.That(mobState.IsDead(yautja), Is.True);
                    Assert.That(mobState.IsDead(human), Is.True);
                    Assert.That(defibrillatorSystem.CanZap(defibrillator, yautja, user), Is.False);
                    Assert.That(defibrillatorSystem.CanZap(defibrillator, human, user), Is.True);
                });

                defibrillatorSystem.Zap(defibrillator, yautja, user);
                Assert.That(mobState.IsDead(yautja), Is.True,
                    "A direct standard-defibrillator attempt must not revive a Yautja.");
            }
            finally
            {
                foreach (var uid in new[] { user, yautja, human, defibrillator })
                {
                    if (!entMan.Deleted(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }
}
