using Content.Client.CMU14.ThreeD.Scene;
using Content.Client.CMU14.ThreeD;
using Content.Client.Inventory;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ThreeD;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DEquipmentAppearanceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ReplicatedEquipmentMatchesItsPoseAndUnknownPaintKeepsTheSprite()
    {
        var map = await Pair.CreateTestMap();
        EntityUid wearer = default;
        EntityUid tool = default;
        EntityUid rifle = default;
        float emptyBottom = 0;
        await Server.WaitAssertion(() =>
        {
            wearer = SSpawnAtPosition("MobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, wearer);
            var uniform = SSpawnAtPosition("AU14CMBUniform", map.GridCoords);
            tool = SSpawnAtPosition("CMCrowbar", map.GridCoords);
            rifle = SSpawnAtPosition("RMCWeaponRifleM54C", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryEquip(wearer, uniform, "jumpsuit", force: true), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(wearer, tool), Is.True);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(wearer, rifle), Is.True);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            Client.ResolveDependency<CMU3DModelLibrary>().LoadWorkbench();
            var local = ToClientUid(wearer);
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            var poses = prototypes.EnumeratePrototypes<CMU3DEquipmentPosePrototype>().ToArray();
            var worn = poses.Single(p => p.Slot == "jumpsuit" && p.SourcePrototypes.Contains("AU14CMBUniform"));
            var held = poses.Single(p => p.Slot == "hand" && p.SourcePrototypes.Contains("CMCrowbar"));
            var sprites = Client.System<SpriteSystem>();
            sprites.ForceUpdate(local);
            sprites.ForceUpdate(ToClientUid(tool));
            sprites.ForceUpdate(ToClientUid(rifle));
            var sprite = CComp<SpriteComponent>(local);
            var scene = Client.System<CMU3DLiveSceneSystem>();
            Assert.That(scene.TryWornEquipmentAppearance(local, sprite, "jumpsuit", worn, out _), Is.True);
            Assert.That(scene.TryEquipmentParts(ToClientUid(tool), prototypes.Index(held.Model), held, out _, out _, out _), Is.True,
                "A resource-rooted source reference must match the actual contained item's RSI.");
            var empty = RifleParts();
            emptyBottom = empty.Min(p => p.Min.Z);


            var keys = CComp<InventorySlotsComponent>(local).VisualLayerKeys["jumpsuit"];
            var key = keys.Single();
            Assert.That(sprites.LayerMapTryGet((local, sprite), key, out var index, false), Is.True);
            sprites.LayerSetColor((local, sprite), index, Color.Magenta);
            Assert.That(scene.TryWornEquipmentAppearance(local, sprite, "jumpsuit", worn, out _), Is.False,
                "An unmodeled dye must retain its original sprite instead of silently adopting the default uniform.");
            sprites.LayerSetColor((local, sprite), index, worn.WornLayers[0].Color);
        });
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<InventorySystem>().TryUnequip(wearer, "jumpsuit", force: true), Is.True);
            var magazine = SSpawnAtPosition("CMMagazineRifleM54C", map.GridCoords);
            Assert.That(Server.System<ItemSlotsSystem>().TryInsert(rifle, "gun_magazine", magazine, wearer), Is.True);
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var local = ToClientUid(wearer);
            var worn = Client.ResolveDependency<IPrototypeManager>().EnumeratePrototypes<CMU3DEquipmentPosePrototype>()
                .Single(p => p.Slot == "jumpsuit" && p.SourcePrototypes.Contains("AU14CMBUniform"));
            Assert.That(Client.System<CMU3DLiveSceneSystem>().TryWornEquipmentAppearance(local,
                CComp<SpriteComponent>(local), "jumpsuit", worn, out _), Is.False,
                "Removing equipment must remove its 3D attachment eligibility after replication.");
            var loaded = RifleParts();
            Assert.That(loaded.Min(p => p.Min.Z), Is.LessThan(emptyBottom),
                "Inserting a magazine must select geometry with the magazine protruding below the empty well.");
        });

        IReadOnlyList<CMU3DModelPart> RifleParts()
        {
            Client.System<SpriteSystem>().ForceUpdate(ToClientUid(rifle));
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            var scene = Client.System<CMU3DLiveSceneSystem>();
            var matches = prototypes.EnumeratePrototypes<CMU3DEquipmentPosePrototype>()
                .Where(p => p.Slot == "hand" && p.SourcePrototypes.Contains("RMCWeaponRifleM54C") &&
                    scene.TryEquipmentParts(ToClientUid(rifle), prototypes.Index(p.Model), p, out _, out _, out _))
                .ToArray();
            Assert.That(matches, Has.Length.EqualTo(1),
                "The replicated rifle magazine and starting attachments must select one complete authored pose.");
            return prototypes.Index(matches.Single().Model).Parts;
        }
    }
}
