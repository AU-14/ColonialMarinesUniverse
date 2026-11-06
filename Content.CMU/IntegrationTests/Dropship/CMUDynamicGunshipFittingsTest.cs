using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.CMU14.Atmos;
using Content.Shared.Atmos;
using Content.Shared.CMU14.Emplacements;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
        Entity<MapGridComponent> grid = default;
        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadMap(new ResPath("Maps/_RMC14/Shuttles/dynamic_gunship.yml"),
                out var loadedMap, out var grids, DeserializationOptions.Default with { InitializeMaps = true }), Is.True);
            Assert.That(grids, Has.Count.EqualTo(1));
            grid = grids!.Single();
            Assert.That(SEntMan.HasComponent<CMUBlockEmplacementDeploymentComponent>(grid), Is.True);
            // The ship template is saved paused; deployment normally moves it to a live map.
            SEntMan.System<SharedMapSystem>().SetPaused(loadedMap!.Value.Owner, false);
        });
        await Pair.RunTicksSync(60);
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
            var atmosphereFans = 0;
            var doors = 0;
            var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var metadata, out var transform))
            {
                if (transform.GridUid != grid.Owner)
                    continue;

                switch (metadata.EntityPrototype?.ID)
                {
                    case "RMCTinyFan":
                        atmosphereFans++;
                        var initializer = SEntMan.GetComponent<CMUGunshipDoorAtmosphereComponent>(uid);
                        Assert.That(maps.IsInitialized(transform.MapID), Is.True);
                        var indices = maps.CoordinatesToTile(grid.Owner, grid.Comp, transform.Coordinates);
                        Assert.That(SEntMan.System<AtmosphereSystem>().HasTileAtmosphere(grid.Owner, indices),
                            Is.True, "Each mapped doorway must have an atmosphere tile.");
                        var air = SEntMan.System<AtmosphereSystem>().GetTileMixture((uid, transform));
                        Assert.That(air, Is.Not.Null);
                        Assert.That(initializer.Initialized, Is.True);
                        Assert.That(air!.Pressure, Is.EqualTo(Atmospherics.OneAtmosphere).Within(1));
                        Assert.That(air.GetMoles(Gas.Oxygen), Is.GreaterThan(0));
                        break;
                    case "CMNormandyDoorAft":
                    case "CMNormandyDoorSinglePort":
                    case "CMNormandyDoorSingleStarboard":
                        doors++;
                        Assert.That(SEntMan.GetComponent<AirtightComponent>(uid).NoAirWhenFullyAirBlocked, Is.False);
                        break;
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
                        var tile = maps.CoordinatesToTile(grid.Owner, grid.Comp,
                            new MapCoordinates(emission, transform.MapID));
                        Assert.That(maps.GetTileRef(grid.Owner, grid.Comp, tile).Tile.IsEmpty, Is.False);
                        foreach (var anchored in maps.GetAnchoredEntities(grid.Owner, grid.Comp, tile))
                            Assert.That(tags.HasTag(anchored, WallTag), Is.False, "A light emitter inside a wall makes the cabin dark.");
                        break;
                }
            }

            Assert.That(defibCabinets, Is.EqualTo(1));
            Assert.That(extinguisherCabinets, Is.EqualTo(1));
            Assert.That(sideLights, Is.EqualTo(2));
            Assert.That(atmosphereFans, Is.EqualTo(5));
            Assert.That(doors, Is.EqualTo(3));
        });
    }
}
