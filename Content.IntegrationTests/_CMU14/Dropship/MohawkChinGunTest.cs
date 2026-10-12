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
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
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

            Target("GOVFOR", new Vector2(1.5f, 5));
            var hostile = Target("OPFOR", new Vector2(1.5f, -10));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(400), "Friendly targets and rear targets must not consume ammo.");
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 10));
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "A forward IFF hostile must fire one HEAP shell.");
            entities.System<SharedSentryTargetingSystem>().SetFriendlyFactions(
                (point, entities.GetComponent<SentryTargetingComponent>(point)), ["GOVFOR", "OPFOR"]);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "Alliance-friendly IFF must be respected.");
            entities.System<SharedSentryTargetingSystem>().SetFriendlyFactions(
                (point, entities.GetComponent<SentryTargetingComponent>(point)), ["GOVFOR"]);
            entities.GetComponent<MobStateComponent>(hostile).CurrentState = MobState.Dead;
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "Dead targets must not be engaged.");
            entities.GetComponent<MobStateComponent>(hostile).CurrentState = MobState.Alive;
            var ftl = entities.AddComponent<FTLComponent>(ship);
            ftl.State = FTLState.Starting;
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "Takeoff must disable automatic ground fire.");
            ftl.State = FTLState.Available;
            entities.AddComponent<DropshipTacticalHoverComponent>(ship);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "Hovering must not enable the landed turret.");
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
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(399), "Manual control must suspend automatic targeting.");
            gun.NextFire = TimeSpan.Zero;
            entities.AddComponent<CMUZLevelShooterComponent>(actor).ShootDown = true;
            Assert.That(entities.System<SharedGunSystem>().AttemptShoot(actor, (weapon, gun),
                new EntityCoordinates(map, 1.5f, 10)), Is.True);
            Assert.That(payload.Rounds, Is.EqualTo(398), "Mouse-coordinate firing must consume an individual shell.");
            gun.NextFire = TimeSpan.Zero;
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunFireMessage(
                entities.GetNetCoordinates(new EntityCoordinates(map, 1.5f, 10)))
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(payload.Rounds, Is.EqualTo(397), "The console camera must support mouse-coordinate firing.");

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
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(397), "A target mark must not authorize fire.");
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
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(396), "Final confirmation enables tracked automatic fire.");
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, -10));
            Scan();
            Assert.That(chin.TrackedTarget, Is.Null, "Leaving the forward arc must stop engagement and clear authorization.");
            Assert.That(consoleState.TrackingFireConfirmed, Is.False);
            Assert.That(payload.Rounds, Is.EqualTo(396));
            transform.SetCoordinates(hostile, new EntityCoordinates(map, 1.5f, 10));
            Track(hostile);
            entities.EventBus.RaiseLocalEvent(console, new MohawkChinGunClearTrackingMessage
                { Actor = actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(chin.TrackedTarget, Is.Null);
            ui.CloseUi(console, DropshipTerminalWeaponsUi.Key, actor);
            Scan();
            Assert.That(chin.Operator, Is.Null);
            Assert.That(entities.HasComponent<RemoteWeaponOperatorComponent>(actor), Is.False);
            Scan();
            Assert.That(payload.Rounds, Is.EqualTo(395), "Closing the console must restore automatic fire.");
            entities.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
