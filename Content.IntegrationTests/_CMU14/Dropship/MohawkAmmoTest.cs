using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Fabricator;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Throwing;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.DoAfter;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using System.Reflection;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;

#pragma warning disable RA0002 // Arrange the mounted ammo pipeline and inspect its consumed payload.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkAmmoTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [Test]
    public async Task DualFeedLoadsAlternatesFallsBackAndUnloadsBothCrates()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid).AttachmentPoints.Clear();
            SEntMan.EnsureComponent<ShipFactionComponent>(map.Grid).Faction = "GOVFOR";
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            SEntMan.GetComponent<DropshipComponent>(map.Grid).AttachmentPoints.Add(point);
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            var weapon = containers.GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
            var firstSlot = containers.GetContainer(point, mount.AmmoContainerSlotId);
            var secondSlot = containers.GetContainer(point, mount.SecondAmmoContainerSlotId!);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            EntityUid Load(string prototype)
            {
                var crate = SEntMan.SpawnEntity(prototype, map.GridCoords);
                SEntMan.GetComponent<PowerLoaderAttachableComponent>(crate).AttachDelay = TimeSpan.Zero;
                var interact = new BeforeRangedInteractEvent(loader, crate, point, map.GridCoords, true);
                SEntMan.EventBus.RaiseLocalEvent(crate, interact);
                return crate;
            }
            var first = Load("CMUMohawkM90Ammo");
            var second = Load("CMUMohawkM90AmmoIncendiary");
            Assert.That(firstSlot.Contains(first), Is.True);
            Assert.That(secondSlot.Contains(second), Is.True);
            var extra = Load("CMUMohawkM90AmmoScatter");
            Assert.That(firstSlot.Contains(extra) || secondSlot.Contains(extra), Is.False,
                "A full dual-feed mount must reject a third box.");
            var count = new GetAmmoCountEvent();
            SEntMan.EventBus.RaiseLocalEvent(weapon, ref count);
            Assert.That(count.Count, Is.EqualTo(800));
            Assert.That(count.Capacity, Is.EqualTo(800));
            Assert.That(Server.System<SharedSentryTargetingSystem>().TryApplyDefaultFaction(point, "GOVFOR"), Is.True);
            var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
            chin.Calibrated = true;
            chin.IffConfigured = true;
            EntityUid Consume()
            {
                var take = new TakeAmmoEvent(1, new(), map.GridCoords, null);
                SEntMan.EventBus.RaiseLocalEvent(weapon, take);
                Assert.That(take.Ammo, Has.Count.EqualTo(1));
                return take.Ammo.Single().Entity!.Value;
            }
            string Prototype(EntityUid entity) => SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype!.ID;
            // Ember is airborne-only and alternation skips feeds the gun cannot fire, so alternate in flight.
            SEntMan.EnsureComponent<Content.Shared.Shuttles.Components.FTLComponent>(map.Grid).State =
                Content.Shared.Shuttles.Systems.FTLState.Travelling;
            SEntMan.EnsureComponent<DropshipInFlyByComponent>(map.Grid);
            chin.AlternateFeeds = true;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90HEAP"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(1));
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90Incendiary"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(0));
            chin.AlternateFeeds = false;
            chin.ActiveFeed = 1;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90Incendiary"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(1));
            SEntMan.GetComponent<DropshipAmmoComponent>(second).Rounds = 0;
            Assert.That(Prototype(Consume()), Is.EqualTo("CMUMohawkM90HEAP"));
            Assert.That(chin.ActiveFeed, Is.EqualTo(0));

            for (var i = 0; i < 10; i++)
                Consume();
            var casings = SEntMan.QueryEntities<CMUSpentCasingComponent>().ToArray();
            Assert.That(casings, Has.Length.EqualTo(14));
            foreach (var casing in casings)
            {
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(casing), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(casing).LinearVelocity.Length(), Is.InRange(2f, 5f));
            }
            Assert.That(casings.Select(casing => SEntMan.GetComponent<PhysicsComponent>(casing).LinearVelocity).Distinct().Count(),
                Is.GreaterThan(1), "Ejection direction and speed must vary between shells.");

            void Unload(EntityUid crate, string slot)
            {
                var detach = new DropshipDetachDoAfterEvent(SEntMan.GetNetEntity(point), SEntMan.GetNetEntity(crate), slot);
                detach.DoAfter = new DoAfter(0, new DoAfterArgs(SEntMan, loader, TimeSpan.Zero, detach, point, point), TimeSpan.Zero);
                SEntMan.EventBus.RaiseLocalEvent(point, detach);
                Assert.That(containers.GetContainer(point, slot).Contains(crate), Is.False);
                Assert.That(containers.GetContainer(point, mount.WeaponContainerSlotId).Contains(weapon), Is.True);
            }
            Unload(first, mount.AmmoContainerSlotId);
            Unload(second, mount.SecondAmmoContainerSlotId!);
        });
    }

    [Test]
    public async Task ThirdFeedIsReservedForSquall()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid).AttachmentPoints.Clear();
            var point = SEntMan.SpawnEntity("CMUMohawkM90Point", map.GridCoords);
            var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = Server.System<SharedContainerSystem>();
            var first = containers.GetContainer(point, mount.AmmoContainerSlotId);
            var second = containers.GetContainer(point, mount.SecondAmmoContainerSlotId!);
            var third = containers.GetContainer(point, mount.ThirdAmmoContainerSlotId!);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            EntityUid Load(string prototype)
            {
                var crate = SEntMan.SpawnEntity(prototype, map.GridCoords);
                SEntMan.GetComponent<PowerLoaderAttachableComponent>(crate).AttachDelay = TimeSpan.Zero;
                SEntMan.EventBus.RaiseLocalEvent(crate, new BeforeRangedInteractEvent(loader, crate, point, map.GridCoords, true));
                return crate;
            }

            var squall = Load("CMUMohawkM90AmmoKinetic");
            Assert.That(third.Contains(squall), Is.True, "Squall fills its dedicated feed first.");
            var heap = Load("CMUMohawkM90Ammo");
            var ember = Load("CMUMohawkM90AmmoIncendiary");
            Assert.That(first.Contains(heap), Is.True);
            Assert.That(second.Contains(ember), Is.True);
            var gale = Load("CMUMohawkM90AmmoScatter");
            Assert.That(third.Contains(gale), Is.False, "Only Squall may use the third feed.");
            Assert.That(first.Contains(gale) || second.Contains(gale), Is.False);
            Assert.That(mount.ThirdSlotAccepts(SEntMan.GetComponent<DropshipAmmoComponent>(squall).AmmoType), Is.True);
            Assert.That(mount.ThirdSlotAccepts(SEntMan.GetComponent<DropshipAmmoComponent>(heap).AmmoType), Is.False);
        });
    }

}
