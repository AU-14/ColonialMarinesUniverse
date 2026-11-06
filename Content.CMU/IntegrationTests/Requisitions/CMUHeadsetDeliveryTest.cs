#pragma warning disable RA0002 // Drive the real delivery queue and lift transition without waiting for travel.
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Inventory;
using Content.Shared.Radio.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Requisitions;

[TestFixture]
public sealed class CMUHeadsetDeliveryTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task DeliveredColonyHeadsetsKeepTheirInstalledRadioKeys()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var elevator = SEntMan.SpawnEntity("CMCargoElevator", map.GridCoords);
            var lift = SEntMan.GetComponent<RequisitionsElevatorComponent>(elevator);
            lift.Faction = "colony";
            lift.RoundStartFreeCrateGiven = true;
            lift.Orders.Add(new RequisitionsEntry { Crate = "CMUCrateColonyHeadsets" });
            lift.Mode = RequisitionsElevatorMode.Raising;
            lift.NextMode = null;
            lift.Busy = true;
            lift.RaiseDelay = TimeSpan.Zero;
            lift.LowerDelay = TimeSpan.Zero;
            lift.ToggleDelay = TimeSpan.FromHours(1);
            lift.ToggledAt = Server.ResolveDependency<IGameTiming>().CurTime - TimeSpan.FromSeconds(1);
            var requisitions = Server.System<RequisitionsSystem>();
            for (var tick = 0; tick < 100 && lift.Mode != RequisitionsElevatorMode.Raised; tick++)
                requisitions.Update(0f);
            Assert.That(lift.Mode, Is.EqualTo(RequisitionsElevatorMode.Raised));
            Assert.That(lift.Orders, Is.Empty);

            var crates = SEntMan.EntityQueryEnumerator<EntityStorageComponent, MetaDataComponent, TransformComponent>();
            EntityStorageComponent? delivered = null;
            while (crates.MoveNext(out var storage, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID == "CMUCrateColonyHeadsets" && transform.MapID == map.MapId)
                    delivered = storage;
            }
            Assert.That(delivered, Is.Not.Null, "The real requisitions transition must publish its prepared crate.");
            var contents = delivered!.Contents.ContainedEntities.ToArray();
            Assert.That(contents, Is.Not.Empty);
            var containers = Server.System<SharedContainerSystem>();
            var inventory = Server.System<InventorySystem>();
            foreach (var headset in contents)
            {
                Assert.That(containers.Remove(headset, delivered.Contents), Is.True);
                var wearer = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                Assert.That(inventory.TryEquip(wearer, headset, "ears", force: true), Is.True);
                Assert.That(SEntMan.GetComponent<EncryptionKeyHolderComponent>(headset).Channels.Select(channel => channel.Id),
                    Does.Contain("Colony"), "A nested key must initialize while its shipment is prepared off-map.");
                Assert.That(SEntMan.GetComponent<ActiveRadioComponent>(headset).Channels.Select(channel => channel.Id),
                    Does.Contain("Colony"), "The delivered headset must actually receive its keyed colony channel when equipped.");
            }
        });
    }
}
