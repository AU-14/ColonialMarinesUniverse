#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Numerics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Damage;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(GunComponent))]
public sealed class GunComponentMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: GunComponentMergeGeneric
  components:
  - type: Gun
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 1

- type: entity
  parent: CMBaseWeaponGun
  id: GunComponentMergeCM
  components:
  - type: Clothing
    slots: [SUITSTORAGE]

- type: entity
  id: GunComponentMergeExplicit
  components:
  - type: Gun
    projectileSpeed: 23
  - type: MeleeWeapon
    damage:
      types:
        Blunt: 1
";

    [Test]
    public async Task DefaultsMapInitAndMeleeCooldownPolicyRemainDistinct()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var generic = SEntMan.SpawnEntity("GunComponentMergeGeneric", map.GridCoords);
            var cm = SEntMan.SpawnEntity("GunComponentMergeCM", map.GridCoords);
            var explicitSpeed = SEntMan.SpawnEntity("GunComponentMergeExplicit", map.GridCoords);

            try
            {
                var genericGun = SEntMan.GetComponent<GunComponent>(generic);
                var cmGun = SEntMan.GetComponent<GunComponent>(cm);
                var explicitGun = SEntMan.GetComponent<GunComponent>(explicitSpeed);

                Assert.Multiple(() =>
                {
                    Assert.That(genericGun.ProjectileSpeed, Is.EqualTo(62));
                    Assert.That(genericGun.ProjectileSpeedModified, Is.EqualTo(62),
                        "MapInit must seed the modified speed from the new generic default");
                    Assert.That(cmGun.ProjectileSpeed, Is.EqualTo(62));
                    Assert.That(cmGun.ProjectileSpeedModified, Is.EqualTo(62),
                        "CMBaseWeaponGun descendants inherit the same projectile-speed default");
                    Assert.That(explicitGun.ProjectileSpeed, Is.EqualTo(23));
                    Assert.That(explicitGun.ProjectileSpeedModified, Is.EqualTo(23),
                        "an explicit prototype speed remains authoritative through MapInit modifiers");
                    Assert.That(genericGun.MeleeCooldownOnShoot, Is.True);
                    Assert.That(cmGun.MeleeCooldownOnShoot, Is.False);
                });

                AssertMeleeLinksShotCooldown(generic, expectedLink: true);
                AssertMeleeLinksShotCooldown(cm, expectedLink: false);
                AssertShotLinksMeleeCooldown(generic, expectedLink: true);
                AssertShotLinksMeleeCooldown(cm, expectedLink: false);
            }
            finally
            {
                SEntMan.DeleteEntity(explicitSpeed);
                SEntMan.DeleteEntity(cm);
                SEntMan.DeleteEntity(generic);
            }
        });
    }

    [Test]
    public async Task SmartGunOriginReachesBothMuzzleEventsAndFireModesReplicate()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid user = default;
        EntityUid gun = default;
        NetEntity gunNet = default;

        try
        {
            await Client.WaitPost(() => Client.System<GunComponentMergeProbeSystem>().Reset());
            await Server.WaitPost(() =>
            {
                var probe = Server.System<GunComponentMergeProbeSystem>();
                probe.Reset();
                user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
                gun = SEntMan.SpawnEntity("RMCSmartGunMountedStatic", map.GridCoords);
                SEntMan.AddComponent<GunComponentMergeProbeComponent>(gun);
                gunNet = SEntMan.GetNetEntity(gun);
                Server.PlayerMan.SetAttachedEntity(session, user);
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var gunComp = SEntMan.GetComponent<GunComponent>(gun);
                Assert.Multiple(() =>
                {
                    Assert.That(gunComp.ShootOriginOffset, Is.EqualTo(new Vector2(0, -0.5f)));
                    Assert.That(gunComp.ProjectileSpeed, Is.EqualTo(62));
                    Assert.That(gunComp.ProjectileSpeedModified, Is.EqualTo(62));
                    Assert.That(gunComp.MeleeCooldownOnShoot, Is.False);
                });

                var concrete = Server.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
                var muzzle = typeof(SharedGunSystem).GetMethod(
                    "MuzzleFlash",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    [typeof(EntityUid), typeof(AmmoComponent), typeof(Angle), typeof(EntityUid?)],
                    modifiers: null);
                Assert.That(muzzle, Is.Not.Null);

                muzzle!.Invoke(concrete, [gun, new AmmoComponent(), Angle.Zero, null]);

                var before = Server.System<GunComponentMergeProbeSystem>().BeforeMuzzleOffsets;
                Assert.That(before, Is.EqualTo(new[] { new Vector2(0, -0.5f) }),
                    "the per-gun origin offset must reach RMCBeforeMuzzleFlashEvent exactly once");

                var selective = Server.System<RMCSelectiveFireSystem>();
                selective.SetFireModes(gun, SelectiveFire.SemiAuto);
                selective.AddFireMode(gun, SelectiveFire.Burst);
                Assert.Multiple(() =>
                {
                    Assert.That(gunComp.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gunComp.AvailableModes,
                        Is.EqualTo(SelectiveFire.SemiAuto | SelectiveFire.Burst));
                });
            });
            await Pair.RunTicksSync(4);

            await Client.WaitAssertion(() =>
            {
                var clientGun = CEntMan.GetEntity(gunNet);
                var gunComp = CEntMan.GetComponent<GunComponent>(clientGun);
                var muzzleOffsets = Client.System<GunComponentMergeProbeSystem>().NetworkMuzzleOffsets;
                Assert.Multiple(() =>
                {
                    Assert.That(muzzleOffsets, Is.EqualTo(new[] { new Vector2(0, -0.5f) }),
                        "the network muzzle effect must carry the same origin offset used by the pre-effect hook");
                    Assert.That(gunComp.SelectedMode, Is.EqualTo(SelectiveFire.SemiAuto));
                    Assert.That(gunComp.AvailableModes,
                        Is.EqualTo(SelectiveFire.SemiAuto | SelectiveFire.Burst),
                        "RMC selective-fire mutations must dirty and replicate the Gun state");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                if (gun.Valid && SEntMan.EntityExists(gun))
                    SEntMan.DeleteEntity(gun);
                if (user.Valid && SEntMan.EntityExists(user))
                    SEntMan.DeleteEntity(user);
            });
        }
    }

    private void AssertMeleeLinksShotCooldown(EntityUid gun, bool expectedLink)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(gun);
        var melee = SEntMan.GetComponent<MeleeWeaponComponent>(gun);
        var initialFire = Server.Timing.CurTime;
        var meleeFire = initialFire + TimeSpan.FromSeconds(5);
        gunComp.NextFire = initialFire;
        melee.NextAttack = meleeFire;

        var ev = new MeleeHitEvent([], gun, gun, new DamageSpecifier(), null);
        SEntMan.EventBus.RaiseLocalEvent(gun, ev);

        Assert.That(gunComp.NextFire, Is.EqualTo(expectedLink ? meleeFire : initialFire),
            "MeleeCooldownOnShoot controls whether a melee hit delays the next gunshot");
    }

    private void AssertShotLinksMeleeCooldown(EntityUid gun, bool expectedLink)
    {
        var gunComp = SEntMan.GetComponent<GunComponent>(gun);
        var melee = SEntMan.GetComponent<MeleeWeaponComponent>(gun);
        var initialMelee = Server.Timing.CurTime;
        var gunFire = initialMelee + TimeSpan.FromSeconds(5);
        melee.NextAttack = initialMelee;
        gunComp.NextFire = gunFire;
        var coordinates = SEntMan.GetComponent<TransformComponent>(gun).Coordinates;

        var ev = new GunShotEvent(gun, [], coordinates, coordinates);
        SEntMan.EventBus.RaiseLocalEvent(gun, ref ev);

        Assert.That(melee.NextAttack, Is.EqualTo(expectedLink ? gunFire : initialMelee),
            "MeleeCooldownOnShoot controls whether firing delays the next melee attack");
    }
}

[RegisterComponent]
public sealed partial class GunComponentMergeProbeComponent : Component;

public sealed partial class GunComponentMergeProbeSystem : EntitySystem
{
    public readonly List<Vector2> BeforeMuzzleOffsets = new();
    public readonly List<Vector2> NetworkMuzzleOffsets = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunComponentMergeProbeComponent, RMCBeforeMuzzleFlashEvent>(OnBeforeMuzzleFlash);
        SubscribeAllEvent<MuzzleFlashEvent>(OnMuzzleFlash);
    }

    public void Reset()
    {
        BeforeMuzzleOffsets.Clear();
        NetworkMuzzleOffsets.Clear();
    }

    private void OnBeforeMuzzleFlash(
        Entity<GunComponentMergeProbeComponent> entity,
        ref RMCBeforeMuzzleFlashEvent args)
    {
        BeforeMuzzleOffsets.Add(args.Offset);
    }

    private void OnMuzzleFlash(MuzzleFlashEvent args)
    {
        NetworkMuzzleOffsets.Add(args.OriginOffset);
    }
}

#pragma warning restore RA0002
