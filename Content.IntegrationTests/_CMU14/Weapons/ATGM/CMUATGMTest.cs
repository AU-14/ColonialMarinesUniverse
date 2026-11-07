#pragma warning disable RA0002 // Arrange guided-missile state directly to exercise the server guidance validator.
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Weapons.ATGM;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Marines.Skills.Pamphlets;
using Content.Shared._RMC14.Targeting;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.Weapons.ATGM;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Storage;
using Content.Shared.Trigger.Systems;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Weapons.ATGM;

[TestFixture]
public sealed class CMUATGMTest : GameTest
{
    private const string Skill = "CMUSkillATGM";
    private static readonly EntProtoId ProjectileId = "CMUProjectileATGM";


    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task OnlyGunshipATGMCanFireWithoutSkillOrFinder()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var transform = Server.System<SharedTransformSystem>();

            bool Attempt(EntProtoId mountId, bool skilled)
            {
                var mount = SEntMan.SpawnEntity(mountId, map.GridCoords);
                Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);
                var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                if (skilled)
                    Server.System<SkillsSystem>().SetSkill(gunner, Skill, 1);
                var from = transform.GetMoverCoordinates(mount);
                var aim = from.Offset(transform.GetWorldRotation(mount).ToWorldVec() * 3);
                var ev = new AttemptShootEvent(gunner, null, from, aim);
                SEntMan.EventBus.RaiseLocalEvent(launcher, ref ev);
                return ev.Cancelled;
            }

            Assert.Multiple(() =>
            {
                Assert.That(Attempt("CMUATGMMount", false), Is.True,
                    "The ground tripod still requires ATGM skill.");
                Assert.That(Attempt("CMUATGMMount", true), Is.True,
                    "A trained ground gunner still needs a separate spotter with the linked finder.");
                Assert.That(Attempt("CMUATGMMountDropship", false), Is.False,
                    "The gunship hardpoint can be fired and guided without a spotter or ATGM skill.");
            });
        });
    }

    [Test]
    public async Task GroundTripodGuidanceStillUsesSkilledFinderHolder()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var skills = Server.System<SkillsSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var mount = SEntMan.SpawnEntity("CMUATGMMount", map.GridCoords);
            Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);

            var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var spotter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(gunner, Skill, 1);
            skills.SetSkill(spotter, Skill, 1);

            var finder = SEntMan.SpawnEntity("CMUATGMFinder", map.GridCoords);
            atgm.Link(launcher, (finder, SEntMan.GetComponent<CMUATGMFinderComponent>(finder)));
            Assert.That(hands.TryPickupAnyHand(spotter, finder), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(atgm.TryGetSpotter(launcher, gunner, out var actualSpotter, out var actualFinder, out _), Is.True);
                Assert.That(actualSpotter, Is.EqualTo(spotter));
                Assert.That(actualFinder, Is.EqualTo(finder));
                Assert.That(atgm.CanGuide(spotter, finder), Is.True);
                Assert.That(atgm.CanGuide(gunner, finder), Is.False);
            });
        });
    }

    [Test]
    public async Task GuidanceOnlyAcceptsValidUpdatesFromTheGuider()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            var transform = Server.System<SharedTransformSystem>();

            var guider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var stranger = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // No physics body, so the steering loop leaves this stand-in alone.
            var missileUid = SEntMan.SpawnEntity(null, map.GridCoords);
            var missile = SEntMan.AddComponent<CMUGuidedMissileComponent>(missileUid);
            missile.Guider = guider;
            var original = new MapCoordinates(transform.GetMapCoordinates(guider).Position, map.MapId);
            missile.Target = original;

            var aim = map.GridCoords.Offset(new Vector2(2, 0));
            Assert.Multiple(() =>
            {
                Assert.That(guidance.TryApplyGuidance(stranger, aim), Is.False,
                    "Only the missile's guider may steer it.");
                Assert.That(missile.Target, Is.EqualTo(original));

                Assert.That(guidance.TryApplyGuidance(guider, new EntityCoordinates(map.Grid, new Vector2(float.NaN, 0))), Is.False,
                    "Non-finite aim points are rejected.");
                Assert.That(missile.Target, Is.EqualTo(original));
            });

            Assert.That(guidance.TryApplyGuidance(guider, aim), Is.True);
            var expected = transform.ToMapCoordinates(aim).Position;
            Assert.That((missile.Target.Position - expected).Length(), Is.LessThan(0.001f));

            Assert.That(missile.HasGuidance, Is.True, "An accepted update enables cursor guidance.");

            // FLOW-06: back-to-back updates are all accepted and the newest wins.
            var newer = aim.Offset(new Vector2(1, 0));
            Assert.That(guidance.TryApplyGuidance(guider, newer), Is.True,
                "An update right after another must not be dropped.");
            Assert.That((missile.Target.Position - transform.ToMapCoordinates(newer).Position).Length(), Is.LessThan(0.001f));

            Assert.That(guidance.TryApplyGuidance(guider, map.GridCoords.Offset(new Vector2(500, 0))), Is.True);
            var reach = (missile.Target.Position - transform.GetMapCoordinates(guider).Position).Length();
            Assert.That(reach, Is.LessThanOrEqualTo(missile.MaxGuidanceDistance + 0.001f),
                "Aim points are clamped to the guidance distance.");

            SEntMan.DeleteEntity(missileUid);
        });
    }

    [Test]
    public async Task MissileTracksLowerZLevelTargetsAndAcceptsCursorGuidanceThere()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var network = zLevels.CreateZNetwork();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            try
            {
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

                var guider = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(upper, Vector2.Zero));
                var target = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(lower, new Vector2(4, 0)));
                var missileUid = SEntMan.SpawnEntity(ProjectileId, new EntityCoordinates(upper, new Vector2(1, 0)));
                var missile = SEntMan.EnsureComponent<CMUGuidedMissileComponent>(missileUid);
                missile.Guider = guider;
                missile.TargetEntity = target;
                missile.TargetLastSeen = SGameTiming.CurTime;
                missile.NextSightCheck = SGameTiming.CurTime + TimeSpan.FromSeconds(1);
                missile.ArmedAt = SGameTiming.CurTime + TimeSpan.FromSeconds(1);
                missile.DetonateAt = SGameTiming.CurTime + TimeSpan.FromSeconds(10);

                var lowerAim = new EntityCoordinates(lower, new Vector2(5, 0));
                Assert.That(guidance.TryApplyGuidance(guider, lowerAim), Is.True,
                    "A guider may send a cursor position on a lower map in the same z-level network.");

                guidance.Update(0.1f);

                Assert.Multiple(() =>
                {
                    Assert.That(missile.TrackingFrozen, Is.False,
                        "A target on a lower z-level remains trackable.");
                    Assert.That(missile.TrackedPosition.MapId, Is.EqualTo(transform.GetMapId(target)));
                    Assert.That(missile.Target.MapId, Is.EqualTo(transform.GetMapId(target)));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(upper);
                SEntMan.DeleteEntity(lower);
                SEntMan.DeleteEntity(network);
            }
        });
    }

    [Test]
    public async Task DownwardShotLaunchesGuidedMissileOnLowerZLevel()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var zLevels = Server.System<CMUZLevelsSystem>();
            var shooting = Server.System<CMUZLevelShootingSystem>();
            var atgm = Server.System<CMUATGMSystem>();
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var network = zLevels.CreateZNetwork();
            var lower = maps.CreateMap(runMapInit: true);
            var upper = maps.CreateMap(runMapInit: true);
            try
            {
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

                var source = new EntityCoordinates(upper, Vector2.Zero);
                var target = new EntityCoordinates(lower, new Vector2(4, 0));
                var gunner = SEntMan.SpawnEntity("CMMobHuman", source);
                shooting.SetShootDown(gunner, true);

                Assert.That(shooting.TryAdjustShotCoordinates(gunner, source, target, out var adjustedFrom, out var adjustedTo), Is.True);
                Assert.That(transform.ToMapCoordinates(adjustedFrom).MapId, Is.EqualTo(transform.GetMapId(lower)),
                    "The standard downward-shot path places the projectile on the lower map.");

                var mount = SEntMan.SpawnEntity("CMUATGMMountDropship", source);
                Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);
                SEntMan.GetComponent<GunComponent>(launcher).ShootCoordinates = adjustedTo;

                var targetEntity = SEntMan.SpawnEntity("CMMobHuman", target);
                var projectile = SEntMan.SpawnEntity(ProjectileId, adjustedFrom);
                SEntMan.GetComponent<ProjectileComponent>(projectile).Shooter = gunner;
                SEntMan.EventBus.RaiseLocalEvent(launcher.Owner,
                    new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { projectile } });

                Assert.That(transform.GetMapId(projectile), Is.EqualTo(transform.GetMapId(lower)));
                Assert.That(guidance.TryApplyGuidance(gunner, target), Is.True,
                    "The launched missile accepts cursor guidance on the lower map.");
                Assert.That(SEntMan.GetComponent<CMUGuidedMissileComponent>(projectile).Target.MapId,
                    Is.EqualTo(transform.GetMapId(lower)));

                SEntMan.DeleteEntity(targetEntity);
                SEntMan.DeleteEntity(projectile);
            }
            finally
            {
                SEntMan.DeleteEntity(upper);
                SEntMan.DeleteEntity(lower);
                SEntMan.DeleteEntity(network);
            }
        });
    }

    [Test]
    public async Task QueuedGuidanceKeepsTheNewestPoint()
    {
        var map = await Pair.CreateTestMap();
        EntityUid missileUid = default;
        CMUGuidedMissileComponent missile = default!;
        EntityCoordinates newest = default;

        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            var guider = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // No physics body, so the steering loop leaves this stand-in alone.
            missileUid = SEntMan.SpawnEntity(null, map.GridCoords);
            missile = SEntMan.AddComponent<CMUGuidedMissileComponent>(missileUid);
            missile.Guider = guider;

            newest = map.GridCoords.Offset(new Vector2(0, 3));
            guidance.QueueGuidance(guider, map.GridCoords.Offset(new Vector2(3, 0)));
            guidance.QueueGuidance(guider, map.GridCoords.Offset(new Vector2(-3, 0)));
            guidance.QueueGuidance(guider, newest);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            Assert.That(missile.HasGuidance, Is.True);
            Assert.That((missile.Target.Position - transform.ToMapCoordinates(newest).Position).Length(), Is.LessThan(0.001f),
                "A burst of updates in one tick must apply the newest, not drop it.");

            SEntMan.DeleteEntity(missileUid);
        });
    }

    [Test]
    public async Task SpotterLockValidatesAndClears()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var skills = Server.System<SkillsSystem>();

            var mount = SEntMan.SpawnEntity("CMUATGMMount", map.GridCoords);
            Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);

            var spotter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var finderUid = SEntMan.SpawnEntity("CMUATGMFinder", map.GridCoords);
            var finderComp = SEntMan.GetComponent<CMUATGMFinderComponent>(finderUid);
            Entity<CMUATGMFinderComponent> finder = (finderUid, finderComp);
            Assert.That(hands.TryPickupAnyHand(spotter, finderUid), Is.True);

            Assert.That(atgm.TryLock(finder, spotter, target), Is.False, "An unlinked finder cannot lock.");

            atgm.Link(launcher, finder);
            Assert.That(atgm.TryLock(finder, spotter, target), Is.False, "An unskilled spotter cannot lock.");

            skills.SetSkill(spotter, Skill, 1);
            Assert.Multiple(() =>
            {
                Assert.That(atgm.TryLock(finder, spotter, null), Is.False, "Clicking an empty tile locks nothing.");
                Assert.That(atgm.TryLock(finder, spotter, spotter), Is.False, "The spotter cannot lock themselves.");
                Assert.That(atgm.TryLock(finder, spotter, mount), Is.False, "The spotter cannot lock their own tripod.");
                Assert.That(atgm.TryLock(finder, spotter, launcher), Is.False, "The spotter cannot lock their own launcher.");
                Assert.That(atgm.TryLock(finder, spotter, finderUid), Is.False, "The spotter cannot lock the finder.");
                Assert.That(finderComp.LockedTarget, Is.Null);
            });

            // The crew and their kit are never valid targets: the missile would home on its own team.
            var carried = SEntMan.SpawnEntity("CMUATGMMissile", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(spotter, carried), Is.True);
            var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.GetComponent<WeaponMountComponent>(mount).User = gunner;
            Assert.Multiple(() =>
            {
                Assert.That(atgm.TryLock(finder, spotter, carried), Is.False, "The spotter cannot lock gear they carry.");
                Assert.That(atgm.TryLock(finder, spotter, gunner), Is.False, "The spotter cannot lock their gunner.");
                Assert.That(finderComp.LockedTarget, Is.Null);
            });
            SEntMan.GetComponent<WeaponMountComponent>(mount).User = null;
            SEntMan.DeleteEntity(carried);

            Assert.That(atgm.TryLock(finder, spotter, target), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(finderComp.LockedTarget, Is.EqualTo(target));
                Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(target), Is.True, "The lock shows the RMC reticle.");
                Assert.That(SEntMan.HasComponent<TargetingComponent>(finderUid), Is.True, "The lock shows the RMC laser.");
            });

            // A click on a vehicle's child entity (turret overlay, hardpoint) locks the vehicle itself.
            var vehicle = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            SEntMan.AddComponent<VehicleComponent>(vehicle);
            var vehiclePart = SEntMan.SpawnAttachedTo(null, new EntityCoordinates(vehicle, Vector2.Zero));
            Assert.That(atgm.TryLock(finder, spotter, vehiclePart), Is.False, "Locks have a cooldown.");
            Assert.That(finderComp.LockedTarget, Is.EqualTo(target));

            finderComp.NextLock = TimeSpan.Zero;
            Assert.That(atgm.TryLock(finder, spotter, vehiclePart), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(finderComp.LockedTarget, Is.EqualTo(vehicle));
                Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(target), Is.False,
                    "Re-locking removes the old target's reticle.");
            });

            SEntMan.EventBus.RaiseLocalEvent(finderUid, new UniqueActionEvent(spotter));
            Assert.Multiple(() =>
            {
                Assert.That(finderComp.LockedTarget, Is.Null, "The unique action clears the lock.");
                Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(vehicle), Is.False);
                Assert.That(SEntMan.HasComponent<TargetingComponent>(finderUid), Is.False, "No laser lingers.");
            });

            finderComp.NextLock = TimeSpan.Zero;
            Assert.That(atgm.TryLock(finder, spotter, target), Is.True);
            Assert.That(hands.TryDrop(spotter, finderUid), Is.True);
            Assert.That(finderComp.LockedTarget, Is.Null, "Dropping the finder breaks the lock.");
            Assert.That(SEntMan.HasComponent<RMCTargetedComponent>(target), Is.False);

            Assert.That(hands.TryPickupAnyHand(spotter, finderUid), Is.True);
            finderComp.NextLock = TimeSpan.Zero;
            Assert.That(atgm.TryLock(finder, spotter, target), Is.True);
            SEntMan.DeleteEntity(target);
            Assert.That(atgm.TryGetLock(finder, out _), Is.False, "A deleted target is no lock.");
            Assert.That(finderComp.LockedTarget, Is.Null);
        });
    }

    [Test]
    public async Task ShotHomesOnSpotterLockNotGunnerClick()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var hands = Server.System<SharedHandsSystem>();
            var skills = Server.System<SkillsSystem>();
            var transform = Server.System<SharedTransformSystem>();

            var mount = SEntMan.SpawnEntity("CMUATGMMount", map.GridCoords);
            Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);

            var gunner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var spotter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            skills.SetSkill(gunner, Skill, 1);
            skills.SetSkill(spotter, Skill, 1);

            var finderUid = SEntMan.SpawnEntity("CMUATGMFinder", map.GridCoords);
            var finderComp = SEntMan.GetComponent<CMUATGMFinderComponent>(finderUid);
            atgm.Link(launcher, (finderUid, finderComp));
            Assert.That(hands.TryPickupAnyHand(spotter, finderUid), Is.True);

            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(5, 0)));
            Assert.That(atgm.TryLock((finderUid, finderComp), spotter, target), Is.True);

            // The gunner clicks somewhere else entirely.
            var click = map.GridCoords.Offset(new Vector2(0, 7));
            SEntMan.GetComponent<GunComponent>(launcher).ShootCoordinates = click;

            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords.Offset(new Vector2(0.5f, 0)));
            SEntMan.GetComponent<ProjectileComponent>(projectile).Shooter = gunner;
            var spawnedAt = transform.GetMapCoordinates(projectile);

            SEntMan.EventBus.RaiseLocalEvent(launcher.Owner, new AmmoShotEvent { FiredProjectiles = new List<EntityUid> { projectile } });

            Assert.That(SEntMan.TryGetComponent(projectile, out CMUGuidedMissileComponent? missile), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(missile!.Guider, Is.EqualTo(spotter));
                Assert.That(missile.Finder, Is.EqualTo(finderUid));
                Assert.That(missile.TargetEntity, Is.EqualTo(target), "The missile homes on the spotter's lock.");
                Assert.That(missile.Target.MapId, Is.EqualTo(MapId.Nullspace),
                    "The gunner's click must not become the guidance target.");
                Assert.That(missile.HasGuidance, Is.False);
                Assert.That(missile.Launcher, Is.EqualTo(mount), "The wire starts at the tripod.");
                Assert.That(missile.WireOrigin.MapId, Is.EqualTo(spawnedAt.MapId));
                Assert.That((missile.WireOrigin.Position - spawnedAt.Position).Length(), Is.LessThan(0.001f));
                Assert.That(finderComp.LockedTarget, Is.EqualTo(target), "The lock persists for follow-up shots.");
            });

            SEntMan.DeleteEntity(projectile);
        });
    }

    [Test]
    public async Task FusedDetonationOnTrackedTargetLandsDirectHit()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            var targetCoords = map.GridCoords.Offset(new Vector2(5, 0));
            var target = SEntMan.SpawnEntity("CMMobHuman", targetCoords);

            // Inside the enlarged fuse box but short of the hull contact the physics hit would need.
            EntityUid Missile(EntityUid target, bool frozen)
            {
                var uid = SEntMan.SpawnEntity(ProjectileId, targetCoords.Offset(new Vector2(0.55f, 0)));
                var missile = SEntMan.EnsureComponent<CMUGuidedMissileComponent>(uid);
                missile.TargetEntity = target;
                missile.TrackingFrozen = frozen;
                return uid;
            }

            var tracked = Missile(target, false);
            guidance.Detonate(tracked);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ProjectileComponent>(tracked).ProjectileSpent, Is.True,
                    "A fused detonation on a live tracked target must deliver the direct AP hit.");
                Assert.That(SEntMan.GetComponent<DamageableComponent>(target).TotalDamage.Float(), Is.GreaterThan(0f));
            });

            // The first target was gibbed by the direct hit; a fresh one keeps this on the frozen-tracking check
            // rather than the deleted-target guard.
            var survivor = SEntMan.SpawnEntity("CMMobHuman", targetCoords);
            var frozen = Missile(survivor, true);
            guidance.Detonate(frozen);
            Assert.Multiple(() =>
            {
                // The blast is only queued here, so any damage now could only have come from a direct hit.
                Assert.That(SEntMan.GetComponent<DamageableComponent>(survivor).TotalDamage.Float(), Is.EqualTo(0f),
                    "A fuse on the last known position is a blast only.");
                Assert.That(SEntMan.IsQueuedForDeletion(survivor) || SEntMan.Deleted(survivor), Is.False,
                    "Only a direct hit gibs.");
            });
        });
    }

    [Test]
    public async Task LockCannotChangeWhileMissileInFlight()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var hands = Server.System<SharedHandsSystem>();

            var mount = SEntMan.SpawnEntity("CMUATGMMount", map.GridCoords);
            Assert.That(atgm.TryGetLauncher(mount, out var launcher), Is.True);

            var spotter = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.System<SkillsSystem>().SetSkill(spotter, Skill, 1);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 2)));
            var finderUid = SEntMan.SpawnEntity("CMUATGMFinder", map.GridCoords);
            var finderComp = SEntMan.GetComponent<CMUATGMFinderComponent>(finderUid);
            Entity<CMUATGMFinderComponent> finder = (finderUid, finderComp);
            atgm.Link(launcher, finder);
            Assert.That(hands.TryPickupAnyHand(spotter, finderUid), Is.True);
            Assert.That(atgm.TryLock(finder, spotter, target), Is.True);

            // No physics body, so the steering loop leaves this stand-in alone.
            var missileUid = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<CMUGuidedMissileComponent>(missileUid).Finder = finderUid;
            finderComp.NextLock = TimeSpan.Zero;

            Assert.Multiple(() =>
            {
                Assert.That(atgm.IsMissileInFlight(finderUid), Is.True);
                Assert.That(atgm.CanLock(finder, spotter, other, out _, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-atgm-lock-in-flight"));
                Assert.That(atgm.TryLock(finder, spotter, other), Is.False, "FLOW-07: no re-targeting mid-flight.");
                Assert.That(finderComp.LockedTarget, Is.EqualTo(target));
            });

            SEntMan.EventBus.RaiseLocalEvent(finderUid, new UniqueActionEvent(spotter));
            Assert.That(finderComp.LockedTarget, Is.EqualTo(target), "FLOW-07: the lock cannot be cleared mid-flight.");

            SEntMan.DeleteEntity(missileUid);
            Assert.That(atgm.IsMissileInFlight(finderUid), Is.False);
            Assert.That(atgm.TryLock(finder, spotter, other), Is.True, "Once the missile is gone the spotter can re-target.");
            Assert.That(finderComp.LockedTarget, Is.EqualTo(other));

            SEntMan.EventBus.RaiseLocalEvent(finderUid, new UniqueActionEvent(spotter));
            Assert.That(finderComp.LockedTarget, Is.Null);
        });
    }

    [Test]
    public async Task DirectHitGibsHumanoids()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords.Offset(new Vector2(2.5f, 0)));
            Server.System<SharedProjectileSystem>().ProjectileCollide(
                (projectile, SEntMan.GetComponent<ProjectileComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(projectile)),
                human);

            Assert.That(SEntMan.IsQueuedForDeletion(human) || SEntMan.Deleted(human), Is.True,
                "ARCH-07: a direct hit gibs a humanoid.");
            SEntMan.QueueDeleteEntity(projectile);
        });

        await RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(human), Is.True));
    }

    /// <summary>
    ///     Lands one direct hit on a freshly spawned <paramref name="xeno"/> and then sets off the warhead's own blast
    ///     at the impact point, as <see cref="CMUATGMGuidanceSystem.Detonate"/> and the TriggerOnCollide path do, on a
    ///     floored area so the blast spreads as it would on a station or planet. Runs past RMC's critical grace.
    /// </summary>
    /// <returns>
    ///     Damage right after the direct hit (including any crit top-up), final damage, and the final mob state, or
    ///     null if the body was gibbed.
    /// </returns>

    [Test]
    public async Task DirectHitUsesWarheadCookOffChance()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            // A bare stand-in: the cook-off roll only needs a damageable TankCookOff target.
            var tank = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(3, 0)));
            SEntMan.AddComponent<DamageableComponent>(tank);
            SEntMan.AddComponent<InjurableComponent>(tank);
            var cookOff = SEntMan.AddComponent<TankCookOffComponent>(tank);
            cookOff.CriticalChance = 0f;

            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords.Offset(new Vector2(2.5f, 0)));
            SEntMan.GetComponent<TankCookOffProjectileComponent>(projectile).CriticalChance = 1f;

            Server.System<SharedProjectileSystem>().ProjectileCollide(
                (projectile, SEntMan.GetComponent<ProjectileComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(projectile)),
                tank);

            Assert.That(SEntMan.HasComponent<ActiveTankCookOffComponent>(tank), Is.True,
                "The warhead's critical chance replaces the tank's own on a direct hit.");
            SEntMan.QueueDeleteEntity(projectile);
        });
    }

    [Test]
    public async Task BlastAloneDoesNotGibHumans()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        await Server.WaitAssertion(() =>
        {
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords);
            Server.System<TriggerSystem>().Trigger(projectile);
        });

        await RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(human) || SEntMan.IsQueuedForDeletion(human), Is.False,
                "ARCH-07: only a direct hit gibs; the blast alone leaves a body.");
            Assert.That(SEntMan.GetComponent<DamageableComponent>(human).TotalDamage.Float(), Is.GreaterThan(0f),
                "The blast reached the human.");
        });
    }

    [Test]
    public async Task BlastOnlyDetonationOverlappingHumanDoesNotDirectHit()
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        await Server.WaitAssertion(() =>
        {
            var guidance = Server.System<CMUATGMGuidanceSystem>();
            human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            // A frozen fuse is blast only, even when the hull sits right on top of a mob.
            var missileUid = SEntMan.SpawnEntity(ProjectileId, map.GridCoords);
            var missile = SEntMan.EnsureComponent<CMUGuidedMissileComponent>(missileUid);
            missile.TargetEntity = human;
            missile.TrackingFrozen = true;
            guidance.Detonate(missileUid);
            Assert.That(SEntMan.GetComponent<ProjectileComponent>(missileUid).ProjectileSpent, Is.True);
        });

        await Pair.RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(human), Is.True,
                "A blast-only detonation must not deliver a late physics direct hit that gibs an overlapping human.");
        });
    }
}
