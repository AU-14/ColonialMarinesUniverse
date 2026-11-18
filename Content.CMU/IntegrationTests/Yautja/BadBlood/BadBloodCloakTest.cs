using Content.Shared._RMC14.Stealth;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
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
    private const string HunterCaster = "CMUYautjaPlasmaCaster";
    private const string BadBloodCaster = "CMUYautjaBadBloodPlasmaCaster";

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

    // ECS-601: the stun bolt deals no damage, so the ambush path never saw it and a Bad Blood could stun-lock from
    // cloak. Any caster shot from cloak now drops a Bad Blood's cloak and starts the short combat lockout, with no
    // forced-decloak or toggle cooldown on top. An honourable hunter firing from cloak stays cloaked
    [Test]
    public async Task BadBloodCasterShotFromCloakDropsTheCloak()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid badBlood = default;
        EntityUid hunter = default;
        EntityUid badBloodBracer = default;
        EntityUid hunterBracer = default;
        EntityUid badBloodCaster = default;
        EntityUid hunterCaster = default;
        var cooldownBefore = TimeSpan.Zero;
        var hunterLockoutBefore = TimeSpan.Zero;
        var lockout = TimeSpan.Zero;
        var shots = new List<EntityUid>();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            badBlood = SpawnCloaker(entMan, map.GridCoords, true, out badBloodBracer);
            hunter = SpawnCloaker(entMan, map.GridCoords.Offset(new Vector2(0, 3)), false, out hunterBracer);
            badBloodCaster = ArmCaster(entMan, badBlood, badBloodBracer, BadBloodCaster);
            hunterCaster = ArmCaster(entMan, hunter, hunterBracer, HunterCaster);
#pragma warning disable RA0002 // reads the caster's mode
            Assert.That(entMan.GetComponent<YautjaCasterComponent>(badBloodCaster).CurrentMode,
                Is.EqualTo(BadBloodEradicatorSystem.StunMode), "Test setup: the Bad Blood caster lands on the stun bolt.");
