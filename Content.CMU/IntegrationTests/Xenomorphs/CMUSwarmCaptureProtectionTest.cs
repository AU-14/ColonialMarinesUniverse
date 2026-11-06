using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Xenomorphs;

[TestFixture]
public sealed class CMUSwarmCaptureProtectionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task CaptureStatePreventsAnAlreadySelectedMeleeHit(bool infected)
    {
        var map = await Pair.CreateTestMap();
        EntityUid lesser = default;
        EntityUid target = default;
        await Server.WaitAssertion(() =>
        {
            lesser = SSpawnAtPosition("CMUSwarmLesser", map.GridCoords);
            target = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(0.4f, 0)));
            var damageable = SEntMan.System<DamageableSystem>();
            var before = damageable.GetTotalDamage(target);
            if (infected)
                SEntMan.EnsureComponent<VictimInfectedComponent>(target);
            else
                SEntMan.EnsureComponent<InfectableComponent>(target).BeingInfected = true;

            SEntMan.System<SharedCombatModeSystem>().SetInCombatMode(lesser, true);
            var melee = SEntMan.System<SharedMeleeWeaponSystem>();
            var weapon = SEntMan.GetComponent<MeleeWeaponComponent>(lesser);
            melee.AttemptLightAttack(lesser, lesser, weapon, target);
            Assert.That(damageable.GetTotalDamage(target), Is.EqualTo(before),
                "a capture becoming protected between target selection and contact must take no swarm damage");
        });

        // A blocked hit still consumes the attacker's ordinary melee cooldown.
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            if (infected)
                SEntMan.RemoveComponent<VictimInfectedComponent>(target);
            else
                SEntMan.GetComponent<InfectableComponent>(target).BeingInfected = false;

            var damageable = SEntMan.System<DamageableSystem>();
            var before = damageable.GetTotalDamage(target);
            SEntMan.System<SharedMeleeWeaponSystem>().AttemptLightAttack(lesser, lesser,
                SEntMan.GetComponent<MeleeWeaponComponent>(lesser), target);
            Assert.That(damageable.GetTotalDamage(target), Is.GreaterThan(before),
                "the same attack must still hurt an unprotected hostile");
        });
    }
}
