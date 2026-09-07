using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Kitchen.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._RMC14.Chemistry.SmartFridge;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Kitchen;
using Content.Shared.Kitchen.Components;
using Content.Shared.Storage;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Kitchen;

[TestFixture]
[TestOf(typeof(ReagentGrinderSystem))]
public sealed class ReagentGrinderWorkflowTest : GameTest
{
    private const string GrinderPrototype = "ReagentGrinderWorkflowTestGrinder";
    private const string BeakerPrototype = "ReagentGrinderWorkflowTestBeaker";
    private const string ExtractablePrototype = "ReagentGrinderWorkflowTestExtractable";
    private const string UnextractablePrototype = "ReagentGrinderWorkflowTestUnextractable";
    private const string FridgePrototype = "RMCSmartFridge";
    private const string PlantBagPrototype = "RMCStoragePlantBag";
    private const string UserPrototype = "MobObserver";
    private const string GrindablePrototype = ExtractablePrototype;
    private const string FridgeContainerId = "rmc_smart_fridge";
    private const string BottleSolution = "drink";
    private static readonly ProtoId<ReagentPrototype> Water = "Water";
    private static readonly ProtoId<ReagentPrototype> Sugar = "Sugar";
    private static readonly ReagentQuantity WaterSelection = new("Water", FixedPoint2.Zero);

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: RMCKitchenReagentGrinder
  id: {GrinderPrototype}
  components:
  - type: ReagentGrinder
    storageMaxEntities: 2
    linkDistance: 5
    linkLimit: 8
  - type: ApcPowerReceiver
    needsPower: false

- type: entity
  parent: BaseItem
  id: {BeakerPrototype}
  components:
  - type: SolutionContainerManager
    solutions:
      beaker:
        maxVol: 200
  - type: FitsInDispenser
    solution: beaker

- type: entity
  parent: BaseItem
  id: {ExtractablePrototype}
  components:
  - type: Produce
  - type: Extractable
    grindableSolutionName: food
  - type: SolutionContainerManager
    solutions:
      food:
        maxVol: 10
        reagents:
        - ReagentId: Water
          Quantity: 1

- type: entity
  parent: BaseItem
  id: {UnextractablePrototype}
  components:
  - type: Produce
";

    [Test]
    public async Task LinksNearestSameMapReplicatesAndInvalidates()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid grinder = default;
        EntityUid nearest = default;
        EntityUid farther = default;
        EntityUid otherMapFridge = default;

        await server.WaitPost(() =>
        {
            grinder = server.EntMan.SpawnEntity(GrinderPrototype, map.GridCoords);
            nearest = server.EntMan.SpawnEntity(FridgePrototype, map.GridCoords.Offset(new Vector2(2, 0)));
            farther = server.EntMan.SpawnEntity(FridgePrototype, map.GridCoords.Offset(new Vector2(4, 0)));

            var mapSystem = server.EntMan.System<SharedMapSystem>();
            mapSystem.CreateMap(out var otherMap);
            otherMapFridge = server.EntMan.SpawnEntity(
                FridgePrototype,
                new MapCoordinates(new Vector2(0.5f, 0), otherMap));

            var receiver = server.EntMan.GetComponent<ApcPowerReceiverComponent>(grinder);
            receiver.NeedsPower = true;
            receiver.Powered = false;
            var link = new ReagentGrinderLinkMessage();
            server.EntMan.EventBus.RaiseLocalEvent(grinder, link);
            Assert.That(server.EntMan.GetComponent<ReagentGrinderComponent>(grinder).SmartFridge, Is.Null,
                "an unpowered grinder must reject a crafted link message");

            receiver.NeedsPower = false;
            receiver.Powered = true;
            server.EntMan.Dirty(grinder, receiver);
            server.EntMan.EventBus.RaiseLocalEvent(grinder, link);
        });
        await Pair.RunTicksSync(3);

        await AssertLinkedTo(grinder, nearest);
        Assert.That(nearest, Is.Not.EqualTo(otherMapFridge), "a closer fridge on another map must be ignored");

