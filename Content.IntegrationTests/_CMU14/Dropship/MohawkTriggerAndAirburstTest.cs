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
using Content.Shared.CMU14.Weapons.Ranged;
using Robust.Shared.Maths;

#pragma warning disable RA0002 // Arrange gun cooldowns, flight state and delayed impacts at system boundaries.

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class MohawkTriggerAndAirburstTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private sealed record Mount(EntityUid Ship, EntityUid Point, EntityUid Weapon, EntityUid Crate,
        EntityUid Console, EntityUid Actor, MohawkChinGunComponent Chin, DropshipTerminalWeaponsComponent Terminal,
        GunComponent Gun, DropshipAmmoComponent Ammo);

    private Mount Create(EntityUid grid, bool airborne, string? cratePrototype = null)
    {
        var coords = new EntityCoordinates(grid, Vector2.Zero);
        var maps = Server.System<SharedMapSystem>();
        var tiles = Server.ResolveDependency<ITileDefinitionManager>();
        var gridEnt = new Entity<MapGridComponent>(grid, SEntMan.GetComponent<MapGridComponent>(grid));
        for (var x = -2; x <= 11; x++)
        {
            for (var y = -2; y <= 10; y++)
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
        var crate = SEntMan.SpawnEntity(cratePrototype ?? (airborne ? "CMUMohawkM90Ammo" : "CMUMohawkM90AmmoKinetic"), coords);
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
        var chin = SEntMan.GetComponent<MohawkChinGunComponent>(point);
        chin.Calibrated = true;
        chin.IffConfigured = true;
        Server.System<Content.Shared._RMC14.Sentry.SharedSentryTargetingSystem>().SetFriendlyFactions(
            (point, SEntMan.GetComponent<Content.Shared._RMC14.Sentry.SentryTargetingComponent>(point)), ["GOVFOR"]);
        SEntMan.EnsureComponent<DropshipFireControlBootComponent>(grid).Complete = true;
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
    public async Task FirstBootFaultBlocksFireUntilPhysicalMultitoolResetWithoutReroll()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            Send(mount, new MohawkChinGunControlMessage());
            var containers = Server.System<SharedContainerSystem>();
            var internalPoint = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 5, 8));
            var utility = SEntMan.AddComponent<DropshipUtilityPointComponent>(internalPoint);
            SEntMan.GetComponent<DropshipComponent>(map.Grid).AttachmentPoints.Add(internalPoint);
            var internalWeapon = SEntMan.SpawnEntity("CMUDropshipAttachmentBOFORS", map.GridCoords);
            Assert.That(containers.Insert(internalWeapon, containers.EnsureContainer<ContainerSlot>(
                internalPoint, utility.UtilitySlotId)), Is.True);
            var crate = SEntMan.SpawnEntity("CMUDropshipAmmoBOFORSMagHE", map.GridCoords);
            Assert.That(containers.Insert(crate, containers.EnsureContainer<ContainerSlot>(
                internalPoint, utility.AmmoContainerSlotId)), Is.True);
            var rounds = SEntMan.GetComponent<DropshipAmmoComponent>(crate).Rounds;
            var ui = Server.System<SharedUserInterfaceSystem>();
            ui.CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            var boot = SEntMan.GetComponent<DropshipFireControlBootComponent>(map.Grid);
            boot.Complete = false;
            boot.ReadyAt = TimeSpan.Zero;
            var config = Server.ResolveDependency<IConfigurationManager>();
            Assert.That(config.GetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance), Is.EqualTo(0.02f),
                "Each installed weapon has a two-percent fault chance by default.");
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance, 1f);
            ui.OpenUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            Assert.That(boot.ReadyAt, Is.GreaterThan(SGameTiming.CurTime));
            mount.Chin.DefensiveFire = true;
            var system = Server.System<MohawkChinGunSystem>();
            Assert.That(system.CanOperate((mount.Point, mount.Chin), mount.Weapon, mount.Weapon, out _), Is.False);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1200));
            boot.ReadyAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            Server.System<DropshipFireControlBootSystem>().Update(0);
            Assert.That(boot.Complete, Is.True);
            var fault = SEntMan.GetComponent<DropshipHardpointFaultComponent>(mount.Point);
            Assert.That(fault.Faulted, Is.True);
            Assert.That(fault.Reason, Is.AnyOf(DropshipFireControlBootSystem.FaultReasons.Cast<object>().ToArray()),
                "Boot faults record a readable reason.");
            Assert.That(mount.Terminal.FireControlStatus, Does.Contain(Loc.GetString(fault.Reason!)));
            Assert.That(SEntMan.GetComponent<DropshipHardpointFaultComponent>(internalPoint).Faulted, Is.True,
                "Internal utility-mounted weapons participate in the same boot test.");
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            Assert.That(Server.System<SharedDropshipWeaponSystem>().TryFireWeapon(internalWeapon,
                new EntityCoordinates(map.MapUid, 30, 30), DropshipWeaponStrikeType.Direct), Is.False);
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(crate).Rounds, Is.EqualTo(rounds),
                "A faulted ordinary CAS weapon must not consume ammunition.");
            Assert.That(mount.Terminal.FireControlStatus, Does.Contain(SEntMan.GetComponent<MetaDataComponent>(mount.Point).EntityName));
            Assert.That(system.CanOperate((mount.Point, mount.Chin), mount.Weapon, mount.Weapon, out _), Is.False);
            var technician = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 0.5f)));
            var tool = SEntMan.SpawnEntity("CMMultitool", map.GridCoords);
            var hands = Server.System<Content.Shared.Hands.EntitySystems.SharedHandsSystem>();
            Assert.That(hands.TryPickupAnyHand(technician, tool), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(mount.Point, new Content.Shared.Interaction.InteractUsingEvent(
                technician, tool, mount.Point, map.GridCoords));
            Assert.That(SEntMan.TryGetComponent(technician, out Content.Shared.DoAfter.DoAfterComponent? unskilled) &&
                        unskilled.DoAfters.Values.Any(d => !d.Completed && !d.Cancelled), Is.False,
                "Untrained users cannot start a fault reset.");
            Server.System<SkillsSystem>().SetSkill(technician, "RMCSkillEngineer", 1);
            void Reset(bool cancel)
            {
                SEntMan.EventBus.RaiseLocalEvent(mount.Point, new Content.Shared.Interaction.InteractUsingEvent(
                    technician, tool, mount.Point, map.GridCoords));
                var after = SEntMan.GetComponent<Content.Shared.DoAfter.DoAfterComponent>(technician).DoAfters.Values.Single(d => !d.Completed && !d.Cancelled);
                var doAfter = Server.System<Content.Shared.DoAfter.SharedDoAfterSystem>();
                if (cancel)
                    doAfter.Cancel(after);
                else
                {
                    after.StartTime = SGameTiming.CurTime - after.Args.Delay - TimeSpan.FromSeconds(1);
                    doAfter.Update(0);
                }
            }
            Reset(true);
            Assert.That(fault.Faulted, Is.True);
            Reset(false);
            Assert.That(fault.Faulted, Is.False);
            Assert.That(fault.Reason, Is.Null);
            Assert.That(system.CanOperate((mount.Point, mount.Chin), mount.Weapon, mount.Weapon, out _), Is.True);
            ui.CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            ui.OpenUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            Server.System<DropshipFireControlBootSystem>().Update(0);
            Assert.That(fault.Faulted, Is.False, "Opening again must not reroll even with 100% configured faults.");
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance, 0.02f);
            mount.Chin.DefensiveFire = false;
        });
    }

    [Test]
    public async Task SuccessfulBootRunsOnceWithoutRerollingOnAnotherOpen()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            Send(mount, new MohawkChinGunControlMessage());
            var ui = Server.System<SharedUserInterfaceSystem>();
            ui.CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            var boot = SEntMan.GetComponent<DropshipFireControlBootComponent>(map.Grid);
            boot.Complete = false;
            boot.ReadyAt = TimeSpan.Zero;
            var config = Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance, 0f);
            ui.OpenUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            Assert.That(mount.Terminal.FireControlBooting, Is.True);
            boot.ReadyAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            var system = Server.System<DropshipFireControlBootSystem>();
            system.Update(0);
            Assert.That(boot.Complete, Is.True);
            Assert.That(mount.Terminal.FireControlBooting, Is.False);
            Assert.That(SEntMan.GetComponent<DropshipHardpointFaultComponent>(mount.Point).Faulted, Is.False);
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance, 1f);
            ui.CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            ui.OpenUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            system.Update(0);
            Assert.That(SEntMan.GetComponent<DropshipHardpointFaultComponent>(mount.Point).Faulted, Is.False);
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipBootFaultChance, 0.02f);
        });
    }

    [Test]
    public async Task LandingRebootRollsEscalatingWearFaults()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var config = Server.ResolveDependency<IConfigurationManager>();
            var system = Server.System<DropshipFireControlBootSystem>();
            var boot = SEntMan.EnsureComponent<DropshipFireControlBootComponent>(map.Grid);
            boot.Complete = true;
            var fault = SEntMan.EnsureComponent<DropshipHardpointFaultComponent>(mount.Point);

            system.RegisterUse(mount.Point);
            system.RegisterUse(mount.Point);
            Assert.That(fault.Uses, Is.EqualTo(1), "Continuous fire counts as one engagement.");
            fault.LastUse -= fault.UseWindow;
            system.RegisterUse(mount.Point);
            Assert.That(fault.Uses, Is.EqualTo(2));

            fault.Landings = 3;
            var normal = system.GetLandingFaultChance(fault, false);
            Assert.That(normal, Is.EqualTo((0.005f + 0.002f * 2) * 1.02f).Within(0.0001f),
                "Landing chance grows per landing and is multiplied by hardpoint use.");
            Assert.That(system.GetLandingFaultChance(fault, true), Is.EqualTo(normal * 3).Within(0.0001f),
                "Crash landings triple the chance.");
            fault.Landings = 500;
            Assert.That(system.GetLandingFaultChance(fault, true), Is.EqualTo(0.75f).Within(0.0001f));
            fault.Landings = 0;

            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipLandingFaultChance, 1f);
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipFaultMaxChance, 1f);
            system.OnLanded(map.Grid, false);
            Assert.That(boot.Complete, Is.False);
            Assert.That(boot.LandingReboot, Is.True);
            Assert.That(mount.Terminal.FireControlStatus, Does.Contain("POST-LANDING"));
            Assert.That(Server.System<MohawkChinGunSystem>().CanOperate((mount.Point, mount.Chin), mount.Weapon,
                mount.Weapon, out _), Is.False, "Weapons are offline while the post-landing diagnostic runs.");
            boot.ReadyAt = SGameTiming.CurTime - TimeSpan.FromSeconds(1);
            system.Update(0);
            Assert.That(boot.Complete, Is.True);
            Assert.That(boot.LandingReboot, Is.False);
            Assert.That(fault.Landings, Is.EqualTo(1));
            Assert.That(fault.Faulted, Is.True);
            Assert.That(fault.Reason, Is.Not.Null);
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipLandingFaultChance, 0.005f);
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipFaultMaxChance, 0.75f);

            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipLandingReboot, false);
            system.OnLanded(map.Grid, true);
            Assert.That(boot.Complete, Is.True, "The landing reboot can be disabled by CVar.");
            config.SetCVar(Content.Shared.CCVar.CCVars.DropshipLandingReboot, true);
        });
    }

    [Test]
    public async Task VisionModesCycleAndUpdateCameraEye()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var eye = SEntMan.GetComponent<EyeComponent>(mount.Chin.Eye!.Value);
            Assert.That(mount.Chin.VisionMode, Is.EqualTo(MohawkChinGunVisionMode.Normal));
            Assert.That(eye.DrawLight, Is.True);
            Send(mount, new MohawkChinGunVisionMessage());
            Assert.That(mount.Chin.VisionMode, Is.EqualTo(MohawkChinGunVisionMode.Night));
            Assert.That(mount.Terminal.ChinGunVision, Is.EqualTo(MohawkChinGunVisionMode.Night));
            Assert.That(eye.DrawLight, Is.False, "Night optics render fullbright.");
            Assert.That(eye.DrawFov, Is.True, "Night optics still respect walls groundside.");
            Send(mount, new MohawkChinGunVisionMessage());
            Assert.That(mount.Chin.VisionMode, Is.EqualTo(MohawkChinGunVisionMode.Thermal));
            Assert.That(eye.DrawFov, Is.False, "Thermal sees through walls even groundside.");
            Send(mount, new MohawkChinGunVisionMessage());
            Assert.That(mount.Chin.VisionMode, Is.EqualTo(MohawkChinGunVisionMode.Normal));
            Assert.That(eye.DrawLight, Is.True);
            Assert.That(eye.DrawFov, Is.True);
        });
    }

    [Test]
    public async Task PilotAndGunnerFlaresAreSharedAcrossShipConsoles()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var other = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, 2, 0));
            var otherTerminal = SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(other);
            var pilotFlare = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 4, 4));
            var gunnerFlare = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 6, 6));
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            weapons.ShareFlare(other, pilotFlare, false);
            weapons.ShareFlare(mount.Console, gunnerFlare, true);
            Assert.That(mount.Terminal.PilotFlare, Is.EqualTo(pilotFlare));
            Assert.That(otherTerminal.GunnerFlare, Is.EqualTo(gunnerFlare));
            Assert.That(otherTerminal.PilotFlare, Is.EqualTo(pilotFlare));
            weapons.ShareFlare(mount.Console, null, true);
            Assert.That(otherTerminal.GunnerFlare, Is.Null);
            SEntMan.DeleteEntity(pilotFlare);
            Assert.That(mount.Terminal.PilotFlare, Is.Null, "Deleted flares leave the shared list immediately.");
            Assert.That(otherTerminal.PilotFlare, Is.Null);
        });
    }

    [Test]
    public async Task DeletedAutomaticCameraAndTargetClearConsoleReferencesImmediately()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var target = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 1.5f, 16));
            SEntMan.AddComponent<NpcFactionMemberComponent>(target).Factions.Add("OPFOR");
            SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(target);
            Send(mount, new MohawkChinGunFreeFireMessage());
            var system = Server.System<MohawkChinGunSystem>();
            system.Update(0);
            Assert.That(mount.Terminal.ChinGunAiTarget, Is.EqualTo(target));
            Assert.That(mount.Terminal.ChinGunEye, Is.Not.Null);
            SEntMan.DeleteEntity(mount.Chin.AutomaticEye!.Value);
            Assert.That(mount.Terminal.ChinGunEye, Is.Null);
            Assert.That(mount.Chin.AutomaticEye, Is.Null);
            system.Update(0);
            Assert.That(mount.Terminal.ChinGunEye, Is.Not.Null);
            Send(mount, new MohawkChinGunApproveAiMessage(SEntMan.GetNetEntity(target)));
            SEntMan.DeleteEntity(target);
            Assert.That(mount.Terminal.ChinGunAiTarget, Is.Null);
            Assert.That(mount.Terminal.ChinGunAiApproved, Is.False);
            Assert.That(mount.Chin.ApprovedAutomaticTarget, Is.Null);
        });
        await Pair.RunSeconds(0.2f);
    }

    [Test]
    public async Task IffToggleControlsFriendlyClearanceAndGroundShotsOutsideCircleKeepTraveling()
    {
        var map = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid wall = default;
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var system = Server.System<MohawkChinGunSystem>();
            var friend = Hostile(new EntityCoordinates(map.MapUid, 1.5f, 6));
            var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(friend);
            faction.Factions.Clear();
            faction.Factions.Add("GOVFOR");
            var transform = Server.System<SharedTransformSystem>();
            var origin = transform.ToMapCoordinates(system.GetMountCoordinates((mount.Point, mount.Chin)));
            var far = new EntityCoordinates(map.MapUid, 1.5f, 30);
            Assert.That(system.IsSafeShot((mount.Point, mount.Chin), origin, transform.ToMapCoordinates(far), mount.Weapon), Is.False);
            Send(mount, new MohawkChinGunIffMessage());
            Assert.That(mount.Chin.IffEnabled, Is.False);
            Assert.That(mount.Terminal.ChinGunIff, Is.False);
            Assert.That(system.IsSafeShot((mount.Point, mount.Chin), origin, transform.ToMapCoordinates(far), mount.Weapon), Is.True);
            Assert.That(system.IsSafeAirburst(mount.Point, transform.GetMapCoordinates(friend), 2), Is.True);
            SEntMan.DeleteEntity(friend);
            wall = SEntMan.SpawnEntity("CMWallMetal", new EntityCoordinates(map.Grid, 1.5f, 23));
            mount.Gun.NextFire = TimeSpan.Zero;
            Trigger(mount, true, far);
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199), "Manual fire outside acquisition range still fires.");
            projectile = SEntMan.QueryEntities<ProjectileComponent>().Single().Owner;
            Assert.That(SEntMan.HasComponent<Content.Shared._RMC14.Weapons.Ranged.ProjectileFixedDistanceComponent>(projectile), Is.False);
            Assert.That(SEntMan.GetComponent<Content.Shared._RMC14.Weapons.Ranged.IFF.ProjectileIFFComponent>(projectile).Enabled, Is.False);
            Send(mount, new MohawkChinGunIffMessage());
            Assert.That(mount.Chin.IffEnabled, Is.True);
        });
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(projectile), Is.True, "A free-travel round still terminates on collision.");
            Assert.That(Server.System<Content.Shared.Damage.Systems.DamageableSystem>().GetTotalDamage(wall).Float(),
                Is.GreaterThan(0), "A wall before the distant cursor intercepts the round.");
        });
    }

    [Test]
    public async Task GroundCameraCannotPanThroughOpaqueWallsButFlybyDisablesFov()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var system = Server.System<MohawkChinGunSystem>();
            var transform = Server.System<SharedTransformSystem>();
            Assert.That(SEntMan.GetComponent<EyeComponent>(mount.Chin.Eye!.Value).DrawFov, Is.True);
            var wall = SEntMan.SpawnEntity("CMWallMetal", new EntityCoordinates(map.Grid, 1.5f, 5));
            mount.Chin.CameraPan = new Vector2(0, 15);
            system.Update(0);
            var eyePosition = transform.GetMapCoordinates(mount.Chin.Eye!.Value);
            var wallPosition = transform.GetMapCoordinates(wall);
            Assert.That(eyePosition.Position.Y, Is.LessThan(wallPosition.Position.Y),
                "A camera pan cannot move the eye behind a blocking wall.");
            SEntMan.DeleteEntity(wall);
            system.Update(0);
            Assert.That(transform.GetMapCoordinates(mount.Chin.Eye!.Value).Position.Y,
                Is.GreaterThan(wallPosition.Position.Y));
            Send(mount, new MohawkChinGunControlMessage());
            SEntMan.EnsureComponent<FTLComponent>(map.Grid).State = FTLState.Travelling;
            SEntMan.AddComponent<DropshipInFlyByComponent>(map.Grid);
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(mount.Chin.Operator, Is.EqualTo(mount.Actor));
            Assert.That(SEntMan.GetComponent<EyeComponent>(mount.Chin.Eye!.Value).DrawFov, Is.False);
            SEntMan.RemoveComponent<FTLComponent>(map.Grid);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(map.Grid);
        });
    }

    [Test]
    public async Task ManualArcIs225DegreesButCloseDefenseCoversRearThreatsWithinEightTiles()
    {
        var map = await Pair.CreateTestMap();
        Mount mount = null!;
        await Server.WaitAssertion(() =>
        {
            mount = Create(map.Grid, false);
            var system = Server.System<MohawkChinGunSystem>();
            var origin = new Vector2(1.5f, 1);
            EntityCoordinates Bearing(float degrees, float distance) =>
                new(map.MapUid, origin + Angle.FromDegrees(degrees).RotateVec(Vector2.UnitY * distance));
            foreach (var degrees in new[] { -112.5f, 112.5f })
            {
                Assert.That(system.IsWithinArc((mount.Point, mount.Chin), mount.Weapon, Bearing(degrees, 10)), Is.True);
                Assert.That(system.RequestAim((mount.Point, mount.Chin), mount.Weapon, Bearing(degrees, 10)), Is.False,
                    "A valid new bearing still waits for slew alignment.");
                Assert.That(MathF.Abs(mount.Chin.DesiredAimDegrees), Is.EqualTo(112.5f).Within(0.001));
            }
            Assert.That(system.IsWithinArc((mount.Point, mount.Chin), mount.Weapon, Bearing(113, 10)), Is.False);
            Assert.That(system.IsWithinArc((mount.Point, mount.Chin), mount.Weapon, Bearing(180, 7.9f)), Is.False);
            Assert.That(mount.Chin.Range, Is.EqualTo(20));
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(system.IsWithinArc((mount.Point, mount.Chin), mount.Weapon, Bearing(180, 7.9f)), Is.True);
            Assert.That(system.IsWithinArc((mount.Point, mount.Chin), mount.Weapon, Bearing(180, 8.1f)), Is.False);
            var target = SEntMan.SpawnEntity(null, Bearing(180, 7));
            SEntMan.AddComponent<NpcFactionMemberComponent>(target).Factions.Add("OPFOR");
            SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(target);
            Assert.That(system.SelectTarget((mount.Point, mount.Chin), mount.Chin.CloseDefenseRange), Is.EqualTo(target));
            mount.Chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(MathF.Abs(mount.Chin.DesiredAimDegrees), Is.EqualTo(180).Within(0.001));
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1200), "Close defense waits for individual approval.");
            Send(mount, new MohawkChinGunApproveAiMessage(SEntMan.GetNetEntity(target)));
            system.Update(6);
            mount.Chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199), "An approved aligned rear close threat permits defense fire.");
        });
    }

    [Test]
    public async Task AutonomousFlybyScansEveryFlareChoosesPayloadAndRetainsTarget()
    {
        var shipMap = await Pair.CreateTestMap();
        var groundMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(shipMap.Grid, true, "CMUMohawkM90AmmoKinetic");
            var point = SEntMan.GetComponent<DropshipWeaponPointComponent>(mount.Point);
            var heavyCrate = SEntMan.SpawnEntity("CMUMohawkM90AmmoDevastator", shipMap.GridCoords);
            Assert.That(Server.System<SharedContainerSystem>().Insert(heavyCrate,
                Server.System<SharedContainerSystem>().GetContainer(mount.Point, point.SecondAmmoContainerSlotId!)), Is.True);
            var heavy = SEntMan.GetComponent<DropshipAmmoComponent>(heavyCrate);
            mount.Ammo.BulletSpread = 0;
            heavy.BulletSpread = 0;
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            EntityUid Flare(Vector2 offset)
            {
                var flare = SEntMan.SpawnEntity(null, groundMap.GridCoords.Offset(offset));
                SEntMan.AddComponent<FlareSignalComponent>(flare);
                weapons.MakeDropshipTarget(flare, "TEST");
                return flare;
            }
            var quiet = Flare(Vector2.Zero);
            var active = Flare(new Vector2(40, 0));
            var far = Flare(new Vector2(80, 0));
            Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(quiet)));
            var first = Hostile(groundMap.GridCoords.Offset(new Vector2(40, 1)));
            Server.System<Content.Shared.Mobs.Systems.MobThresholdSystem>()
                .SetMobStateThreshold(first, 100, Content.Shared.Mobs.MobState.Dead);
            Send(mount, new MohawkChinGunFreeFireMessage());
            void Scan()
            {
                mount.Chin.NextScan = TimeSpan.Zero;
                mount.Gun.NextFire = TimeSpan.Zero;
                Server.System<MohawkChinGunSystem>().Update(0);
            }
            Scan();
            Assert.That(mount.Terminal.Target, Is.EqualTo(active), "The AI must leave a quiet manually selected flare.");
            Assert.That(mount.Chin.AutomaticTarget, Is.EqualTo(first));
            Assert.That(mount.Chin.ActiveFeed, Is.Zero, "A soft isolated target favors rapid kinetic fire.");
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1200), "Scanning selects a feed but never authorizes fire.");
            Send(mount, new MohawkChinGunApproveAiMessage(SEntMan.GetNetEntity(first)));
            Scan();
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199));
            var second = Hostile(groundMap.GridCoords.Offset(new Vector2(40, 2)));
            var third = Hostile(groundMap.GridCoords.Offset(new Vector2(41, 1)));
            Scan();
            Assert.That(mount.Chin.AutomaticTarget, Is.EqualTo(first), "A larger cluster must not cause target thrashing.");
            Assert.That(mount.Chin.ActiveFeed, Is.EqualTo(1), "Clustered threats favor the loaded area-effect payload.");
            Assert.That(heavy.Rounds, Is.EqualTo(399));
            Assert.That(mount.Chin.AlternateFeeds, Is.False, "Different payloads use the selected tactical feed.");
            var friend = Hostile(groundMap.GridCoords.Offset(new Vector2(40, 5)));
            var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(friend);
            faction.Factions.Clear();
            faction.Factions.Add("GOVFOR");
            Scan();
            Assert.That(mount.Chin.ActiveFeed, Is.Zero, "Nearby friendlies rule out the wider explosive payload.");
            Assert.That(heavy.Rounds, Is.EqualTo(399));
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1198));
            foreach (var target in new[] { first, second, third })
                SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(target).CurrentState = Content.Shared.Mobs.MobState.Critical;
            var next = Hostile(groundMap.GridCoords.Offset(new Vector2(80, 1)));
            Scan();
            Assert.That(mount.Terminal.Target, Is.EqualTo(far));
            Assert.That(mount.Chin.AutomaticTarget, Is.EqualTo(next),
                "Critical targets release the retained lock and permit acquisition under another flare.");
            SEntMan.RemoveComponent<FlareSignalComponent>(far);
            Scan();
            Assert.That(mount.Chin.AutomaticTarget, Is.Null);
            Assert.That(mount.Terminal.Target, Is.Null, "An expired flare cannot remain an engagement anchor.");
            SEntMan.RemoveComponent<FTLComponent>(shipMap.Grid);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(shipMap.Grid);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FlareLockWarnsNearbyPlayerInChatButNotDistantPlayerOrRepeatedLock(bool popupsInChat)
    {
        var map = await Pair.CreateTestMap();
        Mount mount = null!;
        EntityUid nearby = default;
        EntityUid distant = default;
        await Server.WaitAssertion(() =>
        {
            mount = Create(map.Grid, true);
            var weapons = Server.System<SharedDropshipWeaponSystem>();
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            EntityUid Flare(float x)
            {
                var flare = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(x, 2)));
                SEntMan.AddComponent<FlareSignalComponent>(flare);
                weapons.MakeDropshipTarget(flare, "TEST");
                return flare;
            }
            nearby = Flare(10);
            distant = Flare(40);
        });
        await Pair.RunUntilSynced();
        var warnings = new List<Content.Shared.Chat.ChatMessage>();
        var controller = Client.ResolveDependency<Robust.Client.UserInterface.IUserInterfaceManager>()
            .GetUIController<Content.Client.UserInterface.Systems.Chat.ChatUIController>();
        var config = Client.ResolveDependency<IConfigurationManager>();
        var previousPopupsInChat = config.GetCVar(Content.Shared.CCVar.CCVars.PopupsInChat);
        void Capture(Content.Shared.Chat.ChatMessage message)
        {
            if (message.Channel == Content.Shared.Chat.ChatChannel.Server &&
                message.Message.Contains("M90 gunner locked"))
                warnings.Add(message);
        }
        await Client.WaitPost(() =>
        {
            config.SetCVar(Content.Shared.CCVar.CCVars.PopupsInChat, popupsInChat);
            controller.MessageAdded += Capture;
        });
        try
        {
            await Server.WaitPost(() => Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(nearby))));
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() => Assert.That(warnings, Has.Count.EqualTo(1)));
            await Server.WaitPost(() => Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(distant))));
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() => Assert.That(warnings, Has.Count.EqualTo(1),
                "A player outside the engagement radius receives no warning."));
            await Server.WaitPost(() => Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(nearby))));
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() => Assert.That(warnings, Has.Count.EqualTo(1),
                "Repeated locks cannot spam the same flare warning within thirty seconds."));
        }
        finally
        {
            await Client.WaitPost(() =>
            {
                controller.MessageAdded -= Capture;
                config.SetCVar(Content.Shared.CCVar.CCVars.PopupsInChat, previousPopupsInChat);
            });
            await Server.WaitPost(() =>
            {
                SEntMan.RemoveComponent<FTLComponent>(map.Grid);
                SEntMan.RemoveComponent<DropshipInFlyByComponent>(map.Grid);
            });
        }
    }

    [Test]
    public async Task GroundShotsStopAtCursorAndPassThroughCorpses()
    {
        var map = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid behind = default;
        var aim = new EntityCoordinates(map.Grid, 1.5f, 12);
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            var mount = Create(map.Grid, false);
            var corpse = Hostile(new EntityCoordinates(map.Grid, 1.5f, 6));
            SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(corpse).CurrentState = Content.Shared.Mobs.MobState.Dead;
            behind = Hostile(new EntityCoordinates(map.Grid, 1.5f, 16));
            Trigger(mount, true, aim);
            Server.System<MohawkChinGunSystem>().Update(0);
            Trigger(mount, false);
            projectile = SEntMan.QueryEntities<ProjectileComponent>().Single().Owner;
            var stop = SEntMan.GetComponent<Content.Shared._RMC14.Weapons.Ranged.ProjectileFixedDistanceComponent>(projectile);
            Assert.That(stop.TargetCoordinates, Is.EqualTo(Server.System<SharedTransformSystem>().ToMapCoordinates(aim)));
            Assert.That(stop.FlyEndTime - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(9.75f / 40)).Within(TimeSpan.FromMilliseconds(1)));
        });
        await Pair.RunSeconds(0.4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(projectile), Is.True, "The cursor endpoint must terminate the round.");
            var impacts = SEntMan.QueryEntities<MetaDataComponent>()
                .Where(ent => ent.Comp.EntityPrototype?.ID == "CMUMohawkM90KineticImpact").ToArray();
            Assert.That(impacts.Any(ent => Vector2.Distance(
                Server.System<SharedTransformSystem>().GetMapCoordinates(ent.Owner).Position,
                Server.System<SharedTransformSystem>().ToMapCoordinates(aim).Position) < 0.01f), Is.True,
                "The visual impact belongs at the cursor, not on the corpse.");
            Assert.That(SEntMan.GetComponent<DamageableComponent>(behind).Damage.GetTotal().Int(), Is.Zero,
                "A living hostile behind the cursor must not be struck.");
        });
    }

    [Test]
    public async Task HighestHealthAiKeepsItsTargetUntilCritical()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var turret = Server.System<MohawkChinGunSystem>();
            var first = Hostile(new EntityCoordinates(map.MapUid, 1.5f, 16));
            var second = Hostile(new EntityCoordinates(map.MapUid, 1.5f, 17));
            var damage = Server.System<Content.Shared.Damage.Systems.DamageableSystem>();
            damage.TryChangeDamage(second, new Content.Shared.Damage.DamageSpecifier { DamageDict = new() { ["Blunt"] = 10 } }, true);
            mount.Chin.TargetPriority = MohawkTargetPriority.HighestHealth;
            Assert.That(turret.SelectAutomaticTarget((mount.Point, mount.Chin)), Is.EqualTo(first));
            damage.TryChangeDamage(first, new Content.Shared.Damage.DamageSpecifier { DamageDict = new() { ["Blunt"] = 20 } }, true);
            Assert.That(turret.SelectTarget((mount.Point, mount.Chin)), Is.EqualTo(second),
                "The other candidate now has more health.");
            Assert.That(turret.SelectAutomaticTarget((mount.Point, mount.Chin)), Is.EqualTo(first),
                "Damage must not make highest-health AI oscillate between targets.");
            SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(first).CurrentState = Content.Shared.Mobs.MobState.Critical;
            Assert.That(turret.SelectAutomaticTarget((mount.Point, mount.Chin)), Is.EqualTo(second),
                "AI switches at critical health rather than waiting for death.");
            SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(second).CurrentState = Content.Shared.Mobs.MobState.Critical;
            Assert.That(turret.SelectAutomaticTarget((mount.Point, mount.Chin)), Is.Null);
        });
    }

    [Test]
    public async Task MarkingUsesAmmoEligibilityInsteadOfHostileFaction()
    {
        var map = await Pair.CreateTestMap();
        var ground = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var turret = Server.System<MohawkChinGunSystem>();
            var coordinates = new EntityCoordinates(map.MapUid, 1.5f, 16);
            var living = Hostile(coordinates);
            var faction = SEntMan.GetComponent<NpcFactionMemberComponent>(living);
            faction.Factions.Clear();
            faction.Factions.Add("GOVFOR");
            Send(mount, new MohawkChinGunTrackMessage(SEntMan.GetNetCoordinates(coordinates)));
            Assert.That(mount.Chin.TrackedTarget, Is.EqualTo(living), "Even living friendlies can be marked.");
            mount.Gun.NextFire = TimeSpan.Zero;
            Send(mount, new MohawkChinGunConfirmTrackingMessage(SEntMan.GetNetEntity(living)));
            turret.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1200), "A friendly mark must not bypass firing safety.");
            SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(living).CurrentState = Content.Shared.Mobs.MobState.Dead;
            Send(mount, new MohawkChinGunTrackMessage(SEntMan.GetNetCoordinates(coordinates)));
            Assert.That(mount.Chin.TrackedTarget, Is.Null, "Squall cannot mark corpses.");
            var item = SEntMan.SpawnEntity("CMWrench", new EntityCoordinates(map.MapUid, 1.5f, 26));
            Assert.That(turret.CanTrack((mount.Point, mount.Chin), item), Is.False, "Squall requires a living entity.");
            Send(mount, new MohawkChinGunControlMessage());
            SEntMan.EnsureComponent<FTLComponent>(map.Grid).State = FTLState.Travelling;
            SEntMan.AddComponent<DropshipInFlyByComponent>(map.Grid);
            var containers = Server.System<SharedContainerSystem>();
            var point = SEntMan.GetComponent<DropshipWeaponPointComponent>(mount.Point);
            var explosive = SEntMan.SpawnEntity("CMUMohawkM90Ammo", map.GridCoords);
            Assert.That(containers.Insert(explosive, containers.GetContainer(mount.Point, point.SecondAmmoContainerSlotId!)), Is.True);
            Send(mount, new MohawkChinGunFeedMessage(1));
            Send(mount, new MohawkChinGunControlMessage());
            Server.ResolveDependency<IConfigurationManager>().SetCVar(RMCCVars.RMCDropshipCASDebug, true);
            var flare = SEntMan.SpawnEntity(null, ground.GridCoords);
            SEntMan.AddComponent<FlareSignalComponent>(flare);
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(flare, "MARK");
            Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare)));
            var prop = SEntMan.SpawnEntity("CMWrench", ground.GridCoords.Offset(new Vector2(0, 3)));
            Send(mount, new MohawkChinGunTrackMessage(SEntMan.GetNetCoordinates(SEntMan.GetComponent<TransformComponent>(prop).Coordinates)));
            Assert.That(mount.Chin.TrackedTarget, Is.EqualTo(prop), "Explosive ammunition can mark a nonliving entity without a faction.");
            SEntMan.RemoveComponent<FTLComponent>(map.Grid);
            SEntMan.RemoveComponent<DropshipInFlyByComponent>(map.Grid);
        });
    }

    [Test]
    public async Task FeedsAndFiringRespectReciprocalGroundAndFlybyRestrictions()
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
            explosive = SEntMan.SpawnEntity("CMUMohawkM90AmmoScatter", shipMap.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(explosive, containers.GetContainer(mount.Point, point.SecondAmmoContainerSlotId!)), Is.True);
            Send(mount, new MohawkChinGunFeedMessage(1));
            Assert.That(mount.Chin.ActiveFeed, Is.Zero, "Groundside operators cannot select airborne-only ammunition.");
            mount.Chin.ActiveFeed = 1;
            Trigger(mount, true, shipMap.GridCoords.Offset(new Vector2(1.5f, 15)));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(explosive).Rounds, Is.EqualTo(400),
                "Stale feed selections cannot bypass the groundside firing restriction.");
            mount.Chin.ActiveFeed = 0;
            Trigger(mount, true, shipMap.GridCoords.Offset(new Vector2(1.5f, 15)));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199));
            Assert.That(mount.Ammo.MaxRounds, Is.EqualTo(1200));
            Assert.That(mount.Gun.NextFire - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(1f / 15)).Within(TimeSpan.FromMilliseconds(1)));
        });
        await Server.WaitAssertion(() =>
        {
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
                groundMap.GridCoords, mount.Terminal), Is.True, "Squall supports delayed flyby impacts.");
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(rounds - 1));
            mount.Gun.NextFire = TimeSpan.Zero;
            Trigger(mount, true, groundMap.GridCoords);
            chinGun.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(rounds - 2), "Manual airborne fire consumes kinetic ammunition.");
            Hostile(groundMap.GridCoords.Offset(new Vector2(0, 4)));
            Send(mount, new MohawkChinGunFreeFireMessage());
            mount.Chin.NextScan = TimeSpan.Zero;
            chinGun.Update(0);
            Assert.That(SEntMan.QueryEntities<AmmoInFlightComponent>().Count(), Is.EqualTo(2));
            Assert.That(SEntMan.QueryEntities<CMUSpentCasingComponent>(), Is.Empty, "Squall never ejects casings.");
            Assert.That(mount.Gun.NextFire - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(1f / 15)).Within(TimeSpan.FromMilliseconds(1)));
            Send(mount, new MohawkChinGunControlMessage());
            Send(mount, new MohawkChinGunFeedMessage(1));
            Send(mount, new MohawkChinGunFlareMessage(SEntMan.GetNetEntity(flare)));
            mount.Gun.NextFire = TimeSpan.Zero;
            Assert.That(weapons.TryFireChinGunAirburst((mount.Point, mount.Chin), mount.Weapon, mount.Actor,
                groundMap.GridCoords, mount.Terminal), Is.True);
            Assert.That(SEntMan.GetComponent<DropshipAmmoComponent>(explosive).Rounds, Is.EqualTo(399),
                "Manual feed choices must remain available during a flyby.");
            Assert.That(mount.Gun.NextFire - SGameTiming.CurTime, Is.EqualTo(TimeSpan.FromSeconds(1f / 3)).Within(TimeSpan.FromMilliseconds(1)));
            Send(mount, new MohawkChinGunFeedMessage(0));
            Assert.That(mount.Chin.ActiveFeed, Is.Zero, "Flyby operators may select Squall.");
            Assert.That(SEntMan.QueryEntities<AmmoInFlightComponent>().Count(ent => ent.Comp.Explosion != null), Is.EqualTo(1));
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
            var hostile = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 1.5f, 16));
            SEntMan.AddComponent<NpcFactionMemberComponent>(hostile).Factions.Add("OPFOR");
            SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(hostile);
            var chin = mount.Chin;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.Operator, Is.Null);
            Assert.That(chin.FreeFireConsole, Is.EqualTo(mount.Console));
            Assert.That(chin.FreeFireEnabled, Is.True);
            Assert.That(mount.Terminal.ChinGunAiEnabled, Is.True);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var chin = mount.Chin;
            Assert.That(chin.AutomaticTarget, Is.Not.Null);
            Assert.That(mount.Ammo.Rounds, Is.LessThan(1200), "Groundside free fire engages without per-target approval.");
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireEnabled, Is.False);
            Assert.That((chin.FreeFireReadyAt - SGameTiming.CurTime).TotalSeconds, Is.EqualTo(300).Within(0.001));
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireEnabled, Is.False, "Cooldown cannot be bypassed by reactivation.");
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(chin.Operator, Is.EqualTo(mount.Actor), "Cooldown does not prevent manual control.");
            chin.FreeFireReadyAt = SGameTiming.CurTime;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Send(mount, new MohawkChinGunControlMessage());
            Assert.That(chin.Operator, Is.EqualTo(mount.Actor));
            Assert.That(chin.FreeFireEnabled, Is.True, "Manual takeover only pauses AI.");
            Assert.That(chin.ApprovedAutomaticTarget, Is.Null);
        });
    }

    [Test]
    public async Task GroundsideFreeFireForcesIffUntilSelfDefenseOrOverwhelmed()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var chin = mount.Chin;
            var turret = Server.System<MohawkChinGunSystem>();
            void Evaluate()
            {
                chin.NextIffEvaluation = TimeSpan.Zero;
                turret.Update(0);
            }
            EntityUid Spawn(float y)
            {
                var hostile = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 1.5f, y));
                SEntMan.AddComponent<NpcFactionMemberComponent>(hostile).Factions.Add("OPFOR");
                SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(hostile);
                return hostile;
            }

            Send(mount, new MohawkChinGunIffMessage());
            Assert.That(chin.IffEnabled, Is.False);
            Evaluate();
            Assert.That(chin.IffOverride, Is.EqualTo(MohawkIffOverride.None), "Manual control never forces IFF.");

            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireEnabled, Is.True);
            Evaluate();
            Assert.That(chin.IffOverride, Is.EqualTo(MohawkIffOverride.Forced));
            Assert.That(MohawkChinGunSystem.IsIffActive(chin), Is.True,
                "Groundside AI free fire holds IFF on even with the gunner's toggle off.");
            Assert.That(mount.Terminal.ChinGunIffOverride, Is.EqualTo(MohawkIffOverride.Forced));

            Spawn(16);
            Evaluate();
            Assert.That(chin.IffOverride, Is.EqualTo(MohawkIffOverride.Forced), "A single distant hostile keeps the lock.");

            var close = Spawn(4);
            Evaluate();
            Assert.That(chin.IffOverride, Is.EqualTo(MohawkIffOverride.SelfDefense));
            Assert.That(MohawkChinGunSystem.IsIffActive(chin), Is.False, "Self defense returns IFF to the gunner's toggle.");

            SEntMan.DeleteEntity(close);
            Spawn(14);
            Spawn(15);
            Spawn(17);
            Evaluate();
            Assert.That(chin.IffOverride, Is.EqualTo(MohawkIffOverride.Overwhelmed));
        });
    }

    [Test]
    public async Task LowAmmoWarnsOnceBelowAQuarterCrate()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = Create(map.Grid, false);
            var turret = Server.System<MohawkChinGunSystem>();
            turret.Update(0);
            Assert.That(mount.Terminal.ChinGunLowAmmo, Is.Empty);
            mount.Ammo.Rounds = mount.Ammo.MaxRounds / 4 - 1;
            turret.Update(0);
            Assert.That(mount.Terminal.ChinGunLowAmmo, Does.Contain("F1"));
            Assert.That(mount.Chin.LowAmmoWarned, Does.Contain(mount.Crate), "Each crate warns the gunner once.");
            mount.Ammo.Rounds = mount.Ammo.MaxRounds;
            turret.Update(0);
            Assert.That(mount.Terminal.ChinGunLowAmmo, Is.Empty);
            Assert.That(mount.Chin.LowAmmoWarned, Is.Empty, "Refilled crates re-arm the warning.");
        });
    }

    [Test]
    public async Task ApprovedAiSquallFiringIsNotThrottledByAcquisitionCadence()
    {
        var map = await Pair.CreateTestMap();
        Mount mount = null!;
        TimeSpan started = default;
        var startingRounds = 0;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            mount = Create(map.Grid, false);
            var target = SEntMan.SpawnEntity(null, new EntityCoordinates(map.MapUid, 1.5f, 16));
            SEntMan.AddComponent<NpcFactionMemberComponent>(target).Factions.Add("OPFOR");
            SEntMan.AddComponent<Content.Shared.Mobs.Components.MobStateComponent>(target);
            Send(mount, new MohawkChinGunFreeFireMessage());
            mount.Gun.NextFire = TimeSpan.Zero;
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199), "Groundside AI engages without per-target approval.");
            started = SGameTiming.CurTime;
            startingRounds = mount.Ammo.Rounds;
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var expected = (int) Math.Floor((SGameTiming.CurTime - started).TotalSeconds * 15);
            Assert.That(startingRounds - mount.Ammo.Rounds, Is.InRange(expected - 1, expected + 1),
                "900 RPM needs fifteen rounds per second, independent of 100ms AI scans.");
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
            Assert.That(chin.FreeFireEnabled, Is.True);
            Assert.That(terminal.Target, Is.EqualTo(flare), "Releasing manual control preserves the AI flare context.");
            Assert.That(system.IsInFlareArea((mount.Point, chin), SEntMan.GetComponent<TransformComponent>(outside).Coordinates), Is.False);
            Assert.That(system.SelectTarget((mount.Point, chin)), Is.EqualTo(healthiest));
            chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(400));
            Send(mount, new MohawkChinGunApproveAiMessage(SEntMan.GetNetEntity(healthiest)));
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
            SEntMan.GetComponent<Content.Shared.Mobs.Components.MobStateComponent>(healthiest).CurrentState = Content.Shared.Mobs.MobState.Critical;
            mount.Gun.NextFire = TimeSpan.Zero;
            chin.NextScan = TimeSpan.Zero;
            system.Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(399), "A replacement AI lock needs fresh approval.");
            Assert.That(chin.ApprovedAutomaticTarget, Is.Null);
            Send(mount, new MohawkChinGunApproveAiMessage(SEntMan.GetNetEntity(healthiest)));
            Assert.That(chin.ApprovedAutomaticTarget, Is.Null, "A stale approval cannot authorize the replacement target.");
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireEnabled, Is.False);
            Assert.That((chin.FreeFireReadyAt - SGameTiming.CurTime).TotalSeconds, Is.EqualTo(300).Within(0.001));
            terminal.Target = flare;
            chin.FreeFireReadyAt = SGameTiming.CurTime;
            Send(mount, new MohawkChinGunFreeFireMessage());
            Assert.That(chin.FreeFireEnabled, Is.True,
                "AI can scan registered flares without manual flare selection.");
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
        var stoppedRounds = 0;
        await Server.WaitAssertion(() =>
        {
            Server.ResolveDependency<IConfigurationManager>().SetCVar(Robust.Shared.CVars.NetTickrate, 30);
            mount = Create(map.Grid, false);
            Trigger(mount, true, target);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(1199));
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
            Assert.That(mount.Ammo.Rounds, Is.LessThan(1198), "Holding and renewing should fire repeatedly at fifteen rounds per second.");
            stoppedRounds = mount.Ammo.Rounds;
            Trigger(mount, false);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(stoppedRounds), "Releasing the button must stop fire.");
            Trigger(mount, true, map.GridCoords.Offset(new Vector2(-15, 1)));
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Chin.PendingManualShot, Is.Not.Null, "A held shot may wait for slew alignment.");
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(mount.Chin.HeldAim, Is.Null);
            Assert.That(mount.Chin.PendingManualShot, Is.Null, "An expired trigger lease must cancel a queued shot.");
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(stoppedRounds));
            SEntMan.GetComponent<GunshipDirectFirePointComponent>(mount.Point).AimOffsetDegrees = 0;
            mount.Chin.DesiredAimDegrees = 0;
            Trigger(mount, true, target);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Ammo.Rounds, Is.EqualTo(stoppedRounds - 1));
            Server.System<SharedUserInterfaceSystem>().CloseUi(mount.Console, DropshipTerminalWeaponsUi.Key, mount.Actor);
            Server.System<MohawkChinGunSystem>().Update(0);
            Assert.That(mount.Chin.Operator, Is.Null);
            Assert.That(mount.Chin.HeldAim, Is.Null);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(mount.Ammo.Rounds, Is.EqualTo(stoppedRounds - 1)));
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
            Assert.That(turret.IsAirborne(mount.Point), Is.True, "Transit flight permits full 90mm usage.");
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
