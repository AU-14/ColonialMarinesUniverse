using System.Linq;
using Content.Server._RMC14.Scorch;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.CMU14.CargoVehicle;
using Content.Shared.CMU14.util;
using Content.Shared.Containers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Storage.Components;
using Content.Shared.SSDIndicator;
using Content.Shared.Trigger.Components;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Crate;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Xenonids.Acid;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.CMU14.CargoVehicle;

[TestFixture]
public sealed class CMUCargoVehicleTest
{
    private const string CarrierId = "CMUCargoCarrier";
    private const string ControllerId = "CMUCargoCarrierController";
    private const string DeploymentCrateId = "CMUCrateCargoCarrier";
    private const string SuppliesCategory = "Supplies";

    private static readonly EntProtoId[] Catalogs =
    [
        "CMBCIUCargoCatalog",
        "HAZOPSCargoCatalog",
        "LACNCargoCatalog",
        "ProdigyCargoCatalog",
        "RMCCargoCatalog",
        "UPPCargoCatalog",
        "USCMCargoCatalog",
        "VAIPOCargoCatalog",
        "WYPMCCargoCatalog",
    ];

    [Test]
    public async Task PrototypeMatchesCargoCarrierDesign()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var carrier = prototypes.Index<EntityPrototype>(CarrierId);

