using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

#pragma warning disable RA0002 // Arrange actual loader-held cargo and its attach delay.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkDualFeedTransferTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task BothHeldBoxesTransferPhysicallyAndDisappearOnClient(bool reverse)
    {
        var map = await Pair.CreateTestMap();
        EntityUid loader = default;
        EntityUid point = default;
        EntityUid first = default;
        EntityUid second = default;
        NetEntity pointNet = default;
        NetEntity firstNet = default;
        NetEntity secondNet = default;
        await Server.WaitAssertion(() =>
        {
            point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords.Offset(new Vector2(1, 0)));
            var pilot = SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(loader).Coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, pilot);
            Server.System<SkillsSystem>().SetSkill(pilot, "RMCSkillPowerLoader", 1);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(pilot, null, loader, popup: false), Is.True);
            first = SEntMan.SpawnEntity(reverse ? "CMUMohawkM90AmmoIncendiary" : "CMUMohawkM90Ammo", map.GridCoords);
            second = SEntMan.SpawnEntity(reverse ? "CMUMohawkM90Ammo" : "CMUMohawkM90AmmoIncendiary", map.GridCoords);
            var hands = Server.System<SharedHandsSystem>();
            hands.DoPickup(loader, "Right Hand", first);
            hands.DoPickup(loader, "Left Hand", second);
            Assert.That(hands.IsHolding(loader, first) && hands.IsHolding(loader, second), Is.True);
            Server.System<PowerLoaderSystem>().TrySyncHands(loader);
            pointNet = SEntMan.GetNetEntity(point);
            firstNet = SEntMan.GetNetEntity(first);
            secondNet = SEntMan.GetNetEntity(second);
        });
        foreach (var crate in new[] { first, second })
        {
            await Server.WaitAssertion(() =>
            {
                SEntMan.GetComponent<PowerLoaderAttachableComponent>(crate).AttachDelay = TimeSpan.FromSeconds(1);
                var interaction = new BeforeRangedInteractEvent(loader, crate, point, map.GridCoords, true);
                SEntMan.EventBus.RaiseLocalEvent(crate, interaction);
                Assert.That(interaction.Handled, Is.True);
            });
            await Pair.RunSeconds(2);
            await Server.WaitAssertion(() =>
            {
                var containers = Server.System<SharedContainerSystem>();
                Assert.That(containers.TryGetContainingContainer(crate, out var actual), Is.True);
                Assert.That(actual!.Owner, Is.EqualTo(point));
                Assert.That(SEntMan.GetComponent<TransformComponent>(crate).ParentUid, Is.EqualTo(point));
                Assert.That(SEntMan.GetComponent<TransformComponent>(crate).LocalPosition, Is.EqualTo(Vector2.Zero));
                Assert.That(Server.System<SharedHandsSystem>().IsHolding(loader, crate), Is.False);
                Assert.That(SEntMan.QueryEntities<PowerLoaderVirtualItemComponent>().Any(e => e.Comp.Grabbed == crate), Is.False,
                    "Cargo visuals must not remain in either pilot hand after insertion.");
            });
        }
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var mount = CEntMan.GetComponent<DropshipWeaponPointComponent>(CEntMan.GetEntity(pointNet));
            var containers = Client.System<SharedContainerSystem>();
            foreach (var (crateNet, slotId) in new[] { (firstNet, mount.AmmoContainerSlotId), (secondNet, mount.SecondAmmoContainerSlotId!) })
            {
                var crate = CEntMan.GetEntity(crateNet);
                var slot = containers.GetContainer(CEntMan.GetEntity(pointNet), slotId);
                Assert.That(slot.Contains(crate), Is.True);
                Assert.That(containers.TryGetContainingContainer(crate, out var actual) && actual == slot, Is.True);
                Assert.That(CEntMan.GetComponent<TransformComponent>(crate).ParentUid, Is.EqualTo(slot.Owner));
                Assert.That(slot.ShowContents, Is.False);
                Assert.That(CEntMan.GetComponent<SpriteComponent>(crate).ContainerOccluded, Is.True,
                    "Both inserted boxes must be hidden, not phantom boxes still on the floor.");
            }
        });
    }
}
