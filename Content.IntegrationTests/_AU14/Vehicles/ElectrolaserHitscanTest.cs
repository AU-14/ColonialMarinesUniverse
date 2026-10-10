using Content.Server.CMU14.Weapons.Ranged.Hitscan;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Projectiles;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Vehicles;

[TestFixture]
public sealed class ElectrolaserHitscanTest
{
    [Test]
    public async Task ElectrolaserFiresAHitscanBeamWithItsProjectileStats()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var factory = server.EntMan.ComponentFactory;
            var cartridge = server.ProtoMan.Index<EntityPrototype>("VehicleCartridgeElectrolaser");
            Assert.That(cartridge.TryComp<CartridgeAmmoComponent>(out var ammo, factory), Is.True);

            var shot = server.ProtoMan.Index(ammo!.Prototype);
            Assert.Multiple(() =>
            {
                Assert.That(shot.HasComp<HitscanAmmoComponent>(factory), Is.True, "The electrolaser should fire a hitscan beam.");
                Assert.That(shot.HasComp<ProjectileComponent>(factory), Is.False, "The electrolaser should no longer fire a projectile.");

                Assert.That(shot.TryComp<HitscanBasicRaycastComponent>(out var raycast, factory), Is.True);
                Assert.That(raycast!.MaxDistance, Is.EqualTo(32f));

                Assert.That(shot.TryComp<CMUHitscanImpactComponent>(out var impact, factory), Is.True);
                Assert.That(impact!.Damage.DamageDict["Piercing"].Float(), Is.EqualTo(10f));
                Assert.That(impact.Damage.DamageDict["Heat"].Float(), Is.EqualTo(160f));
                Assert.That(impact.Damage.DamageDict["Asphyxiation"].Float(), Is.EqualTo(15f));

                Assert.That(shot.TryComp<CMArmorPiercingComponent>(out var piercing, factory), Is.True);
                Assert.That(piercing!.Amount, Is.EqualTo(15));

                Assert.That(shot.TryComp<RMCAreaDamageComponent>(out var area, factory), Is.True);
                Assert.That(area!.DamageArea, Is.EqualTo(0.65f));

                Assert.That(shot.TryComp<HitscanStaminaDamageComponent>(out var stamina, factory), Is.True);
                Assert.That(stamina!.StaminaDamage, Is.EqualTo(15f));
            });
        });

        await pair.CleanReturnAsync();
    }
}
