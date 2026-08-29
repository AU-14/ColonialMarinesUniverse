using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedWieldableSystem))]
public sealed class WieldableUseOrderingMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          parent: WeaponPistolCHIMP
          id: WieldableMergeBatteryGun
          components:
          - type: Wieldable
            unwieldOnUse: false
        """;

    [Test]
    public async Task FirstUseWieldsOrHandlesFailureBeforeGunUseHandlers()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var hands = Server.System<HandsSystem>();
            var chamberUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var batteryUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var blockedUser = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            var chamberGun = SEntMan.SpawnEntity("WeaponLightMachineGunL6", map.GridCoords);
            var batteryGun = SEntMan.SpawnEntity("WieldableMergeBatteryGun", map.GridCoords);
            var blockedChamberGun = SEntMan.SpawnEntity("WeaponLightMachineGunL6", map.GridCoords);
            var blockedBatteryGun = SEntMan.SpawnEntity("WieldableMergeBatteryGun", map.GridCoords);

            try
            {
                Assert.That(hands.TryPickupAnyHand(chamberUser, chamberGun), Is.True);
                var chamberBefore = ChamberSnapshot(chamberGun);
                var chamberUse = new UseInHandEvent(chamberUser);
                SEntMan.EventBus.RaiseLocalEvent(chamberGun, chamberUse);
                Assert.Multiple(() =>
                {
                    Assert.That(chamberUse.Handled, Is.True);
                    Assert.That(SEntMan.GetComponent<WieldableComponent>(chamberGun).Wielded, Is.True);
                    Assert.That(ChamberSnapshot(chamberGun), Is.EqualTo(chamberBefore),
                        "the first use must wield without changing bolt, chambered cartridge, or ammo count");
                });

                Assert.That(hands.TryPickupAnyHand(batteryUser, batteryGun), Is.True);
                var batteryBefore = BatterySnapshot(batteryGun);
                var batteryUse = new UseInHandEvent(batteryUser);
                SEntMan.EventBus.RaiseLocalEvent(batteryGun, batteryUse);
                Assert.Multiple(() =>
                {
                    Assert.That(batteryUse.Handled, Is.True);
                    Assert.That(SEntMan.GetComponent<WieldableComponent>(batteryGun).Wielded, Is.True);
                    Assert.That(BatterySnapshot(batteryGun), Is.EqualTo(batteryBefore),
                        "the first use must not cycle a multi-mode battery weapon or rewrite its projectile/cost");
                });

                SEntMan.EnsureComponent<WieldingBlockerComponent>(blockedUser);
                Assert.That(hands.TryPickupAnyHand(blockedUser, blockedChamberGun), Is.True);
                var blockedChamberBefore = ChamberSnapshot(blockedChamberGun);
                var blockedChamberUse = new UseInHandEvent(blockedUser);
                SEntMan.EventBus.RaiseLocalEvent(blockedChamberGun, blockedChamberUse);
                Assert.Multiple(() =>
                {
                    Assert.That(blockedChamberUse.Handled, Is.True,
                        "an unwielded use is consumed even when WieldAttemptEvent is cancelled");
                    Assert.That(SEntMan.GetComponent<WieldableComponent>(blockedChamberGun).Wielded, Is.False);
                    Assert.That(ChamberSnapshot(blockedChamberGun), Is.EqualTo(blockedChamberBefore),
                        "blocked wielding must not fall through into chamber cycling");
                });

                Assert.That(hands.TryDrop(blockedUser, blockedChamberGun), Is.True);
                Assert.That(hands.TryPickupAnyHand(blockedUser, blockedBatteryGun), Is.True);
                var blockedBatteryBefore = BatterySnapshot(blockedBatteryGun);
                var blockedBatteryUse = new UseInHandEvent(blockedUser);
                SEntMan.EventBus.RaiseLocalEvent(blockedBatteryGun, blockedBatteryUse);
                Assert.Multiple(() =>
                {
                    Assert.That(blockedBatteryUse.Handled, Is.True);
                    Assert.That(SEntMan.GetComponent<WieldableComponent>(blockedBatteryGun).Wielded, Is.False);
                    Assert.That(BatterySnapshot(blockedBatteryGun), Is.EqualTo(blockedBatteryBefore),
                        "blocked wielding must not fall through into BatteryWeaponFireModesSystem");
                });
            }
            finally
            {
                SEntMan.DeleteEntity(blockedBatteryGun);
                SEntMan.DeleteEntity(blockedChamberGun);
                SEntMan.DeleteEntity(batteryGun);
                SEntMan.DeleteEntity(chamberGun);
                SEntMan.DeleteEntity(blockedUser);
                SEntMan.DeleteEntity(batteryUser);
                SEntMan.DeleteEntity(chamberUser);
            }
        });
    }

    private (bool? BoltClosed, EntityUid? Chambered, int Ammo) ChamberSnapshot(EntityUid gun)
    {
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(gun);
        var chambered = Server.System<ItemSlotsSystem>().GetItemOrNull(gun, "gun_chamber");
        var ammo = Server.System<SharedGunSystem>().GetAmmoCount(gun);
        return (chamber.BoltClosed, chambered, ammo);
    }

    private (int Mode, string Projectile, float Cost) BatterySnapshot(EntityUid gun)
    {
        var modes = SEntMan.GetComponent<BatteryWeaponFireModesComponent>(gun);
        var ammo = SEntMan.GetComponent<BatteryAmmoProviderComponent>(gun);
        return (modes.CurrentFireMode, ammo.Prototype.Id, ammo.FireCost);
    }
}
