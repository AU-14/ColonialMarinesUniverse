using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// ECS-101: a forced decloak starts a cooldown, and Bad Bloods trade their lockout exemption for the ambush rule
[TestFixture]
public sealed class BadBloodCloakTest
{
    private const string HunterBracer = "CMUYautjaBracer";
    private const string BadBloodBracer = "CMUYautjaBadBloodBracer";

    // honourable hunters keep their normal cooldown; only Bad Bloods get the forced one
    [TestCase(true)]
    public async Task ForcedDecloakBlocksRecloakUntilItsCooldownEnds(bool badBlood)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid user = default;
        EntityUid bracer = default;
        var cooldown = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            user = SpawnCloaker(entMan, map.GridCoords, badBlood, out bracer);
            cooldown = entMan.GetComponent<YautjaBracerComponent>(bracer).CloakForcedDecloakCooldown;
            Assert.That(cooldown, Is.GreaterThan(TimeSpan.Zero));

            PressCloak(entMan, user, bracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True, "The cloak must raise first.");

            entMan.System<YautjaCloakSystem>().ForceDecloak(user);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.False);
            Assert.That(entMan.GetComponent<YautjaBracerComponent>(bracer).CloakCooldownUntil,
                Is.GreaterThan(timing.CurTime), "A forced decloak must start a cooldown.");

            PressCloak(entMan, user, bracer);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.False,
                "Re-cloaking must be refused while the forced-decloak cooldown runs.");
        });

        await pair.RunTicksSync(pair.SecondsToTicks((float) cooldown.TotalSeconds + 1f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            PressCloak(entMan, user, bracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True,
                "The cloak must raise again once the cooldown has ended.");
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(user));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForcedDecloakStartsNoCooldownForAnHonourableHunter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid user = default;
        EntityUid bracer = default;
        var cooldownBefore = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            user = SpawnCloaker(entMan, map.GridCoords, false, out bracer);
            var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            Assert.That(comp.CloakForcedDecloakCooldown, Is.GreaterThan(TimeSpan.Zero),
                "the bracer must carry a forced cooldown for this to prove anything");

            PressCloak(entMan, user, bracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True, "The cloak must raise first.");
            cooldownBefore = comp.CloakCooldownUntil;

            entMan.System<YautjaCloakSystem>().ForceDecloak(user);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.False);
            Assert.That(entMan.GetComponent<YautjaBracerComponent>(bracer).CloakCooldownUntil,
                Is.EqualTo(cooldownBefore), "A forced decloak must not start a cooldown for an honourable hunter.");

            PressCloak(entMan, user, bracer);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(user), Is.True,
                "An honourable hunter must be able to re-cloak straight after a forced decloak.");
        });

        await server.WaitPost(() => server.EntMan.DeleteEntity(user));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BadBloodAmbushHitsHarderDecloaksAndStartsTheShortLockout()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid badBlood = default;
        EntityUid hunter = default;
        EntityUid badBloodBracer = default;
        EntityUid hunterBracer = default;
        EntityUid ambushed = default;
        EntityUid control = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            badBlood = SpawnCloaker(entMan, map.GridCoords, true, out badBloodBracer);
            hunter = SpawnCloaker(entMan, map.GridCoords.Offset(new Vector2(0, 3)), false, out hunterBracer);
            ambushed = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            control = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 3)));

            PressCloak(entMan, badBlood, badBloodBracer);
            PressCloak(entMan, hunter, hunterBracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.True);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);

            Hit(entMan, badBlood, ambushed);
            Hit(entMan, hunter, control);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var plain = damageable.GetTotalDamage(control).Float();
            var ambush = damageable.GetTotalDamage(ambushed).Float();
            var badBloodComp = entMan.GetComponent<YautjaBracerComponent>(badBloodBracer);
            var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
            var now = timing.CurTime;

            Assert.Multiple(() =>
            {
                Assert.That(plain, Is.GreaterThan(0f));
                Assert.That(ambush, Is.EqualTo(plain * (1f + badBloodComp.CloakBadBloodAmbushBonus)).Within(0.5f),
                    "The first hit from cloak must deal the ambush bonus.");
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.False,
                    "The ambush hit must drop the Bad Blood's cloak.");
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True,
                    "An honourable hunter's hit must not drop the cloak.");

                Assert.That(badBloodComp.CloakCombatLockoutUntil, Is.GreaterThan(now),
                    "Bad Bloods are no longer exempt from the combat lockout.");
                Assert.That(badBloodComp.CloakCombatLockoutUntil,
                    Is.LessThanOrEqualTo(now + badBloodComp.CloakBadBloodCombatLockout));
                Assert.That(hunterComp.CloakCombatLockoutUntil,
                    Is.GreaterThan(now + hunterComp.CloakCombatLockout - TimeSpan.FromSeconds(1)),
                    "Honourable hunters keep their own combat lockout.");
            });
        });

        await server.WaitPost(() =>
        {
            foreach (var uid in new[] { badBlood, hunter, ambushed, control })
                server.EntMan.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid SpawnCloaker(IEntityManager entMan, EntityCoordinates coords, bool badBlood, out EntityUid bracer)
    {
        var inventory = entMan.System<InventorySystem>();
        var user = entMan.SpawnEntity("CMUMobYautja", coords);
        if (badBlood)
            entMan.EnsureComponent<YautjaBadBloodComponent>(user);

        if (inventory.TryUnequip(user, "gloves", out var oldBracer, silent: true, force: true))
            entMan.DeleteEntity(oldBracer.Value);

        bracer = entMan.SpawnEntity(badBlood ? BadBloodBracer : HunterBracer, coords);
        Assert.That(inventory.TryEquip(user, bracer, "gloves", silent: true, force: true), Is.True);
        entMan.GetComponent<YautjaBracerComponent>(bracer).Charge = 1500;
        return user;
    }

    private static void PressCloak(IEntityManager entMan, EntityUid user, EntityUid bracer)
    {
        var comp = entMan.GetComponent<YautjaBracerComponent>(bracer);
        var cloak = new YautjaToggleCloakActionEvent { Performer = user, Action = comp.ToggleCloakAction!.Value };
        entMan.EventBus.RaiseLocalEvent(bracer, cloak);
    }

    // The attacker doubles as the tool so the hit counts as a deliberate strike, like a melee swing.
    private static void Hit(IEntityManager entMan, EntityUid attacker, EntityUid victim)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 20;
        entMan.System<DamageableSystem>()
            .TryChangeDamage(victim, damage, ignoreResistances: true, origin: attacker, tool: attacker);
    }
}
