#nullable enable

using System.Numerics;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Weapons.Melee;

[TestFixture]
[TestOf(typeof(SharedMeleeWeaponSystem))]
public sealed class RMCXenoMeleeDamageTest
{
    [Test]
    public async Task XenoMeleeHitDamagesHuman()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid xeno = default;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            xeno = entMan.SpawnEntity("CMXenoDrone", map.GridCoords);
            human = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(Vector2.UnitX * 0.5f));
            server.System<SharedCombatModeSystem>().SetInCombatMode(xeno, true);

            var melee = server.System<Content.Server.Weapons.Melee.MeleeWeaponSystem>();
            Assert.That(melee.TryGetWeapon(xeno, out var weapon, out var component), Is.True);
            Assert.That(melee.GetDamage(weapon, xeno, component).GetTotal(), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(melee.AttemptLightAttack(xeno, weapon, component!, human), Is.True);
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var damageable = server.System<DamageableSystem>();
            Assert.That(damageable.GetTotalDamage(human), Is.GreaterThan(FixedPoint2.Zero));
        });

        await pair.CleanReturnAsync();
    }
}