        await server.WaitPost(() =>
        {
            var transform = server.EntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(nearest, map.GridCoords.Offset(new Vector2(20, 0)));
        });
        await Pair.RunTicksSync(2);
        await AssertUnlinked(grinder);

        await server.WaitPost(() =>
        {
            var link = new ReagentGrinderLinkMessage();
            server.EntMan.EventBus.RaiseLocalEvent(grinder, link);
        });
        await Pair.RunTicksSync(2);
        await AssertLinkedTo(grinder, farther);

        await server.WaitPost(() => server.EntMan.DeleteEntity(farther));
        await Pair.RunTicksSync(2);
        await AssertUnlinked(grinder);
    }

    [Test]
    public async Task PlantBagTransferHonorsCapacityAndExtractableFilter()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid grinder = default;
        EntityUid plantBag = default;
        EntityUid user = default;
        EntityUid unextractable = default;
        var extractables = new List<EntityUid>();
        InteractUsingEvent? interaction = null;

        await server.WaitPost(() =>
        {
            grinder = server.EntMan.SpawnEntity(GrinderPrototype, map.GridCoords);
            plantBag = server.EntMan.SpawnEntity(PlantBagPrototype, map.GridCoords);
            user = server.EntMan.SpawnEntity(UserPrototype, map.GridCoords);
            var containers = server.EntMan.System<SharedContainerSystem>();
            var bag = server.EntMan.GetComponent<StorageComponent>(plantBag);

            for (var i = 0; i < 3; i++)
            {
                var item = server.EntMan.SpawnEntity(ExtractablePrototype, map.GridCoords);
                Assert.That(containers.Insert(item, bag.Container, force: true), Is.True);
                extractables.Add(item);
            }

            unextractable = server.EntMan.SpawnEntity(UnextractablePrototype, map.GridCoords);
            Assert.That(containers.Insert(unextractable, bag.Container, force: true), Is.True);

            interaction = new InteractUsingEvent(
                user,
                plantBag,
                grinder,
                server.EntMan.GetComponent<TransformComponent>(grinder).Coordinates);
            server.EntMan.EventBus.RaiseLocalEvent(grinder, interaction);
        });

        await server.WaitAssertion(() =>
        {
            var grinderComp = server.EntMan.GetComponent<ReagentGrinderComponent>(grinder);
            var bag = server.EntMan.GetComponent<StorageComponent>(plantBag);
            Assert.Multiple(() =>
            {
                Assert.That(interaction, Is.Not.Null);
                Assert.That(interaction!.Handled, Is.True,
                    "the plant-bag pre-handler must consume the interaction before the base insertion handler");
                Assert.That(grinderComp.InputContainer.ContainedEntities, Has.Count.EqualTo(2));
                Assert.That(grinderComp.InputContainer.ContainedEntities.All(server.EntMan.HasComponent<ExtractableComponent>),
                    Is.True);
                Assert.That(bag.Container.ContainedEntities, Has.Count.EqualTo(2));
                Assert.That(bag.Container.ContainedEntities, Does.Contain(unextractable));
                Assert.That(bag.Container.ContainedEntities.Count(extractables.Contains), Is.EqualTo(1));
                Assert.That(grinderComp.InputContainer.ContainedEntities, Does.Not.Contain(plantBag),
                    "the base interaction handler must not also insert the plant bag");
            });
        });
    }

    [Test]
    public async Task BottleDisposeAndRejectedActionsConserveReagents()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        EntityUid grinder = default;
        EntityUid fridge = default;
        EntityUid beaker = default;
        EntityUid unlinkedGrinder = default;
        EntityUid unlinkedBeaker = default;

        await server.WaitPost(() =>
        {
            var rejectInsert = server.EntMan.System<ReagentGrinderTestRejectInsertSystem>();
            rejectInsert.Clear();

            grinder = server.EntMan.SpawnEntity(GrinderPrototype, map.GridCoords);
            fridge = server.EntMan.SpawnEntity(FridgePrototype, map.GridCoords.Offset(new Vector2(2, 0)));
            beaker = InsertBeaker(server.EntMan, grinder, map.GridCoords);
            AddReagent(server.EntMan, beaker, Water, FixedPoint2.New(130));
            Raise(server.EntMan, grinder, new ReagentGrinderLinkMessage());
            Raise(server.EntMan, grinder, new ReagentGrinderBottleMessage(WaterSelection));
        });

        await server.WaitAssertion(() =>
        {
            var bottles = GetFridgeContents(server.EntMan, fridge);
            Assert.Multiple(() =>
            {
                Assert.That(bottles, Has.Count.EqualTo(3));
                Assert.That(SumReagent(server.EntMan, bottles, Water), Is.EqualTo(FixedPoint2.New(130)));
                Assert.That(GetReagent(server.EntMan, beaker, Water), Is.EqualTo(FixedPoint2.Zero));
            });
        });

        await server.WaitPost(() =>
        {
            AddReagent(server.EntMan, beaker, Water, FixedPoint2.New(70));
            server.EntMan.System<ReagentGrinderTestRejectInsertSystem>().Reject(fridge, true);
            Raise(server.EntMan, grinder, new ReagentGrinderBottleMessage(WaterSelection));
        });
        await Pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetFridgeContents(server.EntMan, fridge), Has.Count.EqualTo(3));
                Assert.That(GetReagent(server.EntMan, beaker, Water), Is.EqualTo(FixedPoint2.New(70)),
                    "a bottle that cannot be inserted must not consume source reagent");
            });
        });

        await server.WaitPost(() =>
        {
            server.EntMan.System<ReagentGrinderTestRejectInsertSystem>().Reject(fridge, false);
            AddReagent(server.EntMan, beaker, Sugar, FixedPoint2.New(20));
            Raise(server.EntMan, grinder, new ReagentGrinderDisposeMessage(WaterSelection));

            unlinkedGrinder = server.EntMan.SpawnEntity(GrinderPrototype, map.GridCoords.Offset(new Vector2(20, 0)));
            unlinkedBeaker = InsertBeaker(server.EntMan, unlinkedGrinder, map.GridCoords.Offset(new Vector2(20, 0)));
            AddReagent(server.EntMan, unlinkedBeaker, Water, FixedPoint2.New(10));
            Raise(server.EntMan, unlinkedGrinder, new ReagentGrinderBottleMessage(WaterSelection));
            Raise(server.EntMan, unlinkedGrinder, new ReagentGrinderDisposeMessage(WaterSelection));
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetReagent(server.EntMan, beaker, Water), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(GetReagent(server.EntMan, beaker, Sugar), Is.EqualTo(FixedPoint2.New(20)),
                    "disposing the selected reagent must preserve other reagents");
                Assert.That(GetReagent(server.EntMan, unlinkedBeaker, Water), Is.EqualTo(FixedPoint2.New(10)),
                    "crafted bottle and dispose messages must be rejected while unlinked");
            });
        });

        await server.WaitPost(() =>
        {
            AddReagent(server.EntMan, beaker, Water, FixedPoint2.New(30));
            var grinderComp = server.EntMan.GetComponent<ReagentGrinderComponent>(grinder);
            var grindable = server.EntMan.SpawnEntity(GrindablePrototype, map.GridCoords);
            var containers = server.EntMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(grindable, grinderComp.InputContainer), Is.True);
            Raise(server.EntMan, grinder, new ReagentGrinderStartMessage(GrinderProgram.Grind));
            Assert.That(grinderComp.EndTime, Is.Not.Null);

            Raise(server.EntMan, grinder, new ReagentGrinderBottleMessage(WaterSelection));
            Raise(server.EntMan, grinder, new ReagentGrinderDisposeMessage(WaterSelection));
        });

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(GetFridgeContents(server.EntMan, fridge), Has.Count.EqualTo(3));
                Assert.That(GetReagent(server.EntMan, beaker, Water), Is.EqualTo(FixedPoint2.New(30)),
                    "crafted bottle and dispose messages must be rejected while active");
            });
        });
    }

    private async Task AssertLinkedTo(EntityUid grinder, EntityUid fridge)
    {
        await Pair.Server.WaitAssertion(() =>
        {
            var component = Pair.Server.EntMan.GetComponent<ReagentGrinderComponent>(grinder);
            Assert.That(component.SmartFridge, Is.EqualTo(fridge));
        });

        var clientGrinder = Pair.ToClientUid(grinder);
        var clientFridge = Pair.ToClientUid(fridge);
        await Pair.Client.WaitAssertion(() =>
        {
            var component = Pair.Client.EntMan.GetComponent<ReagentGrinderComponent>(clientGrinder);
            Assert.That(component.SmartFridge, Is.EqualTo(clientFridge),
                "the networked fridge reference drives the client link UI");
        });
    }

    private async Task AssertUnlinked(EntityUid grinder)
    {
        await Pair.Server.WaitAssertion(() =>
        {
            var component = Pair.Server.EntMan.GetComponent<ReagentGrinderComponent>(grinder);
            Assert.That(component.SmartFridge, Is.Null);
        });

        var clientGrinder = Pair.ToClientUid(grinder);
        await Pair.Client.WaitAssertion(() =>
        {
            var component = Pair.Client.EntMan.GetComponent<ReagentGrinderComponent>(clientGrinder);
            Assert.That(component.SmartFridge, Is.Null);
        });
    }

    private static EntityUid InsertBeaker(IEntityManager entMan, EntityUid grinder, EntityCoordinates coordinates)
    {
        var beaker = entMan.SpawnEntity(BeakerPrototype, coordinates);
        var containers = entMan.System<SharedContainerSystem>();
        var slot = containers.EnsureContainer<ContainerSlot>(grinder, ReagentGrinderComponent.BeakerSlotId);
        Assert.That(containers.Insert(beaker, slot), Is.True);
        return beaker;
    }

    private static void AddReagent(
        IEntityManager entMan,
        EntityUid beaker,
        ProtoId<ReagentPrototype> reagent,
        FixedPoint2 quantity)
    {
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetFitsInDispenser(beaker, out var solutionEntity, out _), Is.True);
        Assert.That(solutions.TryAddReagent(solutionEntity.Value, reagent, quantity), Is.True);
    }

    private static FixedPoint2 GetReagent(
        IEntityManager entMan,
        EntityUid beaker,
        ProtoId<ReagentPrototype> reagent)
    {
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        Assert.That(solutions.TryGetFitsInDispenser(beaker, out _, out var solution), Is.True);
        return solution.GetReagentQuantity(new ReagentId(reagent, null));
    }

    private static IReadOnlyList<EntityUid> GetFridgeContents(IEntityManager entMan, EntityUid fridge)
    {
        var containers = entMan.System<SharedContainerSystem>();
        var container = containers.EnsureContainer<Container>(fridge, FridgeContainerId);
        return container.ContainedEntities.ToList();
    }

    private static FixedPoint2 SumReagent(
        IEntityManager entMan,
        IEnumerable<EntityUid> bottles,
        ProtoId<ReagentPrototype> reagent)
    {
        var solutions = entMan.System<SharedSolutionContainerSystem>();
        var total = FixedPoint2.Zero;
        foreach (var bottle in bottles)
        {
            Assert.That(solutions.TryGetSolution(bottle, BottleSolution, out _, out var solution), Is.True);
            total += solution.GetReagentQuantity(new ReagentId(reagent, null));
        }

        return total;
    }

    private static void Raise<T>(IEntityManager entMan, EntityUid grinder, T message)
        where T : notnull
    {
        entMan.EventBus.RaiseLocalEvent(grinder, message);
    }
}

public sealed partial class ReagentGrinderTestRejectInsertSystem : EntitySystem
{
    private readonly HashSet<EntityUid> _rejected = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCSmartFridgeComponent, ContainerIsInsertingAttemptEvent>(OnInsertAttempt);
    }

    public void Reject(EntityUid fridge, bool reject)
    {
        if (reject)
            _rejected.Add(fridge);
        else
            _rejected.Remove(fridge);
    }

    public void Clear()
    {
        _rejected.Clear();
    }

    private void OnInsertAttempt(Entity<RMCSmartFridgeComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (_rejected.Contains(ent.Owner))
            args.Cancel();
    }
}