#pragma warning restore RA0002

            PressCloak(entMan, badBlood, badBloodBracer);
            PressCloak(entMan, hunter, hunterBracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.True);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True);
        });

        // past the cloak's 1 s weapon lock after it raises
        await pair.RunSeconds(1.5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var badBloodComp = entMan.GetComponent<YautjaBracerComponent>(badBloodBracer);
            cooldownBefore = badBloodComp.CloakCooldownUntil;
            lockout = badBloodComp.CloakBadBloodCombatLockout;
            hunterLockoutBefore = entMan.GetComponent<YautjaBracerComponent>(hunterBracer).CloakCombatLockoutUntil;

            shots.AddRange(Shoot(entMan, badBlood, badBloodCaster, map.GridCoords.Offset(new Vector2(10, 0))));
            shots.AddRange(Shoot(entMan, hunter, hunterCaster, map.GridCoords.Offset(new Vector2(10, 3))));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var badBloodComp = entMan.GetComponent<YautjaBracerComponent>(badBloodBracer);
            var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
            var now = timing.CurTime;

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.False,
                    "A stun bolt fired from cloak must drop the Bad Blood's cloak.");
                Assert.That(entMan.GetComponent<EntityTurnInvisibleComponent>(badBlood).Enabled, Is.False);
                Assert.That(badBloodComp.CloakCombatLockoutUntil, Is.GreaterThan(now),
                    "The shot must start the Bad Blood combat lockout.");
                Assert.That(badBloodComp.CloakCombatLockoutUntil, Is.LessThanOrEqualTo(now + lockout));
                Assert.That(badBloodComp.CloakCooldownUntil, Is.EqualTo(cooldownBefore),
                    "The Bad Blood's own shot starts no forced-decloak or toggle cooldown.");

                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True,
                    "An honourable hunter's shot must not drop the cloak.");
                Assert.That(hunterComp.CloakCombatLockoutUntil, Is.EqualTo(hunterLockoutBefore),
                    "An honourable hunter's shot that hits nothing starts no lockout.");
            });

            PressCloak(entMan, badBlood, badBloodBracer);
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.False,
                "Re-cloaking must be refused while the combat lockout runs.");
        });

        await pair.RunSeconds((float) lockout.TotalSeconds + 1f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            PressCloak(entMan, badBlood, badBloodBracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.True,
                "The cloak must raise again once the combat lockout has ended.");
        });

        await server.WaitPost(() =>
        {
            foreach (var uid in shots.Concat(new[] { badBloodCaster, hunterCaster, badBlood, hunter }))
            {
                if (server.EntMan.EntityExists(uid))
                    server.EntMan.DeleteEntity(uid);
            }
        });
        await pair.CleanReturnAsync();
    }

    // ECS-605: a hit on a Bad Blood blocks re-cloaking for a short while, so it cannot cloak and sprint off the moment
    // it is shot. A tick that does not interrupt (bleeding, hunger) is not a hit. An honourable hunter is untouched
    [Test]
    public async Task DamagedBadBloodCannotCloakUntilTheDamagedLockoutEnds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid badBlood = default;
        EntityUid hunter = default;
        EntityUid badBloodBracer = default;
        EntityUid hunterBracer = default;
        var lockout = TimeSpan.Zero;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            badBlood = SpawnCloaker(entMan, map.GridCoords, true, out badBloodBracer);
            hunter = SpawnCloaker(entMan, map.GridCoords.Offset(new Vector2(0, 3)), false, out hunterBracer);
            var badBloodComp = entMan.GetComponent<YautjaBracerComponent>(badBloodBracer);
            var hunterComp = entMan.GetComponent<YautjaBracerComponent>(hunterBracer);
            lockout = badBloodComp.CloakBadBloodDamagedLockout;
            var hunterLockoutBefore = hunterComp.CloakCombatLockoutUntil;
            var now = timing.CurTime;
            Assert.That(lockout, Is.GreaterThan(TimeSpan.Zero));

            TakeDamage(entMan, badBlood, interrupts: false);
            Assert.That(badBloodComp.CloakCombatLockoutUntil, Is.LessThanOrEqualTo(now),
                "A tick that does not interrupt, like bleeding, must not lock the cloak.");

            TakeDamage(entMan, badBlood, interrupts: true);
            TakeDamage(entMan, hunter, interrupts: true);

            Assert.Multiple(() =>
            {
                Assert.That(badBloodComp.CloakCombatLockoutUntil, Is.EqualTo(now + lockout),
                    "A hit must lock the Bad Blood's cloak for the damaged lockout.");
                Assert.That(hunterComp.CloakCombatLockoutUntil, Is.EqualTo(hunterLockoutBefore),
                    "A hit must not lock an honourable hunter's cloak.");
            });

            PressCloak(entMan, badBlood, badBloodBracer);
            PressCloak(entMan, hunter, hunterBracer);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.False,
                    "A Bad Blood that was just hit must not cloak.");
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(hunter), Is.True,
                    "An honourable hunter that was just hit cloaks as before.");
            });
        });

        await pair.RunSeconds((float) lockout.TotalSeconds + 1f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            PressCloak(entMan, badBlood, badBloodBracer);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(badBlood), Is.True,
                "The cloak must raise again once the damaged lockout has ended.");
        });

        await server.WaitPost(() =>
        {
            server.EntMan.DeleteEntity(badBlood);
            server.EntMan.DeleteEntity(hunter);
        });
        await pair.CleanReturnAsync();
    }

    // ECS-610: hanging back cloaked costs charge. A cloaked Bad Blood's bracer regenerates at the cloaked rate, an
    // uncloaked one at full rate. An honourable hunter regenerates the same cloaked or not
    [Test]
    public async Task CloakedBadBloodRegeneratesAtTheCloakedRate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        // cloaked Bad Blood, open Bad Blood, cloaked hunter, open hunter
        var users = new EntityUid[4];
        var bracers = new EntityUid[4];
        var before = new float[4];
        var multiplier = 1f;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            for (var i = 0; i < users.Length; i++)
            {
                users[i] = SpawnCloaker(entMan, map.GridCoords.Offset(new Vector2(0, 3 * i)), i < 2, out var bracer);
                bracers[i] = bracer;
            }

            multiplier = entMan.GetComponent<YautjaBracerComponent>(bracers[0]).CloakBadBloodRegenMultiplier;
            Assert.That(multiplier, Is.LessThan(1f).And.GreaterThanOrEqualTo(0f));

            PressCloak(entMan, users[0], bracers[0]);
            PressCloak(entMan, users[2], bracers[2]);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(users[0]), Is.True);
            Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(users[2]), Is.True);

            // read after the toggle cost is paid, so only regen moves the charge from here
            for (var i = 0; i < bracers.Length; i++)
            {
                before[i] = entMan.GetComponent<YautjaBracerComponent>(bracers[i]).Charge.Float();
            }
        });

        await pair.RunSeconds(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var gained = new float[4];
            for (var i = 0; i < bracers.Length; i++)
            {
                gained[i] = entMan.GetComponent<YautjaBracerComponent>(bracers[i]).Charge.Float() - before[i];
            }

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<EntityActiveInvisibleComponent>(users[0]), Is.True,
                    "Test setup: the Bad Blood stays cloaked for the whole window.");
                Assert.That(gained[1], Is.GreaterThan(0f), "Test setup: the bracers must regenerate.");
                Assert.That(gained[0], Is.EqualTo(gained[1] * multiplier).Within(1f),
                    "A cloaked Bad Blood's bracer must regenerate at the cloaked rate.");
                Assert.That(gained[3], Is.GreaterThan(0f));
                Assert.That(gained[2], Is.EqualTo(gained[3]).Within(1f),
                    "An honourable hunter's regen must not change while cloaked.");
            });
        });

        await server.WaitPost(() =>
        {
            foreach (var uid in users)
                server.EntMan.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }

    // puts the caster in the user's hand, wired to the bracer they wear
    private static EntityUid ArmCaster(IEntityManager entMan, EntityUid user, EntityUid bracer, string prototype)
    {
        var caster = entMan.SpawnEntity(prototype, entMan.GetComponent<TransformComponent>(user).Coordinates);
        Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, caster), Is.True);

        // setup wires the caster to the bracer directly, as YautjaCasterParityTest does
        var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
        stored.Bracer = bracer;
        stored.Deployed = true;
        return caster;
    }

    private static List<EntityUid> Shoot(IEntityManager entMan, EntityUid user, EntityUid caster, EntityCoordinates target)
    {
        var gun = entMan.GetComponent<GunComponent>(caster);
        var shot = entMan.System<SharedGunSystem>().AttemptShoot((caster, gun), user, target);
        Assert.That(shot, Is.Not.Null.And.Count.EqualTo(1), "Test setup: the caster must fire.");
        return shot!;
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

    // no attacker, so only the victim's own rules run. interrupts: false is how bleeding and hunger ticks land
    private static void TakeDamage(IEntityManager entMan, EntityUid victim, bool interrupts)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 10;
        entMan.System<DamageableSystem>()
            .TryChangeDamage(victim, damage, ignoreResistances: true, interruptsDoAfters: interrupts);
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
