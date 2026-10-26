using System.Reflection;
using Content.IntegrationTests.Pair;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Marines;
using Content.Shared.Doors.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Nutrition;

/// <summary>
/// Regression coverage for the ship MRE mess-hall gate. Every branch is driven through the real paths — the lock
/// CVar, the dropship crash latch, kitchen shutter events — and the verdict is read off the same
/// <see cref="OpenableOpenAttemptEvent"/> the open flow raises.
/// </summary>
[TestFixture]
public sealed class ShipMreGateTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUShipMreGateTarget
  components:
  - type: MRERequireOpenBeforeStorage
""";

    private static readonly TimeSpan SetupTime = TimeSpan.FromMinutes(10);

    [Test]
    public async Task MreSealedDuringSetupWindow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (_, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() => SetRoundDuration(server, TimeSpan.Zero));
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.True, "MREs stay sealed while the mess technicians set up.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreOpensWhenLockDisabled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (_, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() =>
        {
            server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.ShipMreLock, false);
            SetRoundDuration(server, TimeSpan.Zero);
        });
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.False, "With the lock CVar off the mess rules don't apply.");
        });

        await server.WaitPost(() =>
            server.ResolveDependency<IConfigurationManager>().SetCVar(CCVars.ShipMreLock, true));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreOpensWhenHijacked()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (map, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var dropship = entMan.SpawnEntity(null, new EntityCoordinates(map, Vector2.Zero));
            var comp = entMan.AddComponent<DropshipComponent>(dropship);
            entMan.System<SharedDropshipSystem>().SetDropshipCrashed((dropship, comp), true);
            SetRoundDuration(server, TimeSpan.Zero);
        });
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.False,
                "Once a hijacked dropship has crashed the mess rules are off, setup window included.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreOpensOffWarship()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (_, mre, user) = await MakeGateAsync(pair, warship: false);

        await server.WaitPost(() => SetRoundDuration(server, TimeSpan.Zero));
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.False, "MREs are only locked aboard warships.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreOpensAfterSetupWithoutShutters()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (_, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() => SetRoundDuration(server, SetupTime + TimeSpan.FromMinutes(1)));
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.False,
                "Past the setup window a map with no kitchen shutters has no foodline to wait for.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreSealedWhenFoodlineOpen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (map, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var shutter = entMan.SpawnEntity("CMUShutterKitchen", new EntityCoordinates(map, Vector2.Zero));
            entMan.System<SharedDoorSystem>().StartOpening(shutter);
        });

        await server.WaitPost(() => SetRoundDuration(server, SetupTime + TimeSpan.FromMinutes(1)));
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.True, "While the DEFAC foodline is open, MREs stay sealed.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MreOpensWhenFoodlineClosed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var (map, mre, user) = await MakeGateAsync(pair);

        await server.WaitPost(() =>
            server.EntMan.SpawnEntity("CMUShutterKitchen", new EntityCoordinates(map, Vector2.Zero)));

        await server.WaitPost(() => SetRoundDuration(server, SetupTime + TimeSpan.FromMinutes(1)));
        await server.WaitAssertion(() =>
        {
            var ev = RaiseAttempt(server, mre, user);
            Assert.That(ev.Cancelled, Is.False, "With the kitchen shutters closed the foodline isn't serving.");
        });

        await pair.CleanReturnAsync();
    }

    private static async Task<(EntityUid Map, EntityUid Mre, EntityUid User)> MakeGateAsync(
        TestPair pair,
        bool warship = true)
    {
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid mre = default;
        EntityUid user = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            if (warship)
                entMan.AddComponent<WarshipComponent>(map.MapUid);

            mre = entMan.SpawnEntity("CMUShipMreGateTarget", map.GridCoords);
            user = entMan.SpawnEntity(null, map.GridCoords);
        });

        await server.WaitRunTicks(1);
        return (map.MapUid, mre, user);
    }

    private static OpenableOpenAttemptEvent RaiseAttempt(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid mre, EntityUid user)
    {
        var ev = new OpenableOpenAttemptEvent(user);
        server.EntMan.EventBus.RaiseLocalEvent(mre, ref ev);
        return ev;
    }

    private static void SetRoundDuration(RobustIntegrationTest.ServerIntegrationInstance server, TimeSpan duration)
    {
        var now = server.ResolveDependency<IGameTiming>().CurTime;
        typeof(SharedGameTicker)
            .GetProperty(nameof(SharedGameTicker.RoundStartTimeSpan),
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(server.System<SharedGameTicker>(), now - duration);
    }
}
