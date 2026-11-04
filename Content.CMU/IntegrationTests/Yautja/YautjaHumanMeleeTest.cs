using Content.IntegrationTests.Fixtures;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHumanMeleeTest : GameTest
{
    // cmss13: a sharp, edged pred weapon hits a human for its listed force whether they wear marine armour or not,
    // and only non-humans get the melee skill bonus
    [TestCase("CMUYautjaClanSword")]
    [TestCase("CMUYautjaWristBlades")]
    public async Task PredWeaponHitsHumansForItsForceThroughArmour(string weaponProto)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var naked = HitOnce(weaponProto, "CMMobHuman", null, map.GridCoords);
            var armoured = HitOnce(weaponProto, "CMMobHuman", "AU14CCAFArmor", map.GridCoords);
            var weapon = SSpawnAtPosition(weaponProto, map.GridCoords);
            var force = SEntMan.GetComponent<MeleeWeaponComponent>(weapon).Damage.GetTotal().Float();

            Assert.That(naked, Is.EqualTo(force).Within(0.5f), "no skill bonus on humans");
            Assert.That(armoured, Is.EqualTo(naked).Within(0.5f), "sharp + edge penetration covers marine armour");
        });
    }

    [Test]
    public async Task XenosStillTakeTheSkillBonus()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = HitOnce("CMUYautjaClanSword", "CMMobHuman", null, map.GridCoords);
            var xeno = HitOnce("CMUYautjaClanSword", "CMXenoDrone", null, map.GridCoords);
            Assert.That(xeno, Is.GreaterThan(human));
        });
    }

    private float HitOnce(string weaponProto, string victimProto, string? armorProto, Robust.Shared.Map.EntityCoordinates coords)
    {
        var hunter = SSpawnAtPosition("CMUMobYautja", coords);
        var victim = SSpawnAtPosition(victimProto, coords);
        if (armorProto != null)
        {
            var armor = SSpawnAtPosition(armorProto, coords);
            Assert.That(Server.System<InventorySystem>().TryEquip(victim, armor, "outerClothing", silent: true, force: true), Is.True);
        }

        var weapon = SSpawnAtPosition(weaponProto, coords);
        Assert.That(Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>().TryPickupAnyHand(hunter, weapon), Is.True);

        var melee = Server.System<SharedMeleeWeaponSystem>();
        Assert.That(melee.TryGetWeapon(hunter, out var used, out var comp), Is.True);
        Assert.That(used, Is.EqualTo(weapon));
        // picking a weapon up starts its swing cooldown
        comp.NextAttack = TimeSpan.Zero;
        SEntMan.RemoveComponent<Content.Shared._RMC14.Weapons.Melee.RMCMeleeUserCooldownComponent>(hunter);
        Assert.That(melee.AttemptLightAttack(hunter, used, comp, victim, requireCombatMode: false), Is.True);
        return Server.System<DamageableSystem>().GetTotalDamage(victim).Float();
    }
}
