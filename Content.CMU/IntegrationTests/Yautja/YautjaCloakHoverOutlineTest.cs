using System.Linq;
using Content.Client.CMU14.Stealth;
using Content.Client.Interactable.Components;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.IntegrationTests.Pair;
using Content.Shared.Inventory;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaCloakHoverOutlineTest
{
    // the hover outline used to draw on top of the cloak shader and give the pred away
    [Test]
    public async Task CloakedYautjaHasNoHoverOutline()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        NetEntity hunterNet = default;
        NetEntity actionNet = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var inventory = entities.System<InventorySystem>();
            Assert.That(inventory.TryEquip(hunter, entities.SpawnEntity("CMUYautjaCloakPack", map.GridCoords), "back",
                silent: true, force: true), Is.True);
            server.PlayerMan.SetAttachedEntity(pair.Player!, hunter);
            hunterNet = entities.GetNetEntity(hunter);
            actionNet = entities.GetNetEntity(entities.System<SharedRMCActionsSystem>()
                .GetActionsWithEvent<YautjaToggleCloakActionEvent>(hunter).Single());
        });
        await pair.RunTicksSync(10);

        await client.WaitAssertion(() =>
        {
            client.EntMan.System<CMUCloakHoverOutlineSystem>().FrameUpdate(0);
            Assert.That(client.EntMan.HasComponent<InteractionOutlineComponent>(client.EntMan.GetEntity(hunterNet)), Is.True,
                "an uncloaked yautja should still get the normal hover outline");
        });

        await ToggleCloak(pair, actionNet);
        await client.WaitAssertion(() =>
        {
            var hunter = client.EntMan.GetEntity(hunterNet);
            Assert.That(client.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
            client.EntMan.System<CMUCloakHoverOutlineSystem>().FrameUpdate(0);
        });
        await pair.RunTicksSync(1);
        await client.WaitAssertion(() =>
            Assert.That(client.EntMan.HasComponent<InteractionOutlineComponent>(client.EntMan.GetEntity(hunterNet)), Is.False,
                "hovering a cloaked yautja must not outline them"));

        await ToggleCloak(pair, actionNet);
        await client.WaitAssertion(() =>
        {
            var hunter = client.EntMan.GetEntity(hunterNet);
            Assert.That(client.EntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);
            client.EntMan.System<CMUCloakHoverOutlineSystem>().FrameUpdate(0);
        });
        await pair.RunTicksSync(1);
        await client.WaitAssertion(() =>
            Assert.That(client.EntMan.HasComponent<InteractionOutlineComponent>(client.EntMan.GetEntity(hunterNet)), Is.True,
                "the hover outline should come back once the cloak drops"));

        await pair.CleanReturnAsync();
    }

    private static async Task ToggleCloak(TestPair pair, NetEntity action)
    {
        var client = pair.Client;
        await client.WaitPost(() => client.EntMan.RaisePredictiveEvent(new Content.Shared.Actions.RequestPerformActionEvent(action,
            client.ResolveDependency<IGameTiming>().CurTick)));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
    }
}
