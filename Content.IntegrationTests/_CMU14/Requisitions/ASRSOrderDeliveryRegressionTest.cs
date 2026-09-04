#pragma warning disable RA0002 // Integration regression intentionally drives authoritative requisitions state.

using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
[TestOf(typeof(RequisitionsSystem))]
public sealed class ASRSOrderDeliveryRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: RMCCrateBase
          id: ASRSOrderDeliveryLegacyCrate

        - type: entity
          parent: RMCCrateBase
          id: ASRSOrderDeliveryItemizedCrate

        - type: entity
          parent: BaseItem
          id: ASRSOrderDeliveryPurchasedItem

        - type: entity
          parent: RMCCrateBase
          id: ASRSOrderDeliveryLegacyBundle
          components:
          - type: StorageFill
            contents:
            - id: ASRSOrderDeliveryPurchasedItem
              amount: 4

        - type: entity
          id: ASRSOrderDeliveryComputer
          components:
          - type: RequisitionsComputer
            faction: asrs-order-delivery-test
            itemShipmentCrate: ASRSOrderDeliveryItemizedCrate
            itemOverrides:
            - prototype: ASRSOrderDeliveryPurchasedItem
              cost: 100
              weight: 1
            categories:
            - name: Test
              entries:
              - cost: 400
                crate: ASRSOrderDeliveryLegacyBundle

        - type: entity
          parent: CMCargoElevator
          id: ASRSOrderDeliveryElevator
          components:
          - type: RequisitionsElevator
            mode: Lowered
            faction: asrs-order-delivery-test
            radius: 2
        """;

    [Test]
    public async Task DelayedLowerRemovesLegacyShipmentBeforeDeliveringFollowingItemizedOrder()
    {
        var map = await Pair.CreateTestMap();
        NetEntity elevatorNet = default;

        await Server.WaitPost(() =>
        {
            var elevator = SEntMan.SpawnEntity("ASRSOrderDeliveryElevator", map.GridCoords);
            var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            component.RoundStartFreeCrateGiven = true;
            for (var i = 0; i < 4; i++)
                component.Orders.Add(new RequisitionsEntry { Crate = "ASRSOrderDeliveryLegacyCrate" });

            SEntMan.Dirty(elevator, component);
            elevatorNet = SEntMan.GetNetEntity(elevator);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var elevator = CEntMan.GetEntity(elevatorNet);
            Assert.That(CEntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Orders, Has.Count.EqualTo(4));
        });

        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.GetEntity(elevatorNet);
            CompleteRaise(elevator);
            Assert.That(SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Orders, Is.Empty);
            Assert.That(CountPrototype("ASRSOrderDeliveryLegacyCrate"), Is.EqualTo(4));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var elevator = CEntMan.GetEntity(elevatorNet);
            Assert.That(CEntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Orders, Is.Empty,
                "the delivered legacy queue must not remain on the client");
        });

        await Server.WaitPost(() => BeginLowerForSell(SEntMan.GetEntity(elevatorNet)));
        await Server.WaitRunTicks(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(CountPrototype("ASRSOrderDeliveryLegacyCrate"), Is.Zero,
                "legacy crates sent down on the platform must be removed before the next shipment");
        });

        await Server.WaitPost(() =>
        {
            var elevator = SEntMan.GetEntity(elevatorNet);
            var computer = SEntMan.SpawnEntity("ASRSOrderDeliveryComputer", map.GridCoords);
            var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            var requisitions = SEntMan.GetComponent<RequisitionsComputerComponent>(computer);
            Assert.That(requisitions.Account, Is.Not.Null);
            var account = SEntMan.GetComponent<RequisitionsAccountComponent>(requisitions.Account!.Value);
            account.Balance = 1000;

            var actor = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(computer,
                new RequisitionsCheckoutMsg(1,
                    [new RequisitionsCheckoutLine("ASRSOrderDeliveryPurchasedItem", 1)])
                {
                    Actor = actor,
                    UiKey = RequisitionsUIKey.Key,
                });

            Assert.Multiple(() =>
            {
                Assert.That(component.Orders, Has.Count.EqualTo(1),
                    "the itemized checkout must replace the already delivered legacy queue");
                Assert.That(component.Orders[0].Crate.Id, Is.EqualTo("ASRSOrderDeliveryItemizedCrate"));
                Assert.That(component.Orders[0].Entities.Select(id => id.Id),
                    Is.EqualTo(new[] { "ASRSOrderDeliveryPurchasedItem" }));
            });
        });
        await Pair.RunUntilSynced();

        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.GetEntity(elevatorNet);
            CompleteRaise(elevator);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Orders, Is.Empty);
                Assert.That(CountPrototype("ASRSOrderDeliveryLegacyCrate"), Is.Zero,
                    "the previous legacy shipment must not return after being sent down");
                Assert.That(CountPrototype("ASRSOrderDeliveryItemizedCrate"), Is.EqualTo(1),
                    "the newly queued itemized shipment must be delivered");
                Assert.That(CountPrototype("ASRSOrderDeliveryPurchasedItem"), Is.EqualTo(1),
                    "the purchased item must be packed into the delivered shipment");
            });
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var elevator = CEntMan.GetEntity(elevatorNet);
            Assert.That(CEntMan.GetComponent<RequisitionsElevatorComponent>(elevator).Orders, Is.Empty);
        });
    }

    private void CompleteRaise(EntityUid elevator)
    {
        var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
        var timing = Server.ResolveDependency<IGameTiming>();
        component.Mode = RequisitionsElevatorMode.Raising;
        component.NextMode = null;
        component.Busy = true;
        component.ToggledAt = timing.CurTime - TimeSpan.FromSeconds(1);
        component.ToggleDelay = TimeSpan.FromHours(1);
        component.LowerDelay = TimeSpan.Zero;
        component.RaiseDelay = TimeSpan.Zero;
        Server.System<RequisitionsSystem>().Update(0f);
    }

    private void BeginLowerForSell(EntityUid elevator)
    {
        var component = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
        var timing = Server.ResolveDependency<IGameTiming>();
        component.Mode = RequisitionsElevatorMode.Lowering;
        component.NextMode = null;
        component.Busy = true;
        // Reproduce a delayed update that crosses both the sell and movement-complete thresholds.
        component.ToggledAt = timing.CurTime - TimeSpan.FromSeconds(3);
        component.ToggleDelay = TimeSpan.FromHours(1);
        component.LowerDelay = TimeSpan.FromSeconds(1);
        component.RaiseDelay = TimeSpan.Zero;
        Server.System<RequisitionsSystem>().Update(0f);
    }

    private int CountPrototype(string prototype)
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out _, out var metadata))
        {
            if (metadata.EntityPrototype?.ID == prototype)
                count++;
        }

        return count;
    }
}

#pragma warning restore RA0002
