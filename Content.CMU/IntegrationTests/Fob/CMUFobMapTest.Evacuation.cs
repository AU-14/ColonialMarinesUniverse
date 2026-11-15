#pragma warning disable RA0002 // Arrange stale messages and short real flight/reserve deadlines.

using System.Numerics;
using Content.Server.CMU14.Fob;
using Content.Server.CMU14.WithdrawConsole;
using Content.Server.GameTicking;
using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Xenonids.Maturing;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.CMU14.WithdrawConsole;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobMapTest
{
    private WithdrawConsoleSystem Withdrawal => Server.System<WithdrawConsoleSystem>();

    private Entity<DropshipNavigationComputerComponent> PrepareFlight()
    {
        SEntMan.EnsureComponent<RMCPlanetComponent>(_map);
        SEntMan.EnsureComponent<ShuttleComponent>(_ship);
        var tile = Maps.GetTileRef(_map, SEntMan.GetComponent<MapGridComponent>(_map),
            SEntMan.GetComponent<TransformComponent>(Site("Nexus")).Coordinates).Tile;
        var grid = SEntMan.GetComponent<MapGridComponent>(_ship);
        for (var x = 0; x < 4; x++)
        for (var y = 0; y < 3; y++)
            Maps.SetTile((_ship.Owner, grid), new Vector2i(x, y), tile);
        Transform.SetCoordinates(_ship, SEntMan.GetComponent<TransformComponent>(Site("Nexus")).Coordinates);
        var nav = SEntMan.SpawnAtPosition("CMComputerDropshipNavigationGovfor", new EntityCoordinates(_ship, 0.5f, 0.5f));
        return (nav, SEntMan.GetComponent<DropshipNavigationComputerComponent>(nav));
    }

    private Entity<WithdrawConsoleComponent> WithdrawalConsole(string faction)
    {
        var uid = SEntMan.SpawnAtPosition("AU14WithdrawConsoleGovFor",
            SEntMan.GetComponent<TransformComponent>(Site("Nexus")).Coordinates);
        var console = SEntMan.GetComponent<WithdrawConsoleComponent>(uid);
        console.Faction = faction;
        console.IsUnlocked = true;
        return (uid, console);
    }

    private void ProtectedReturn(Entity<DropshipNavigationComputerComponent> nav, EntityUid actor) =>
        SEntMan.EventBus.RaiseLocalEvent(nav.Owner, new DropshipWithdrawReturnMsg
        {
            Actor = actor, UiKey = DropshipNavigationUiKey.Key,
        });

    [Test]
    public async Task ContestedEvacuationRejectsStaleWithdrawalLocksReturnAndStalemate()
    {
        Entity<WithdrawConsoleComponent> console = default;
        Entity<DropshipNavigationComputerComponent> nav = default;
        await Server.WaitAssertion(() =>
        {
            StartRound();
            LoadSurface();
            nav = PrepareFlight();
            console = WithdrawalConsole("govfor");
            // Before the selector's first update or first landing, seeded state cannot grant protection.
            console.Comp.HijackLockApplied = true;
            console.Comp.DropdownLockApplied = true;
            Assert.That(Withdrawal.IsWithdrawReturnUnlocked("govfor"), Is.False);
            Assert.That(Withdrawal.IsHijackBlocked("govfor"), Is.False);
            Assert.That(Withdrawal.IsDropdownBlocked("govfor"), Is.False);
            var lockEvent = new WithdrawFactionHijackLockEvent("govfor");
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref lockEvent);
            Assert.That(nav.Comp.Hijackable, Is.True);
            ProtectedReturn(nav, nav);
            Assert.That(SEntMan.HasComponent<FTLComponent>(_ship), Is.False);
            Assert.That(_ship.Comp.WithdrawEvacuating, Is.False);

            console.Comp.HijackLockApplied = false;
            console.Comp.DropdownLockApplied = false;
            console.Comp.WithdrawActive = true;
            console.Comp.WithdrawStartTime = SGameTiming.CurTime - console.Comp.WithdrawDuration;
            console.Comp.StalemateToggled = true;
            var opponent = WithdrawalConsole("opfor");
            SEntMan.EventBus.RaiseLocalEvent(opponent.Owner, new WithdrawConsoleToggleStalemateMsg
            {
                Actor = nav, UiKey = WithdrawConsoleUiKey.Key,
            });
            Assert.That(opponent.Comp.StalemateToggled, Is.False);
            Assert.That(opponent.Comp.RoundEndTriggered, Is.False);
            Land(Site("Nexus"));
            Assert.That(Reserve.TryGetNetwork("govfor", out var bank), Is.True);
            SEntMan.DeleteEntity(bank);
            // Deleting equipment and changing next round's flag cannot restore the protected path.
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, false);
            ProtectedReturn(nav, nav);
            Assert.That(SEntMan.HasComponent<FTLComponent>(_ship), Is.False);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(console.Comp.AnnouncementSent || console.Comp.HijackLockApplied ||
                console.Comp.DropdownLockApplied || console.Comp.RoundEndTriggered, Is.False,
                "A seeded expired countdown must have no milestone or round-end side effects in the pilot.");
            Assert.That(nav.Comp.Hijackable, Is.True);
            SEntMan.DeleteEntity(_map);
            Assert.That(Withdrawal.IsProtectedWithdrawalAllowed(), Is.False, "Losing the map does not release a round policy.");
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            StartRound(false);
            Assert.That(Withdrawal.IsProtectedWithdrawalAllowed(), Is.True, "Cleanup releases the policy for the next round.");
        });
    }

    [TestCase(false, "DistressSignal")]
    [TestCase(true, "ColonyFall")]
    public async Task ContestedEvacuationLeavesLegacyReturnOutsidePilot(bool enabled, string preset)
    {
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            StartRound(enabled, preset);
            var nav = PrepareFlight();
            var console = WithdrawalConsole("govfor");
            // Characterize the inherited protected-return path; the pilot must not change other configurations.
            console.Comp.HijackLockApplied = true;
            Assert.That(Withdrawal.IsWithdrawReturnUnlocked("govfor"), Is.True);
            var lockEvent = new WithdrawFactionHijackLockEvent("govfor");
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref lockEvent);
            Assert.That(nav.Comp.Hijackable, Is.False);
            ProtectedReturn(nav, nav);
            Assert.That(_ship.Comp.WithdrawEvacuating, Is.True);
            Assert.That(SEntMan.GetComponent<FTLComponent>(_ship).State, Is.EqualTo(FTLState.Starting));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ContestedEvacuationUsesNormalBoardingFlightAndReserve(bool powered)
    {
        Entity<CMUFobNetworkComponent> bank = default;
        Entity<DropshipNavigationComputerComponent> nav = default;
        EntityUid pilot = default, queen = default, leftBehind = default;
        TimeSpan beforeFlight = default;
        await Server.WaitAssertion(() =>
        {
            StartRound();
            LoadSurface();
            nav = PrepareFlight();
            Land(Site("Nexus"));
            Assert.That(Reserve.TryGetNetwork("govfor", out bank), Is.True);
            bank.Comp.StartupDuration = TimeSpan.FromSeconds(0.1);
            bank.Comp.StoredReserve = TimeSpan.FromSeconds(powered ? 60 : 0.2);
            Assert.That(Reserve.TryActivate(bank), Is.True);
            pilot = SEntMan.SpawnAtPosition("CMMobHuman", new EntityCoordinates(_ship, 1.5f, 1.5f));
            queen = SEntMan.SpawnAtPosition("CMXenoQueen", new EntityCoordinates(_ship, 0.5f, 1.5f));
            SEntMan.EnsureComponent<XenoMaturingComponent>(queen);
            leftBehind = SEntMan.SpawnAtPosition("CMMobHuman",
                SEntMan.GetComponent<TransformComponent>(Site("Nexus").Owner).Coordinates);
            SEntMan.EnsureComponent<Content.Shared._RMC14.Marines.MarineComponent>(pilot).Faction = "govfor";
            SEntMan.EnsureComponent<Content.Shared._RMC14.Marines.MarineComponent>(leftBehind).Faction = "govfor";
            var config = Server.ResolveDependency<IConfigurationManager>();
            config.SetCVar(RMCCVars.RMCDropshipInitialDelayMinutes, 0f);
            config.SetCVar(RMCCVars.RMCDropshipHijackInitialDelayMinutes, 0);
            config.SetCVar(CCVars.FTLStartupTime, 3f);
            config.SetCVar(CCVars.FTLTravelTime, 10f);
            var carrier = Maps.CreateMap(out _);
            SEntMan.AddComponent<Content.Shared.CMU14.ShipFactionComponent>(carrier).Faction = "govfor";
            var crash = SEntMan.SpawnEntity(null, new EntityCoordinates(carrier, 20, 20));
            SEntMan.AddComponent<DropshipHijackDestinationComponent>(crash);
        });
        await Pair.RunSeconds(0.4f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(bank.Comp.State, Is.EqualTo(powered ? CMUFobReserveState.Active : CMUFobReserveState.Empty));
            // Use the actual queen UI; only the unrelated console-lockout DoAfter is skipped.
            nav.Comp.LockedOutUntil = SGameTiming.CurTime + TimeSpan.FromMinutes(1);
            SEntMan.EventBus.RaiseLocalEvent(nav.Owner, new ActivateInWorldEvent(queen, nav, true));
            Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(nav.Owner, DropshipHijackerUiKey.Key, queen), Is.True);
            var ui = (DropshipHijackerBuiState) SEntMan.GetComponent<UserInterfaceComponent>(nav)
                .States[DropshipHijackerUiKey.Key];
            Assert.That(ui.CanHijack && ui.CanDeclineHijack, Is.True);
            nav.Comp.LockedOutUntil = TimeSpan.Zero;

            var home = Maps.CreateMap(out _);
            var destination = SEntMan.SpawnEntity("CMDropshipDestination", new EntityCoordinates(home, Vector2.Zero));
            var dropships = Server.System<SharedDropshipSystem>();
            Assert.That(dropships.FlyTo(nav, destination, pilot), Is.False, "Reserve cannot bypass navigation access.");
            Assert.That(SEntMan.HasComponent<FTLComponent>(_ship), Is.False);
            SEntMan.EnsureComponent<AccessComponent>(pilot).Tags.Add("AU14AccessGovforPilot");
            beforeFlight = Reserve.GetRemaining(bank);
            Assert.That(dropships.FlyTo(nav, destination, pilot), Is.True, "Zero reserve cannot lock departure.");
            var flight = SEntMan.GetComponent<FTLComponent>(_ship);
            Assert.That(flight.State, Is.EqualTo(FTLState.Starting));
            Assert.That(flight.StateTime.End, Is.EqualTo(SGameTiming.CurTime + TimeSpan.FromSeconds(3)));
            Assert.That(_ship.Comp.WithdrawEvacuating, Is.False);
            var damage = Server.System<DamageableSystem>();
            var beforeDamage = damage.GetTotalDamage(pilot);
            damage.TryChangeDamage(pilot, new DamageSpecifier { DamageDict = new() { ["Blunt"] = 12 } }, ignoreResistances: true);
            Assert.That(damage.GetTotalDamage(pilot), Is.GreaterThan(beforeDamage), "Spooling up does not protect passengers.");
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<FTLComponent>(_ship).State, Is.EqualTo(FTLState.Travelling));
            var flightMap = SEntMan.GetComponent<TransformComponent>(_ship).MapUid;
            Assert.That(flightMap, Is.Not.EqualTo(_map));
            Assert.That(SEntMan.GetComponent<TransformComponent>(pilot).MapUid, Is.EqualTo(flightMap));
            Assert.That(SEntMan.GetComponent<TransformComponent>(queen).MapUid, Is.EqualTo(flightMap), "Boarded enemies are not ejected.");
            Assert.That(SEntMan.GetComponent<TransformComponent>(leftBehind).MapUid, Is.EqualTo(_map));
            Assert.That(SEntMan.GetComponent<TransformComponent>(bank).MapUid, Is.EqualTo(_map));
            Assert.That(SEntMan.GetComponent<CMUFobTrialComponent>(bank).Departures, Is.EqualTo(1),
                "Only actual FTL entry produces a departure observation.");
            Assert.That(bank.Comp.State, Is.EqualTo(powered ? CMUFobReserveState.Active : CMUFobReserveState.Empty));
            Assert.That(Reserve.GetRemaining(bank), powered ? Is.LessThan(beforeFlight) : Is.EqualTo(TimeSpan.Zero));
            SEntMan.EventBus.RaiseEvent(EventSource.Local, new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));
            Assert.That(bank.Comp.State, Is.EqualTo(CMUFobReserveState.Closed));
            Assert.That(Reserve.TryActivate(bank), Is.False);
        });
    }
}
