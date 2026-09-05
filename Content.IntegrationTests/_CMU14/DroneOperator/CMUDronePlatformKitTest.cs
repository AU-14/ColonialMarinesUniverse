using System.Linq;
using Content.Server.CMU14.DroneOperator;
using Content.Shared.CMU14.DroneOperator;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.DroneOperator;

[TestFixture]
public sealed class CMUDronePlatformKitTest
{
    [TestCase(CMUDronePlatform.Humanoid, "CMUDroneBodyFrame", 13)]
    [TestCase(CMUDronePlatform.Tracked, "CMUCombatDroneHull", 7)]
    [TestCase(CMUDronePlatform.Flamer, "CMUFlamerDroneHull", 7)]
    public async Task InGameChoiceUnpacksOneKitThroughTheUi(CMUDronePlatform platform, string hullId, int itemCount)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid user = default, pack = default, kit = default;
        NetEntity kitNet = default;
        CMUDronePlatformKitComponent kitComponent = null!;

        await server.WaitAssertion(() =>
        {
            user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            pack = entities.SpawnEntity("CMUDroneOperatorChoicePackFilled", map.GridCoords);
            kit = entities.GetComponent<StorageComponent>(pack).Container.ContainedEntities.Single();
            kitComponent = entities.GetComponent<CMUDronePlatformKitComponent>(kit);
            var system = entities.System<CMUDronePlatformKitSystem>();
            Assert.That(system.TrySelectPlatform((kit, kitComponent), user, platform), Is.False,
                "Only a drone operator may redeem this equipment.");
            entities.AddComponent<CMUDroneOperatorComponent>(user);
            Assert.That(system.TrySelectPlatform((kit, kitComponent), user, (CMUDronePlatform)255), Is.False);
            Assert.That(entities.System<InventorySystem>().TryEquip(user, pack, "back", force: true), Is.True);
            Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(user, kit, checkActionBlocker: false), Is.True);
            var use = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(kit, use);
            Assert.That(use.Handled, Is.True);
            Assert.That(entities.System<SharedUserInterfaceSystem>().IsUiOpen(kit, CMUDronePlatformKitUi.Key, user), Is.True);
            kitNet = entities.GetNetEntity(kit);
        });
        await pair.RunUntilSynced();
        await pair.Client.WaitPost(() => pair.Client.EntMan.System<SharedUserInterfaceSystem>().ClientSendUiMessage(
            pair.Client.EntMan.GetEntity(kitNet), CMUDronePlatformKitUi.Key, new CMUDronePlatformSelectedMessage(platform)));
        await pair.RunUntilSynced();
        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(kit), Is.False);
            var contents = entities.GetComponent<StorageComponent>(pack).Container.ContainedEntities;
            Assert.That(contents, Has.Count.EqualTo(itemCount));
            Assert.That(contents.Select(uid => entities.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID), Does.Contain(hullId));
            Assert.That(entities.System<CMUDronePlatformKitSystem>().TrySelectPlatform((kit, kitComponent), user, platform), Is.False);
            Assert.That(contents, Has.Count.EqualTo(itemCount), "A second redemption must not duplicate the supplies.");
        });
        await pair.CleanReturnAsync();
    }
}
