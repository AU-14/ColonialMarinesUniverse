using System.Linq;
using System.Numerics;
using Content.Shared.Effects;
using Content.Server.Humanoid.Systems;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Armor.ThermalCloak;
using Content.Shared._RMC14.CameraShake;
using Content.Shared._RMC14.Stealth;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Jittering;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHealthShardCloakTest
{
    [Test]
    public async Task FreshHealthShardPreservesCloakAfterFeedback()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        foreach (var randomSpawn in new[] { false, true })
        {
            NetEntity hunterNet = default;
            EntityUid half = default;

            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                var hunter = randomSpawn
                    ? entities.System<RandomHumanoidSystem>().SpawnRandomHumanoid("CMUYautjaHunter", map.GridCoords, string.Empty)
                    : entities.SpawnEntity("CMUMobYautja", map.GridCoords);
                hunterNet = entities.GetNetEntity(hunter);
                server.PlayerMan.SetAttachedEntity(pair.Player!, hunter);
                var hands = entities.System<SharedHandsSystem>();
                var shard = entities.SpawnEntity("CMUYautjaHealthShard", map.GridCoords);
                Assert.That(hands.TryPickupAnyHand(hunter, shard), Is.True);
                var split = new UseInHandEvent(hunter);
                entities.EventBus.RaiseLocalEvent(shard, split);
                Assert.That(split.Handled, Is.True);
                half = hands.EnumerateHeld(hunter).Single(uid => entities.HasComponent<YautjaHealthShardHalfComponent>(uid));
                Assert.That(hands.EnumerateHeld(hunter).Count(), Is.EqualTo(2));
                ToggleCloak(entities, hunter);
            });
            await pair.RunUntilSynced();

            await server.WaitAssertion(() =>
            {
                var entities = server.EntMan;
                var hunter = entities.GetEntity(hunterNet);
                var use = new AfterInteractEvent(hunter, half, hunter, default, true);
                entities.EventBus.RaiseLocalEvent(half, use);
                Assert.That(use.Handled, Is.True);
            });
            await pair.RunSeconds(3);
            await pair.RunUntilSynced();

            for (var second = 0; second < 10; second++)
            {
                await server.WaitAssertion(() =>
                {
                    var entities = server.EntMan;
                    var hunter = entities.GetEntity(hunterNet);
                    Assert.That(entities.Deleted(half), Is.True, "The application must complete and consume the shard.");
                    Assert.That(entities.System<SharedHandsSystem>().EnumerateHeld(hunter).Count(), Is.Zero);
                    AssertCloak(entities, hunter);
                    Assert.That(entities.System<DamageableSystem>().GetTotalDamage((hunter, entities.GetComponent<DamageableComponent>(hunter))).Float(), Is.Zero);
                    var solutions = entities.System<SharedSolutionContainerSystem>();
                    Assert.That(solutions.TryGetSolution(hunter, BloodstreamComponent.DefaultBloodSolutionName, out _, out var blood), Is.True);
                    TestContext.Out.WriteLine($"server t={second}: blood={string.Join(",", blood!.Contents)}");
                    Assert.That(blood.GetTotalPrototypeQuantity("CMBicaridine").Float(), Is.GreaterThan(0).And.LessThan(30));
                    Assert.That(blood.GetTotalPrototypeQuantity("CMUYautjaAnalgesic").Float(), Is.GreaterThan(0));
                    Assert.That(blood.GetTotalPrototypeQuantity("CMUParacetamol").Float(), Is.Zero);
                    Assert.That(entities.HasComponent<RMCCameraShakingComponent>(hunter), Is.False);
                });
                await client.WaitAssertion(() =>
                {
                    var entities = client.EntMan;
                    var hunter = entities.GetEntity(hunterNet);
                    AssertCloak(entities, hunter);
                    var sprite = entities.GetComponent<SpriteComponent>(hunter);
                    Assert.That(entities.System<SpriteSystem>().TryGetPostShader(sprite, "RMCInvisible", out _), Is.True);
                    TestContext.Out.WriteLine($"client t={second}: color={sprite.Color}, offset={sprite.Offset}");
                    Assert.That(sprite.Color, Is.EqualTo(Color.White));
                    Assert.That(sprite.Offset, Is.EqualTo(Vector2.Zero));
                    Assert.That(entities.HasComponent<ColorFlashEffectComponent>(hunter), Is.False);
                    Assert.That(entities.HasComponent<JitteringComponent>(hunter), Is.False);
                    Assert.That(entities.HasComponent<RMCCameraShakingComponent>(hunter), Is.False);
                });
                await pair.RunSeconds(1);
            }

            await server.WaitAssertion(() =>
            {
                server.PlayerMan.SetAttachedEntity(pair.Player!, null);
                server.EntMan.DeleteEntity(server.EntMan.GetEntity(hunterNet));
            });
        }

        await pair.CleanReturnAsync();
    }

    private static void AssertCloak(IEntityManager entities, EntityUid hunter)
    {
        Assert.That(entities.GetComponent<EntityTurnInvisibleComponent>(hunter).Enabled, Is.True);
        Assert.That(entities.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
        Assert.That(entities.HasComponent<ThermalCloakUserComponent>(hunter), Is.True);
        var thermal = entities.GetComponent<ThermalCloakUserComponent>(hunter);
        Assert.That(thermal.CurrentOpacity, Is.EqualTo(thermal.Opacity));
        Assert.That(entities.GetComponent<EntityActiveInvisibleComponent>(hunter).Opacity, Is.EqualTo(thermal.Opacity));
    }

    private static void ToggleCloak(IEntityManager entities, EntityUid hunter)
    {
        Assert.That(entities.System<InventorySystem>().TryGetSlotEntity(hunter, "gloves", out var bracer), Is.True);
        var action = entities.System<SharedRMCActionsSystem>().GetActionsWithEvent<YautjaToggleCloakActionEvent>(hunter).Single();
        var toggle = new YautjaToggleCloakActionEvent { Performer = hunter, Action = action };
        entities.EventBus.RaiseLocalEvent(bracer!.Value, toggle);
        Assert.That(toggle.Handled, Is.True);
    }
}
