using System.Linq;
using Content.Shared.CMU14.SceneAnimation;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared._RMC14.Item;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.SceneAnimation;

[TestFixture]
public sealed class CMUSceneAnimationPrototypeTest
{
    private static readonly HashSet<string> RequisitionsRifleIds =
    [
        "RMCWeaponRifleM54CMK1Unloaded",
        "RMCWeaponRifleM54C",
        "WeaponRifleM4SPR",
    ];

    private static readonly string[] SpawnerIds =
    [
        "CMUSceneVehicleSpawner",
        "CMUSceneHumveeSpawner",
        "CMUSceneAPCSpawner",
        "CMUSceneTruckSpawner",
        "CMUSceneTankSpawner",
        "CMUSceneVehiclePropSpawner",
        "CMUSceneMarineSpawner",
    ];

    [Test]
    public async Task SceneAnimationMarkersHaveSafeGroups()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();

            foreach (var id in SpawnerIds)
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(id, out var prototype), Is.True, id);
                Assert.That(prototype!.TryComp<CMUSceneSpawnMarkerComponent>(out var marker, factory), Is.True, id);
                Assert.That(marker!.SpawnGroups, Is.Not.Empty, id);
                var expectedMinInterval = id == "CMUSceneVehiclePropSpawner" ? 0.5 : 3.0;
                var expectedMaxInterval = id == "CMUSceneVehiclePropSpawner" ? 1.5 : 10.0;
                Assert.That(marker.MinSpawnInterval, Is.EqualTo(TimeSpan.FromSeconds(expectedMinInterval)), id);
                Assert.That(marker.MaxSpawnInterval, Is.EqualTo(TimeSpan.FromSeconds(expectedMaxInterval)), id);
                Assert.That(marker.SpawnGroups.SelectMany(group => group.Spawns).Any(spawn => spawn.Id == "VehicleAev"),
                    Is.False,
                    id);
            }

            var all = GetSpawner(prototypes, factory, "CMUSceneVehicleSpawner");
            Assert.That(all.SpawnGroups, Has.Count.EqualTo(4));

            var humvees = all.SpawnGroups.Single(group => group.Spawns.Any(spawn => spawn.Id == "VehicleHumvee"));
            Assert.Multiple(() =>
            {
                Assert.That(humvees.MinSize, Is.EqualTo(2));
                Assert.That(humvees.MaxSize, Is.EqualTo(5));
                Assert.That(humvees.MixPrototypes, Is.True);
            });

            var apcs = all.SpawnGroups.Single(group => group.Apc);
            Assert.Multiple(() =>
            {
                Assert.That(apcs.MinSize, Is.EqualTo(2));
                Assert.That(apcs.MaxSize, Is.EqualTo(4));
                Assert.That(apcs.MemberClearance, Is.GreaterThanOrEqualTo(7));
            });

            var trucks = all.SpawnGroups.Single(group => group.Spawns.Any(spawn => spawn.Id == "AU14VehicleCivTruck"));
            Assert.That(trucks.Spawns.Select(spawn => spawn.Id), Is.EquivalentTo(new[]
            {
                "AU14VehicleCivArmoredTruck",
                "AU14VehicleCivTruck",
                "AU14VehicleCivVan",
            }));

            var tanks = all.SpawnGroups.Single(group => group.Tank);
            Assert.Multiple(() =>
            {
                Assert.That(tanks.MinSize, Is.EqualTo(2));
                Assert.That(tanks.MaxSize, Is.EqualTo(4));
                Assert.That(tanks.MixPrototypes, Is.False);
            });

            var props = GetSpawner(prototypes, factory, "CMUSceneVehiclePropSpawner").SpawnGroups.Single();
            Assert.Multiple(() =>
            {
                Assert.That(props.Spawns, Has.Count.EqualTo(49));
                Assert.That(props.Spawns.Any(spawn => spawn.Id.StartsWith("RMCPropTyrargoVehicle")), Is.False);
                Assert.That(props.Spawns.Any(spawn =>
                    spawn.Id.Contains("Destroyed") || spawn.Id.Contains("Damaged") || spawn.Id.Contains("Broken")),
                    Is.False);
                Assert.That(props.Spawns.Select(spawn => spawn.Id), Does.Contain("RMCPropVehicleMonoSupronCop"));
                Assert.That(props.Spawns.Select(spawn => spawn.Id), Does.Contain("RMCPropVehicleMonoSupronTaxi"));
                Assert.That(props.Spawns.Select(spawn => spawn.Id), Does.Contain("RMCPropVehicleAmbulance"));
                Assert.That(props.Spawns.Select(spawn => spawn.Id), Does.Contain("RMCPropVehicleBoxVanPizza"));
                Assert.That(props.Spawns.Select(spawn => spawn.Id), Does.Contain("RMCPropVehicleLongTruckBlue"));
                Assert.That(props.MinSize, Is.EqualTo(1));
                Assert.That(props.MaxSize, Is.EqualTo(1));
                Assert.That(props.HorizontalOnly, Is.True);
                Assert.That(props.DirectMoveSpeed, Is.EqualTo(20.1168f).Within(0.001f));
                Assert.That(GetSpawner(prototypes, factory, "CMUSceneVehiclePropSpawner").WaitForGroupClear, Is.False);
            });

            Assert.That(prototypes.TryIndex<EntityPrototype>("CMUSceneDeleteMarker", out var deleteMarker), Is.True);
            Assert.That(deleteMarker!.TryComp<CMUSceneDeleteMarkerComponent>(out _, factory), Is.True);
            Assert.That(prototypes.TryIndex<EntityPrototype>("CMUGroundTextDecalTool", out var textTool), Is.True);
            Assert.That(textTool!.TryComp<CMUGroundTextDecalToolComponent>(out _, factory), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SceneVehiclePropsMoveEastAndColdMarinesWearWinterGear()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid propMarker = default;
        EntityUid marineMarker = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            propMarker = entities.SpawnEntity("CMUSceneVehiclePropSpawner", map.GridCoords);
            marineMarker = entities.SpawnEntity("CMUSceneMarineSpawner", map.GridCoords.Offset(new(0, 8)));

            var propSpawner = entities.GetComponent<CMUSceneSpawnMarkerComponent>(propMarker);
            propSpawner.SpawnGroups =
            [
                new CMUSceneSpawnGroup
                {
                    Spawns = ["RMCPropVehicleMonoSupronBlue"],
                    MinSize = 1,
                    MaxSize = 1,
                    DirectMoveSpeed = 20.1168f,
                    HorizontalOnly = true,
                    MaxTravelDistance = 200f,
                    MemberClearance = 0,
                },
            ];
            propSpawner.MinSpawnInterval = TimeSpan.Zero;
            propSpawner.MaxSpawnInterval = TimeSpan.Zero;
            propSpawner.NextSpawnAt = TimeSpan.Zero;

            ConfigureSingleSpawn(
                entities.GetComponent<CMUSceneSpawnMarkerComponent>(marineMarker),
                "CMUSceneMarine",
                marine: true);
        });

        await pair.RunTicksSync(5);

        EntityUid prop = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var activeProps = entities.GetComponent<CMUSceneSpawnMarkerComponent>(propMarker).ActiveGroupMembers;
            Assert.That(activeProps.Count, Is.GreaterThanOrEqualTo(2),
                "continuous traffic should spawn at 0.5-1.5 second intervals");
            prop = activeProps
                .OrderBy(uid => entities.System<SharedTransformSystem>().GetMapCoordinates(uid).Position.X)
                .First();
            var mover = entities.GetComponent<CMUSceneMoverComponent>(prop);
            var position = entities.System<SharedTransformSystem>().GetMapCoordinates(prop);
            Assert.Multiple(() =>
            {
                Assert.That(mover.Direction, Is.EqualTo(new Vector2i(1, 0)));
                Assert.That(PrototypeId(entities, prop), Is.EqualTo("RMCPropVehicleMonoSupronBlueFlipped"));
                Assert.That(position.Position.X, Is.GreaterThan(mover.StartPosition.X));
                Assert.That(position.Position.Y, Is.EqualTo(mover.StartPosition.Y).Within(0.01f));
            });

            var marine = entities.GetComponent<CMUSceneSpawnMarkerComponent>(marineMarker).ActiveGroupMembers.Single();
            var marineMover = entities.GetComponent<CMUSceneMoverComponent>(marine);
            var marineInput = entities.GetComponent<InputMoverComponent>(marine);
            var marinePosition = entities.System<SharedTransformSystem>().GetMapCoordinates(marine);
            var inventory = entities.System<InventorySystem>();
            Assert.Multiple(() =>
            {
                Assert.That((marineInput.HeldMoveButtons & MoveButtons.Walk) != 0, Is.True);
                Assert.That((marineInput.HeldMoveButtons & MoveButtons.AnyDirection) != 0, Is.True);
                Assert.That((marinePosition.Position - marineMover.StartPosition).LengthSquared(), Is.GreaterThan(0.001f));
                Assert.That(GetMarineRiflePrototype(entities, marine), Is.AnyOf(RequisitionsRifleIds.ToArray()));
            });
            Assert.That(inventory.TryGetSlotEntity(marine, "gloves", out var gloves), Is.True);
            Assert.That(PrototypeId(entities, gloves), Is.EqualTo("AU14PVEHandsInsulatedBlackMarine"));
            Assert.That(inventory.TryGetSlotEntity(marine, "mask", out var mask), Is.True);
            Assert.That(ColdWeatherMaskIds, Does.Contain(PrototypeId(entities, mask)));
            Assert.That(inventory.TryGetSlotEntity(marine, "jumpsuit", out var fatigues), Is.True);
            Assert.That(entities.GetComponent<ItemCamouflageComponent>(fatigues!.Value).CamouflageOverride,
                Is.EqualTo(CamouflageType.Snow));
        });

        await server.WaitPost(() => server.EntMan.SpawnEntity(
            "CMUSceneDeleteMarker",
            map.GridCoords.Offset(new(12, 0))));
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var active = server.EntMan.GetComponent<CMUSceneSpawnMarkerComponent>(propMarker).ActiveGroupMembers;
            Assert.That(active.Contains(prop), Is.False);
            Assert.That(server.EntMan.Deleted(prop), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static readonly string[] ColdWeatherMaskIds =
    [
        "CMMaskCoif",
        "RMCMaskScarfBlack",
        "RMCMaskScarfGray",
        "RMCMaskScarfTan",
        "RMCMaskTornScarfBlack",
        "RMCMaskTornScarfSnow",
        "RMCMaskSmockBlack",
    ];

    [Test]
    public async Task SceneVehicleSpawnInstallsApcAndTruckWheels()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid apcMarker = default;
        EntityUid truckMarker = default;
        EntityUid tankMarker = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            apcMarker = entities.SpawnEntity("CMUSceneAPCSpawner", map.GridCoords);
            truckMarker = entities.SpawnEntity("CMUSceneTruckSpawner", map.GridCoords.Offset(new(8, 0)));
            tankMarker = entities.SpawnEntity("CMUSceneTankSpawner", map.GridCoords.Offset(new(16, 0)));

            ConfigureSingleSpawn(entities.GetComponent<CMUSceneSpawnMarkerComponent>(apcMarker), "VehicleAPC", apc: true);
            ConfigureSingleSpawn(entities.GetComponent<CMUSceneSpawnMarkerComponent>(truckMarker), "AU14VehicleCivTruck");
            var tankSpawner = entities.GetComponent<CMUSceneSpawnMarkerComponent>(tankMarker);
            ConfigureSingleSpawn(tankSpawner, "VehicleTank", tank: true);
            tankSpawner.TankRiderChance = 1;
        });

        await pair.RunTicksSync(4);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var slots = server.System<ItemSlotsSystem>();
            var apc = entities.GetComponent<CMUSceneSpawnMarkerComponent>(apcMarker).ActiveGroupMembers.Single();
            var truck = entities.GetComponent<CMUSceneSpawnMarkerComponent>(truckMarker).ActiveGroupMembers.Single();
            var tank = entities.GetComponent<CMUSceneSpawnMarkerComponent>(tankMarker).ActiveGroupMembers.Single();

            Assert.That(PrototypeId(entities, slots.GetItemOrNull(apc, "wheel-1")), Is.EqualTo("RMCAPCWheel"));
            Assert.That(PrototypeId(entities, slots.GetItemOrNull(truck, "wheel-1")), Is.EqualTo("RMCVanWheel"));

            var primary = PrototypeId(entities, slots.GetItemOrNull(apc, "primary"));
            var support = PrototypeId(entities, slots.GetItemOrNull(apc, "support"));
            Assert.That(primary == "RMCAPCDualCannon" || support == "RMCAPCFlareLauncher", Is.True);

            var riders = entities.GetComponent<CMUSceneMoverComponent>(tank).LinkedEntities;
            Assert.That(riders.Count, Is.InRange(2, 4));
            foreach (var rider in riders)
            {
                Assert.That(entities.HasComponent<CMUSceneTankRiderComponent>(rider), Is.True);
                Assert.That(GetMarineRiflePrototype(entities, rider), Is.AnyOf(RequisitionsRifleIds.ToArray()));
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void ConfigureSingleSpawn(
        CMUSceneSpawnMarkerComponent marker,
        EntProtoId prototype,
        bool apc = false,
        bool marine = false,
        bool tank = false)
    {
        marker.SpawnGroups =
        [
            new CMUSceneSpawnGroup
            {
                Spawns = [prototype],
                MinSize = 1,
                MaxSize = 1,
                Apc = apc,
                Marine = marine,
                Tank = tank,
                MemberClearance = 0,
            },
        ];
        marker.MinSpawnInterval = TimeSpan.Zero;
        marker.MaxSpawnInterval = TimeSpan.Zero;
        marker.NextSpawnAt = TimeSpan.Zero;
    }

    private static string PrototypeId(IEntityManager entities, EntityUid? uid)
    {
        if (uid is not { } entity)
            return null;

        return entities.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID;
    }

    private static string? GetMarineRiflePrototype(IEntityManager entities, EntityUid marine)
    {
        var inventory = entities.System<InventorySystem>();
        if (inventory.TryGetSlotEntity(marine, "suitstorage", out var stored) &&
            RequisitionsRifleIds.Contains(PrototypeId(entities, stored)))
        {
            return PrototypeId(entities, stored);
        }

        var hands = entities.System<SharedHandsSystem>();
        foreach (var held in hands.EnumerateHeld(marine))
        {
            var prototype = PrototypeId(entities, held);
            if (RequisitionsRifleIds.Contains(prototype))
                return prototype;
        }

        return null;
    }

    private static CMUSceneSpawnMarkerComponent GetSpawner(
        IPrototypeManager prototypes,
        IComponentFactory factory,
        string id)
    {
        var prototype = prototypes.Index<EntityPrototype>(id);
        Assert.That(prototype.TryComp<CMUSceneSpawnMarkerComponent>(out var marker, factory), Is.True, id);
        return marker!;
    }
}
