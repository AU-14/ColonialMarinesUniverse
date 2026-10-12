using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Sentry;
using Content.Shared.Buckle;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

#pragma warning disable RA0002 // Arrange flight, cooldown and mob state at the system boundary.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkChinGunTest
{
    [TestCase(0, -90)]
    [TestCase(0, 90)]
    [TestCase(45, -90)]
    [TestCase(45, 90)]
    [TestCase(180, -90)]
    [TestCase(180, 90)]
    public void GimbalCoversExactlyTheForwardHalfCircle(float shipDegrees, float targetDegrees)
    {
        var rotation = Angle.FromDegrees(shipDegrees);
        var target = rotation.RotateVec(Angle.FromDegrees(targetDegrees).RotateVec(Vector2.UnitY)) * 10;
        Assert.That(GunshipDirectFireSystem.TryGetClampedAim(rotation, Vector2.Zero, target, 180,
            out var direction, out var degrees), Is.True);
        Assert.That(degrees, Is.EqualTo(targetDegrees).Within(0.001));
        Assert.That(Vector2.Distance(direction, Vector2.Normalize(target)), Is.LessThan(0.001));

        var behind = rotation.RotateVec(Angle.FromDegrees(targetDegrees * 1.5).RotateVec(Vector2.UnitY));
        Assert.That(GunshipDirectFireSystem.TryGetClampedAim(rotation, Vector2.Zero, behind, 180,
            out _, out degrees), Is.True);
        Assert.That(MathF.Abs(degrees), Is.EqualTo(90).Within(0.001));
    }

    [Test]
    public async Task GroundTurretRespectsIffArcFlightAndManualOverride()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var map = maps.CreateMap(out var mapId);
            var grid = maps.CreateGridEntity(mapId);
            var ship = grid.Owner;
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["FloorSteel"].TileId));
            var dropship = entities.AddComponent<DropshipComponent>(ship);
            entities.AddComponent<ShipFactionComponent>(ship).Faction = "GOVFOR";
            var coordinates = new EntityCoordinates(ship, Vector2.Zero);
            var point = entities.SpawnEntity("CMUMohawkM90Point", coordinates);
            dropship.AttachmentPoints.Add(point);
            var mount = entities.GetComponent<DropshipWeaponPointComponent>(point);
            var containers = entities.System<SharedContainerSystem>();
            var weapon = containers.GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
            var ammo = entities.SpawnEntity("CMUMohawkM90Ammo", coordinates);
            Assert.That(containers.Insert(ammo, containers.GetContainer(point, mount.AmmoContainerSlotId)), Is.True);
            var payload = entities.GetComponent<DropshipAmmoComponent>(ammo);
            var turret = entities.System<MohawkChinGunSystem>();
            var chin = entities.GetComponent<MohawkChinGunComponent>(point);
            var gun = entities.GetComponent<GunComponent>(weapon);
            var transform = entities.System<SharedTransformSystem>();

            EntityUid Target(string faction, Vector2 position)
            {
                var target = entities.SpawnEntity(null, new EntityCoordinates(map, position));
                entities.AddComponent<NpcFactionMemberComponent>(target).Factions.Add(faction);
                entities.AddComponent<MobStateComponent>(target);
                return target;
            }

            void Scan()
            {
                chin.NextScan = TimeSpan.Zero;
                gun.NextFire = TimeSpan.Zero;
                turret.Update(0);
            }

            var blocker = Target("GOVFOR", new Vector2(1.5f, 5));
            var hostile = Target("OPFOR", new Vector2(1.5f, -10));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Friendly targets and rear targets must not consume ammo.");
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 10));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Acquiring a hostile must not authorize fire.");
            entities.System<SharedSentryTargetingSystem>().SetFriendlyFactions(
                (point, entities.GetComponent<SentryTargetingComponent>(point)), ["GOVFOR", "OPFOR"]);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Alliance-friendly IFF must be respected.");
            entities.System<SharedSentryTargetingSystem>().SetFriendlyFactions(
                (point, entities.GetComponent<SentryTargetingComponent>(point)), ["GOVFOR"]);
            entities.GetComponent<MobStateComponent>(hostile).CurrentState = MobState.Dead;
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Dead targets must not be engaged.");
            entities.GetComponent<MobStateComponent>(hostile).CurrentState = MobState.Alive;
            var ftl = entities.AddComponent<FTLComponent>(ship);
            ftl.State = FTLState.Starting;
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Takeoff must disable automatic ground fire.");
            ftl.State = FTLState.Available;
            entities.AddComponent<DropshipTacticalHoverComponent>(ship);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Hovering must not enable the landed turret.");
            entities.RemoveComponent<DropshipTacticalHoverComponent>(ship);

            var seat = entities.SpawnEntity("CMUMohawkMidwayMidwayGunner9ec7eb79e9", coordinates);
            var actor = entities.SpawnEntity("CMMobHuman", coordinates);
            entities.EnsureComponent<EyeComponent>(actor);
            entities.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 1);
            Assert.That(entities.System<SharedBuckleSystem>().TryBuckle(actor, null, seat, popup: false), Is.True);
            var console = entities.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", coordinates);
            var ui = entities.System<SharedUserInterfaceSystem>();
            ui.OpenUi(console, DropshipTerminalWeaponsUi.Key, actor);
            Assert.That(ui.IsUiOpen(console, DropshipTerminalWeaponsUi.Key, actor), Is.True);
            Assert.That(turret.IsGrounded(point), Is.True);
            Assert.That(entities.System<SharedDropshipSystem>().TryGetGridDropship(console, out var consoleShip), Is.True);
            Assert.That(consoleShip.Owner, Is.EqualTo(ship));
            Assert.That(entities.System<SharedDropshipSystem>().TryGetGridDropship(seat, out var seatShip), Is.True);
            Assert.That(seatShip.Owner, Is.EqualTo(ship));
            Assert.That(entities.GetComponent<MobStateComponent>(actor).CurrentState, Is.EqualTo(MobState.Alive));
            Assert.That(entities.HasComponent<RemoteWeaponOperatorComponent>(actor), Is.False);
            Assert.That(entities.System<SkillsSystem>().HasSkills(actor,
                entities.GetComponent<DropshipWeaponComponent>(weapon).Skills!), Is.True);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunControlMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.Operator, Is.EqualTo(actor));
            Assert.That(entities.GetComponent<RemoteWeaponOperatorComponent>(actor).SelectedWeapon, Is.EqualTo(weapon));
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunAimMessage(
                entities.GetNetCoordinates(new EntityCoordinates(map, -8.5f, 11)))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.DesiredAimDegrees, Is.EqualTo(45).Within(0.001),
                "Hovering the camera must request an aim bearing without firing.");
            Assert.That(payload.Rounds, Is.EqualTo(400));
            for (var pan = 0; pan < 15; pan++)
                entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunPanMessage(Direction.North)
                    { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            turret.Update(0);
            Assert.That(Vector2.Distance(transform.GetMapCoordinates(chin.Eye!.Value).Position, new Vector2(1.5f, 1)),
                Is.EqualTo(30).Within(0.001), "Camera pan must reach the full thirty-tile range.");
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunPanMessage(Direction.South)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            turret.Update(0);
            Assert.That(Vector2.Distance(transform.GetMapCoordinates(chin.Eye!.Value).Position, new Vector2(1.5f, 1)),
                Is.EqualTo(28).Within(0.001), "Panning back from the limit must move immediately without a dead zone.");
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunPanMessage(null)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(turret.RequestAim((point, chin), weapon, new EntityCoordinates(map, -8.5f, 11)), Is.False);
            Assert.That(entities.GetComponent<GunshipDirectFirePointComponent>(point).AimOffsetDegrees, Is.Zero,
                "Requesting a new bearing must not snap the turret.");
            turret.Update(0.5f);
            Assert.That(entities.GetComponent<GunshipDirectFirePointComponent>(point).AimOffsetDegrees,
                Is.EqualTo(15).Within(0.001), "A half-second update must slew fifteen degrees.");
            chin.DesiredAimDegrees = 0;
            turret.Update(0.5f);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Taking control must not authorize fire.");
            gun.NextFire = TimeSpan.Zero;
            entities.AddComponent<CMUZLevelShooterComponent>(actor).ShootDown = true;
            Assert.That(entities.System<SharedGunSystem>().AttemptShoot(actor, (weapon, gun),
                new EntityCoordinates(map, 1.5f, 10)), Is.False);
            Assert.That(payload.Rounds, Is.EqualTo(400), "A friendly in the path must block manual fire.");
            Assert.That(entities.QueryEntities<CMUSpentCasingComponent>(), Is.Empty,
                "Blocked shots must not eject casings.");
            transform.SetCoordinates(blocker, new EntityCoordinates(map, 15, 5));
            Assert.That(entities.System<SharedGunSystem>().AttemptShoot(actor, (weapon, gun),
                new EntityCoordinates(map, 1.5f, 10)), Is.True);
            Assert.That(payload.Rounds, Is.EqualTo(399), "Mouse-coordinate firing must consume an individual shell.");
            var casing = entities.QueryEntities<CMUSpentCasingComponent>().Single();
            Assert.That(transform.GetMapCoordinates(casing).Position, Is.EqualTo(new Vector2(1.5f, 1)));
            Assert.That(transform.GetMapCoordinates(casing).MapId, Is.EqualTo(mapId));
            Assert.That(entities.HasComponent<ThrownItemComponent>(casing), Is.True);
            Assert.That(entities.GetComponent<PhysicsComponent>(casing).LinearVelocity.Length(),
                Is.InRange(2f, 5f), "Casings must be physically thrown, not just placed under the mount.");
            Assert.That(entities.QueryEntities<ProjectileIFFComponent>().Any(projectile => projectile.Comp.Enabled), Is.True,
                "Fired shells must inherit the turret's smartgun-compatible IFF.");
            Assert.That(entities.System<SharedGunSystem>().AttemptShoot(actor, (weapon, gun),
                new EntityCoordinates(map, 1.5f, 10)), Is.False, "A second shell must wait for the third-second cooldown.");
            Assert.That(gun.FireRate, Is.EqualTo(3));
            Assert.That(gun.NextFire - pair.Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime,
                Is.EqualTo(TimeSpan.FromSeconds(1f / 3f)).Within(TimeSpan.FromMilliseconds(1)));
            gun.NextFire = TimeSpan.Zero;
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunFireMessage(
                entities.GetNetCoordinates(new EntityCoordinates(map, 1.5f, 10)))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(payload.Rounds, Is.EqualTo(398), "The console camera must support mouse-coordinate firing.");

            var shot = new AttemptShootEvent
            {
                User = actor,
                ToCoordinates = new EntityCoordinates(map, 1.5f, 100),
            };
            entities.EventBus.RaiseLocalEvent(weapon, ref shot);
            Assert.That(shot.Cancelled, Is.True, "Manual fire must not bypass maximum range.");

            void Track(EntityUid target)
            {
                entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunTrackMessage(
                    entities.GetNetCoordinates(transform.GetMoverCoordinates(target)))
                    { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            }
            var friendly = Target("GOVFOR", new Vector2(2, 12));
            Track(friendly);
            Assert.That(chin.TrackedTarget, Is.Null, "Manual tracking must retain IFF protection.");
            Track(hostile);
            Assert.That(chin.TrackedTarget, Is.EqualTo(hostile));
            Assert.That(chin.TrackingFireConfirmed, Is.False);
            var window = entities.SpawnEntity("RMCWindowStrata", new EntityCoordinates(ship, 1.5f, 6));
            Assert.That(turret.CanTrack((point, chin), hostile), Is.True,
                "Transparent windows must not prevent target acquisition.");
            entities.DeleteEntity(window);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(398), "A target mark must not authorize fire.");
            var consoleState = entities.GetComponent<DropshipTerminalWeaponsComponent>(console);
            Assert.That(consoleState.TrackedTarget, Is.EqualTo(hostile));
            var trackedEye = transform.GetMapCoordinates(chin.Eye!.Value);
            Assert.That(trackedEye.Position, Is.EqualTo(transform.GetMapCoordinates(hostile).Position));
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunConfirmTrackingMessage(entities.GetNetEntity(friendly))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackingFireConfirmed, Is.False, "A stale or mismatched confirmation cannot authorize a different target.");
            Track(hostile);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunConfirmTrackingMessage(entities.GetNetEntity(hostile))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackingFireConfirmed, Is.True);
            Assert.That(chin.AuthorizedUntil - pair.Server.ResolveDependency<Robust.Shared.Timing.IGameTiming>().CurTime,
                Is.EqualTo(TimeSpan.FromSeconds(30)));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(397), "Final confirmation enables tracked automatic fire.");
            var wall = entities.SpawnEntity("CMWallMetal", new EntityCoordinates(ship, 1.5f, 6));
            Assert.That(turret.CanTrack((point, chin), hostile), Is.False, "Opaque cover must prevent acquisition.");
            Scan();
            Assert.That(chin.TrackingFireConfirmed, Is.True, "Occlusion must preserve unexpired authorization.");
            Assert.That(payload.Rounds, Is.EqualTo(397), "Occlusion must pause fire.");
            entities.DeleteEntity(wall);
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, -10));
            Scan();
            Assert.That(chin.TrackedTarget, Is.EqualTo(hostile), "Losing sight or the firing arc must preserve the marked entity.");
            Assert.That(consoleState.TrackingFireConfirmed, Is.True);
            Assert.That(payload.Rounds, Is.EqualTo(397));
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 10));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(396), "Reacquiring the authorized entity must resume fire.");
            chin.AuthorizedUntil = TimeSpan.Zero;
            Scan();
            Assert.That(chin.TrackingFireConfirmed, Is.False);
            Assert.That(payload.Rounds, Is.EqualTo(396), "Expired authorization must not fire.");
            Track(hostile);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunClearTrackingMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackedTarget, Is.Null);
            var flare = entities.SpawnEntity(null, new EntityCoordinates(map, 1.5f, 15));
            entities.AddComponent<FlareSignalComponent>(flare);
            entities.AddComponent<DropshipTargetComponent>(flare);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunFlareMessage(entities.GetNetEntity(flare))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(consoleState.Target, Is.Null, "Groundside gunnery must reject flare targeting.");
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunAcquireMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackedTarget, Is.EqualTo(hostile));
            Assert.That(chin.TrackingFireConfirmed, Is.False);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunFlareMessage(null)
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackedTarget, Is.Null);
            Assert.That(consoleState.Target, Is.Null);

            var tough = Target("OPFOR", new Vector2(1.5f, 20));
            entities.AddComponent<DamageableComponent>(hostile);
            entities.AddComponent<DamageableComponent>(tough);
            entities.AddComponent<MobThresholdsComponent>(hostile);
            entities.AddComponent<MobThresholdsComponent>(tough);
            entities.System<MobThresholdSystem>().SetMobStateThreshold(hostile, 100, MobState.Dead);
            entities.System<MobThresholdSystem>().SetMobStateThreshold(tough, 300, MobState.Dead);
            Assert.That(turret.SelectTarget((point, chin)), Is.EqualTo(hostile));
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunPriorityMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(turret.SelectTarget((point, chin)), Is.EqualTo(tough));
            Assert.That(consoleState.ChinGunPriority, Is.EqualTo(MohawkTargetPriority.HighestHealth));

            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 5));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(396), "An active manual gunner must suppress close defense.");
            ui.CloseUi(console, DropshipTerminalWeaponsUi.Key, actor);
            Scan();
            Assert.That(chin.Operator, Is.Null);
            Assert.That(entities.HasComponent<RemoteWeaponOperatorComponent>(actor), Is.False);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(395),
                "Unmanned close defense must fire at a threat exactly four tiles from the muzzle mount.");
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 5.01f));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(395), "Targets outside four tiles require authorization.");
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
