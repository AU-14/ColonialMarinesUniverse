using Content.Client.CMU14.Automation;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Automation;
using Content.Shared.CMU14.Automation;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU.Industry;

public sealed class CMUInserterReplicationTest : GameTest
{
    [Test]
    public async Task CarriedItemReplicatesMovesWithTheClawAndRestoresItsSpriteAfterDelivery()
    {
        var map = await Pair.CreateTestMap();
        EntityUid arm = default;
        EntityUid target = default;
        EntityUid ore = default;
        NetEntity armNet = default;
        NetEntity oreNet = default;
        Vector2 originalOffset = default;
        Vector2 originalScale = default;
        Vector2 pickupOffset = default;
        int originalDepth = 0;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, map.Grid.Owner);
            var source = SSpawnAtPosition("RUMCLathe", map.GridCoords);
            arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            target = SSpawnAtPosition("RUMCElectricSmelter", map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedTransformSystem>().SetLocalRotation(arm, Angle.FromDegrees(90));
            ore = SSpawnAtPosition("SteelOre1", map.GridCoords);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(source, "output1", ore, null), Is.True);
            var component = SComp<CMUInserterComponent>(arm);
            component.Interval = 10;
            Server.System<CMUInserterSystem>().SetEnabled((arm, component), false);
            armNet = SEntMan.GetNetEntity(arm);
            oreNet = SEntMan.GetNetEntity(ore);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var sprite = CComp<SpriteComponent>(CEntMan.GetEntity(oreNet));
            originalOffset = sprite.Offset;
            originalScale = sprite.Scale;
            originalDepth = sprite.DrawDepth;
        });

        await Server.WaitAssertion(() =>
        {
            var arms = Server.System<CMUInserterSystem>();
            var component = SComp<CMUInserterComponent>(arm);
            arms.SetEnabled((arm, component), true);
            arms.Update(0.1f);
            arms.SetEnabled((arm, component), false);
            Assert.That(component.Claw.ContainedEntity, Is.EqualTo(ore));
            Assert.That(Server.System<ItemSlotsSystem>().GetItemOrNull(target, "material1"), Is.Null);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var clientArm = CEntMan.GetEntity(armNet);
            var clientOre = CEntMan.GetEntity(oreNet);
            Assert.That(Client.System<SharedContainerSystem>().TryGetContainer(clientArm,
                CMUInserterComponent.ClawContainerId, out var claw), Is.True);
            Assert.That(claw.ContainedEntities, Does.Contain(clientOre));
            Client.System<CMUInserterVisualsSystem>().FrameUpdate(0);
            var sprite = CComp<SpriteComponent>(clientOre);
            Assert.That(sprite.ContainerOccluded, Is.False, "The actual held item must be visible inside the claw.");
            Assert.That(sprite.Scale.X, Is.LessThan(originalScale.X));
            pickupOffset = sprite.Offset;
        });

        await Server.WaitAssertion(() =>
        {
            var component = SComp<CMUInserterComponent>(arm);
            var arms = Server.System<CMUInserterSystem>();
            arms.Update(20);
            Assert.That(component.Claw.ContainedEntity, Is.EqualTo(ore), "Stopping must pause the real transfer.");
            arms.SetEnabled((arm, component), true);
            arms.Update(component.PhaseDuration / 2);
            arms.SetEnabled((arm, component), false);
            // Reconfiguration must not redirect the item already in flight.
            Server.System<SharedTransformSystem>().SetLocalRotation(arm, Angle.FromDegrees(-90));
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Client.System<CMUInserterVisualsSystem>().FrameUpdate(0);
            var sprite = CComp<SpriteComponent>(CEntMan.GetEntity(oreNet));
            Assert.That(Vector2.Distance(sprite.Offset, pickupOffset), Is.GreaterThan(0.5f));
            Assert.That(CComp<TransformComponent>(CEntMan.GetEntity(oreNet)).ParentUid,
                Is.EqualTo(CEntMan.GetEntity(armNet)));
        });

        await Server.WaitAssertion(() =>
        {
            var component = SComp<CMUInserterComponent>(arm);
            var arms = Server.System<CMUInserterSystem>();
            arms.SetEnabled((arm, component), true);
            arms.Update(component.PhaseDuration);
            arms.SetEnabled((arm, component), false);
            Assert.That(Server.System<ItemSlotsSystem>().GetItemOrNull(target, "material1"), Is.EqualTo(ore));
            Assert.That(component.Claw.ContainedEntity, Is.Null);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Client.System<CMUInserterVisualsSystem>().FrameUpdate(0);
            var sprite = CComp<SpriteComponent>(CEntMan.GetEntity(oreNet));
            Assert.That(sprite.Offset, Is.EqualTo(originalOffset));
            Assert.That(sprite.Scale, Is.EqualTo(originalScale));
            Assert.That(sprite.DrawDepth, Is.EqualTo(originalDepth));
            Assert.That(sprite.ContainerOccluded, Is.True, "The delivered item must obey its machine's container visibility again.");
        });
    }
}
