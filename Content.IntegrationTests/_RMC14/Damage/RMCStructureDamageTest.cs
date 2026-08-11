#nullable enable

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Damage;

[TestFixture]
[TestOf(typeof(DamageableSystem))]
public sealed class RMCStructureDamageTest
{
    [TestCase("CMWallMetal")]
    [TestCase("CMAirlock")]
    [TestCase("WallXenoResin")]
    public async Task ProjectileDamageDamagesStructure(string targetPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            target = entMan.SpawnEntity(targetPrototype, map.GridCoords);
            var projectile = entMan.SpawnEntity("BulletRifle10x24mm", map.GridCoords);
            var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
            var damageable = server.System<DamageableSystem>();

            Assert.That(entMan.HasComponent<InjurableComponent>(target), Is.True);
            Assert.That(damageable.TryChangeDamage(target, projectileComp.Damage, tool: projectile), Is.True);
            Assert.That(damageable.GetTotalDamage(target), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMWallMetal", false)]
    [TestCase("CMAirlock", true)]
    public async Task XenoClawsDamageStructure(string targetPrototype, bool weldDoor)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid target = default;

        await server.WaitAssertion(() =>
        {
            var xeno = entMan.SpawnEntity("CMXenoLurker", map.GridCoords);
            target = entMan.SpawnEntity(targetPrototype, map.GridCoords);

            if (weldDoor)
            {
                Assert.That(server.System<SharedDoorSystem>().SetState(target, DoorState.Welded), Is.True);
            }

            var melee = server.System<Content.Server.Weapons.Melee.MeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var meleeComp), Is.True);

            var damageable = server.System<DamageableSystem>();
            var damage = melee.GetDamage(weapon, xeno, meleeComp);
            Assert.That(damageable.TryChangeDamage(target, damage, origin: xeno, tool: weapon), Is.True);
            Assert.That(damageable.GetTotalDamage(target), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
