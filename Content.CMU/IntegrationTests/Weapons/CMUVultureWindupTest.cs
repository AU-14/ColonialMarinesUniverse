#pragma warning disable RA0002 // reads the shooter's damage straight off the component

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Content.Shared._RMC14.Wieldable.Components;
using Content.Shared.CMU14.Weapons.Ranged.Vulture;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
[TestOf(typeof(CMUVultureWindupSystem))]
public sealed class CMUVultureWindupTest : GameTest
{
    private const string Vulture = "RMCWeaponSniperM707Vulture";

    private EntityUid _shooter;
    private EntityUid _target;
    private EntityUid _gun;

    private SharedGunSystem Guns => Server.System<SharedGunSystem>();

    private async Task Setup()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _shooter = SSpawnAtPosition("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<VultureWhitelistComponent>(_shooter);
            _target = SSpawnAtPosition("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 8)));

            _gun = SSpawnAtPosition(Vulture, map.GridCoords);
            // the 6s wield lockout isn't what's under test here
            SEntMan.RemoveComponent<WieldDelayComponent>(_gun);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(_shooter, _gun), Is.True);
            Assert.That(Server.System<SharedWieldableSystem>().TryWield(_gun, _shooter), Is.True);
        });
        await Pair.RunSeconds(0.2f);
    }

    private void Pull()
    {
        var gun = SEntMan.GetComponent<GunComponent>(_gun);
        var aim = SEntMan.GetComponent<TransformComponent>(_target).Coordinates;
        Assert.That(Guns.AttemptShoot((_gun, gun), _shooter, aim), Is.Null, "the trigger pull itself never fires");
    }

    [Test]
    public async Task ShotWaitsForTheWindupAndMarksTheTarget()
    {
        await Setup();
        await Server.WaitAssertion(() =>
        {
            Pull();
            Assert.That(Guns.GetAmmoCount(_gun), Is.EqualTo(4));
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(_target), Is.True, "target should get the laser mark");
        });

        await Pair.RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(Guns.GetAmmoCount(_gun), Is.EqualTo(4), "still settling at 1s"));

        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Guns.GetAmmoCount(_gun), Is.EqualTo(3), "fires once the 1.5s windup is done");
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(_target), Is.False, "mark clears after the shot");
        });
    }

    [Test]
    public async Task MovingBreaksTheWindup()
    {
        await Setup();
        await Server.WaitAssertion(() =>
        {
            Pull();
            var xform = Server.System<SharedTransformSystem>();
            xform.SetCoordinates(_shooter, SEntMan.GetComponent<TransformComponent>(_shooter).Coordinates.Offset(new Vector2(1, 0)));
        });

        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Guns.GetAmmoCount(_gun), Is.EqualTo(4));
            Assert.That(SEntMan.GetComponent<CMUVultureWindupComponent>(_gun).WindupEndsAt, Is.Null);
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(_target), Is.False);
        });
    }

    [Test]
    public async Task UnbracedShotHurtsButDoesNotCritTheShooter()
    {
        await Setup();
        await Server.WaitAssertion(Pull);
        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Guns.GetAmmoCount(_gun), Is.EqualTo(3));
            var damage = SEntMan.GetComponent<DamageableComponent>(_shooter).Damage;
            // recoil is a price, not half your health: one unbraced shot used to cost 95 of the 100 to crit
            Assert.That(damage.DamageDict["Blunt"].Float(), Is.GreaterThan(0f).And.LessThan(50f));
        });
    }
}
