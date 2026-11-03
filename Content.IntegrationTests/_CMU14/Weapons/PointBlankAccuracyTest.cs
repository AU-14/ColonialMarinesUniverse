#pragma warning disable RA0002 // the point blank test sets the gun target directly

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class PointBlankAccuracyTest : GameTest
{
    private const string Shotgun = "RMCWeaponShotgunM42A2Filled";
    private const string BuckshotPellet = "CMPelletShotgunBuckshot";
    private const string Xeno = "CMXenoDrone";
    private const int PelletCount = 4;

    // buckshot pellets inherit their accuracy from CMBulletBase
    private const float PelletAccuracy = 60f;

    [Test]
    public async Task PointBlankShotDoublesPelletAccuracy()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var shotgun = SEntMan.SpawnEntity(Shotgun, map.GridCoords);
            var shooter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // one tile away, which is inside the point blank range of the shotgun
            var xeno = SEntMan.SpawnEntity(Xeno, map.GridCoords.Offset(Vector2.UnitX));

            try
            {
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(shooter, shotgun), Is.True);

                var pointBlank = SEntMan.GetComponent<GunPointBlankComponent>(shotgun);
                Assert.That((float) pointBlank.AccuracyMultiplier, Is.GreaterThan(1f),
                    "point blank shots are meant to gain accuracy");

                var pellets = FirePellets(shotgun, map, xeno);
                var weaponMultiplier = SEntMan.GetComponent<RMCWeaponAccuracyComponent>(shotgun).ModifiedAccuracyMultiplier;
                var expected = FixedPoint2.New(PelletAccuracy) * weaponMultiplier * pointBlank.AccuracyMultiplier;

                foreach (var pellet in pellets)
                {
                    Assert.That(SEntMan.TryGetComponent(pellet, out RMCProjectileAccuracyComponent? accuracy),
                        Is.True, "buckshot pellets carry an accuracy score");
                    Assert.That(accuracy!.Accuracy, Is.EqualTo(expected),
                        $"a point blank pellet should be at {expected} instead of the unboosted " +
                        $"{FixedPoint2.New(PelletAccuracy) * weaponMultiplier}");
                }
            }
            finally
            {
                SEntMan.DeleteEntity(shooter);
                if (!SEntMan.Deleted(shotgun))
                    SEntMan.DeleteEntity(shotgun);
                if (!SEntMan.Deleted(xeno))
                    SEntMan.DeleteEntity(xeno);
            }
        });
    }

    [Test]
    public async Task RangedShotKeepsPelletAccuracy()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var shotgun = SEntMan.SpawnEntity(Shotgun, map.GridCoords);
            var shooter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // five tiles away, which is well outside the point blank range of the shotgun
            var xeno = SEntMan.SpawnEntity(Xeno, map.GridCoords.Offset(Vector2.UnitX * 5));

            try
            {
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(shooter, shotgun), Is.True);

                var pellets = FirePellets(shotgun, map, xeno);
                var weaponMultiplier = SEntMan.GetComponent<RMCWeaponAccuracyComponent>(shotgun).ModifiedAccuracyMultiplier;
                var expected = FixedPoint2.New(PelletAccuracy) * weaponMultiplier;

                foreach (var pellet in pellets)
                {
                    Assert.That(SEntMan.TryGetComponent(pellet, out RMCProjectileAccuracyComponent? accuracy),
                        Is.True, "buckshot pellets carry an accuracy score");
                    Assert.That(accuracy!.Accuracy, Is.EqualTo(expected),
                        "only point blank shots get the accuracy bonus");
                }
            }
            finally
            {
                SEntMan.DeleteEntity(shooter);
                if (!SEntMan.Deleted(shotgun))
                    SEntMan.DeleteEntity(shotgun);
                if (!SEntMan.Deleted(xeno))
                    SEntMan.DeleteEntity(xeno);
            }
        });
    }

    /// <summary>
    ///     Fires a buckshot load through the gun's shot event. The pellets spawn ten tiles away from the target on
    ///     purpose: the point blank hit itself consumes the pellets it lands on, and their accuracy has to stay
    ///     readable for the assertions.
    /// </summary>
    private List<EntityUid> FirePellets(EntityUid shotgun, TestMapData map, EntityUid target)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(shotgun);
        gunComp.Target = target;

        var pellets = new List<EntityUid>(PelletCount);
        for (var i = 0; i < PelletCount; i++)
            pellets.Add(SEntMan.SpawnEntity(BuckshotPellet, map.GridCoords.Offset(Vector2.UnitX * 10)));

        SEntMan.EventBus.RaiseLocalEvent(shotgun, new AmmoShotEvent { FiredProjectiles = pellets });
        return pellets;
    }
}
