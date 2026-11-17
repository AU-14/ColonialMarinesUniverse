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
