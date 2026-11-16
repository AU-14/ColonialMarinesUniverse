#pragma warning disable RA0002 // setup fills the bracer and wires the caster to it directly, as YautjaCasterParityTest does
using System.Numerics;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

// A Bad Blood Leader fired lethal bolts back to back. The lethal mode now refires every 10 s, and the caster applies its
// mode's fire rate before SharedGunSystem spends the cooldown, so a lethal bolt fired after a mode change that skipped
// SetMode (ForceMode) used to refire at the stun bolt's rate instead of its own.
[TestFixture]
public sealed class BadBloodCasterLethalCooldownTest
{
    private const string BadBloodCaster = "CMUYautjaBadBloodPlasmaCaster";

    [Test]
    public async Task LethalBoltWaitsOutItsOwnCooldownAfterAForcedModeChange()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var timing = server.ResolveDependency<IGameTiming>();

        EntityUid hunter = default;
        EntityUid bracer = default;
        EntityUid caster = default;
        EntityCoordinates target = default;
        TimeSpan firstShotAt = default;
        TimeSpan lethalDelay = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var at = map.GridCoords.Offset(new Vector2(0.5f, 0.5f));
            target = map.GridCoords.Offset(new Vector2(10.5f, 0.5f));

            hunter = entMan.SpawnEntity("CMMobHuman", at);
            entMan.EnsureComponent<YautjaComponent>(hunter);
            entMan.EnsureComponent<YautjaBadBloodComponent>(hunter);
            entMan.EnsureComponent<BadBloodLethalBoltComponent>(hunter);

            bracer = entMan.SpawnEntity("CMUYautjaBracer", at);
            caster = entMan.SpawnEntity(BadBloodCaster, at);
            Assert.That(entMan.System<InventorySystem>().TryEquip(hunter, bracer, "gloves", silent: true, force: true), Is.True);
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(hunter, caster), Is.True);

            var stored = entMan.GetComponent<YautjaStoredGearComponent>(caster);
            stored.Bracer = bracer;
            stored.Deployed = true;

            var casterComp = entMan.GetComponent<YautjaCasterComponent>(caster);
            var gun = entMan.GetComponent<GunComponent>(caster);
            var stunRate = casterComp.Modes[BadBloodEradicatorSystem.StunMode].FireRate;
            var lethalRate = casterComp.Modes[BadBloodEradicatorSystem.LethalMode].FireRate;
            lethalDelay = TimeSpan.FromSeconds(1f / lethalRate);

            Assert.That(casterComp.CurrentMode, Is.EqualTo(BadBloodEradicatorSystem.StunMode),
                "Test setup: the Bad Blood caster lands on the stun bolt.");
            Assert.That(gun.FireRateModified, Is.EqualTo(stunRate).Within(0.0001f),
                "Test setup: the gun starts at the stun bolt's fire rate.");
            Assert.That(1f / stunRate, Is.LessThan(1.5f),
                "Test setup: the stun refire must be shorter than the 1.5 s probe below.");
            Assert.That(lethalDelay.TotalSeconds, Is.EqualTo(10).Within(0.01),
                "A Bad Blood lethal bolt waits 10 seconds before the next one.");

            // the same call the armory gate makes after an eradicator shot: CurrentMode moves, SetMode is skipped
            entMan.System<YautjaCasterSystem>().ForceMode(caster, BadBloodEradicatorSystem.LethalMode);

            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            bracerComp.Charge = bracerComp.MaxCharge;
            var before = bracerComp.Charge;
            firstShotAt = timing.CurTime;

            var shot = Shoot(entMan, hunter, caster, target);
            Assert.That(shot, Has.Count.EqualTo(1), "The first lethal bolt fires.");
            Assert.That(bracerComp.Charge, Is.EqualTo(before - casterComp.Modes[BadBloodEradicatorSystem.LethalMode].PowerCost!.Value),
                "The lethal bolt drains its own cost.");
            Assert.That((gun.NextFire - firstShotAt).TotalSeconds, Is.EqualTo(lethalDelay.TotalSeconds).Within(0.05),
                "The lethal bolt must start the lethal refire, not the stun bolt's.");
            Delete(entMan, shot);
        });

        // past the stun refire, short of the lethal one
        await pair.RunSeconds(1.5f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(timing.CurTime - firstShotAt, Is.LessThan(lethalDelay),
                "Test setup: this probe must land inside the lethal cooldown.");

            var bracerComp = entMan.GetComponent<YautjaBracerComponent>(bracer);
            var before = bracerComp.Charge;
            var shot = Shoot(entMan, hunter, caster, target);

            Assert.Multiple(() =>
            {
                Assert.That(shot, Is.Null, "A second lethal bolt inside the cooldown must be refused.");
                Assert.That(bracerComp.Charge, Is.EqualTo(before), "A refused shot must not drain the bracer.");
            });
            Delete(entMan, shot);
        });

        await pair.RunSeconds(9f);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(timing.CurTime - firstShotAt, Is.GreaterThanOrEqualTo(lethalDelay),
                "Test setup: this probe must land after the lethal cooldown.");

            var shot = Shoot(entMan, hunter, caster, target);
            Assert.That(shot, Has.Count.EqualTo(1), "Once the cooldown is over the caster fires again.");
            Delete(entMan, shot);

            foreach (var uid in new[] { caster, bracer, hunter })
            {
                if (entMan.EntityExists(uid))
                    entMan.DeleteEntity(uid);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static List<EntityUid>? Shoot(IEntityManager entMan, EntityUid user, EntityUid caster, EntityCoordinates target)
    {
        var gun = entMan.GetComponent<GunComponent>(caster);
        // a fresh trigger pull each time; this overload also releases the trigger afterwards
        return entMan.System<SharedGunSystem>().AttemptShoot((caster, gun), user, target);
    }

    private static void Delete(IEntityManager entMan, List<EntityUid>? projectiles)
    {
        if (projectiles == null)
            return;

        foreach (var uid in projectiles)
        {
            if (entMan.EntityExists(uid))
                entMan.DeleteEntity(uid);
        }
    }
}
