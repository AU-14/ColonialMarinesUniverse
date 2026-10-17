using Stopwatch = System.Diagnostics.Stopwatch;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Storage.Components;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class ASRSDeliveryBudgetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: CMUASRSShipmentCrate
          id: CMUBudgetFlareCrate
          components:
          - type: StorageFill
            contents:
            - id: CMPackFlare
              amount: 40
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task BulkDeliverySpreadsSpawnsAndPublishesCompleteCrates(bool legacy)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
            var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            component.RoundStartFreeCrateGiven = true;
            component.Orders.Add(new RequisitionsEntry
            {
                Crate = legacy ? "CMUBudgetFlareCrate" : "CMUASRSShipmentCrate",
                Entities = legacy ? [] : Enumerable.Repeat<Robust.Shared.Prototypes.EntProtoId>("CMPackFlare", 40).ToList(),
            });
            component.Mode = RequisitionsElevatorMode.Raising;
            component.NextMode = null;
            component.Busy = true;
            component.RaiseDelay = TimeSpan.Zero;
            component.LowerDelay = TimeSpan.Zero;
            component.ToggledAt = Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(1);
            var system = Server.System<RequisitionsSystem>();
            system.Update(0f);
            Assert.That(component.Mode, Is.EqualTo(RequisitionsElevatorMode.Raising),
                "A forty-pack shipment must not be materialized in one gameplay tick.");
            Assert.That(component.Orders, Has.Count.EqualTo(1));

            var steps = 1;
            var longest = 0d;
            while (component.Mode != RequisitionsElevatorMode.Raised && steps++ < 200)
            {
                var start = Stopwatch.GetTimestamp();
                system.Update(0f);
                longest = Math.Max(longest, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
            }
            TestContext.Progress.WriteLine($"Delivery legacy={legacy} updates={steps} longestAfterFirstMs={longest:F3}");
            Assert.That(component.Mode, Is.EqualTo(RequisitionsElevatorMode.Raised));
            Assert.That(component.Orders, Is.Empty);
            var crates = SEntMan.EntityQueryEnumerator<MetaDataComponent, EntityStorageComponent, TransformComponent>();
            var delivered = 0;
            while (crates.MoveNext(out _, out var metadata, out var storage, out var transform))
            {
                if (metadata.EntityPrototype?.ID != (legacy ? "CMUBudgetFlareCrate" : "CMUASRSShipmentCrate"))
                    continue;
                Assert.That(transform.MapID, Is.EqualTo(map.MapId));
                Assert.That(storage.Contents.ContainedEntities, Has.Count.EqualTo(40));
                foreach (var pack in storage.Contents.ContainedEntities)
                {
                    var slots = SEntMan.GetComponent<ItemSlotsComponent>(pack);
                    Assert.That(slots.Slots.Values.Count(slot => slot.ContainerSlot?.ContainedEntity != null), Is.EqualTo(8));
                }
                delivered++;
            }
            Assert.That(delivered, Is.EqualTo(1));
        });
    }
    [Test]
    public async Task RemovingElevatorCleansUpCargoPreparedOffMap()
    {
        var map = await Pair.CreateTestMap();
        EntityUid[] roots = [];
        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
            var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            component.RoundStartFreeCrateGiven = true;
            component.Mode = RequisitionsElevatorMode.Preparing;
            component.NextMode = RequisitionsElevatorMode.Raising;
            component.RaiseDelay = TimeSpan.FromHours(1);
            component.ToggledAt = Server.ResolveDependency<IGameTiming>().CurTime;
            component.Orders.Add(new RequisitionsEntry { Crate = "CMUBudgetFlareCrate" });
            Server.System<RequisitionsSystem>().Update(0f);
            roots = SEntMan.GetComponent<RequisitionsDeliveryComponent>(elevator).Roots.ToArray();
            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(SEntMan.GetComponent<TransformComponent>(roots[0]).MapID, Is.EqualTo(Robust.Shared.Map.MapId.Nullspace));
            Assert.That(component.Mode, Is.EqualTo(RequisitionsElevatorMode.Preparing));
            SEntMan.DeleteEntity(elevator);
        });
        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            foreach (var root in roots)
                Assert.That(SEntMan.EntityExists(root), Is.False);
        });
    }
}
