#pragma warning disable RA0002 // swaps the gun's loaded shell proto to breaching before the first shot
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Weapons;

// playtest: the lacn breacher with breaching shells barely scratched walls.
// fires the real gun point blank at a real wall, so the pellets go through the whole
// projectile -> damage modify -> destructible chain instead of a hand-applied number
[TestFixture]
public sealed class LacnBreacherWallTest : InteractionTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    private const string Breacher = "CMUWeaponShotgunLACNBreacher";
    private const string BreachingShell = "RMCShellShotgunBreaching1";

    [Test]
    public async Task MetalWallTakesTwoShots()
    {
        await AssertShotsToBreak("CMWallMetal", 2);
    }

    // CMWallReinforced is the plain concrete child of RMCBaseWallReinforced
    [Test]
    public async Task ReinforcedWallTakesFourShots()
    {
        await AssertShotsToBreak("CMWallReinforced", 4);
    }

    // buckshot out of the same gun must not get the wall multiplier, only the breaching pellets
    [Test]
    public async Task OnlyBreachingPelletsGetTheWallMultiplier()
    {
        await AddAtmosphere();
        var gunNet = await PlaceInHands(Breacher);
        var gun = ToServer(gunNet);
        await RunSeconds(2);

        await Server.WaitAssertion(() =>
        {
            var gunComp = SEntMan.GetComponent<GunComponent>(gun);
            var from = SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates;
            var to = SEntMan.GetCoordinates(TargetCoords);

            // default load is buckshot
            var buckshot = SEntMan.SpawnEntity("CMShellShotgunBuckshot", from);
            var shot = SGun.Shoot((gun, gunComp), buckshot, from, to, out _, SPlayer);
            Assert.That(shot, Is.Not.Null.And.Not.Empty);
            foreach (var pellet in shot!)
                Assert.That(SEntMan.HasComponent<DamageMultipliersComponent>(pellet), Is.False, "buckshot should stay stock");

            var breaching = SEntMan.SpawnEntity(BreachingShell, from);
            shot = SGun.Shoot((gun, gunComp), breaching, from, to, out _, SPlayer);
            Assert.That(shot, Has.Count.EqualTo(4), "breaching shell is 4 pellets");
            foreach (var pellet in shot!)
            {
                Assert.That(SEntMan.TryGetComponent<DamageMultipliersComponent>(pellet, out var mult), Is.True,
                    "every spread pellet needs the multiplier, not just the first one");
                Assert.That(mult!.Multipliers.ContainsKey(DamageMultiplierFlag.Turf), Is.True);
            }
        });
    }

    private async Task AssertShotsToBreak(string wallProto, int shots)
    {
        await AddAtmosphere();
        var wallNet = await SpawnTarget(wallProto);
        var wall = ToServer(wallNet);
        var gunNet = await PlaceInHands(Breacher);
        var gun = ToServer(gunNet);

        await Server.WaitAssertion(() =>
        {
            // load it with breaching shells instead of the default buckshot
            var ammo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(gun);
            Assert.That(ammo.Container.ContainedEntities, Is.Empty, "expected an unspawned default load");
            ammo.Proto = BreachingShell;
            Assert.That(ammo.Count, Is.GreaterThanOrEqualTo(shots));
        });

        // guns have a pickup delay
        await RunSeconds(2);

        for (var i = 1; i <= shots; i++)
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.Deleted(wall), Is.False, $"{wallProto} went down early, before shot {i}");
                var pump = SEntMan.GetComponent<PumpActionComponent>(gun);
                SEntMan.System<PumpActionSystem>().Pump((gun, pump), SPlayer);
            });

            await AttemptShoot(wallNet);
            await RunSeconds(1.5f);

            if (i == shots - 1)
            {
                await Server.WaitAssertion(() =>
                {
                    Assert.That(SEntMan.Deleted(wall), Is.False, $"{wallProto} should survive {i} shots");
                    var damage = SEntMan.GetComponent<DamageableComponent>(wall).TotalDamage;
                    // a miss would read as 0 here and make the final assert lie about why it passed/failed
                    Assert.That(damage.Float(), Is.GreaterThan(0), "pellets never hit the wall");
                });
            }
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(wall), Is.True, $"{wallProto} should be gone after {shots} breaching shots");
        });
    }
}
