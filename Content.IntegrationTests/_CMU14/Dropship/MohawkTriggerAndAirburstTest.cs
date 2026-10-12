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

    private Mount Create(EntityUid grid, bool airborne, string cratePrototype = "CMUMohawkM90Ammo")
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
        var crate = SEntMan.SpawnEntity(cratePrototype, coords);
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

    private void Send(Mount mount, BoundUserInterfaceMessage message)
    {
        message.Actor = mount.Actor;
        message.UiKey = DropshipTerminalWeaponsUi.Key;
        SEntMan.EventBus.RaiseLocalEvent(mount.Console, (object) message);
    }

    private EntityUid Hostile(EntityCoordinates coordinates)
    {
        var hostile = SEntMan.SpawnEntity("CMMobHuman", coordinates);
        var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(hostile);
        faction.Factions.Clear();
        faction.Factions.Add("OPFOR");
        return hostile;
    }

    [Test]
    public async Task KineticIsGroundOnlyAndAlternatingRatesStayFeedSpecific()
    {
        var shipMap = await Pair.CreateTestMap();
        var groundMap = await Pair.CreateTestMap();
        Mount mount = null!;
        EntityUid explosive = default;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            mount = Create(shipMap.Grid, false, "CMUMohawkM90AmmoKinetic");
            var point = SEntMan.GetComponent<DropshipWeaponPointComponent>(mount.Point);
            explosive = SEntMan.SpawnEntity("CMUMohawkM90Ammo", shipMap.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(explosive, containers.GetContainer(mount.Point, point.SecondAmmoContainerSlotId!)), Is.True);
            mount.Chin.AlternateFeeds = true;
            Trigger(mount, true, shipMap.GridCoords.Offset(new Vector2(1.5f, 15)));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399));
            Assert.That(mount.Gun.NextFire - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(0.1)).Within(TimeSpan.FromMilliseconds(1)));
        });
        await Server.WaitAssertion(() =>
        {
            mount.Gun.NextFire = SGameTiming.CurTime;
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(explosive).Rounds, Is.EqualTo(399));
            Assert.That(mount.Gun.NextFire - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(1f / 3)).Within(TimeSpan.FromMilliseconds(1)),
                "The fired explosive round must retain its three-per-second cooldown even after the feed alternates.");
            Trigger(mount, false);
            mount.Chin.ActiveFeed = 0;
            mount.Chin.AlternateFeeds = false;
            Send(mount, new MohawkChinGunControlMessage());
            SEntMan.EnsureComponent<FTLComponent>(shipMap.Grid).State = FTLState.Travelling;
            SEntMan.AddComponent<DropshipInFlyByComponent>(shipMap.Grid);
            Send(mount, new MohawkChinGunControlMessage());
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var flare = SEntMan.SpawnEntity(null, groundMap.GridCoords);
            SEntMan.AddComponent<FlareSignalComponent>(flare);
            weapons.MakeDropshipTarget(flare, "TEST");
            Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare)));
            mount.Gun.NextFire = TimeSpan.Zero;
            mount.Ammo.BulletSpread = 0;
            var chinGun = Server.System<MohawkChinGunSystem>();
            Assert.That(chinGun.CanOperate((mount.Point, mount.Chin), mount.Actor, mount.Weapon, out _), Is.True);
            Assert.That(chinGun.IsInFlareArea((mount.Point, mount.Chin), groundMap.GridCoords), Is.True);
            var rounds = mount.Ammo.Rounds;
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.False, "Squall cannot bypass its restriction through the CAS firing API.");
            Trigger(mount, true, groundMap.GridCoords);
            chinGun.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(rounds), "Manual airborne fire must not consume kinetic ammunition.");
            Hostile(groundMap.GridCoords.Offset(new Vector2(0, 4)));
            Send(mount, new MohawkChinGunFreeFireMessage());
            mount.Chin.NextScan = TimeSpan.Zero;
            chinGun.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(rounds), "AI free fire must also respect groundside-only ammunition.");
            Assert.That(SEntMan.QueryEntities<AmmoInFlightComponent>(), Is.Empty);
            Assert.That(mount.Gun.NextFire, Is.EqualTo(TimeSpan.Zero), "Blocked airborne shots do not incur a firing cooldown.");
            Send(mount, new MohawkChinGunFeedMessage(1));
            mount.Chin.NextScan = TimeSpan.Zero;
            chinGun.Update(0);
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(explosive).Rounds, Is.EqualTo(398),
                "Selecting the explosive feed must still allow airborne AI fire.");
            Assert.That(SEntMan.QueryEntities<AmmoInFlightComponent>().Single().Comp.Explosion, Is.Not.Null);
            SEntMan.RemoveComponent<FTLComponent>(shipMap.Grid);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(shipMap.Grid);
        });
    }

    [Test]
    public async Task FreeFireAcquiresBeyondCloseDefenseAndCooldownStartsAtEnd()
    {
        var map = await Pair.CreateTestMap();
        Mount mount = null!;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            mount = Create(map.Grid, false);
            var hostile = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 1.5f, 24));
            SEntMan.AddComponent<NpcFactionMemberComponent>(hostile).Factions.Add("OPFOR");
            SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(hostile);
            var chin = mount.Chin;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.Operator, Is.Null);
            Assert.That(chin.FreeFireConsole, Is.EqualTo(mount.Console));
            Assert.That((chin.FreeFireUntil - SGameTiming.CurTime).TotalSeconds, Is.EqualTo(120).Within(0.001));
            Assert.That((chin.FreeFireReadyAt - chin.FreeFireUntil).TotalSeconds, Is.EqualTo(300).Within(0.001));
            Assert.That(mount.Terminal.ChinGunFreeFireSeconds, Is.EqualTo(120));
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Ammo.Rounds, Is.LessThan(399),
                "AI repeatedly fires beyond the normal four-tile close-defense radius.");
            var chin = mount.Chin;
            chin.FreeFireUntil = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            var expiry = chin.FreeFireUntil;
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(chin.FreeFireUntil, Is.EqualTo(TimeSpan.Zero));
            Assert.That(chin.FreeFireReadyAt, Is.EqualTo(expiry + TimeSpan.FromMinutes(5)));
            Assert.That(mount.Terminal.ChinGunFreeFireCooldown, Is.EqualTo(299));
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireUntil, Is.EqualTo(TimeSpan.Zero), "Cooldown cannot be bypassed by reactivation.");
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(chin.Operator, Is.EqualTo(mount.Actor), "Cooldown does not prevent manual control.");
            chin.FreeFireReadyAt = SGameTiming.CurTime;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(chin.Operator, Is.EqualTo(mount.Actor));
            Assert.That((chin.FreeFireReadyAt - SGameTiming.CurTime).TotalSeconds, Is.EqualTo(300).Within(0.001),
                "Early manual takeover starts a full cooldown.");
            Assert.That(chin.FreeFireConsole, Is.Null);
        });
    }

    [Test]
    public async Task FlybyFreeFireUsesRegisteredFlarePriorityAndFriendlyClearance()
    {
        var shipMap = await Pair.CreateTestMap();
        var groundMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(shipMap.Grid, true);
            var chin = mount.Chin;
            var system = Server.System<MohawkChinGunSystem>();
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            mount.Ammo.BulletSpread = 0;
            var flare = SEntMan.SpawnEntity(null, groundMap.GridCoords);
            SEntMan.AddComponent<FlareSignalComponent>(flare);
            weapons.MakeDropshipTarget(flare, "TEST");
            Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare)));
            var terminal = mount.Terminal;
            var nearest = Hostile(groundMap.GridCoords.Offset(new Vector2(0, 1)));
            var healthiest = Hostile(groundMap.GridCoords.Offset(new Vector2(0, 5)));
            var outside = Hostile(groundMap.GridCoords.Offset(new Vector2(0, 11)));
            chin.TargetPriority = MohawkTargetPriority.HighestHealth;
            Server.System<Content.Shared.Damage.Systems.DamageableSystem>().TryChangeDamage(nearest,
                new Content.Shared.Damage.DamageSpecifier { DamageDict = new() { ["Blunt"] = 20 } }, true);
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireUntil, Is.GreaterThan(SGameTiming.CurTime));
            Assert.That(terminal.Target, Is.EqualTo(flare), "Releasing manual control preserves the AI flare context.");
            Assert.That(system.IsInFlareArea((mount.Point, chin), SEntMan.GetComponent<TransformComponent>(outside).Coordinates), Is.False);
            Assert.That(system.SelectTarget((mount.Point, chin)), Is.EqualTo(healthiest));
            chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399));
            var payload = SEntMan.QueryEntities<AmmoInFlightComponent>().Single().Comp;
            var impact = payload.Target.Position;
            Assert.That(Vector2.Distance(impact, new Vector2(0.5f, 5.5f)), Is.LessThanOrEqualTo(0.01f),
                "Highest-health priority, not nearest or outside-flare health, selects the AI target.");
            var friend = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(groundMap.Grid, impact));
            var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(friend);
            faction.Factions.Clear();
            faction.Factions.Add("GOVFOR");
            mount.Gun.NextFire = TimeSpan.Zero;
            chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399),
                "Free fire must not override friendly impact clearance.");
            chin.TargetPriority = MohawkTargetPriority.Distance;
            mount.Gun.NextFire = TimeSpan.Zero;
            chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(398));
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireUntil, Is.EqualTo(TimeSpan.Zero));
            Assert.That((chin.FreeFireReadyAt - SGameTiming.CurTime).TotalSeconds, Is.EqualTo(300).Within(0.001));
            terminal.Target = flare;
            chin.FreeFireReadyAt = SGameTiming.CurTime;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireUntil, Is.EqualTo(TimeSpan.Zero), "AI cannot start without valid manual flare selection.");
            SEntMan.RemoveComponent<FTLComponent>(shipMap.Grid);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(shipMap.Grid);
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
