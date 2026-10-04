using Content.Shared._RMC14.Projectiles;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class RMCSmartGunPrototypeRegressionTest
{
    private const string HazopsMagazine = "AU14MagazineMinigunHAZOPS";
    private const string HazopsCartridge = "AU14CartridgeMinigunHAZOPS9mm";
    private const string HazopsBullet = "AU14BulletMinigunHAZOPS9mm";

    [TestCase("CMBulletSmartGun10x30mm", 12f, 10f, 4)]
    [TestCase("RMCBulletSmartGun10x30mmirradiated", 12f, 10f, 4)]
    [TestCase("RMCBulletSmartGun10x30mmHT", 12f, 10f, 4)]
    [TestCase("AU14BulletSmartGun127x40mm", 12f, 10f, 4)]
    [TestCase(HazopsBullet, 12f, 10f, 4)]
    public async Task SmartGunProjectilesKeepDamageFalloff(
        string projectileId,
        float expectedCapRange,
        float expectedFalloffRange,
        int expectedFalloff)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(projectileId, out var bullet), Is.True);
            Assert.That(bullet!.TryGetComponent<RMCProjectileDamageFalloffComponent>(out var falloff, factory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(falloff!.Thresholds, Has.Count.EqualTo(2));
                Assert.That(falloff.Thresholds[0].Range, Is.EqualTo(expectedCapRange));
                Assert.That(falloff.Thresholds[0].Falloff, Is.EqualTo((FixedPoint2) 9999));
                Assert.That(falloff.Thresholds[0].IgnoreModifiers, Is.True);
                Assert.That(falloff.Thresholds[1].Range, Is.EqualTo(expectedFalloffRange));
                Assert.That(falloff.Thresholds[1].Falloff, Is.EqualTo((FixedPoint2) expectedFalloff));
                Assert.That(falloff.Thresholds[1].IgnoreModifiers, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HazopsSmartGunUsesDedicatedFalloffProjectile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            Assert.That(prototypes.TryIndex<EntityPrototype>(HazopsMagazine, out var magazine), Is.True);
            Assert.That(magazine!.TryGetComponent<BallisticAmmoProviderComponent>(out var ammo, factory), Is.True);
            Assert.That(ammo!.Proto, Is.EqualTo((EntProtoId) HazopsCartridge));

            Assert.That(prototypes.TryIndex<EntityPrototype>(HazopsCartridge, out var cartridge), Is.True);
            Assert.That(cartridge!.TryGetComponent<CartridgeAmmoComponent>(out var cartridgeAmmo, factory), Is.True);
            Assert.That(cartridgeAmmo!.Prototype, Is.EqualTo((EntProtoId) HazopsBullet));
        });

        await pair.CleanReturnAsync();
    }
}
