using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Automation;
using Content.Server.CMU14.Portable;
using Content.Shared.CMU14.Portable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.CMU14.Automation;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU.Industry;

public sealed class CMUInserterTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    // A player next to a placed inserter can rotate it and pick it up (which unanchors it) through its verbs.
    [Test]
    public async Task APlayerCanRotateAndPickUpAnInserter()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid arm = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 1; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            user = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var verbs = Server.System<Content.Shared.Verbs.SharedVerbSystem>();
            Assert.That(SComp<TransformComponent>(arm).Anchored, Is.True);

            var all = verbs.GetLocalVerbs(arm, user, new List<Type> { typeof(Content.Shared.Verbs.Verb), typeof(Content.Shared.Verbs.AlternativeVerb) });
            var names = all.Select(v => v.Text).ToList();
            var rotate = all.FirstOrDefault(v => v.Text == Loc.GetString("rotate-verb-get-data-text"));
            var pickUp = all.FirstOrDefault(v => v.Text == Loc.GetString("au14-portable-pick-up"));
            Assert.That(rotate, Is.Not.Null, $"No rotate verb; verbs: {string.Join(", ", names)}");
            Assert.That(pickUp, Is.Not.Null, $"No pick-up verb; verbs: {string.Join(", ", names)}");

            var before = SComp<TransformComponent>(arm).LocalRotation;
            verbs.ExecuteVerb(rotate!, user, arm, forced: true);
            Assert.That(SComp<TransformComponent>(arm).LocalRotation, Is.Not.EqualTo(before), "Rotating did nothing.");

            verbs.ExecuteVerb(pickUp!, user, arm, forced: true);
        });

        await Pair.RunTicksSync(150);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedHandsSystem>().IsHolding(user, arm), Is.True, "The inserter was not picked up.");
            Assert.That(SComp<TransformComponent>(arm).Anchored, Is.False, "The carried inserter is still anchored.");
        });
    }

    [Test]
    public async Task WaitingForItemsDoesNotSpendTheTransferCooldown()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            SSpawnAtPosition("RUMCConveyorBeltTileFast", map.GridCoords);
            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = SSpawnAtPosition("RUMCElectricSmelter", map.GridCoords.Offset(new Vector2(2, 0)));
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(arm, Angle.FromDegrees(90));
            var arms = Server.System<CMUInserterSystem>();
            var interval = SComp<CMUInserterComponent>(arm).Interval;
            arms.Update(interval);

            // An item arrives shortly after the arm found an empty belt.
            var first = SSpawnAtPosition("SteelOre1", map.GridCoords.Offset(new Vector2(0, 1)));
            var stacks = Server.System<SharedStackSystem>();
            stacks.SetCount(first, 1);
            transform.SetCoordinates(first, map.GridCoords);
            arms.Update(interval / 4);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(SComp<CMUInserterComponent>(arm).Claw.ContainedEntity, Is.EqualTo(first));
            Assert.That(slots.GetItemOrNull(target, "material1"), Is.Null, "The item must travel in the claw before delivery.");
            arms.Update(interval / 2);
            Assert.That(slots.GetItemOrNull(target, "material1"), Is.EqualTo(first));

            var second = SSpawnAtPosition("SteelOre1", map.GridCoords.Offset(new Vector2(0, 1)));
            stacks.SetCount(second, 1);
            transform.SetCoordinates(second, map.GridCoords);
            arms.Update(interval / 4);
            Assert.That(SComp<StackComponent>(first).Count, Is.EqualTo(1), "A successful transfer must still impose the arm's cooldown.");
            arms.Update(interval);
            arms.Update(interval / 4);
            arms.Update(interval / 2);
            Assert.That(SComp<StackComponent>(first).Count, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ArmsBridgeMachineOutputsAndInputsThroughABeltWithoutAutomaticPorts()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 4; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            var source = SSpawnAtPosition("RUMCSteamDrill", map.GridCoords);
            var first = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var belt = SSpawnAtPosition("RUMCConveyorBeltTile", map.GridCoords.Offset(new Vector2(2, 0)));
            var second = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(3, 0)));
            var target = SSpawnAtPosition("RUMCElectricSmelter", map.GridCoords.Offset(new Vector2(4, 0)));
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(first, Angle.FromDegrees(90));
            transform.SetLocalRotation(second, Angle.FromDegrees(90));
            SComp<CMUInserterComponent>(second).Enabled = false;
            var ore = SSpawnAtPosition("SteelOre1", map.GridCoords);
            Server.System<SharedStackSystem>().SetCount(ore, 7);
            var slots = Server.System<ItemSlotsSystem>();
            Assert.That(slots.TryInsert(source, "output1", ore, null), Is.True);

            var arms = Server.System<CMUInserterSystem>();
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(source, "output1"), Is.Null);
            Assert.That(SComp<CMUInserterComponent>(first).Claw.ContainedEntity, Is.EqualTo(ore));
            arms.Update(1);
            Assert.That(SComp<TransformComponent>(ore).Coordinates, Is.EqualTo(SComp<TransformComponent>(belt).Coordinates));

            SComp<CMUInserterComponent>(first).Enabled = false;
            SComp<CMUInserterComponent>(second).Enabled = true;
            arms.Update(1);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "material1"), Is.EqualTo(ore));
            Assert.That(SComp<StackComponent>(ore).Count, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task BlockedAndPartialTransfersPreserveOutputsAndNeverExtractIngredients()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var source = SSpawnAtPosition("RUMCLathe", map.GridCoords);
            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = SSpawnAtPosition("RUMCElectricSmelter", map.GridCoords.Offset(new Vector2(2, 0)));
            Server.System<SharedTransformSystem>().SetLocalRotation(arm, Angle.FromDegrees(90));
            var slots = Server.System<ItemSlotsSystem>();
            var stacks = Server.System<SharedStackSystem>();
            var ingredient = SSpawnAtPosition("CMSheetMetal", map.GridCoords);
            var output = SSpawnAtPosition("CMSheetMetal", map.GridCoords);
            stacks.SetCount(output, 5);
            Assert.That(slots.TryInsert(source, "input1", ingredient, null), Is.True);
            Assert.That(slots.TryInsert(source, "output1", output, null), Is.True);

            EntityUid first = default;
            for (var i = 1; i <= 4; i++)
            {
                var item = SSpawnAtPosition("CMSheetMetal", map.GridCoords);
                stacks.SetCount(item, stacks.GetMaxCount(item));
                Assert.That(slots.TryInsert(target, $"material{i}", item, null), Is.True);
                if (i == 1)
                    first = item;
            }

            var arms = Server.System<CMUInserterSystem>();
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(source, "output1"), Is.EqualTo(output));
            Assert.That(SComp<StackComponent>(output).Count, Is.EqualTo(5));

            stacks.SetCount(first, stacks.GetMaxCount(first) - 2);
            slots.SetLock(source, "output1", true);
            arms.Update(1);
            Assert.That(SComp<StackComponent>(output).Count, Is.EqualTo(5), "A locked output must not be merged remotely.");
            slots.SetLock(source, "output1", false);
            arms.Update(1);
            arms.Update(1);
            Assert.That(SComp<StackComponent>(first).Count, Is.EqualTo(stacks.GetMaxCount(first)));
            Assert.That(SComp<StackComponent>(output).Count, Is.EqualTo(3));
            Assert.That(SComp<CMUInserterComponent>(arm).Claw.ContainedEntity, Is.EqualTo(output), "The remainder must stay in the claw until it fits.");

            Assert.That(slots.TryEject(target, "material2", null, out _), Is.True);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "material2"), Is.EqualTo(output));
            arms.Update(1);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(source, "input1"), Is.EqualTo(ingredient), "An empty output must not expose the input slots.");
        });
    }

    [TestCase("RMCCrateBase")]
    [TestCase("CMBackpack")]
    public async Task RotationAndDisablingControlNonStackTransfersBetweenStorageAndMachines(string storagePrototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var storage = SSpawnAtPosition(storagePrototype, map.GridCoords);
            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = SSpawnAtPosition("RUMCRecycler", map.GridCoords.Offset(new Vector2(2, 0)));
            var tool = SSpawnAtPosition("CMScrewdriver", map.GridCoords);
            Assert.That(Server.System<ConveyorBeltSystem>().TryPut(storage, tool), Is.True);
            var component = SComp<CMUInserterComponent>(arm);
            var arms = Server.System<CMUInserterSystem>();
            var slots = Server.System<ItemSlotsSystem>();
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(arm, Angle.FromDegrees(90));
            component.Enabled = false;
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "input1"), Is.Null);
            component.Enabled = true;
            transform.Unanchor(arm);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "input1"), Is.Null);
            transform.AnchorEntity(arm);
            arms.Update(1);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "input1"), Is.EqualTo(tool));
            arms.Update(1);

            // Reverse the arm to send a finished, non-stack item back into the same storage.
            var product = SSpawnAtPosition("CMScrewdriver", map.GridCoords);
            Assert.That(slots.TryInsert(target, "output1", product, null), Is.True);
            transform.SetLocalRotation(arm, Angle.FromDegrees(-90));
            arms.Update(1);
            arms.Update(1);
            Assert.That(slots.GetItemOrNull(target, "output1"), Is.Null);
            Assert.That(SComp<TransformComponent>(product).ParentUid, Is.EqualTo(storage));
            Assert.That(slots.GetItemOrNull(target, "input1"), Is.EqualTo(tool));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnanchoringOrDeletingAnArmReleasesItsCarriedItem(bool delete)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 2; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);
            var source = SSpawnAtPosition("RUMCLathe", map.GridCoords);
            var arm = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            SSpawnAtPosition("RUMCElectricSmelter", map.GridCoords.Offset(new Vector2(2, 0)));
            var transform = Server.System<SharedTransformSystem>();
            transform.SetLocalRotation(arm, Angle.FromDegrees(90));
            var ore = SSpawnAtPosition("SteelOre1", map.GridCoords);
            Server.System<SharedStackSystem>().SetCount(ore, 5);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(source, "output1", ore, null), Is.True);
            Server.System<CMUInserterSystem>().Update(1);
            Assert.That(SComp<CMUInserterComponent>(arm).Claw.ContainedEntity, Is.EqualTo(ore));

            if (delete)
                SEntMan.DeleteEntity(arm);
            else
                transform.Unanchor(arm);

            Assert.That(SEntMan.EntityExists(ore), Is.True, "Removing the arm must not destroy its cargo.");
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(ore), Is.False);
            Assert.That(SComp<StackComponent>(ore).Count, Is.EqualTo(5));
            Assert.That(SComp<TransformComponent>(ore).GridUid, Is.EqualTo(map.Grid.Owner));
        });
    }
}
