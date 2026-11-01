using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.ThirdParty;

/// <summary>
///     Dutch's Dozen exist to hunt Yautja, so the two things that make them work are pinned here: their EMP grenade
///     breaks a cloak and empties a bracer from far off, and Arnold's mask shows up on the hunters' gear tracker.
/// </summary>
[TestFixture]
public sealed class DutchsDozenTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase("CMUDutchsDozen")]
    [TestCase("CMUDutchsDozenFull")]
    public async Task PartiesAreAdminOnly(string party)
    {
        await Server.WaitAssertion(() =>
        {
            var proto = SProtoMan.Index<ThirdPartyPrototype>(party);
            Assert.That(proto.MaxPlayers, Is.LessThan(0),
                "cmss13 gives Dutch's Dozen probability = 0, so it must never come up in an automatic roll");
        });
    }

    [Test]
    public async Task ConcoctionDecloaksAndDrainsAYautjaFarAway()
    {
        EntityUid map = default;
        EntityUid hunter = default;
        EntityUid bracer = default;
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            hunter = SEntMan.SpawnEntity("CMMobHuman", new MapCoordinates(Vector2.Zero, mapId));
            bracer = SEntMan.SpawnEntity("CMUYautjaBracer", MapCoordinates.Nullspace);
            SEntMan.EnsureComponent<YautjaComponent>(hunter);
            SEntMan.EnsureComponent<EntityActiveInvisibleComponent>(hunter);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true));
            SEntMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;

            // well past any normal EMP, inside cmss13's 15 tile heavy range
            var grenade = SEntMan.SpawnEntity("CMUGrenadeEMPDutch", new MapCoordinates(new Vector2(12, 0), mapId));
            SEntMan.System<TriggerSystem>().Trigger(grenade, key: "trigger");
        });

        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<YautjaBracerComponent>(bracer).Charge, Is.EqualTo((FixedPoint2) 500));
                Assert.That(SEntMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.False);
            });
            SEntMan.DeleteEntity(map);
        });
    }

    [Test]
    public async Task ArnoldsMaskIsOnTheGearTracker()
    {
        EntityUid map = default;
        EntityUid arnold = default;
        await Server.WaitAssertion(() =>
        {
            map = SEntMan.System<SharedMapSystem>().CreateMap(out var mapId, runMapInit: true);
            arnold = SEntMan.SpawnEntity("CMUMobDutchsDozenArnold", new MapCoordinates(Vector2.Zero, mapId));
        });

        await RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var inventory = SEntMan.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(arnold, "mask", out var mask), Is.True);
            Assert.That(SEntMan.HasComponent<YautjaTrackedItemComponent>(mask), Is.True,
                "the Yautja track Dutch by the mask on his face");
            Assert.That(inventory.TryGetSlotEntity(arnold, "pocket1", out var pouch), Is.True);
            var grenades = SEntMan.System<SharedContainerSystem>().GetContainer(pouch.Value, StorageComponent.ContainerId)
                .ContainedEntities
                .Count(e => SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "CMUGrenadeEMPDutch");
            Assert.That(grenades, Is.EqualTo(3));

            SEntMan.DeleteEntity(map);
        });
    }
}
