using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedGunSystem))]
public sealed class MagazineAutoEjectTest : GameTest
{
    [Test]
    public async Task EmptyMagazineDropsInsteadOfEnteringFreeHand()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var hands = Server.System<HandsSystem>();
            var slots = Server.System<ItemSlotsSystem>();
            var containers = Server.System<SharedContainerSystem>();
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var gun = SEntMan.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
            var magazine = slots.GetItemOrNull(gun, SharedGunSystem.MagazineSlot);

            Assert.That(magazine, Is.Not.Null);

            var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
            var takeAmmo = new TakeAmmoEvent(100, ammo, map.GridCoords, user);
            SEntMan.EventBus.RaiseLocalEvent(gun, takeAmmo);

            Assert.Multiple(() =>
            {
                Assert.That(slots.GetItemOrNull(gun, SharedGunSystem.MagazineSlot), Is.Null,
                    "an empty auto-ejecting magazine must leave the gun");
                Assert.That(hands.IsHolding(user, magazine!.Value), Is.False,
                    "an empty auto-ejecting magazine must not enter a free hand");
                Assert.That(containers.IsEntityInContainer(magazine.Value), Is.False,
                    "an empty auto-ejecting magazine must land loose on the floor");
            });
        });
    }

    [Test]
    public async Task EjectDestinationVerbSwitchesEmptyMagazineToHand()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var hands = Server.System<HandsSystem>();
            var slots = Server.System<ItemSlotsSystem>();
            var user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var gun = SEntMan.SpawnEntity("CMWeaponPistolM1984", map.GridCoords);
            var provider = SEntMan.GetComponent<MagazineAmmoProviderComponent>(gun);
            var magazine = slots.GetItemOrNull(gun, SharedGunSystem.MagazineSlot);

            Assert.That(magazine, Is.Not.Null);

            GetDestinationVerb(user, gun, "cmu-gun-magazine-auto-eject-to-hand").Act!();
            Assert.That(provider.EjectToHand, Is.True);

            GetDestinationVerb(user, gun, "cmu-gun-magazine-auto-eject-to-ground").Act!();
            Assert.That(provider.EjectToHand, Is.False);

            GetDestinationVerb(user, gun, "cmu-gun-magazine-auto-eject-to-hand").Act!();

            var ammo = new List<(EntityUid? Entity, IShootable Shootable)>();
            var takeAmmo = new TakeAmmoEvent(100, ammo, map.GridCoords, user);
            SEntMan.EventBus.RaiseLocalEvent(gun, takeAmmo);

            Assert.Multiple(() =>
            {
                Assert.That(slots.GetItemOrNull(gun, SharedGunSystem.MagazineSlot), Is.Null,
                    "an empty auto-ejecting magazine must leave the gun");
                Assert.That(hands.IsHolding(user, magazine!.Value), Is.True,
                    "hand ejection mode must put the empty magazine in a free hand");
            });
        });
    }

    private AlternativeVerb GetDestinationVerb(EntityUid user, EntityUid gun, string locId)
    {
        var verbs = new GetVerbsEvent<AlternativeVerb>(
            user,
            gun,
            null,
            null,
            true,
            true,
            true,
            []);
        SEntMan.EventBus.RaiseLocalEvent(gun, verbs);
        return verbs.Verbs.Single(verb => verb.Text == Loc.GetString(locId));
    }
}
