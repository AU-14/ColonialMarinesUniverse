#pragma warning disable RA0002 // Configure isolated rounds and compress scheduling through real events.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Fob;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CCVar;
using Content.Shared.CMU14;
using Content.Shared.CMU14.Fob;
using Content.Shared.GameTicking;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Fob;

[TestFixture]
[TestOf(typeof(CMUFobMapSystem))]
public sealed partial class CMUFobMapTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private EntityUid _map;
    private CMUFobMapComponent _config = default!;
    private Entity<DropshipComponent> _ship;
    private CMUFobReserveSystem Reserve => Server.System<CMUFobReserveSystem>();
    private SharedMapSystem Maps => Server.System<SharedMapSystem>();
    private SharedTransformSystem Transform => Server.System<SharedTransformSystem>();

    private void StartRound(bool enabled = true, string preset = "DistressSignal")
    {
        // The integration ticker intentionally ignores SetGamePreset.
        typeof(GameTicker).GetProperty(nameof(GameTicker.CurrentPreset))!
            .SetValue(Server.System<GameTicker>(), SProtoMan.Index<GamePresetPrototype>(preset));
        Server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.CMUFobReserveEnabled, enabled);
        SetElapsed(TimeSpan.Zero);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, new RoundStartingEvent(1));
    }

    private void SetElapsed(TimeSpan elapsed) => typeof(SharedGameTicker)
        .GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan))!
        .SetValue(Server.System<GameTicker>(), SGameTiming.CurTime - elapsed);

    private void LoadSurface(string path = "/Maps/CMU14/Planets/LV624/LV624MultiZ0.yml")
    {
        var options = DeserializationOptions.Default with { InitializeMaps = true, PauseMaps = true };
        Assert.That(Server.System<MapLoaderSystem>().TryLoadMap(
            new ResPath(path), out var map, out _, options), Is.True);
        _map = map!.Value.Owner;
        _config = SEntMan.GetComponent<CMUFobMapComponent>(_map);
        Maps.SetPaused(_map, false);
        var grid = Maps.CreateGridEntity(SEntMan.GetComponent<MapComponent>(_map).MapId);
        _ship = (grid, SEntMan.AddComponent<DropshipComponent>(grid));
        SEntMan.AddComponent<ShipFactionComponent>(grid).Faction = "govfor";
    }

    private Entity<CMUFobSiteComponent> Site(string id)
    {
        var uid = _config.Sites.Single(uid => SEntMan.GetComponent<CMUFobSiteComponent>(uid).Id == id);
        return (uid, SEntMan.GetComponent<CMUFobSiteComponent>(uid));
    }

    private void Land(EntityUid destination)
    {
        _ship.Comp.Destination = destination;
        var ev = new DropshipLandedOnPlanetEvent(_ship);
        SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }

    private EntityUid Spawn(string prototype, EntityUid at) =>
        SEntMan.SpawnAtPosition(prototype, SEntMan.GetComponent<TransformComponent>(at).Coordinates);

    private EntityUid[] OnSurface<T>() where T : Component
    {
        var entities = new List<EntityUid>();
        var query = SEntMan.AllEntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.MapUid == _map)
                entities.Add(uid);
        }
        return entities.ToArray();
    }

    [TestCase(false, "DistressSignal")]
    [TestCase(true, "ColonyFall")]
    public async Task DisabledOrOtherPresetPreservesLegacyAndNeverDeploys(bool enabled, string preset)
    {
        EntityUid[] legacy = [];
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            legacy = _config.Sites.Select(site => Spawn("RMCTurretGaussSpaceborne", site)).ToArray();
            StartRound(enabled, preset);
            Land(Site("Nexus"));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            // Changing the flag/preset and repeating the event cannot opt an existing round in.
            StartRound();
            Land(Site("Robotics"));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(legacy.All(SEntMan.EntityExists), Is.True);
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.Empty);
            Assert.That(OnSurface<CMUFobControlComponent>(), Is.Empty);
            Assert.That(OnSurface<CMUFobRelayComponent>(), Is.Empty);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
        });
    }

    [TestCase("Nexus", false)]
    [TestCase("Robotics", true)]
    public async Task FirstFriendlyLandingDeploysOneFobAndReplacesOnlyLegacy(string selected, bool startBeforeMap)
    {
        EntityUid[] legacy = [];
        EntityUid ordinary = default;
        EntityUid elsewhere = default;
        await Server.WaitAssertion(() =>
        {
            if (startBeforeMap)
                StartRound();
            LoadSurface();
            legacy = _config.Sites.Select(site => Spawn("RMCTurretGaussSpaceborne", site)).ToArray();
            ordinary = Spawn("RMCSentryDropship", Site(selected));
            var otherMap = Maps.CreateMap(out _);
            elsewhere = SEntMan.SpawnEntity("RMCTurretGaussSpaceborne", new EntityCoordinates(otherMap, Vector2.Zero));
            if (!startBeforeMap)
                StartRound();
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(legacy.Any(SEntMan.EntityExists), Is.False, "Both LZs lose automatic legacy protection.");
            Assert.That(SEntMan.EntityExists(ordinary) && SEntMan.EntityExists(elsewhere), Is.True);
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.Empty, "Preparation is not a landing grant.");
            var site = Site(selected);
            SEntMan.GetComponent<ShipFactionComponent>(_ship).Faction = "opfor";
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
            SEntMan.GetComponent<ShipFactionComponent>(_ship).Faction = "govfor";
            _ship.Comp.IsHumanHijack = true;
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
            _ship.Comp.IsHumanHijack = false;
            var tactical = Spawn("CMDropshipDestination", site);
            Land(tactical);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
            var shipCoords = SEntMan.GetComponent<TransformComponent>(_ship).Coordinates;
            Transform.SetCoordinates(_ship, SEntMan.GetComponent<TransformComponent>(elsewhere).Coordinates);
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False, "Arrival on a different level cannot claim this site.");
            Transform.SetCoordinates(_ship, shipCoords);
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out var bank), Is.True);
            Assert.That(bank.Comp.State, Is.EqualTo(CMUFobReserveState.Standby), "Landing does not activate support.");
            Assert.That(Server.System<CMUFobInteractionSystem>().HasOperationalControl(bank), Is.False,
                "Selecting an LZ cannot substitute for a physically delivered terminal.");
            var weapons = OnSurface<CMUFobSupportGunComponent>();
            Assert.That(weapons, Is.Empty, "Landing cannot mint weapons or ammunition.");
            Assert.That(OnSurface<CMUFobGunMountComponent>(), Is.Empty);
            var utilities = OnSurface<CMUFobEquipmentComponent>();
            Assert.That(utilities, Is.Empty, "All supplies must be transported from requisitions.");
            var reserve = Reserve.GetRemaining(bank);
            var job = bank.Comp.JobGeneration;
            Land(site);
            Land(_config.Sites.Single(uid => uid != site.Owner));
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.EquivalentTo(weapons));
            Assert.That(OnSurface<CMUFobEquipmentComponent>(), Is.EquivalentTo(utilities), "Repeat landings must not reissue utilities.");
            Assert.That(Reserve.GetRemaining(bank), Is.EqualTo(reserve));
            Assert.That(OnSurface<CMUFobGunMountComponent>(), Is.Empty);
            Assert.That(bank.Comp.JobGeneration, Is.EqualTo(job));
            AssertPair(site, bank, CMUFobRelayTier.Near, CMUFobRelayTier.Near);
            SEntMan.DeleteEntity(bank);
            Land(_config.Sites.Single(uid => uid != site.Owner));
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False, "Deleting the bank cannot claim the alternate LZ.");
        });
        EntityUid late = default;
        await Server.WaitPost(() => late = Spawn("RMCTurretGaussSpaceborne", Site(selected)));
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(late), Is.False));
    }

    [Test]
    public async Task InvalidAuthoredLayoutRetainsLegacyWithoutPartialDeployment()
    {
        EntityUid legacy = default;
        await Server.WaitAssertion(() =>
        {
            LoadSurface();
            var site = Site("Nexus");
            SEntMan.DeleteEntity(site.Comp.Relays[0].Anchor);
            legacy = Spawn("RMCTurretGaussSpaceborne", site);
            StartRound();
            Land(site);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(legacy), Is.True);
            Assert.That(OnSurface<CMUFobSupportGunComponent>(), Is.Empty);
            Assert.That(Reserve.TryGetNetwork("govfor", out _), Is.False);
            Assert.That(Withdrawal.IsProtectedWithdrawalAllowed(), Is.True, "A rejected layout does not activate the pilot's evacuation policy.");
        });
    }

    private void AssertPair(Entity<CMUFobSiteComponent> site, Entity<CMUFobNetworkComponent> bank,
        CMUFobRelayTier first, CMUFobRelayTier second)
    {
        CMUFobRelayRoute Route(EntityUid? relay) => site.Comp.Relays.Single(route =>
            SEntMan.GetComponent<CMUFobMapAnchorComponent>(route.Anchor).Relay == relay);
        var a = Route(bank.Comp.FirstRelay);
        var b = Route(bank.Comp.SecondRelay);
        Assert.That(a.Tier, Is.EqualTo(first));
        Assert.That(b.Tier, Is.EqualTo(second));
        Assert.That(a.Approach, Is.Not.EqualTo(b.Approach));
        Assert.That(Reserve.TryGetRelayJob(bank.Comp.FirstRelay!.Value, out _, out _), Is.True);
    }

    [Test]
    public async Task RelayProgressionKeepsLiveJobsAndUnavailableRoutesNeverAwardReserve()
    {
        Entity<CMUFobNetworkComponent> bank = default;
        Entity<CMUFobSiteComponent> site = default;
        EntityUid[] original = [];
        await Server.WaitAssertion(() =>
        {
            StartRound();
            LoadSurface();
            site = Site("Nexus");
            Land(site);
            Assert.That(Reserve.TryGetNetwork("govfor", out bank), Is.True);
            bank.Comp.RelayInterval = TimeSpan.FromSeconds(0.3);
            bank.Comp.StoredReserve = TimeSpan.FromSeconds(1);
            AssertPair(site, bank, CMUFobRelayTier.Near, CMUFobRelayTier.Near);
            original = [bank.Comp.FirstRelay!.Value, bank.Comp.SecondRelay!.Value];
            SetElapsed(TimeSpan.FromMinutes(65));
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(new[] { bank.Comp.FirstRelay, bank.Comp.SecondRelay }, Is.EqualTo(original.Cast<EntityUid?>()),
                "A tier boundary must not reroll work already offered.");
            Assert.That(Reserve.TryGetRelayJob(original[0], out var job, out _), Is.True);
            Assert.That(Reserve.TryCompleteRelay(original[0], job, out _), Is.True);
            bank.Comp.StoredReserve = TimeSpan.FromSeconds(1);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            AssertPair(site, bank, CMUFobRelayTier.Near, CMUFobRelayTier.Intermediate);
            Assert.That(original, Does.Not.Contain(bank.Comp.FirstRelay), "Consumed alternatives retain their reuse delay.");
            Assert.That(Reserve.TryGetRelayJob(bank.Comp.FirstRelay!.Value, out var job, out _), Is.True);
            Assert.That(Reserve.TryCompleteRelay(bank.Comp.FirstRelay.Value, job, out _), Is.True);
            bank.Comp.StoredReserve = TimeSpan.FromSeconds(1);
            SetElapsed(TimeSpan.FromMinutes(95));
            foreach (var route in site.Comp.Relays.Where(route => route.Tier == CMUFobRelayTier.Far))
                Transform.Unanchor(SEntMan.GetComponent<CMUFobMapAnchorComponent>(route.Anchor).Relay!.Value);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(bank.Comp.JobExpiresAt, Is.Null);
            Assert.That(Reserve.GetRemaining(bank), Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(site.Comp.NextOfferAttempt, Is.GreaterThan(SGameTiming.CurTime));
            Assert.That(Server.System<CMUFobMapSystem>().TryGetRelayFailure(bank, out var retry), Is.True);
            Assert.That(retry, Is.EqualTo(site.Comp.NextOfferAttempt));
            foreach (var route in site.Comp.Relays.Where(route => route.Tier == CMUFobRelayTier.Far))
                Transform.AnchorEntity(SEntMan.GetComponent<CMUFobMapAnchorComponent>(route.Anchor).Relay!.Value);
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(bank.Comp.JobExpiresAt, Is.Null, "Restoring a route does not bypass the retry delay.");
            site.Comp.NextOfferAttempt = SGameTiming.CurTime;
        });
        await Pair.RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            AssertPair(site, bank, CMUFobRelayTier.Intermediate, CMUFobRelayTier.Far);
            Assert.That(Server.System<CMUFobMapSystem>().TryGetRelayFailure(bank, out _), Is.False,
                "A successful retry must clear the unavailable status.");
        });
    }
}
