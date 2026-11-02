#pragma warning disable RA0002 // Compress fixture durations; exercise real gun, damage and interaction systems.

using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking.Events;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Sentry;
using Content.Shared._RMC14.Vents;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Burrow;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.Sentry;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Friction;
using Content.Shared.GameTicking;
using Content.Shared.Mobs.Components;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
[TestOf(typeof(CMUFobDefenseSystem))]
public sealed partial class CMUFobDefenseTest : InteractionTest
{
    [TestPrototypes]
    private const string TargetPrototype = """
- type: entity
  id: CMUFobTestTarget
  components:
  - type: MobState
  - type: NpcFactionMember
    factions: [RMCXeno]
  - type: Damageable
  - type: Injurable
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeCircle
          radius: 0.2
        hard: true
        layer: [BulletImpassable]
        mask: [MidImpassable]
""";

    private Entity<CMUFobNetworkComponent> _network;
    private EntityUid _mount;
    private EntityUid _secondMount;
    private EntityUid _control;
    private EntityUid _gun;
    private EntityUid _victim;
    private EntityCoordinates _gunCoords;
    private CMUFobReserveSystem Reserve => Server.System<CMUFobReserveSystem>();
    private CMUFobDefenseSystem Defense => Server.System<CMUFobDefenseSystem>();
    private SharedGunSystem Guns => Server.System<SharedGunSystem>();
    private DamageableSystem Damage => Server.System<DamageableSystem>();
    private CMUFobSupportGunComponent Gun => SEntMan.GetComponent<CMUFobSupportGunComponent>(_gun);
    private int Ammo => Guns.GetAmmoCount(_gun);

    private void Hurt(EntityUid uid, int amount)
        => Damage.TryChangeDamage(uid, new DamageSpecifier { DamageDict = new() { ["Blunt"] = amount } }, ignoreResistances: true);

    private EntityUid Magazine(EntityUid gun)
        => Server.System<SharedContainerSystem>().GetContainer(gun, "gun_magazine").ContainedEntities[0];

    private void SetAmmo(EntityUid magazine, int count)
        => Guns.SetBallisticUnspawned((magazine, SEntMan.GetComponent<BallisticAmmoProviderComponent>(magazine)), count);