            Assert.That(carrier.TryComp<CMUCargoVehicleComponent>(out var cargo, factory), Is.True);
            Assert.That(carrier.TryComp<VehicleComponent>(out var vehicle, factory), Is.True);
            Assert.That(carrier.TryComp<GridVehicleMoverComponent>(out var mover, factory), Is.True);
            Assert.That(carrier.TryComp<DamageableComponent>(out var damageable, factory), Is.True);
            Assert.That(carrier.TryComp<ExplosiveComponent>(out var explosive, factory), Is.True);
            Assert.That(carrier.TryComp<CMExplosionEffectComponent>(out var explosionEffect, factory), Is.True);
            Assert.That(carrier.TryComp<RMCScorchEffectComponent>(out _, factory), Is.True);
            Assert.That(carrier.TryComp<VehicleSoundComponent>(out var vehicleSound, factory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(cargo!.AutomaticArmDamage.Int(), Is.EqualTo(300));
                Assert.That(cargo.MaximumDamage.Int(), Is.EqualTo(400));
                Assert.That(cargo.CargoDelay, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(cargo.DetonationDelay, Is.EqualTo(5));
                Assert.That(cargo.BeepInterval, Is.EqualTo(1));
                Assert.That(((SoundPathSpecifier) cargo.BeepSound).Path,
                    Is.EqualTo(new ResPath("/Audio/_RMC14/Medical/reset_key_shortbeep.ogg")));
                Assert.That(((SoundPathSpecifier) cargo.RampSound).Path,
                    Is.EqualTo(new ResPath("/Audio/_RMC14/Machines/hydraulics_1.ogg")));
                Assert.That(cargo.DebrisPrototypes, Has.Count.EqualTo(4));
                Assert.That(cargo.FirePrototype.Id, Is.EqualTo("RMCTileFire"));
                Assert.That(cargo.FireRange, Is.EqualTo(1));
                Assert.That(cargo.FireDuration, Is.EqualTo(20));

                Assert.That(vehicle!.MovementKind, Is.EqualTo(VehicleMovementKind.Grid));
                Assert.That(vehicle.TransferDamage, Is.False);
                Assert.That(vehicle.RequiresHands, Is.False);

                Assert.That(mover!.MaxSpeed, Is.EqualTo(5));
                Assert.That(mover.MaxReverseSpeed, Is.EqualTo(3));
                Assert.That(mover.Acceleration, Is.EqualTo(4));
                Assert.That(mover.Deceleration, Is.EqualTo(10));
                Assert.That(mover.MaxRotationSpeedDegrees, Is.EqualTo(90));
                Assert.That(mover.TurnInPlace, Is.True);
                Assert.That(mover.CanSmashWalls, Is.False);
                Assert.That(mover.CanPushVehicles, Is.False);

                Assert.That(damageable!.DamageModifierSetId?.Id, Is.EqualTo("VehicleFrameCLFVan"));
                Assert.That(explosive!.ExplosionType.Id, Is.EqualTo("RMC"));
                Assert.That(explosive.TotalIntensity, Is.EqualTo(160));
                Assert.That(explosive.IntensitySlope, Is.EqualTo(6));
                Assert.That(explosive.MaxIntensity, Is.EqualTo(16));
                Assert.That(explosive.CanCreateVacuum, Is.False);
                Assert.That(explosionEffect!.Explosion?.Id, Is.EqualTo("CMExplosionEffectGrenade"));
                Assert.That(explosionEffect.ShockWave?.Id, Is.EqualTo("RMCExplosionEffectGrenadeShockWave"));
                Assert.That(explosionEffect.ShrapnelEffects, Has.Count.EqualTo(2));
                Assert.That(((SoundPathSpecifier) vehicleSound!.HornSound!).Path,
                    Is.EqualTo(new ResPath("/Audio/Effects/double_beep.ogg")));
            });

            Assert.That(carrier.Name, Is.EqualTo("Bison remote cargo carrier"));
            Assert.That(carrier.Description, Does.Contain("carry one crate"));

            Assert.That(carrier.TryComp<FixturesComponent>(out var fixtures, factory), Is.True);
            foreach (var fixtureName in new[] { "world", "mob" })
            {
                Assert.That(fixtures!.Fixtures[fixtureName].Shape, Is.TypeOf<PolygonShape>());
                var polygon = (PolygonShape) fixtures.Fixtures[fixtureName].Shape;
                var minX = polygon.Vertices.Min(vertex => vertex.X);
                var maxX = polygon.Vertices.Max(vertex => vertex.X);
                var minY = polygon.Vertices.Min(vertex => vertex.Y);
                var maxY = polygon.Vertices.Max(vertex => vertex.Y);
                Assert.That(maxX - minX, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(maxY - minY, Is.EqualTo(1.9f).Within(0.001f));
            }

            Assert.That(prototypes.TryIndex<EntityPrototype>(ControllerId, out var controller), Is.True);
            Assert.That(controller!.TryComp<CMUCargoVehicleControllerComponent>(out _, factory), Is.True);
            Assert.That(prototypes.TryIndex<EntityPrototype>("CMUCargoCarrierWreck", out var wreck), Is.True);
            Assert.That(wreck!.TryComp<ExplosionResistanceComponent>(out var resistance, factory), Is.True);
            Assert.That(resistance!.DamageCoefficient, Is.Zero);
            Assert.That(wreck.TryComp<PhysicsComponent>(out var wreckPhysics, factory), Is.True);
            Assert.That(wreckPhysics!.BodyType, Is.EqualTo(BodyType.Dynamic));
            Assert.That(wreck.TryComp<FixturesComponent>(out var wreckFixtures, factory), Is.True);
            Assert.That(wreckFixtures!.Fixtures["fix1"].Shape, Is.TypeOf<PolygonShape>());
            var wreckShape = (PolygonShape) wreckFixtures.Fixtures["fix1"].Shape;
            Assert.That(wreckShape.Vertices.Max(vertex => vertex.X) - wreckShape.Vertices.Min(vertex => vertex.X),
                Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(wreckShape.Vertices.Max(vertex => vertex.Y) - wreckShape.Vertices.Min(vertex => vertex.Y),
                Is.EqualTo(1.9f).Within(0.001f));
            Assert.That(wreck.TryComp<CorrodibleComponent>(out var corrodible, factory), Is.True);
            Assert.That(corrodible!.IsCorrodible, Is.True);
            Assert.That(corrodible.Structure, Is.True);
            Assert.That(wreck.TryComp<RMCWallExplosionDeletableComponent>(out _, factory), Is.True);

            foreach (var debris in cargo.DebrisPrototypes)
                Assert.That(prototypes.TryIndex<EntityPrototype>(debris, out _), Is.True, debris.Id);

            foreach (var action in new[]
                     {
                         "CMUActionCargoVehicleReturn",
                         "CMUActionCargoVehicleToggleBay",
                         "CMUActionCargoVehicleSelfDestruct",
                     })
            {
                Assert.That(prototypes.TryIndex<EntityPrototype>(action, out _), Is.True, action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AccessItemTraversalTerminatesForSelfOperatedCarrier()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var carrier = entities.SpawnEntity(CarrierId, MapCoordinates.Nullspace);
            var vehicle = entities.GetComponent<VehicleComponent>(carrier);

            Assert.That(vehicle.Operator, Is.EqualTo(carrier));

            var accessItems = entities.System<AccessReaderSystem>().FindPotentialAccessItems(carrier);
            Assert.That(accessItems, Does.Contain(carrier));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DirectionalRsiFramesHaveExpectedMappingsAndAlignment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var resources = pair.Server.ResolveDependency<IResourceManager>();

        var expected = new Dictionary<string, (int MinX, int MinY, int MaxX, int MaxY)[]>
        {
            ["cargo_closed"] =
            [
                (22, 17, 73, 81), // south: source frame 19
                (22, 12, 73, 75), // north: source frame 22
                (19, 25, 88, 69), // east: source frame 29
                (7, 25, 76, 69),  // west: source frame 24
            ],
            ["cargo_open"] =
            [
                (22, 13, 73, 81), // south: source frame 17
                (22, 12, 73, 83), // north: source frame 23
                (7, 25, 88, 69),  // east: source frame 26
                (7, 25, 88, 69),  // west: source frame 20
            ],
        };

        foreach (var (state, expectedBounds) in expected)
        {
            using var stream = resources.ContentFileRead(
                new ResPath($"/Textures/CMU14/Structures/vehicles/cargo_carrier.rsi/{state}.png"));
            using var image = Image.Load<Rgba32>(stream);
            Assert.That(image.Size, Is.EqualTo(new Size(192, 192)));

            for (var direction = 0; direction < 4; direction++)
                Assert.That(GetFrameBounds(image, direction), Is.EqualTo(expectedBounds[direction]), $"{state} direction {direction}");
        }

        var debrisBounds = new HashSet<(int MinX, int MinY, int MaxX, int MaxY)>();
        for (var index = 1; index <= 4; index++)
        {
            using var stream = resources.ContentFileRead(
                new ResPath($"/Textures/CMU14/Structures/vehicles/cargo_carrier.rsi/cargo_debris_{index}.png"));
            using var image = Image.Load<Rgba32>(stream);
            Assert.That(image.Size, Is.EqualTo(new Size(96, 96)));
            debrisBounds.Add(GetFrameBounds(image, 0));
        }

        Assert.That(debrisBounds, Has.Count.EqualTo(4));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeploymentCrateAndAllFactionCatalogsAreConfigured()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;
            var crate = prototypes.Index<EntityPrototype>(DeploymentCrateId);

            Assert.That(crate.TryComp<CrateOpenableComponent>(out var openable, factory), Is.True);
            Assert.That(openable!.Spawn, Has.Count.EqualTo(1));
            Assert.That(openable.Spawn[0].PrototypeId?.Id, Is.EqualTo(ControllerId));
            Assert.That(openable.Spawn[0].Amount, Is.EqualTo(1));
            Assert.That(crate.TryComp<SpawnOnTerminateComponent>(out var spawn, factory), Is.True);
            Assert.That(spawn!.Spawn.Id, Is.EqualTo(CarrierId));

            foreach (var catalogId in Catalogs)
            {
                var catalog = prototypes.Index<EntityPrototype>(catalogId);
                Assert.That(catalog.TryComp<RequisitionsComputerComponent>(out var requisitions, factory), Is.True);
                var supplies = requisitions!.Categories.Single(category => category.Name == SuppliesCategory);
                var entry = supplies.Entries.Single(entry => entry.Crate.Id == DeploymentCrateId);
                Assert.That(entry.Cost, Is.EqualTo(3000), catalogId.Id);
                Assert.That(entry.MaxStock, Is.EqualTo(2), catalogId.Id);
                Assert.That(entry.StockReplenishDelay, Is.EqualTo(TimeSpan.Zero), catalogId.Id);
            }

            var platoonCatalogs = new Dictionary<string, string>
            {
                ["CMBCIU"] = "CMBCIUCargoCatalog",
                ["HAZOPS"] = "HAZOPSCargoCatalog",
                ["LACN"] = "LACNCargoCatalog",
                ["ProdigySF"] = "ProdigyCargoCatalog",
                ["RMC"] = "RMCCargoCatalog",
                ["UPP"] = "UPPCargoCatalog",
                ["USCM"] = "USCMCargoCatalog",
                ["VAIPO"] = "VAIPOCargoCatalog",
                ["WEYU"] = "WYPMCCargoCatalog",
            };

            foreach (var (platoonId, catalogId) in platoonCatalogs)
                Assert.That(prototypes.Index<PlatoonPrototype>(platoonId).Reqlist, Is.EqualTo(catalogId), platoonId);
        });

        var carrierBefore = 0;
        var controllerBefore = 0;
        EntityUid deploymentCrate = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var asrs = entities.SpawnEntity("WYPMCCargoCatalog", map.GridCoords);
            var itemCatalog = entities.GetComponent<RequisitionsComputerComponent>(asrs).ItemCatalog;
            Assert.That(itemCatalog.Any(entry => entry.Prototype.Id == CarrierId), Is.True);
            Assert.That(itemCatalog.Any(entry => entry.Prototype.Id == ControllerId), Is.True);

            carrierBefore = CountPrototype(entities, CarrierId);
            controllerBefore = CountPrototype(entities, ControllerId);
            deploymentCrate = entities.SpawnEntity(DeploymentCrateId, map.GridCoords);
            var user = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var crowbar = entities.SpawnEntity("Crowbar", map.GridCoords);
            var interact = new InteractUsingEvent(user, crowbar, deploymentCrate, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(deploymentCrate, interact);
            Assert.That(interact.Handled, Is.True);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.Deleted(deploymentCrate), Is.True);
            Assert.That(CountPrototype(entities, CarrierId), Is.EqualTo(carrierBefore + 1));
            Assert.That(CountPrototype(entities, ControllerId), Is.EqualTo(controllerBefore + 1));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AutomaticAndManualArmingHaveDifferentMovementRulesAndCannotBeReversed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid automaticCarrier = default;
        EntityUid manualCarrier = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var damageable = entities.System<DamageableSystem>();
            automaticCarrier = entities.SpawnEntity(CarrierId, MapCoordinates.Nullspace);
            manualCarrier = entities.SpawnEntity(CarrierId, MapCoordinates.Nullspace);

            damageable.TryChangeDamage(
                automaticCarrier,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 300 } },
                ignoreResistances: true);

            entities.EnsureComponent<CMUCargoVehicleControlSessionComponent>(manualCarrier);
            var action = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var manualEvent = new CMUCargoVehicleSelfDestructActionEvent
            {
                Performer = manualCarrier,
                Action = action,
            };
            entities.EventBus.RaiseLocalEvent(manualCarrier, manualEvent);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var automatic = entities.GetComponent<CMUCargoVehicleComponent>(automaticCarrier);
            var manual = entities.GetComponent<CMUCargoVehicleComponent>(manualCarrier);

            Assert.That(automatic.ArmingMode, Is.EqualTo(CMUCargoVehicleArmingMode.Automatic));
            Assert.That(manual.ArmingMode, Is.EqualTo(CMUCargoVehicleArmingMode.Manual));
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(automaticCarrier), Is.True);
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(manualCarrier), Is.True);

            var automaticRun = new VehicleCanRunEvent((automaticCarrier, entities.GetComponent<VehicleComponent>(automaticCarrier)));
            entities.EventBus.RaiseLocalEvent(automaticCarrier, ref automaticRun);
            Assert.That(automaticRun.CanRun, Is.False);

            var manualRun = new VehicleCanRunEvent((manualCarrier, entities.GetComponent<VehicleComponent>(manualCarrier)));
            entities.EventBus.RaiseLocalEvent(manualCarrier, ref manualRun);
            Assert.That(manualRun.CanRun, Is.True);

            var damageable = entities.System<DamageableSystem>();
            damageable.TryChangeDamage(
                automaticCarrier,
                new DamageSpecifier { DamageDict = { ["Blunt"] = -300 } },
                ignoreResistances: true);
            Assert.That(automatic.ArmingMode, Is.EqualTo(CMUCargoVehicleArmingMode.Automatic));
            Assert.That(entities.HasComponent<ActiveTimerTriggerComponent>(automaticCarrier), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ControllerWorksAcrossMapsAndDroppingItReturnsTheMindToItsBody()
    {
        await using var pair = await PoolManager.GetServerClient();
        var firstMap = await pair.CreateTestMap();
        var secondMap = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid user = default;
        EntityUid controller = default;
        EntityUid carrier = default;
        EntityUid mindId = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var minds = entities.System<SharedMindSystem>();

            user = entities.SpawnEntity("CMMobHuman", firstMap.GridCoords);
            controller = entities.SpawnEntity(ControllerId, firstMap.GridCoords);
            carrier = entities.SpawnEntity(CarrierId, firstMap.GridCoords);
            Assert.That(hands.TryPickupAnyHand(user, controller, checkActionBlocker: false), Is.True);

            mindId = minds.CreateMind(null).Owner;
            minds.TransferTo(mindId, user);

            var pairEvent = new InteractUsingEvent(user, controller, carrier, entities.GetComponent<TransformComponent>(carrier).Coordinates);
            entities.EventBus.RaiseLocalEvent(carrier, pairEvent);
            Assert.That(pairEvent.Handled, Is.True);
            Assert.That(entities.GetComponent<CMUCargoVehicleControllerComponent>(controller).LinkedVehicle, Is.EqualTo(carrier));

            entities.System<SharedTransformSystem>().SetCoordinates(carrier, secondMap.GridCoords);
            var useEvent = new UseInHandEvent(user);
            entities.EventBus.RaiseLocalEvent(controller, useEvent);
            Assert.That(useEvent.Handled, Is.True);
        });

        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mind = entities.GetComponent<MindComponent>(mindId);
            Assert.That(mind.VisitingEntity, Is.EqualTo(carrier));
            Assert.That(entities.HasComponent<CMUCargoVehicleControlSessionComponent>(carrier), Is.True);
            Assert.That(entities.HasComponent<CMUCargoVehicleRemotePilotComponent>(user), Is.True);
            Assert.That(entities.HasComponent<SSDIndicatorComponent>(user), Is.False);

            var hands = entities.System<SharedHandsSystem>();
            Assert.That(hands.TryDrop(user, controller, checkActionBlocker: false), Is.True);
        });

        // GotUnequipped ends control immediately in normal play; allow the periodic
        // session validator to run as a second line of defense in the headless fixture.
        await server.WaitRunTicks(10);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<MindComponent>(mindId).VisitingEntity, Is.Null);
            Assert.That(entities.HasComponent<CMUCargoVehicleControlSessionComponent>(carrier), Is.False);
            Assert.That(entities.HasComponent<CMUCargoVehicleRemotePilotComponent>(user), Is.False);
            Assert.That(entities.TryGetComponent<SSDIndicatorComponent>(user, out var ssd), Is.True);
            Assert.That(ssd!.IsSSD, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExplosionScattersDirectContentsAndCreatesPersistentAftermath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid carrier = default;
        EntityUid crate = default;
        EntityUid item = default;
        var wrecksBefore = 0;
        var debrisBefore = new Dictionary<string, int>();
        var fireBefore = 0;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            carrier = entities.SpawnEntity(CarrierId, map.GridCoords);
            crate = entities.SpawnEntity("RMCCrateSupplyInternals", map.GridCoords);
            item = entities.SpawnEntity("Crowbar", map.GridCoords);

            var cargo = entities.GetComponent<CMUCargoVehicleComponent>(carrier);
            var storage = entities.GetComponent<EntityStorageComponent>(crate);
            Assert.That(containers.Insert(item, storage.Contents, force: true), Is.True);
            Assert.That(containers.Insert(crate, cargo.CargoContainer!, force: true), Is.True);

            wrecksBefore = CountPrototype(entities, "CMUCargoCarrierWreck");
            foreach (var debris in entities.GetComponent<CMUCargoVehicleComponent>(carrier).DebrisPrototypes)
                debrisBefore[debris.Id] = CountPrototype(entities, debris.Id);
            fireBefore = CountPrototype(entities, "RMCTileFire");

            // The RMC presentation component is configured by the prototype test.
            // Remove it here so this synthetic event isolates carrier aftermath;
            // the effect system expects to be entered through the trigger pipeline.
            entities.RemoveComponent<CMExplosionEffectComponent>(carrier);
            var explosion = new CMExplosiveTriggeredEvent();
            entities.EventBus.RaiseLocalEvent(carrier, ref explosion);
            entities.QueueDeleteEntity(carrier);

            Assert.That(storage.Contents.ContainedEntities, Is.Empty);
            Assert.That(cargo.CargoContainer!.ContainedEntity, Is.Null);
            Assert.That(entities.GetComponent<TransformComponent>(item).ParentUid, Is.Not.EqualTo(crate));
            Assert.That(CountPrototype(entities, "CMUCargoCarrierWreck"), Is.EqualTo(wrecksBefore + 1));
            var debrisSpawned = debrisBefore.Sum(pair => CountPrototype(entities, pair.Key) - pair.Value);
            Assert.That(debrisSpawned, Is.InRange(4, 7));
            foreach (var (prototype, before) in debrisBefore)
                Assert.That(CountPrototype(entities, prototype), Is.GreaterThan(before), prototype);
            Assert.That(CountPrototype(entities, "RMCTileFire"), Is.GreaterThan(fireBefore));
            Assert.That(entities.IsQueuedForDeletion(crate), Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static (int MinX, int MinY, int MaxX, int MaxY) GetFrameBounds(Image<Rgba32> image, int direction)
    {
        var cellX = direction % 2 * 96;
        var cellY = direction / 2 * 96;
        var minX = 96;
        var minY = 96;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < 96; y++)
        {
            for (var x = 0; x < 96; x++)
            {
                if (image[cellX + x, cellY + y].A == 0)
                    continue;

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }
        }

        return (minX, minY, maxX, maxY);
    }

    private static int CountPrototype(IEntityManager entities, string prototype)
    {
        var count = 0;
        var query = entities.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}
