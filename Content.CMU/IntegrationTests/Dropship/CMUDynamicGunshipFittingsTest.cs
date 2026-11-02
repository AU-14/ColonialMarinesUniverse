using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUDynamicGunshipFittingsTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";

    [Test]
    public async Task LoadedGunshipHasStockedCabinetsAndInwardSideLights()
    {
        var map = await Pair.LoadTestMap(new ResPath("Maps/_RMC14/Shuttles/dynamic_gunship.yml"));
        await Server.WaitAssertion(() =>
        {
            var slots = SEntMan.System<ItemSlotsSystem>();
            var transforms = SEntMan.System<SharedTransformSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            var lights = SEntMan.System<SharedPointLightSystem>();
            var tags = SEntMan.System<TagSystem>();
            var defibCabinets = 0;
            var extinguisherCabinets = 0;
            var sideLights = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (transform.GridUid != map.Grid.Owner)
                    continue;

                switch (metadata.EntityPrototype?.ID)
                {
                    case "AU14DefibrillatorCabinet":
                        defibCabinets++;
                        var defib = slots.GetItemOrNull(uid, "ItemCabinet");
                        Assert.That(defib, Is.Not.Null);
                        Assert.That(SEntMan.GetComponent<MetaDataComponent>(defib!.Value).EntityPrototype?.ID, Is.EqualTo("AU14CMDefibrillator"));
                        var cell = slots.GetItemOrNull(defib.Value, "cell_slot");
                        Assert.That(cell, Is.Not.Null);
                        Assert.That(SEntMan.System<SharedBatterySystem>().GetCharge(cell!.Value), Is.GreaterThan(0));
                        Assert.That(transform.LocalRotation.GetCardinalDir(), Is.EqualTo(Direction.West));
                        break;
                    case "RMCExtinguisherCabinetAlt":
                        extinguisherCabinets++;
                        var extinguisher = slots.GetItemOrNull(uid, "ItemCabinet");
                        Assert.That(extinguisher, Is.Not.Null);
                        Assert.That(SEntMan.GetComponent<MetaDataComponent>(extinguisher!.Value).EntityPrototype?.ID, Is.EqualTo("CMFireExtinguisher"));
                        break;
                    case "RMCLightFixtureDoubleAlwaysPowered":
                        sideLights++;
                        Assert.That(lights.TryGetLight(uid, out var light), Is.True);
                        Assert.That(light!.Enabled, Is.True);
                        var emission = transforms.GetWorldPosition(uid) + transforms.GetWorldRotation(uid).RotateVec(light.Offset);
                        var tile = maps.CoordinatesToTile(map.Grid.Owner, map.Grid.Comp,
                            new MapCoordinates(emission, map.MapId));
                        Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, tile).Tile.IsEmpty, Is.False);
                        foreach (var anchored in maps.GetAnchoredEntities(map.Grid.Owner, map.Grid.Comp, tile))
                            Assert.That(tags.HasTag(anchored, WallTag), Is.False, "A light emitter inside a wall makes the cabin dark.");
                        break;
                }
            }

            Assert.That(defibCabinets, Is.EqualTo(1));
            Assert.That(extinguisherCabinets, Is.EqualTo(1));
            Assert.That(sideLights, Is.EqualTo(2));
        });
    }
}
