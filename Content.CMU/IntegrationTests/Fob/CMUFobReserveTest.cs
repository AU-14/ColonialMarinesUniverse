#pragma warning disable RA0002 // Compress durations on the isolated network; transitions use the real system.

using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Fob;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
[TestOf(typeof(CMUFobReserveSystem))]
public sealed partial class CMUFobReserveTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private Entity<CMUFobNetworkComponent> _network;
    private EntityUid _site;
    private EntityUid _first;
    private EntityUid _second;
    private EntityUid _map;
    private EntityCoordinates _coords;
    private CMUFobReserveSystem Reserve => Server.System<CMUFobReserveSystem>();

    private void StartRound(bool enabled)
    {
        Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, enabled);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
    }

    private void CleanupRound()
    {
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
    }

    private EntityUid Relay()
    {
        var relay = SEntMan.SpawnEntity(null, _coords);
        SEntMan.AddComponent<CMUFobRelayComponent>(relay);
        return relay;
    }

    private async Task SetupNetwork()
    {
        var map = await Pair.CreateTestMap();
        _coords = map.GridCoords;
        await Server.WaitAssertion(() =>
        {
            StartRound(true);
            _site = SEntMan.SpawnEntity(null, _coords);
            _map = SEntMan.GetComponent<TransformComponent>(_site).MapUid!.Value;
            Assert.That(Reserve.TryCreateNetwork(_site, "govfor", out _network), Is.True);
            // Short, nonzero intervals retain the ordering and boundary behavior without minutes of simulated ticks.
            var comp = _network.Comp;
            comp.StoredReserve = TimeSpan.FromSeconds(4);
            comp.Capacity = TimeSpan.FromSeconds(6);
            comp.RelayReward = TimeSpan.FromSeconds(3);
            comp.StartupDuration = TimeSpan.FromSeconds(0.5);
            comp.MinimumActivation = TimeSpan.FromSeconds(1.5);
            comp.RestartDelay = TimeSpan.FromSeconds(0.75);
            comp.RelayInterval = TimeSpan.FromSeconds(1.5);
            comp.JobLifetime = TimeSpan.FromSeconds(2);
            comp.ExpiredJobDelay = TimeSpan.FromSeconds(0.5);
            comp.RelayReuseDelay = TimeSpan.FromSeconds(3);
            _first = Relay();
            _second = Relay();
        });
    }

    [Test]
    public async Task DisabledRoundRejectsNetworksAndFlagChangesWaitForNextRound()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var site = SEntMan.SpawnEntity(null, map.GridCoords);
            StartRound(false);
            Assert.That(Reserve.TryCreateNetwork(site, "govfor", out _), Is.False);

            // A changed cvar (or a repeated start notification) must not enable defenses mid-round.
            StartRound(true);
            Assert.That(Reserve.TryCreateNetwork(site, "govfor", out _), Is.False);
            CleanupRound();
            StartRound(true);
            Assert.That(Reserve.TryCreateNetwork(site, "govfor", out var network), Is.True);
            Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, false);
            Assert.That(Reserve.TryActivate(network), Is.True, "Changing next round's flag cannot stop this round.");
        });
    }

    [Test]
    public async Task ReserveDrainsThroughStartupButFreezesInStandbyAndDepletes()
    {
        await SetupNetwork();
        await Server.WaitAssertion(() => Assert.That(Reserve.TryActivate(_network), Is.True));
        await Pair.RunSeconds(0.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Starting));
            Assert.That(Reserve.GetRemaining(_network).TotalSeconds, Is.EqualTo(3.75).Within(0.08));
            Assert.That(Reserve.TryDeactivate(_network), Is.False);
        });
        await Pair.RunSeconds(1.4f);
        var stored = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Active));
            Assert.That(Reserve.TryDeactivate(_network), Is.True);
            stored = Reserve.GetRemaining(_network);
            Assert.That(stored.TotalSeconds, Is.InRange(2.2, 2.5));
            Assert.That(Reserve.TryActivate(_network), Is.False, "Shutdown must start the restart cooldown.");
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(stored));
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await Pair.RunSeconds((float)stored.TotalSeconds + 0.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Empty));
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.Zero));
            Assert.That(Reserve.TryActivate(_network), Is.False);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var generation), Is.True);
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out var awarded), Is.True);
            Assert.That(awarded, Is.EqualTo(TimeSpan.FromSeconds(3)));
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby),
                "A repair must not auto-activate.");
            Assert.That(Reserve.TryActivate(_network), Is.False, "Replenishing cannot clear depletion's cooldown.");
        });
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() => Assert.That(Reserve.TryActivate(_network), Is.True));
    }

    [Test]
    public async Task AlternativesAwardOnceAndFullBankDoesNotConsumeNextJob()
    {
        await SetupNetwork();
        CMUFobRelayJob firstGeneration = default;
        CMUFobRelayJob nextGeneration = default;
        EntityUid third = default;
        EntityUid fourth = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out firstGeneration), Is.True);
            Assert.That(Reserve.TryCompleteRelay(_first, firstGeneration, out var awarded), Is.True);
            Assert.That(awarded, Is.EqualTo(TimeSpan.FromSeconds(2)), "The award must fit the cap.");
            Assert.That(Reserve.TryCompleteRelay(_second, firstGeneration, out awarded), Is.False);
            Assert.That(awarded, Is.EqualTo(TimeSpan.Zero));
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(6)));
            third = Relay();
            fourth = Relay();
            Assert.That(Reserve.TryOfferRelays(_network, third, fourth, out _), Is.False);
        });
        await Pair.RunSeconds(1.6f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, third, fourth, out nextGeneration), Is.True);
            Assert.That(Reserve.TryCompleteRelay(third, nextGeneration, out _), Is.False);
            Assert.That(_network.Comp.FirstRelay, Is.EqualTo(third), "Full bank must leave the offer redeemable.");
            Assert.That(Reserve.TryActivate(_network), Is.True);
        });
        await Pair.RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryCompleteRelay(third, firstGeneration, out _), Is.False);
            var headroom = TimeSpan.FromSeconds(6) - Reserve.GetRemaining(_network);
            Assert.That(Reserve.TryCompleteRelay(third, nextGeneration, out var awarded), Is.True);
            Assert.That(awarded, Is.EqualTo(headroom));
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(6)));
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Active));
            Assert.That(Reserve.TryCompleteRelay(fourth, nextGeneration, out _), Is.False);
            Assert.That(Reserve.TryCompleteRelay(_first, firstGeneration, out _), Is.False);
        });
    }

    [Test]
    public async Task ExpiryRejectsLateRepairAndPreservesJobAndAnchorCooldowns()
    {
        await SetupNetwork();
        CMUFobRelayJob generation = default;
        EntityUid third = default;
        EntityUid fourth = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _first, out _), Is.False);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out generation), Is.True);
            third = Relay();
            fourth = Relay();
        });
        await Pair.RunSeconds(2.15f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out _), Is.False);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(4)));
            Assert.That(Reserve.TryOfferRelays(_network, third, fourth, out _), Is.False);
        });
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out _), Is.False);
            Assert.That(Reserve.TryOfferRelays(_network, third, fourth, out var next), Is.True);
            Assert.That(Reserve.TryCompleteRelay(third, next, out _), Is.True);
        });
        await Pair.RunSeconds(2.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var next), Is.True);
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out _), Is.False,
                "Reusing the same physical anchor must not revive an old completion.");
            Assert.That(next.Generation, Is.GreaterThan(generation.Generation));
        });
    }

    [Test]
    public async Task MapPauseFreezesReserveAndOutstandingRelayDeadline()
    {
        await SetupNetwork();
        CMUFobRelayJob generation = default;
        var remaining = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out generation), Is.True);
        });
        await Pair.RunSeconds(0.25f);
        await Server.WaitAssertion(() =>
        {
            Server.System<SharedMapSystem>().SetPaused(_map, true);
            remaining = Reserve.GetRemaining(_network);
        });
        await Pair.RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(remaining));
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out _), Is.False);
            Server.System<SharedMapSystem>().SetPaused(_map, false);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(remaining));
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out _), Is.True,
                "An unpaused job must retain the time it had before pausing.");
            Assert.That(Reserve.TryDeactivate(_network), Is.False, "Pause must also preserve the minimum activation.");
        });
    }

    [Test]
    public async Task ControlLossAndReplacementCannotRefundOrRecreateReserve()
    {
        await SetupNetwork();
        CMUFobRelayJob oldJob = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out oldJob), Is.True);
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Reserve.DisableControl(_network);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(2.5)));
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Standby));
            Assert.That(Reserve.TryActivate(_network), Is.False);
            Reserve.DisableControl(_network);
            Assert.That(Reserve.GetRemaining(_network), Is.EqualTo(TimeSpan.FromSeconds(2.5)));

            SEntMan.DeleteEntity(_site);
            var replacement = SEntMan.SpawnEntity(null, _coords);
            Assert.That(Reserve.TryCreateNetwork(replacement, "GOVFOR", out _), Is.False);
            Assert.That(Reserve.TryGetNetwork("govfor", out var existing), Is.True);
            Assert.That(existing.Owner, Is.EqualTo(_network.Owner));
            SEntMan.DeleteEntity(_network);
            Assert.That(Reserve.TryCreateNetwork(replacement, "govfor", out _), Is.False);
            CleanupRound();
            StartRound(true);
            Assert.That(Reserve.TryCreateNetwork(replacement, "govfor", out _network), Is.True);
        });
        await Pair.RunSeconds(3.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var newJob), Is.True);
            Assert.That(Reserve.TryCompleteRelay(_first, oldJob, out _), Is.False,
                "The first job of a new network must reject the previous network's first-job receipt.");
            Assert.That(Reserve.TryCompleteRelay(_first, newJob, out _), Is.True);
        });
    }

    [Test]
    public async Task WrongMapRelayAndRoundEndCannotRedeemOrRestartSupport()
    {
        await SetupNetwork();
        var otherMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var other = SEntMan.SpawnEntity(null, otherMap.GridCoords);
            SEntMan.AddComponent<CMUFobRelayComponent>(other);
            Assert.That(Reserve.TryOfferRelays(_network, _first, other, out _), Is.False);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var generation), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(_first, otherMap.GridCoords);
            Assert.That(Reserve.TryCompleteRelay(_first, generation, out _), Is.False);
            Assert.That(Reserve.TryActivate(_network), Is.True);
            SEntMan.EventBus.RaiseEvent(EventSource.Local,
                new GameRunLevelChangedEvent(GameRunLevel.InRound, GameRunLevel.PostRound));
            Assert.That(_network.Comp.State, Is.EqualTo(CMUFobReserveState.Closed));
            Assert.That(Reserve.TryCompleteRelay(_second, generation, out _), Is.False);
            Assert.That(Reserve.TryActivate(_network), Is.False);
            Assert.That(Reserve.TryCreateNetwork(_site, "opfor", out _), Is.False);
        });
    }
}
