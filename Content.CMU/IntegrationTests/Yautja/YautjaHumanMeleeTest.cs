using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Melee;
using Content.Shared.Wieldable;
using Content.Shared._RMC14.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaHumanMeleeTest : GameTest
{
    private const string Armor = "AU14CCAFArmor";

    // cmss13: a sharp, edged pred weapon hits a human for its listed force whether they wear marine armour or not,
    // and only non-humans get the melee skill bonus
    [TestCase("CMUYautjaClanSword")]
    [TestCase("CMUYautjaWristBlades")]
    public async Task PredWeaponHitsHumansForItsForceThroughArmour(string weaponProto)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var naked = Hit(weaponProto, null, map.GridCoords, stripChance: 0f).Damage;
            var armoured = Hit(weaponProto, Armor, map.GridCoords, stripChance: 0f).Damage;
            var weapon = SSpawnAtPosition(weaponProto, map.GridCoords);
            var force = SEntMan.GetComponent<MeleeWeaponComponent>(weapon).Damage.GetTotal().Float();

            Assert.That(naked, Is.EqualTo(force).Within(0.5f), "no skill bonus on humans");
            Assert.That(armoured, Is.EqualTo(naked).Within(0.5f), "sharp + edge penetration covers marine armour");
        });
    }

    // cm's prob(armour) roll: when it lands the blade loses its pen and the armour counts in full
    [Test]
    public async Task ArmourRollStripsThePenetration()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var naked = Hit("CMUYautjaClanSword", null, map.GridCoords, stripChance: 1f).Damage;
            var stripped = Hit("CMUYautjaClanSword", Armor, map.GridCoords, stripChance: 1f).Damage;

            Assert.That(naked, Is.GreaterThan(0f));
            Assert.That(stripped, Is.LessThan(naked * 0.75f), "armour should cut a stripped hit hard");
        });
    }

    [Test]
    public async Task XenosStillTakeTheSkillBonus()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = Hit("CMUYautjaClanSword", null, map.GridCoords).Damage;
            var xeno = Hit("CMUYautjaClanSword", null, map.GridCoords, victimProto: "CMXenoDrone").Damage;
            Assert.That(xeno, Is.GreaterThan(human));
        });
    }

    private (EntityUid Victim, float Damage) Hit(
        string weaponProto,
        string? armorProto,
        EntityCoordinates coords,
        int hits = 1,
        bool wield = false,
        float? stripChance = null,
        string victimProto = "CMMobHuman")
    {
        var hunter = SSpawnAtPosition("CMUMobYautja", coords);
        var victim = SSpawnAtPosition(victimProto, coords);
        if (armorProto != null)
        {
            var armor = SSpawnAtPosition(armorProto, coords);
            Assert.That(Server.System<InventorySystem>().TryEquip(victim, armor, "outerClothing", silent: true, force: true), Is.True);
        }

        var weapon = SSpawnAtPosition(weaponProto, coords);
        if (stripChance is { } chance)
            SEntMan.GetComponent<YautjaHumanMeleeComponent>(weapon).StripChancePerArmor = chance;

        Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(hunter, weapon), Is.True);
        if (wield)
            Assert.That(Server.System<SharedWieldableSystem>().TryWield(weapon, hunter), Is.True);

        var melee = Server.System<SharedMeleeWeaponSystem>();
        Assert.That(melee.TryGetWeapon(hunter, out var used, out var comp), Is.True);
        Assert.That(used, Is.EqualTo(weapon));
        for (var i = 0; i < hits; i++)
        {
            // picking a weapon up starts its swing cooldown
            comp.NextAttack = TimeSpan.Zero;
            SEntMan.RemoveComponent<RMCMeleeUserCooldownComponent>(hunter);
            Assert.That(melee.AttemptLightAttack(hunter, used, comp, victim, requireCombatMode: false), Is.True);
        }

        return (victim, Server.System<DamageableSystem>().GetTotalDamage(victim).Float());
    }
}