    private async Task SetupGuns(int ammo = 20)
    {
        await SetTile(Plating, TargetCoords);
        await SpawnTarget("CMUFobControl");
        NetCoordinates second = default;
        await Server.WaitPost(() => second = SEntMan.GetNetCoordinates(SEntMan.GetCoordinates(TargetCoords).Offset(new Vector2(0, 8))));
        await SetTile(Plating, second);
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<MarineComponent>(SPlayer).Faction = "govfor";
            SEntMan.EnsureComponent<MobStateComponent>(SPlayer);
            Server.System<SkillsSystem>().SetSkill(SPlayer, "RMCSkillConstruction", 3);
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
            _control = STarget!.Value;
            Assert.That(Reserve.TryCreateNetwork(_control, "govfor", out _network), Is.True);
            _network.Comp.StartupDuration = TimeSpan.FromSeconds(0.6);
            _network.Comp.MinimumActivation = TimeSpan.FromSeconds(0.8);
            _network.Comp.RestartDelay = TimeSpan.FromSeconds(0.2);
            _network.Comp.StoredReserve = TimeSpan.FromSeconds(60);
            Assert.That(Server.System<CMUFobInteractionSystem>().TryBindControl((_control,
                SEntMan.GetComponent<CMUFobControlComponent>(_control)), out _), Is.True);
            _gunCoords = SEntMan.GetCoordinates(TargetCoords);
            _mount = SEntMan.SpawnAtPosition("CMUFobGunMount", _gunCoords);
            _secondMount = SEntMan.SpawnAtPosition("CMUFobGunMount", SEntMan.GetCoordinates(second));
            Transform.SetWorldRotation(_mount, Vector2.UnitX.ToWorldAngle());
            Transform.SetWorldRotation(_secondMount, Vector2.UnitX.ToWorldAngle());
            Assert.That(Defense.TryRegisterMounts(_network, _mount, _secondMount), Is.True);
            _gun = SEntMan.GetComponent<CMUFobGunMountComponent>(_mount).Gun!.Value;
            Gun.AcquisitionDelay = TimeSpan.FromSeconds(0.5);
            Gun.ReloadDelay = TimeSpan.FromSeconds(0.4);
            SetAmmo(Magazine(_gun), ammo);
            Target = SEntMan.GetNetEntity(_gun);
            _victim = SEntMan.SpawnAtPosition("CMUFobTestTarget", _gunCoords.Offset(new Vector2(3, 0)));
        });
    }

    [Test]
    public async Task DisabledAndUnregisteredWeaponsRejectForcedFiring()
    {
        await SetTile(Plating, TargetCoords);
        await SpawnTarget("CMUFobSupportGun");
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, false);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
            var gun = STarget!.Value;
            Assert.That(SEntMan.TrySpawnInContainer("CMUFob30mmMagazine", gun, "gun_magazine", out _), Is.True);
            var before = Guns.GetAmmoCount(gun);
            var target = SEntMan.SpawnAtPosition("CMUFobTestTarget", SEntMan.GetCoordinates(TargetCoords).Offset(new Vector2(3, 0)));
            Server.System<SentrySystem>().TrySetMode((gun, SEntMan.GetComponent<SentryComponent>(gun)), SentryMode.On);
            Assert.That(Guns.AttemptShoot(gun, (gun, SEntMan.GetComponent<GunComponent>(gun)),
                SEntMan.GetComponent<TransformComponent>(target).Coordinates, target), Is.False);
            Assert.That(Reserve.TryCreateNetwork(gun, "govfor", out _), Is.False);
            Assert.That(Guns.GetAmmoCount(gun), Is.EqualTo(before));
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, true);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(2));
            SEntMan.GetComponent<GunComponent>(gun).NextFire = TimeSpan.Zero;
            Assert.That(Guns.AttemptShoot(gun, (gun, SEntMan.GetComponent<GunComponent>(gun)),
                SEntMan.GetComponent<TransformComponent>(target).Coordinates, target), Is.False,
                "Enabling the feature cannot authorize a gun outside its two registered mounts.");
            Assert.That(Guns.GetAmmoCount(gun), Is.EqualTo(before));
        });
        await RunSeconds(2);
    }

    [Test]
    public async Task TwoMountsWarnBeforeFiringAndExhaustFiniteAmmoWithoutRefills()
    {
        await SetupGuns(ammo: 3);
        await Server.WaitAssertion(() =>
        {
            var extra = SEntMan.SpawnAtPosition("CMUFobGunMount", _gunCoords);
            Assert.That(Defense.TryRegisterMounts(_network, _mount, extra), Is.False);
            Assert.That(SEntMan.GetComponent<CMUFobGunMountComponent>(extra).Gun, Is.Null,
                "A rejected third mount must not mint another gun or magazine.");
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo, Is.EqualTo(3), "Startup cannot fire.");
            Assert.That(SEntMan.System<SharedAppearanceSystem>().TryGetData<bool>(_gun, CMUFobGunVisuals.Warning, out var warning)
                && warning, Is.True);
        });
        await RunSeconds(0.7f);
        await Server.WaitAssertion(() => Assert.That(Ammo, Is.EqualTo(3), "A new target needs its acquisition warning too."));
        await RunSeconds(1.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo, Is.Zero);
            Assert.That(Damage.GetTotalDamage(_victim), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(Reserve.TryDeactivate(_network), Is.True);
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() => Assert.That(Reserve.TryActivate(_network), Is.True));
        await RunSeconds(1.8f);
        await Server.WaitAssertion(() => Assert.That(Ammo, Is.Zero, "Activation must never refill magazines."));
    }

    [Test]
    public async Task HeavyGunTracksAndFiresAroundItsFixedMount()
    {
        await SetupGuns(ammo: 30);
        await Server.WaitAssertion(() =>
        {
            var secondGun = SEntMan.GetComponent<CMUFobGunMountComponent>(_secondMount).Gun!.Value;
            SetAmmo(Magazine(secondGun), 0);
            Transform.SetCoordinates(SPlayer, _gunCoords.Offset(new Vector2(1, 1)));
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(1.4f);

        foreach (var position in new[] {new Vector2(0, 3), new Vector2(-3, 0), new Vector2(0, -3)})
        {
            var before = 0;
            var damage = FixedPoint2.Zero;
            await Server.WaitPost(() =>
            {
                before = Ammo;
                damage = Damage.GetTotalDamage(_victim);
                Transform.SetCoordinates(_victim, _gunCoords.Offset(position));
            });
            await RunSeconds(0.9f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Gun.Target, Is.EqualTo(_victim), $"Tracking must continue at {position}.");
                Assert.That(Ammo, Is.LessThan(before), $"The heavy gun must fire at {position}.");
                Assert.That(Damage.GetTotalDamage(_victim), Is.GreaterThan(damage));
                Assert.That(Transform.GetWorldRotation(_mount), Is.EqualTo(Vector2.UnitX.ToWorldAngle()),
                    "Aiming must not turn the foundation or invalidate its registered pose.");
            });
        }
    }

    [Test]
    public async Task GeometryConcealmentAndIffLossRequireFreshAcquisition()
    {
        await SetupGuns(ammo: 50);
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _network.Comp.StartupDuration = TimeSpan.FromSeconds(0.1);
            Gun.AcquisitionDelay = TimeSpan.FromSeconds(0.8);
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(0.3f);
        foreach (var cause in new[] { "range", "wall", "door", "burrow", "vent", "map", "friendly" })
        {
            var before = 0;
            EntityUid? obstruction = null;
            await Server.WaitPost(() =>
            {
                before = Ammo;
                switch (cause)
                {
                    case "range": Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(8, 0))); break;
                    case "wall": obstruction = SEntMan.SpawnAtPosition("CMWallMetal", _gunCoords.Offset(new Vector2(2, 0))); break;
                    case "door": obstruction = SEntMan.SpawnAtPosition("CMAirlock", _gunCoords.Offset(new Vector2(2, 0))); break;
                    case "burrow": SEntMan.EnsureComponent<XenoBurrowComponent>(_victim).Active = true; break;
                    case "vent": SEntMan.EnsureComponent<VentCrawlingComponent>(_victim); break;
                    case "map": Transform.SetCoordinates(_victim, otherMap.GridCoords); break;
                    case "friendly": Server.System<GunIFFSystem>().SetUserFaction(_victim, "GOVFOR"); break;
                }
            });
            await RunSeconds(1.1f);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Ammo, Is.EqualTo(before), cause);
                Assert.That(Gun.Target, Is.Null, cause);
                if (obstruction != null)
                    SEntMan.DeleteEntity(obstruction.Value);
                if (cause == "burrow") SEntMan.RemoveComponent<XenoBurrowComponent>(_victim);
                if (cause == "vent") SEntMan.RemoveComponent<VentCrawlingComponent>(_victim);
                if (cause == "friendly") Server.System<GunIFFSystem>().SetUserFaction(_victim, "OPFOR");
                Transform.SetCoordinates(_victim, _gunCoords.Offset(new Vector2(3, 0)));
            });
            await RunSeconds(0.25f);
            await Server.WaitAssertion(() => Assert.That(Ammo, Is.EqualTo(before), $"{cause}: returning needs a fresh warning"));
            await RunSeconds(0.85f);
            await Server.WaitAssertion(() => Assert.That(Ammo, Is.LessThan(before), $"{cause}: valid reacquisition must resume firing"));
        }
    }

    [Test]
    public async Task PoweredGunTakesProjectileDamageAndItsWreckAllowsShotsAndMovementThrough()
    {
        await SetupGuns(ammo: 0);
        await Server.WaitAssertion(() => Assert.That(Reserve.TryActivate(_network), Is.True));
        await RunSeconds(0.8f);
        async Task ShootThroughGun()
        {
            await Server.WaitPost(() =>
            {
                var bullet = SEntMan.SpawnAtPosition("BulletRifle10x24mm", _gunCoords.Offset(new Vector2(-2, 0)));
                Guns.ShootProjectile(bullet, Vector2.UnitX, Vector2.Zero, null, speed: 20);
            });
            await RunSeconds(0.5f);
        }
        await ShootThroughGun();
        var victimDamage = FixedPoint2.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Active));
            Assert.That(Damage.GetTotalDamage(_gun), Is.GreaterThan(FixedPoint2.Zero));
            Hurt(_gun, 220);
            Assert.That(SEntMan.HasComponent<CMUSentryBrokenComponent>(_gun), Is.True);
            victimDamage = Damage.GetTotalDamage(_victim);
        });
        await ShootThroughGun();
        EntityUid mover = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Damage.GetTotalDamage(_victim), Is.GreaterThan(victimDamage), "A wreck must not intercept projectiles.");
            mover = SEntMan.SpawnAtPosition("CMUFobTestTarget", _gunCoords.Offset(new Vector2(-1, 0)));
            // Keep the collision probe moving; tile friction would otherwise stop it short of the wreck.
            SEntMan.EnsureComponent<TileFrictionModifierComponent>(mover).Modifier = 0;
            Server.System<SharedPhysicsSystem>().SetLinearVelocity(mover, new Vector2(3, 0));
        });
        await RunSeconds(0.8f);
        await Server.WaitAssertion(() => Assert.That(Transform.GetWorldPosition(mover).X,
            Is.GreaterThan(Transform.GetWorldPosition(_gun).X + 0.5f), "The broken gun cannot form an immortal wall."));
    }

    [Test]
    public async Task MaterialRepairReloadAndReplacementCannotMintAmmoOrBypassRestart()
    {
        await SetupGuns(ammo: 8);
        var afterBreak = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Hurt(_gun, 220);
            afterBreak = Ammo;
            Server.System<TagSystem>().AddTag(SPlayer, "InstantDoAfters");
        });
        foreach (var tool in new[] { "CMWirecutter", "RMCWelderAdmin", "RMCCableCoil", "CMScrewdriver", "CMMultitool", "RMCWelderAdmin" })
        {
            var item = await PlaceInHands(tool, quantity: tool == "RMCCableCoil" ? 30 : 1);
            await Interact();
            if (tool == "RMCCableCoil")
                await Server.WaitAssertion(() => Assert.That(SEntMan.GetComponent<StackComponent>(SEntMan.GetEntity(item)).Count,
                    Is.LessThan(30), "Repair must consume material instead of producing free hardware."));
        }
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUSentryBrokenComponent>(_gun), Is.False);
            Assert.That(Gun.RequiresRestart, Is.True);
            Assert.That(Ammo, Is.EqualTo(afterBreak), "Repair must neither refill nor resume firing.");
            Server.System<TagSystem>().RemoveTag(SPlayer, "InstantDoAfters");
            Assert.That(Reserve.TryDeactivate(_network), Is.True);
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() => Assert.That(Reserve.TryActivate(_network), Is.True));
        await RunSeconds(1.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo, Is.LessThan(afterBreak));
            Assert.That(Reserve.TryDeactivate(_network), Is.True);
            SetAmmo(Magazine(_gun), 0);
        });
        var newDrum = SEntMan.GetEntity(await PlaceInHands("CMUFob30mmMagazine"));
        await Server.WaitAssertion(() =>
        {
            SetAmmo(newDrum, 4);
            SEntMan.GetComponent<MarineComponent>(SPlayer).Faction = "opfor";
            Assert.That(Defense.TryStartReload((_gun, Gun), SPlayer, newDrum), Is.False);
            SEntMan.GetComponent<MarineComponent>(SPlayer).Faction = "govfor";
            Assert.That(Defense.TryStartReload((_gun, Gun), SPlayer, newDrum), Is.True);
        });
        await RunSeconds(0.7f);
        var remaining = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Magazine(_gun), Is.EqualTo(newDrum));
            Assert.That(Ammo, Is.EqualTo(4));
            remaining = Reserve.GetRemaining(_network);
            SEntMan.DeleteEntity(_gun);
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            var replacement = SEntMan.GetComponent<CMUFobGunMountComponent>(_mount).Gun!.Value;
            Assert.That(replacement, Is.Not.EqualTo(_gun));
            Assert.That(SEntMan.HasComponent<CMUSentryBrokenComponent>(replacement), Is.True);
            Assert.That(Guns.GetAmmoCount(replacement), Is.Zero);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(remaining));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DepletionOrBrokenControlCancelsAcquisition(bool breakControl)
    {
        await SetupGuns();
        await Server.WaitAssertion(() =>
        {
            _network.Comp.StartupDuration = TimeSpan.FromSeconds(0.1);
            Gun.AcquisitionDelay = TimeSpan.FromSeconds(1);
            if (!breakControl)
                _network.Comp.StoredReserve = TimeSpan.FromSeconds(0.5);
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Gun.Target, Is.EqualTo(_victim));
            if (breakControl)
                Hurt(_control, 150);
        });
        await RunSeconds(1.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Ammo, Is.EqualTo(20));
            Assert.That(Gun.Target, Is.Null);
            Assert.That(Damage.GetTotalDamage(_victim), Is.EqualTo(FixedPoint2.Zero));
        });
    }
}
