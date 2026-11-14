using Content.Shared._RMC14.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-113: Bad Blood melee gets a structure-only Breaching boost, mobs and honourable hunters see no change
[TestFixture]
public sealed class BadBloodBreachingTest
{
    private const string MetalBarricade = "CMBarricadeMetal";
    private const float BreachSeconds = 6f;

    [Test]
    public async Task BadBloodWristBladesBreakAMetalBarricadeInSixSeconds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var spawned = new List<EntityUid>();

            try
            {
                var badBloodSeconds = SecondsToBreak(entMan, map.GridCoords, "CMUYautjaBadBloodWristBlades", spawned);
                var honourableSeconds = SecondsToBreak(entMan, map.GridCoords.Offset(new Vector2(0, 2)), "CMUYautjaWristBlades", spawned);

                Assert.Multiple(() =>
                {
                    Assert.That(badBloodSeconds, Is.LessThanOrEqualTo(BreachSeconds),
                        "Bad Blood wrist blades must break a metal barricade within 6 seconds of swings.");
                    Assert.That(honourableSeconds, Is.GreaterThan(badBloodSeconds),
                        "Honourable wrist blades must not get the Breaching boost: they must be slower than the Bad Blood pair.");
                });
            }
            finally
            {
                foreach (var uid in spawned)
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMUYautjaBadBloodWristBlades", "CMUYautjaWristBlades")]
    [TestCase("CMUYautjaBadBloodScimitar", "CMUYautjaScimitar")]
    [TestCase("CMUYautjaBadBloodChainGauntlet", "CMUYautjaChainGauntlet")]
    public async Task BreachingBoostLeavesMobDamageAndHonourableGearAlone(string badBloodWeapon, string honourableWeapon)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var badBlood = entMan.SpawnEntity(badBloodWeapon, map.GridCoords);
            var honourable = entMan.SpawnEntity(honourableWeapon, map.GridCoords);
            var marine = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var badBloodMelee = entMan.GetComponent<MeleeWeaponComponent>(badBlood);
                var honourableMelee = entMan.GetComponent<MeleeWeaponComponent>(honourable);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<DamageBoostsComponent>(badBlood), Is.True,
                        "Bad Blood melee gear must carry the Breaching boost.");
                    Assert.That(entMan.HasComponent<DamageBoostsComponent>(honourable), Is.False,
                        "Honourable melee gear must stay as it is.");

                    // the weapon's own damage, which is all a mob ever takes from it, is untouched
                    Assert.That(badBloodMelee.Damage.Equals(honourableMelee.Damage), Is.True,
                        "The Bad Blood variant must hit mobs exactly as hard as the hunter's weapon.");
                    Assert.That(badBloodMelee.AttackRate, Is.EqualTo(honourableMelee.AttackRate));

                    // the boost only lands on targets flagged Breaching, and a marine is not one
                    Assert.That(
                        entMan.TryGetComponent(marine, out DamageMultiplierFlagsComponent? flags) &&
                        (flags.Flags & DamageMultiplierFlag.Breaching) != DamageMultiplierFlag.None,
                        Is.False,
                        "A marine must not be a Breaching target.");
                });
            }
            finally
            {
                foreach (var uid in new[] { badBlood, honourable, marine })
                {
                    if (entMan.EntityExists(uid))
                        entMan.DeleteEntity(uid);
                }
            }
        });

        await pair.CleanReturnAsync();
    }

    // swings the weapon at a fresh metal barricade through the same damage call a melee hit makes, and returns how
    // long that many swings take at the weapon's attack rate
    private static float SecondsToBreak(
        IEntityManager entMan,
        EntityCoordinates coords,
        string weaponPrototype,
        List<EntityUid> spawned)
    {
        var damageable = entMan.System<DamageableSystem>();
        var attacker = entMan.SpawnEntity("CMUMobYautja", coords);
        var weapon = entMan.SpawnEntity(weaponPrototype, coords);
        var barricade = entMan.SpawnEntity(MetalBarricade, coords.Offset(new Vector2(1, 0)));
        spawned.Add(attacker);
        spawned.Add(weapon);
        spawned.Add(barricade);

        var melee = entMan.GetComponent<MeleeWeaponComponent>(weapon);
        var hits = 0;
        while (hits < 100 && entMan.EntityExists(barricade) && !entMan.IsQueuedForDeletion(barricade))
        {
            damageable.TryChangeDamage(barricade, melee.Damage, origin: attacker, tool: weapon);
            hits++;
        }

        Assert.That(hits, Is.LessThan(100), $"{weaponPrototype} never broke the metal barricade.");
        return hits / melee.AttackRate;
    }
}
