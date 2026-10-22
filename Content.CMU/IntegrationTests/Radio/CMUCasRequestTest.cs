using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.Marines;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Radio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUCasRequestTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestCase(30f, true)]
    [TestCase(30.01f, false)]
    public async Task RequestsRespectRangeAndRejectDuplicates(float distance, bool allowed)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var mark = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(distance, 0)));
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(mark, "TEST", "govfor");
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Any, out var id), Is.EqualTo(allowed));
            if (allowed)
            {
                Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Any, out _), Is.False);
                Assert.That(requests.TryAbort(rto, id), Is.True);
            }
        });
    }

    [Test]
    public async Task ManualDoesNotRequireClearanceButAutoDoes()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var mark = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            var coordinates = SEntMan.GetComponent<TransformComponent>(mark).Coordinates;
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(mark, "TEST", "govfor");
            var console = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(console);
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Any, out var id), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Accepted)
                { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Accepted));
            Assert.That(requests.SetCarryMode(id, CMUCasCarryMode.Manual), Is.True);
            Assert.That(requests.CheckClearance(console, coordinates), Is.True);
            Assert.That(requests.SetCarryMode(id, CMUCasCarryMode.Auto), Is.True);
            Assert.That(requests.CheckClearance(console, coordinates), Is.False);
            Assert.That(requests.TryClearHot(rto, id), Is.True);
            Assert.That(requests.CheckClearance(console, coordinates), Is.True);
            Assert.That(requests.TryAbort(rto, id), Is.True);
            Assert.That(requests.CheckClearance(console, coordinates), Is.True);
        });
    }

    [Test]
    public async Task UndecidedDefaultsToAutoWithoutFiringBeforeClearance()
    {
        var map = await Pair.CreateTestMap();
        var id = 0;
        EntityUid console = default;
        EntityUid rto = default;
        await Server.WaitAssertion(() =>
        {
            rto = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var mark = SEntMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(10, 0)));
            Server.System<SharedDropshipWeaponSystem>().MakeDropshipTarget(mark, "TEST", "govfor");
            console = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(console);
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryCreateRequest(rto, mark, CMUCasMunition.Gau, out id), Is.True);
            SEntMan.EventBus.RaiseLocalEvent(console, new CMUCasRequestActionMsg(id, CMUCasRequestStatus.Accepted)
                { Actor = rto, UiKey = DropshipTerminalWeaponsUi.Key });
        });
        await Pair.RunSeconds(14);
        await Server.WaitAssertion(() =>
        {
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.CarryMode, Is.EqualTo(CMUCasCarryMode.Auto));
            Assert.That(request.CarryVolleys, Is.Zero);
            Assert.That(request.Status, Is.EqualTo(CMUCasRequestStatus.Accepted));
            Assert.That(requests.TryAbort(rto, id), Is.True);
        });
    }

    [TestCase("RMCDropshipAttachmentGau21Cannon", CMUCasMunition.Guns)]
    [TestCase("RMCDropshipAttachmentRocketPod", CMUCasMunition.Rockets)]
    [TestCase("RMCDropshipAttachmentGuidedMissileLauncher", CMUCasMunition.Missiles)]
    public async Task AutoMatchesExistingMasterWeapons(string prototype, CMUCasMunition munition)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var weapon = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var component = SEntMan.GetComponent<DropshipWeaponComponent>(weapon);
            Assert.That(CMUCasRequestSystem.Matches(CMUCasMunition.Any, component), Is.True);
            Assert.That(CMUCasRequestSystem.Matches(munition, component), Is.True);
            Assert.That(CMUCasRequestSystem.Matches(CMUCasMunition.Bombs, component), Is.False);
        });
    }

    [Test]
    public async Task DangerCloseRequiresRepeatAndDesignationIsRecognized()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var friendly = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            SEntMan.EnsureComponent<MarineComponent>(friendly).Faction = "govfor";
            var console = SEntMan.SpawnEntity(null, map.GridCoords);
            var terminal = SEntMan.AddComponent<DropshipTerminalWeaponsComponent>(console);
            var weapon = SEntMan.SpawnEntity("RMCDropshipAttachmentGau21Cannon", map.GridCoords);
            var requests = Server.System<CMUCasRequestSystem>();
            Assert.That(requests.CheckDangerClose((console, terminal), map.GridCoords, friendly, weapon), Is.False);
            Assert.That(requests.CheckDangerClose((console, terminal), map.GridCoords, friendly, weapon), Is.True);
            SEntMan.AddComponent<CMUCasRtoDesignationComponent>(friendly);
            Assert.That(requests.IsRtoDesignated(map.GridCoords, friendly), Is.True);
            Assert.That(CMUCasRequestSystem.DesignationSpreadMultiplier, Is.EqualTo(0.75f));
        });
    }

    [Test]
    public async Task UplinkDoesNotAutomaticallyTargetHostilesAndGpsNeedsProvider()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rto = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<MarineComponent>(rto).Faction = "govfor";
            var radio = SEntMan.SpawnEntity("ANPRC117GRadio", map.GridCoords);
            SEntMan.AddComponent<WearingANPRCComponent>(rto).Radio = radio;
            var hostile = SEntMan.SpawnEntity("MobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            SEntMan.EnsureComponent<MarineComponent>(hostile).Faction = "opfor";
            var requests = Server.System<CMUCasRequestSystem>();
            var existingCount = requests.GetRequests(null).Count;
            SEntMan.EventBus.RaiseLocalEvent(radio, new CMUCasCallSendMsg(null, CMUCasMunition.Any,
                CMUCasEffect.Any, "") { Actor = rto, UiKey = CMUCasCallUi.Key });
            Assert.That(requests.GetRequests(null).Count, Is.EqualTo(existingCount));
            Assert.That(requests.TryCreateGpsRequest(rto, 42, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);

            var provider = Server.System<CasGroundReportProviderSystem>();
            provider.Actor = rto;
            provider.Coordinates = map.GridCoords.Offset(new Vector2(10, 0));
            var reports = new CMUCasGroundReportsEvent(rto, new List<CMUCasGroundReport>());
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref reports);
            Assert.That(reports.Reports, Has.Count.EqualTo(1));
            Assert.That(requests.TryCreateGpsRequest(rto, 999, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out _), Is.False);
            Assert.That(requests.TryCreateGpsRequest(rto, 42, CMUCasMunition.Any,
                CMUCasEffect.Any, "", out var id), Is.True);
            Assert.That(requests.TryGetRequest(id, out var request), Is.True);
            Assert.That(request.RadioMark, Is.True);
            Assert.That(requests.TryAbort(rto, id), Is.True);
            provider.Actor = null;
        });
    }
}

public sealed class CasGroundReportProviderSystem : EntitySystem
{
    public EntityUid? Actor;
    public EntityCoordinates Coordinates;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCasGroundReportsEvent>(OnReports);
        SubscribeLocalEvent<CMUCasResolveGroundReportEvent>(OnResolve);
    }

    private void OnReports(ref CMUCasGroundReportsEvent args)
    {
        if (args.Actor == Actor)
            args.Reports.Add(new CMUCasGroundReport(42, "Test observer", "Ground", 10, 0));
    }

    private void OnResolve(ref CMUCasResolveGroundReportEvent args)
    {
        if (args.Actor == Actor && args.ReportId == 42)
            args.Coordinates = Coordinates;
    }
}
