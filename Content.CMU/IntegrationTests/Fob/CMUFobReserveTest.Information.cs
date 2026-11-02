#pragma warning disable RA0002 // Arrange reserve deadlines; observe actual transitions, map views and radio attempts.
using System.Linq;
using System.Numerics;
using Content.Server._RMC14.TacticalMap;
using Content.Server.CMU14.Fob;
using Content.Server.CMU14.Radio;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.CMU14.Fob;
using Content.Shared.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Network;

namespace Content.IntegrationTests.CMU14.Fob;

public sealed partial class CMUFobReserveTest
{
    [Test]
    public async Task InformationWarningsRespectPauseRearmAndCommsWithoutRepeating()
    {
        await SetupNetwork();
        var probe = Server.System<CMUFobInformationProbe>();
        await Server.WaitAssertion(() =>
        {
            probe.Warnings.Clear(); probe.Radio.Clear();
            SEntMan.EnsureComponent<RMCPlanetComponent>(_map);
            _network.Comp.StoredReserve = TimeSpan.FromSeconds(121);
            _network.Comp.Capacity = TimeSpan.FromSeconds(200);
            Assert.That(Reserve.TryActivate(_network), Is.True);
            Server.System<SharedMapSystem>().SetPaused(_map, true);
        });
        await Pair.RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(probe.Warnings, Is.Empty);
            Assert.That(SEntMan.GetComponent<CMUFobTrialComponent>(_network).ActiveSeconds, Is.Zero);
            Server.System<SharedMapSystem>().SetPaused(_map, false);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            var trial = SEntMan.GetComponent<CMUFobTrialComponent>(_network);
            Assert.That(probe.Warnings, Is.EqualTo(new[] { 120 }));
            Assert.That(trial.ActiveSeconds + trial.StartupSeconds, Is.InRange(1.1, 1.4));
            _network.Comp.DepletesAt = SGameTiming.CurTime + TimeSpan.FromSeconds(59);
            Reserve.IsActive(_network);
            Assert.That(probe.Warnings, Is.EqualTo(new[] { 120, 60 }));
            _network.Comp.RelayReward = TimeSpan.FromSeconds(10);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var job), Is.True);
            Assert.That(Reserve.TryCompleteRelay(_first, job, out _), Is.True);
            _network.Comp.DepletesAt = SGameTiming.CurTime + TimeSpan.FromSeconds(59);
            Reserve.IsActive(_network);
            Assert.That(probe.Warnings, Has.Count.EqualTo(2), "A small top-up must not cause warning spam.");
            _network.Comp.NextJobAt = SGameTiming.CurTime;
            _network.Comp.RelayReward = TimeSpan.FromSeconds(180);
            var relay = Relay();
            Assert.That(Reserve.TryOfferRelays(_network, relay, Relay(), out var next), Is.True);
            Assert.That(Reserve.TryCompleteRelay(relay, next, out var award), Is.True);
            Assert.That(award.TotalSeconds, Is.EqualTo(141));
            Assert.That(Reserve.TryCompleteRelay(relay, next, out _), Is.False);
            Assert.That(trial.Completions, Is.EqualTo(2));
            Assert.That(trial.CappedSeconds, Is.EqualTo(39));
            _network.Comp.DepletesAt = SGameTiming.CurTime + TimeSpan.FromSeconds(119);
            Reserve.IsActive(_network);
            _network.Comp.DepletesAt = SGameTiming.CurTime + TimeSpan.FromSeconds(59);
            Reserve.IsActive(_network);
            _network.Comp.DepletesAt = SGameTiming.CurTime;
            Reserve.IsActive(_network);
            Assert.That(probe.Warnings, Is.EqualTo(new[] { 120, 60, 120, 60, 0 }));
            Assert.That(probe.Radio, Has.Count.EqualTo(7), "Five reserve warnings and two successful relay offers each attempt one call.");
            Assert.That(probe.Radio.All(attempt => attempt.Cancelled), Is.True, "FOB calls must not bypass the missing comms tower.");
            Assert.That(trial.Depletions, Is.EqualTo(1));
            Reserve.CloseNetwork(_network);
            Assert.That(trial.Finalized, Is.True);
            Reserve.RecordGunUse(_network, shots: 4);
            Assert.That(trial.Shots, Is.Zero, "Finalized summaries cannot accumulate later unrelated events.");
        });
        await Pair.RunSeconds(1);
        await Server.WaitAssertion(() => Assert.That(probe.Warnings, Has.Count.EqualTo(5)));
    }

    [Test]
    public async Task InformationRelayOffersAnnounceLocationsOnceWithoutBypassingComms()
    {
        await SetupNetwork();
        var probe = Server.System<CMUFobInformationProbe>();
        EntityUid nextFirst = default;
        EntityUid nextSecond = default;
        await Server.WaitAssertion(() =>
        {
            probe.Radio.Clear();
            SEntMan.EnsureComponent<RMCPlanetComponent>(_map);
            var transform = Server.System<SharedTransformSystem>();
            transform.SetWorldPosition(_coords.EntityId, new Vector2(100, -20));
            transform.SetCoordinates(_first, new EntityCoordinates(_coords.EntityId, -12.25f, 7.75f));
            transform.SetCoordinates(_second, new EntityCoordinates(_coords.EntityId, 8.5f, -4.25f));
            var metadata = Server.System<MetaDataSystem>();
            metadata.SetEntityName(_first, "West ridge relay");
            metadata.SetEntityName(_second, "East road relay");

            Assert.That(Reserve.TryOfferRelays(_network, _first, _first, out _), Is.False);
            Assert.That(probe.Radio, Is.Empty, "Rejected candidates must not announce an unavailable objective.");
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out _), Is.True);
            Assert.That(probe.Radio, Has.Count.EqualTo(1));
            var call = probe.Radio.Single();
            var loc = Server.ResolveDependency<ILocalizationManager>();
            Assert.That(call.Channel.ID, Is.EqualTo("radioGovforCommand"));
            Assert.That(call.RadioSource, Is.EqualTo(_network.Owner));
            Assert.That(call.Message, Does.Contain(loc.GetString("cmu-fob-relay-location",
                ("name", "West ridge relay"), ("x", 87), ("y", -13))).And.Contain(loc.GetString("cmu-fob-relay-location",
                ("name", "East road relay"), ("x", 108), ("y", -25))),
                "Calls must carry both current names and world-tile coordinates, including negative-coordinate flooring.");
            Assert.That(call.Cancelled, Is.True, "A missing comms tower must still block the announcement.");
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out _), Is.False);
        });
        await Pair.RunSeconds(0.25f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(probe.Radio, Has.Count.EqualTo(1), "Updates and repeated offers must not repeat a call.");
            _network.Comp.JobExpiresAt = SGameTiming.CurTime;
            Reserve.IsActive(_network);
            nextFirst = Relay();
            nextSecond = Relay();
            Server.System<MetaDataSystem>().SetEntityName(nextFirst, "North pass relay");
            Server.System<MetaDataSystem>().SetEntityName(nextSecond, "South clearing relay");
            Assert.That(Reserve.TryOfferRelays(_network, nextFirst, nextSecond, out _), Is.False);
            Assert.That(probe.Radio, Has.Count.EqualTo(1), "The replacement delay must not advertise a pair early.");
        });
        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Reserve.TryOfferRelays(_network, nextFirst, nextSecond, out _), Is.True);
            Assert.That(probe.Radio, Has.Count.EqualTo(2));
            Assert.That(probe.Radio.Last().Message, Does.Contain("North pass relay").And.Contain("South clearing relay")
                .And.Not.Contain("West ridge relay").And.Not.Contain("East road relay"));
            Reserve.CloseNetwork(_network);
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out _), Is.False);
            Assert.That(probe.Radio, Has.Count.EqualTo(2), "Closed networks must not announce more objectives.");
        });
    }

    [Test]
    public async Task InformationRelayMarkersStayPrivateAndBothDisappearOnResolution()
    {
        await SetupNetwork();
        await Server.WaitAssertion(() =>
        {
            var grid = SEntMan.GetComponent<TransformComponent>(_site).GridUid!.Value;
            var map = SEntMan.EnsureComponent<TacticalMapComponent>(grid);
            var maps = Server.System<TacticalMapSystem>();
            Entity<TacticalMapUserComponent> Viewer(string faction)
            {
                var uid = SEntMan.SpawnEntity(null, _coords);
                SEntMan.AddComponent<MarineComponent>(uid).Faction = faction;
                var user = SEntMan.AddComponent<TacticalMapUserComponent>(uid);
                user.Map = grid; user.Govfor = true; user.Opfor = true; user.Xenos = true;
                return (uid, user);
            }
            var friendly = Viewer("govfor");
            var enemy = Viewer("opfor");
            Assert.That(Reserve.TryOfferRelays(_network, _first, _second, out var job), Is.True);
            maps.UpdateUserData(friendly, map);
            maps.UpdateUserData(enemy, map);
            Assert.That(friendly.Comp.GovforBlips.Keys, Is.EquivalentTo(new[] { _first.Id, _second.Id }));
            Assert.That(friendly.Comp.GovforBlips.Values.All(blip => blip.CMUFobLabel != null), Is.True);
            Assert.That(enemy.Comp.GovforBlips, Is.Empty, "A viewer flag alone cannot grant a different faction's missions.");
            Assert.That(friendly.Comp.XenoBlips, Is.Empty);
            Assert.That(maps.GetFobMarkers(enemy), Is.Empty);
            Assert.That(maps.GetFobMarkers(friendly), Has.Count.EqualTo(2));
            var console = SEntMan.SpawnEntity(null, _coords);
            var computer = SEntMan.AddComponent<TacticalMapComputerComponent>(console);
            computer.Faction = "GOVFOR";
            maps.RefreshReconstructionContacts((console, computer));
            Assert.That(computer.Blips.Values.Any(blip => blip.CMUFobLabel != null), Is.False,
                "Private markers must not leak through PVS-replicated console components.");
            Assert.That(map.GovforBlips, Is.Empty, "Sensor source buckets must never contain private relay missions.");
            Assert.That(map.LastUpdateGovforBlips, Is.Empty);
            Assert.That(Reserve.TryCompleteRelay(_second, job, out _), Is.True);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty, "Both alternatives must vanish immediately, without another publication.");
            _network.Comp.NextJobAt = SGameTiming.CurTime;
            var fullBankRelay = Relay();
            Assert.That(Reserve.TryOfferRelays(_network, fullBankRelay, Relay(), out _), Is.True);
            Assert.That(Reserve.TryGetRelayAssignment(fullBankRelay, out _), Is.True);
            Assert.That(Reserve.TryGetRelayJob(fullBankRelay, out _, out _), Is.False,
                "A full bank preserves the visible mission without allowing a wasteful redemption.");
            Assert.That(friendly.Comp.GovforBlips, Has.Count.EqualTo(2));
            _network.Comp.JobExpiresAt = SGameTiming.CurTime;
            Reserve.IsActive(_network);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty, "Expiry must revoke both alternatives immediately.");
            _network.Comp.NextJobAt = SGameTiming.CurTime;
            Assert.That(Reserve.TryOfferRelays(_network, Relay(), Relay(), out _), Is.True);
            Reserve.CloseNetwork(_network);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty);
            CleanupRound(); StartRound(false);
            maps.UpdateUserData(friendly, map);
            Assert.That(friendly.Comp.GovforBlips, Is.Empty);
        });
    }
}

public sealed partial class CMUFobInformationProbe : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    public readonly List<int> Warnings = new();
    public readonly List<RadioSendAttemptEvent> Radio = new();
    public override void Initialize()
    {
        if (_net.IsClient) return;
        SubscribeLocalEvent<CMUFobNetworkComponent, CMUFobWarningEvent>((Entity<CMUFobNetworkComponent> _, ref CMUFobWarningEvent ev) => Warnings.Add(ev.Seconds));
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadio, after: [typeof(ANPRCRangeSystem)]);
    }
    private void OnRadio(ref RadioSendAttemptEvent ev)
    {
        if (HasComp<CMUFobNetworkComponent>(ev.RadioSource)) Radio.Add(ev);
    }
}
