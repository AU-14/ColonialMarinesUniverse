using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Buckle;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Damage.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

#pragma warning disable RA0002 // Arrange gun cooldowns, flight state and delayed impacts at system boundaries.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkTriggerAndAirburstTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private sealed record Mount(EntityUid Ship, EntityUid Point, EntityUid Weapon, EntityUid Crate,
        EntityUid Console, EntityUid Actor, MohawkChinGunComponent Chin, DropshipTerminalWeaponsComponent Terminal,
        GunComponent Gun, DropshipAmmoComponent Ammo);

    private Mount Create(EntityUid grid, bool airborne)
    {
        var coords = new EntityCoordinates(grid, Vector2.Zero);
        var maps = Server.System<SharedMapSystem>();
        var tiles = Server.ResolveDependency<ITileDefinitionManager>();
        var gridEnt = new Entity<MapGridComponent>(grid, SEntMan.GetComponent<MapGridComponent>(grid));
        for (var x = -2; x <= 11; x++)
        {
            for (var y = -2; y <= 20; y++)
                maps.SetTile(gridEnt, new Vector2i(x, y), new Tile(tiles["FloorSteel"].TileId));
        }
        var ship = SEntMan.EnsureComponent<DropshipComponent>(grid);
        ship.AttachmentPoints.Clear();
        SEntMan.EnsureComponent<ShipFactionComponent>(grid).Faction = "GOVFOR";
        var point = SEntMan.SpawnEntity("CMUMohawkM90Point", coords);
        ship.AttachmentPoints.Add(point);
        var mount = SEntMan.GetComponent<DropshipWeaponPointComponent>(point);
        var containers = Server.System<SharedContainerSystem>();
        var weapon = containers.GetContainer(point, mount.WeaponContainerSlotId).ContainedEntities.Single();
        var crate = SEntMan.SpawnEntity("CMUMohawkM90Ammo", coords);
        Assert.That(containers.Insert(crate, containers.GetContainer(point, mount.AmmoContainerSlotId)), Is.True);
        if (airborne)
        {
            SEntMan.EnsureComponent<FTLComponent>(grid).State = FTLState.Travelling;
            SEntMan.AddComponent<DropshipInFlyByComponent>(grid);
        }
        var seatCoords = coords.Offset(new Vector2(10.5f, 0.5f));
        var seat = SEntMan.SpawnEntity("CMUMohawkMidwayMidwayGunner9ec7eb79e9", seatCoords);
        var actor = SEntMan.SpawnEntity("CMMobHuman", seatCoords);
        Server.PlayerMan.SetAttachedEntity(ServerSession!, actor);
        var actorFaction = SEntMan.GetComponent<NpcFactionMemberComponent>(actor);
        actorFaction.Factions.Clear();
        actorFaction.Factions.Add("GOVFOR");
        SEntMan.EnsureComponent<EyeComponent>(actor);
        Server.System<SkillsSystem>().SetSkill(actor, "RMCSkillPilot", 1);
        Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(actor, null, seat, popup: false), Is.True);
        Assert.That(SEntMan.GetComponent<Content.Shared.Buckle.Components.BuckleComponent>(actor).BuckledTo,
            Is.EqualTo(seat), "The test gunner must remain buckled after seat placement.");
        var console = SEntMan.SpawnEntity("CMUMohawkMidwayGunneryea0f8d621b", coords.Offset(new Vector2(10.5f, 1.5f)));
        Server.System<SharedUserInterfaceSystem>().OpenUi(console, DropshipTerminalWeaponsUi.Key, actor);
        Assert.That(Server.System<SharedDropshipSystem>().TryGetGridDropship(console, out var consoleShip), Is.True);
        Assert.That(consoleShip.Owner, Is.EqualTo(grid));
        Assert.That(Server.System<SharedDropshipSystem>().TryGetGridDropship(seat, out var seatShip), Is.True);
        Assert.That(seatShip.Owner, Is.EqualTo(grid));
        SEntMan.EventBus.RaiseLocalEvent(console, new MohawkChinGunControlMessage
        {
            Actor = actor,
            UiKey = DropshipTerminalWeaponsUi.Key,
        });
        var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
        Assert.That(chin.Operator, Is.EqualTo(actor));
        return new Mount(grid, point, weapon, crate, console, actor, chin,
            SEntMan.GetComponent<DropshipTerminalWeaponsComponent>(console),
            SEntMan.GetComponent<GunComponent>(weapon), SEntMan.GetComponent<DropshipAmmoComponent>(crate));
    }

    private void Trigger(Mount mount, bool held, EntityCoordinates? target = null)
    {
        SEntMan.EventBus.RaiseLocalEvent(mount.Console, new MohawkChinGunTriggerMessage(held,
            target is { } coords ? SEntMan.GetNetCoordinates(coords) : null)
        {
            Actor = mount.Actor,
            UiKey = DropshipTerminalWeaponsUi.Key,
        });
    }

    [Test]
    public async Task HoldingFiresRepeatedlyButReleaseExpiryAndClosingStopIt()
    {
        var map = await Pair.CreateTestMap();
        Mount mount = null!;
        var target = map.GridCoords.Offset(new Vector2(1.5f, 15));
        var started = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            mount = Create(map.Grid, false);
            Trigger(mount, true, target);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399));
            started = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(0.2f);
        await Server.WaitPost(() => Trigger(mount, true, target));
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SGameTiming.CurTime - started, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1f / 3f)));
            Assert.That(mount.Chin.Operator, Is.EqualTo(mount.Actor));
            Assert.That(mount.Chin.HeldAim, Is.Not.Null);
            Assert.That(mount.Chin.HeldFireUntil, Is.GreaterThan(SGameTiming.CurTime));
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(398), "Holding and renewing should fire again after one third second.");
            Trigger(mount, false);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(398), "Releasing the button must stop fire.");
            Trigger(mount, true, map.GridCoords.Offset(new Vector2(-15, 1)));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Chin.PendingManualShot, Is.Not.Null, "A held shot may wait for slew alignment.");
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Chin.HeldAim, Is.Null);
            Assert.That(mount.Chin.PendingManualShot, Is.Null, "An expired trigger lease must cancel a queued shot.");
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(398));
            SEntMan.GetComponent<GunshipDirectFirePointComponent>(mount.Point).AimOffsetDegrees = 0;
            mount.Chin.DesiredAimDegrees = 0;
            Trigger(mount, true, target);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(397));
            Server.System<SharedUserInterfaceSystem>().CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Chin.Operator, Is.Null);
            Assert.That(mount.Chin.HeldAim, Is.Null);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(mount.Ammo.Rounds, Is.EqualTo(397)));
    }

    [Test]
    public async Task FlybyFlareShotsConsumeOneRoundAndProduceDelayedImpacts()
    {
        var shipMap = await Pair.CreateTestMap();
        var groundMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(shipMap.Grid, true);
            mount.Ammo.BulletSpread = 0;
            var chinSystem = Server.System<MohawkChinGunSystem>();
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            var clock = Server.ResolveDependency<IGameTiming>();
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var flare = SEntMan.SpawnEntity(null, groundMap.GridCoords);
            SEntMan.AddComponent<FlareSignalComponent>(flare);
            weapons.MakeDropshipTarget(flare, "TEST");
            SEntMan.EventBus.RaiseLocalEvent(mount.Console, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare))
            {
                Actor = mount.Actor,
                UiKey = DropshipTerminalWeaponsUi.Key,
            });
            Assert.That(mount.Terminal.Target, Is.EqualTo(flare));
            Assert.That(mount.Terminal.ChinGunAirborne, Is.True);
            chinSystem.Update(0);
            Assert.That(Server.System<SharedTransformSystem>().GetMapCoordinates(mount.Chin.Eye!.Value).MapId,
                Is.EqualTo(Server.System<SharedTransformSystem>().GetMapCoordinates(flare).MapId),
                "Airborne cameras must anchor to the flare's ground map, not the flying ship map.");
            Assert.That(chinSystem.IsInFlareArea((mount.Point, mount.Chin),
                groundMap.GridCoords.Offset(new Vector2(10, 0))), Is.True);
            Assert.That(chinSystem.IsInFlareArea((mount.Point, mount.Chin),
                groundMap.GridCoords.Offset(new Vector2(10.01f, 0))), Is.False);
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                shipMap.GridCoords, mount.Terminal), Is.False);
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.True);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399));
            Assert.That(SEntMan.QueryEntities<ProjectileComponent>(), Is.Empty,
                "Flyby gunnery must not spawn a physically travelling 90mm projectile.");
            var flight = SEntMan.QueryEntities<AmmoInFlightComponent>().Single();
            Assert.That(flight.Comp.MarkerAt - clock.CurTime,
                Is.InRange(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));
            Assert.That(flight.Comp.ShotsLeft, Is.EqualTo(1));
            Assert.That(flight.Comp.ImpactsPerVolley, Is.EqualTo(1));
            Assert.That(flight.Comp.Explosion, Is.Not.Null);
            Assert.That(mount.Gun.NextFire - clock.CurTime,
                Is.EqualTo(TimeSpan.FromSeconds(1f / 3)).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.False, "Cooldown must block another immediate shot.");
            weapons.Update(0);
            Assert.That(flight.Comp.ShotsLeft, Is.EqualTo(1), "No impact may occur before its travel delay.");
            var victim = SEntMan.SpawnEntity("CMMobHuman", flight.Comp.Target);
            var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(victim);
            faction.Factions.Clear();
            faction.Factions.Add("OPFOR");
            flight.Comp.MarkerAt = TimeSpan.Zero;
            weapons.Update(0);
            Assert.That(flight.Comp.ShotsLeft, Is.EqualTo(0));
            Assert.That(SEntMan.GetComponent<DamageableComponent>(victim).Damage.GetTotal().Float(), Is.GreaterThan(0),
                "The delayed shot must apply its captured impact payload.");

            mount.Gun.NextFire = TimeSpan.Zero;
            var friendly = SEntMan.SpawnEntity("CMMobHuman", groundMap.GridCoords);
            var friendlyFaction = SEntMan.GetComponent<NpcFactionMemberComponent>(friendly);
            friendlyFaction.Factions.Clear();
            friendlyFaction.Factions.Add("GOVFOR");
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.False);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399), "Unsafe shots must not consume a shell.");
            SEntMan.DeleteEntity(friendly);
            SEntMan.DeleteEntity(victim);
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.True);
            var pending = SEntMan.QueryEntities<AmmoInFlightComponent>().Single(e => e.Comp.ShotsLeft == 1);
            var lateFriendly = SEntMan.SpawnEntity("CMMobHuman", pending.Comp.Target);
            var lateFaction = SEntMan.GetComponent<NpcFactionMemberComponent>(lateFriendly);
            lateFaction.Factions.Clear();
            lateFaction.Factions.Add("GOVFOR");
            pending.Comp.MarkerAt = TimeSpan.Zero;
            weapons.Update(0);
            Assert.That(pending.Comp.ShotsLeft, Is.EqualTo(0));
            Assert.That(SEntMan.GetComponent<DamageableComponent>(lateFriendly).Damage.GetTotal().Float(), Is.EqualTo(0),
                "A friendly entering the impact area during travel must cancel the payload.");
            Assert.That(SEntMan.QueryEntities<ProjectileComponent>(), Is.Empty);
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, false);
            SEntMan.RemoveComponent<FTLComponent>(mount.Ship);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(mount.Ship);
            Server.System<SharedUserInterfaceSystem>().CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            chinSystem.Update(0);
        });
    }

    [Test]
    public async Task FlareGunneryRejectsGroundHoverTransitAndUnregisteredFlares()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var turret = Server.System<MohawkChinGunSystem>();
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            var flare = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1.5f, 15)));
            SEntMan.AddComponent<FlareSignalComponent>(flare);
            weapons.MakeDropshipTarget(flare, "TEST");
            void Select() => SEntMan.EventBus.RaiseLocalEvent(mount.Console,
                new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare))
                { Actor = mount.Actor, UiKey = DropshipTerminalWeaponsUi.Key });
            Select();
            Assert.That(mount.Terminal.Target, Is.Null);
            var ftl = SEntMan.EnsureComponent<FTLComponent>(mount.Ship);
            ftl.State = FTLState.Travelling;
            Assert.That(turret.IsAirborne(mount.Point), Is.False, "Ordinary transit is not a CAS flyby.");
            SEntMan.AddComponent<DropshipInFlyByComponent>(mount.Ship);
            SEntMan.AddComponent<DropshipTacticalHoverComponent>(mount.Ship);
            Assert.That(turret.IsAirborne(mount.Point), Is.False, "Tactical hover must never allow flare fire.");
            Select();
            Assert.That(mount.Terminal.Target, Is.Null);
            SEntMan.RemoveComponent<DropshipTacticalHoverComponent>(mount.Ship);
            Assert.That(turret.IsAirborne(mount.Point), Is.True);
            mount.Terminal.Targets.Clear();
            Select();
            Assert.That(mount.Terminal.Target, Is.Null, "A forged/unlisted flare ID must not be accepted.");
            SEntMan.RemoveComponent<FTLComponent>(mount.Ship);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(mount.Ship);
        });
    }
}
