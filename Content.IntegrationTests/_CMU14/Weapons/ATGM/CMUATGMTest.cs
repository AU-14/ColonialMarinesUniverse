#pragma warning disable RA0002 // Arrange guided-missile state directly to exercise the server guidance validator.
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Weapons.ATGM;
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
    private static readonly EntProtoId LauncherId = "CMUATGMLauncher";

    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task KitContainsCrewEquipmentAndTwoPamphlets()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var kit = SEntMan.SpawnEntity("CMUATGMCase", map.GridCoords);
            var contents = SEntMan.GetComponent<StorageComponent>(kit).Container.ContainedEntities
                .Select(item => SEntMan.GetComponent<MetaDataComponent>(item).EntityPrototype?.ID)
                .ToList();

            Assert.Multiple(() =>
            {
                Assert.That(contents.Count(id => id == "CMUATGMMount"), Is.EqualTo(1));
                Assert.That(contents.Count(id => id == "CMUATGMFinder"), Is.EqualTo(1));
                Assert.That(contents.Count(id => id == "CMUPamphletATGM"), Is.EqualTo(2),
                    "Both crewmen need a pamphlet.");
                Assert.That(contents.Count(id => id == "CMUATGMMissile"), Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public async Task PamphletGrantsSkillAfterAnotherPamphletWasRead()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var reader = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.EnsureComponent<UsedSkillPamphletComponent>(reader).Used = true;
            var pamphlet = SEntMan.SpawnEntity("CMUPamphletATGM", map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(reader, pamphlet), Is.True);

            var use = new UseInHandEvent(reader);
            SEntMan.EventBus.RaiseLocalEvent(pamphlet, use);

            Assert.That(Server.System<SkillsSystem>().HasSkill(reader, Skill, 1), Is.True,
                "The A.T.G.M. pamphlet bypasses the one-pamphlet limit.");
        });
    }

    [Test]
    public async Task MissileDoesNotUseFixedPointFlight()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var projectile = SProtoMan.Index(ProjectileId);
            var launcher = SProtoMan.Index(LauncherId);

            Assert.Multiple(() =>
            {
                Assert.That(projectile.TryComp<ProjectileComponent>(out var projectileComp, factory), Is.True);
                Assert.That(projectileComp?.MaxFixedRange, Is.Null,
                    "A fixed range would stop the guided missile mid-flight.");
                Assert.That(launcher.HasComp<ShootAtFixedPointComponent>(factory), Is.False,
                    "ShootAtFixedPoint is the only source of ProjectileFixedDistanceComponent on shot projectiles.");
                Assert.That(launcher.HasComp<GunUseGunOriginComponent>(factory), Is.True,
                    "The missile must launch from the tripod, not the gunner.");
                Assert.That(launcher.HasComp<CMUATGMLauncherComponent>(factory), Is.True);
            });
        });
    }

    [Test]
    public async Task FireInterlockRequiresSeparateSkilledSpotterInRange()
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

            var from = transform.GetMoverCoordinates(mount);
            var aim = from.Offset(transform.GetWorldRotation(mount).ToWorldVec() * 3);

            Assert.That(Attempt(), Is.True, "No finder is linked.");

            var finder = SEntMan.SpawnEntity("CMUATGMFinder", map.GridCoords);
            atgm.Link(launcher, (finder, SEntMan.GetComponent<CMUATGMFinderComponent>(finder)));
            Assert.That(launcher.Comp.LinkedFinder, Is.EqualTo(finder));
            Assert.That(Attempt(), Is.True, "The linked finder is not held by anyone.");

            Assert.That(hands.TryPickupAnyHand(spotter, finder), Is.True);
            Assert.That(Attempt(), Is.False, "A skilled spotter holding the linked finder allows firing.");

            skills.SetSkill(spotter, Skill, 0);
            Assert.That(Attempt(), Is.True, "An unskilled spotter cannot guide.");
            skills.SetSkill(spotter, Skill, 1);

            transform.SetCoordinates(spotter, map.GridCoords.Offset(new Vector2(30, 0)));
            Assert.That(Attempt(), Is.True, "A spotter out of range cannot guide.");
            transform.SetCoordinates(spotter, map.GridCoords);

            // The two-man interlock: the gunner can never be their own spotter.
            Assert.That(hands.TryDrop(spotter, finder), Is.True);
            Assert.That(hands.TryPickupAnyHand(gunner, finder), Is.True);
            Assert.That(Attempt(), Is.True, "The gunner holding the finder cannot spot for themselves.");

            bool Attempt()
            {
                var ev = new AttemptShootEvent(gunner, null, from, aim);
                SEntMan.EventBus.RaiseLocalEvent(launcher, ref ev);
                return ev.Cancelled;
            }
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
    public async Task MountExamineDoesNotLogMissingItemSlots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = SEntMan.SpawnEntity("CMUATGMMount", map.GridCoords);
            Assert.That(Server.System<CMUATGMSystem>().TryGetLauncher(mount, out var launcher), Is.True);
            Assert.That(SEntMan.HasComponent<ItemSlotsComponent>(launcher), Is.True,
                "ECS-05: RMC's mount examine resolves the launcher's ItemSlots and logs an error without it.");

            // Integration tests fail on error logs, so this also checks the lookup stays quiet.
            Assert.That(Server.System<SharedWeaponMountSystem>().TryGetWeaponAmmo(mount, out _, out _), Is.False,
                "The launcher has no gun_magazine slot; the CMU examine reports the loaded missile instead.");
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

    [Test]
    public async Task DirectHitDoublesDamageAgainstXenos()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var drone = SEntMan.SpawnEntity("CMXenoDrone", map.GridCoords.Offset(new Vector2(3, 0)));
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 3)));
            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords.Offset(new Vector2(2.5f, 0)));
            var baseDamage = SEntMan.GetComponent<ProjectileComponent>(projectile).Damage;

            var xenoHit = new ProjectileHitEvent(new DamageSpecifier(baseDamage), drone);
            SEntMan.EventBus.RaiseLocalEvent(projectile, ref xenoHit);
            var humanHit = new ProjectileHitEvent(new DamageSpecifier(baseDamage), human);
            SEntMan.EventBus.RaiseLocalEvent(projectile, ref humanHit);
            Assert.Multiple(() =>
            {
                Assert.That(xenoHit.Damage.GetTotal().Float(), Is.EqualTo(baseDamage.GetTotal().Float() * 2).Within(0.01f));
                Assert.That(humanHit.Damage.GetTotal().Float(), Is.EqualTo(baseDamage.GetTotal().Float()).Within(0.01f),
                    "Only xenos take the multiplier.");
            });

            Server.System<SharedProjectileSystem>().ProjectileCollide(
                (projectile, SEntMan.GetComponent<ProjectileComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(projectile)),
                drone);
            Assert.That(Server.System<MobStateSystem>().IsIncapacitated(drone), Is.True,
                "ARCH-07: a direct hit crits or kills a T1.");
            SEntMan.QueueDeleteEntity(projectile);
        });
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
    private async Task<(float AfterHit, float Final, MobState? State)> DirectHitThenDetonate(string xeno, float critChance)
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        var afterHit = 0f;
        await Server.WaitAssertion(() =>
        {
            var tiles = new List<(Vector2i, Tile)>();
            for (var x = -8; x <= 8; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    tiles.Add((new Vector2i(x, y), map.Tile.Tile));
                }
            }

            Server.System<SharedMapSystem>().SetTiles(map.Grid.Owner, map.Grid.Comp, tiles);

            // Same tile as the target: the blast's peak intensity, the worst case for the struck xeno.
            target = SEntMan.SpawnEntity(xeno, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));
            var projectile = SEntMan.SpawnEntity(ProjectileId, map.GridCoords.Offset(new Vector2(0.2f, 0.5f)));
            SEntMan.GetComponent<CMUDirectHitWarheadComponent>(projectile).XenoCritChance = critChance;

            Server.System<SharedProjectileSystem>().ProjectileCollide(
                (projectile, SEntMan.GetComponent<ProjectileComponent>(projectile), SEntMan.GetComponent<PhysicsComponent>(projectile)),
                target);
            afterHit = SEntMan.GetComponent<DamageableComponent>(target).TotalDamage.Float();

            Assert.That(Server.System<TriggerSystem>().Trigger(projectile), Is.True);
            // The explosion is queued at the missile's position; its spent hull must not collide again next tick.
            SEntMan.DeleteEntity(projectile);
        });

        await RunSeconds(3);

        var final = float.NaN;
        MobState? state = null;
        await Server.WaitAssertion(() =>
        {
            if (SEntMan.Deleted(target) || SEntMan.IsQueuedForDeletion(target))
                return;

            final = SEntMan.GetComponent<DamageableComponent>(target).TotalDamage.Float();
            state = SEntMan.GetComponent<MobStateComponent>(target).CurrentState;
        });

        return (afterHit, final, state);
    }

    [Test]
    public async Task DirectHitWithBlastDropsT1AndT2Xenos(
        [Values("CMXenoDrone", "CMXenoRunner", "CMXenoDefender",
            "CMXenoWarrior", "CMXenoSpitter", "CMXenoCarrier", "CMXenoHivelord")] string xeno,
        [Values(0f, 1f)] float critChance)
    {
        var (_, _, state) = await DirectHitThenDetonate(xeno, critChance);
        Assert.That(state is null or MobState.Dead or MobState.Critical, Is.True,
            $"ARCH-07: a direct hit kills or crits a T1/T2 whatever the crit roll; {xeno} ended {state}.");
    }

    [Test]
    public async Task DirectHitWithBlastCritsT3XenoOnCritRoll(
        [Values("CMXenoRavager", "RMCXenoCrusher", "CMXenoPraetorian", "RMCXenoBoiler")] string xeno)
    {
        var (afterHit, final, state) = await DirectHitThenDetonate(xeno, 1f);
        Assert.Multiple(() =>
        {
            Assert.That(state, Is.EqualTo(MobState.Critical),
                $"ARCH-07: a T3 that wins the crit roll ends critical - not dead, not standing - even after the warhead's blast " +
                $"(direct hit {afterHit}, final {final}).");
            Assert.That(final - afterHit, Is.LessThan(10f),
                "The xeno brought down by the roll is spared the same warhead's blast.");
        });
    }

    [Test]
    public async Task DirectHitThatLosesCritRollStillTakesBlast()
    {
        // Praetorians have light explosion armour, so the blast on top of the direct hit is easy to see.
        var (afterHit, final, state) = await DirectHitThenDetonate("CMXenoPraetorian", 0f);
        Assert.Multiple(() =>
        {
            Assert.That(state, Is.Not.Null, "Xenos are never gibbed by the warhead's direct hit.");
            Assert.That(final - afterHit, Is.GreaterThan(10f),
                "Only a won crit roll spares the struck xeno the blast.");
        });
    }

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
    public async Task WarheadPrototypeBreachesAndCooksOff()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            var projectile = SProtoMan.Index(ProjectileId);
            Assert.That(projectile.TryComp<ExplosiveComponent>(out var explosive, factory), Is.True);
            Assert.That(projectile.TryComp<RMCExplosiveDeleteComponent>(out var delete, factory), Is.True);
            Assert.That(projectile.TryComp<TankCookOffProjectileComponent>(out var cookOff, factory), Is.True);
            Assert.That(SProtoMan.TryIndex(explosive!.ExplosionType, out ExplosionPrototype? type), Is.True);

            var radius = Server.System<ExplosionSystem>().IntensityToRadius(explosive.TotalIntensity,
                explosive.IntensitySlope,
                explosive.MaxIntensity);
            Assert.Multiple(() =>
            {
                Assert.That(explosive.ExplosionType.Id, Is.EqualTo("CMUATGM"));
                Assert.That(type!.DamagePerIntensity.DamageDict.ContainsKey("Structural"), Is.True);
                Assert.That(radius, Is.InRange(5f, 6f), "ARCH-07: roughly a 5-6 tile blast.");
                Assert.That(explosive.MaxTileBreak, Is.Zero);
                Assert.That(delete!.Range, Is.EqualTo(1), "Walls at the impact are breached.");
                Assert.That(cookOff!.CriticalChance, Is.EqualTo(0.2f).Within(0.001f), "FLOW-08: about one direct hit in five cooks a tank off.");
            });
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
