using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Client.CMU14.TacticalMap.Scout;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.TacticalMap;
using Content.Server.CMU14.TacticalMap.Scout;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Rangefinder.Spotting;
using Content.Shared._RMC14.Scoping;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared._RMC14.Weapons.Ranged.Whitelist;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.TacticalMap.Scout;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.UserInterface;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.TacticalMap;

[TestFixture]
public sealed partial class CMUScoutReportsTest : GameTest
{
    private EntityUid _map;
    private EntityUid _actor;
    private EntityUid _optics;
    private TacticalMapSystem _tactical = null!;
    private EntityCoordinates Target => new(_map, new Vector2(3.5f, 0.5f));
    private CMUScoutReporterComponent Reporter => SComp<CMUScoutReporterComponent>(_actor);

    [SetUp]
    public async Task SetupMap()
    {
        await Server.WaitPost(() =>
        {
            _tactical = SEntMan.System<TacticalMapSystem>();
            var maps = SEntMan.System<SharedMapSystem>();
            _map = maps.CreateMap(runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(_map);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -8; x <= 8; x++)
            for (var y = -8; y <= 8; y++)
                maps.SetTile(_map, grid, new Vector2i(x, y), floor);
            SEntMan.EnsureComponent<TacticalMapComponent>(_map);
            _actor = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_map, new Vector2(0.5f)));
            SComp<MarineComponent>(_actor).Faction = "GOVFOR";
        });
    }

    [TearDown]
    public async Task CleanupMap()
    {
        // Scope actions reference their container; deleting leaves across ticks exposes dangling network references.
        await Server.WaitPost(() => SEntMan.DeleteEntity(_map));
    }

    private async Task Scope(string prototype = "RMCVultureSpotterScope", bool trained = true, bool mounted = false)
    {
        await Server.WaitAssertion(() =>
        {
            _optics = SEntMan.SpawnEntity(prototype, SComp<TransformComponent>(_actor).Coordinates);
            if (trained)
            {
                if (SComp<CMUScoutOpticsComponent>(_optics).RequiresScoutTraining)
                    SEntMan.EnsureComponent<ScoutWhitelistComponent>(_actor);
                else
                    SEntMan.EnsureComponent<SpotterWhitelistComponent>(_actor);
            }
            if (mounted)
            {
                var tripod = SEntMan.SpawnEntity("RMCVultureSpotterTripodDeployed", SComp<TransformComponent>(_actor).Coordinates);
                Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(tripod, "vulture_spotter_scope", _optics, _actor), Is.True);
            }
            else
                Assert.That(SEntMan.System<SharedHandsSystem>().TryPickup(_actor, _optics), Is.True);
            Assert.That(SEntMan.System<SharedScopeSystem>().StartScoping(
                (_optics, SComp<ScopeComponent>(_optics)), _actor), Is.Not.Null);
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(SComp<ScopingComponent>(_actor).Scope, Is.EqualTo(_optics)));
    }

    [Test]
    public async Task MountedSpotterScopeSupportsReporting()
    {
        await Scope(mounted: true);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUScoutOpticsComponent>(_optics).VehicleAction, Is.Not.Null);
            Assert.That(Report(), Is.True);
        });
    }

    [Test]
    public async Task PreviousObserverShutdownCannotRevokeTheNewObserversActions()
    {
        await Scope(mounted: true);
        EntityUid observer = default;
        await Server.WaitAssertion(() =>
        {
            observer = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_map, new Vector2(1.5f, 0.5f)));
            SEntMan.EnsureComponent<SpotterWhitelistComponent>(observer);
            Assert.That(SEntMan.System<SharedScopeSystem>().StartScoping(
                (_optics, SComp<ScopeComponent>(_optics)), observer), Is.Not.Null);
        });
        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var action = SComp<CMUScoutOpticsComponent>(_optics).VehicleAction!.Value;
            Assert.That(SComp<ActionsComponent>(observer).Actions, Does.Contain(action));
            SEntMan.RemoveComponent<ScopingComponent>(_actor);
            Assert.That(SComp<ActionsComponent>(observer).Actions, Does.Contain(action));
        });
    }

    private bool Report(CMUScoutContactType type = CMUScoutContactType.Vehicle, EntityCoordinates? target = null)
    {
        var ev = new CMUScoutReportActionEvent { Performer = _actor, Target = target ?? Target, ContactType = type };
        SEntMan.EventBus.RaiseLocalEvent(_optics, ev);
        return ev.Handled;
    }

    [TestCase("RMCVultureSpotterScope")]
    [TestCase("RMCLaserDesignatorSpotter")]
    [TestCase("RMCLaserDesignatorScout")]
    public async Task TrainedOpticsGrantFourActionsAndUnscopingRevokesThem(string prototype)
    {
        await Scope(prototype);
        EntityUid[] actions = [];
        await Server.WaitAssertion(() =>
        {
            var optics = SComp<CMUScoutOpticsComponent>(_optics);
            actions = [optics.InfantryAction!.Value, optics.VehicleAction!.Value,
                optics.EmplacementAction!.Value, optics.UnknownAction!.Value];
            foreach (var action in actions)
                Assert.That(SComp<ActionsComponent>(_actor).Actions, Does.Contain(action));
            Assert.That(Report(), Is.True);
            Assert.That(Reporter.Reports, Has.Count.EqualTo(1));
            Assert.That(SEntMan.System<SharedScopeSystem>().Unscope((_optics, SComp<ScopeComponent>(_optics))), Is.True);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            foreach (var action in actions)
                Assert.That(SComp<ActionsComponent>(_actor).Actions, Does.Not.Contain(action));
            Assert.That(Report(), Is.False);
        });
    }

    [Test]
    public async Task TrainingAndDedicatedOpticsAreRequired()
    {
        await Scope(trained: false);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUScoutOpticsComponent>(_optics).VehicleAction, Is.Null);
            Assert.That(Report(), Is.False);
            SEntMan.EnsureComponent<ScoutWhitelistComponent>(_actor);
            Assert.That(Report(), Is.False, "Scout training does not substitute for spotter training.");
            foreach (var prototype in new[] { "RMCBinoculars", "RMCBinocularsCiv", "RMCRangefinder", "RMCVultureScope" })
            {
                var item = SEntMan.SpawnEntity(prototype, Target);
                Assert.That(SEntMan.HasComponent<CMUScoutOpticsComponent>(item), Is.False);
            }
        });
    }

    [Test]
    public async Task RejectsUnseenInvalidAndUnaffiliatedReportsWithoutConsumingCooldown()
    {
        await Scope();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Report((CMUScoutContactType) 255), Is.False);
            Assert.That(Report(target: new EntityCoordinates(_map, new Vector2(float.NaN))), Is.False);
            Assert.That(Report(target: new EntityCoordinates(_map, new Vector2(float.PositiveInfinity))), Is.False);
            Assert.That(Report(target: new EntityCoordinates(_map, new Vector2(100))), Is.False);
            var elsewhere = SEntMan.System<SharedMapSystem>().CreateMap(runMapInit: true);
            try
            {
                Assert.That(Report(target: new EntityCoordinates(elsewhere, Vector2.Zero)), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(elsewhere);
            }
            var wall = SEntMan.SpawnEntity("WallSolid", new EntityCoordinates(_map, new Vector2(2.5f, 0.5f)));
            Assert.That(Report(), Is.False, "A wall between the observer and the mark must block reporting.");
            SEntMan.DeleteEntity(wall);
            SComp<MarineComponent>(_actor).Faction = "unrecognized";
            Assert.That(Report(), Is.False);
            SComp<MarineComponent>(_actor).Faction = "GOVFOR";
            var blindness = SEntMan.System<BlindableSystem>();
            blindness.AdjustEyeDamage(_actor, 100);
            Assert.That(Report(), Is.False, "Blind observers cannot submit visual reports.");
            blindness.AdjustEyeDamage(_actor, -100);
            Assert.That(Report(), Is.True, "Rejected requests must not consume the reporting cooldown.");
        });
    }

    [Test]
    public async Task SwappingOpticsCannotResetCooldownOrBudgetAndOtherScoutsHaveTheirOwnLimit()
    {
        await Scope();
        var firstScout = _actor;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Report(), Is.True);
            SEntMan.DeleteEntity(_optics);
        });
        await Pair.RunTicksSync(2);
        await Scope("RMCLaserDesignatorScout");
        await Server.WaitAssertion(() =>
        {
            Assert.That(Report(), Is.False, "Changing optics must not reset the character's cooldown.");
            for (var i = 0; i < 3; i++)
            {
                Reporter.NextReport = SGameTiming.CurTime;
                Assert.That(Report(), Is.True);
            }
            Assert.That(Reporter.Reports, Has.Count.EqualTo(3));
            _actor = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_map, new Vector2(0.5f, 1.5f)));
            SComp<MarineComponent>(_actor).Faction = "GOVFOR";
        });
        await Scope();
        await Server.WaitAssertion(() =>
        {
            Assert.That(Report(), Is.True);
            Assert.That(Reporter.Reports, Has.Count.EqualTo(1));
            var reports = SComp<CMUScoutReporterComponent>(firstScout).Reports.ToArray();
            Assert.That(reports, Has.Length.EqualTo(3));
            SEntMan.DeleteEntity(firstScout);
            foreach (var report in reports)
                Assert.That(_tactical.TryGetIntelBlipGrid(report.Key, out _), Is.False);
            Assert.That(Reporter.Reports, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task PositionsAreSnapshotsAndTheFourthReportReplacesOnlyTheOldest()
    {
        await Scope();
        await Server.WaitAssertion(() =>
        {
            var target = SEntMan.SpawnEntity(null, Target);
            Assert.That(Report(target: new EntityCoordinates(target, Vector2.Zero)), Is.True);
            var first = Reporter.Reports.Single();
            var info = first.Blip.ScoutReport!.Value;
            Assert.Multiple(() =>
            {
                Assert.That(info.ContactType, Is.EqualTo(CMUScoutContactType.Vehicle));
                Assert.That(info.Reporter, Is.Not.Empty);
                Assert.That(info.ExpiresAt - info.ReportedAt, Is.EqualTo(TimeSpan.FromSeconds(240)));
                Assert.That(Reporter.NextReport - info.ReportedAt, Is.EqualTo(TimeSpan.FromSeconds(15)));
                Assert.That(first.Blip.Indices, Is.EqualTo(new Vector2i(3, 0)));
            });
            SEntMan.System<SharedTransformSystem>().SetCoordinates(target, new EntityCoordinates(_map, new Vector2(6.5f)));
            SEntMan.DeleteEntity(target);
            Assert.That(Reporter.Reports.Single(), Is.EqualTo(first), "Reports cannot follow or depend on target entities.");
            Assert.That(Report(CMUScoutContactType.Infantry), Is.False, "Contact types share one cooldown.");
            Reporter.NextReport = SGameTiming.CurTime + TimeSpan.FromTicks(1);
            Assert.That(Report(), Is.False, "The cooldown is exclusive until its exact deadline.");
            foreach (var type in new[] { CMUScoutContactType.Infantry, CMUScoutContactType.Emplacement, CMUScoutContactType.Unknown })
            {
                Reporter.NextReport = SGameTiming.CurTime;
                Assert.That(Report(type), Is.True);
            }
            Assert.That(Reporter.Reports, Has.Count.EqualTo(3));
            Assert.That(Reporter.Reports.Any(report => report.Key == first.Key), Is.False);
            Assert.That(_tactical.TryGetIntelBlipGrid(first.Key, out _), Is.False);
            Assert.That(Reporter.Reports.Select(report => report.Blip.ScoutReport!.Value.ContactType),
                Is.EqualTo(new[] { CMUScoutContactType.Infantry, CMUScoutContactType.Emplacement, CMUScoutContactType.Unknown }));
        });
    }

    #pragma warning disable RA0002 // Test setup and inspection of faction-authorized map feeds.
    [Test]
    public async Task FactionDeliveryAndExpiryReachClassicAndReconstructionClients()
    {
        await Scope();
        var session = ServerSession!;
        var original = session.AttachedEntity;
        var key = 0;
        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(Report(), Is.True);
                key = Reporter.Reports.Single().Key;
                var map = SComp<TacticalMapComponent>(_map);
                foreach (var faction in new[] { "GOVFOR", "OPFOR", "MARINES", "CLF", "WEYU" })
                {
                    var console = SEntMan.SpawnEntity(null, Target);
                    var computer = SEntMan.EnsureComponent<TacticalMapComputerComponent>(console);
                    _tactical.SetComputerFaction((console, computer), faction);
                    _tactical.RefreshReconstructionContacts((console, computer));
                    Assert.That(computer.Blips.ContainsKey(key), Is.EqualTo(faction == "GOVFOR"));
                }
                Assert.That(map.GovforBlips.ContainsKey(key), Is.False, "Reports must not enter sensor source buckets.");
                Assert.That(map.LastUpdateGovforBlips.ContainsKey(key), Is.False);

                var user = SComp<TacticalMapUserComponent>(_actor);
                user.Marines = false;
                user.Govfor = true;
                user.Opfor = user.Clf = user.Xenos = user.WeYu = false;
                user.Map = _map;
                _tactical.UpdateUserData((_actor, user), map);
                Assert.That(user.GovforBlips.ContainsKey(key), Is.True, "Personal snapshots receive reports without a manual map update.");
                Server.PlayerMan.SetAttachedEntity(session, _actor);
            });
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                SEntMan.EventBus.RaiseLocalEvent(_actor, new OpenTacticalMapActionEvent { Performer = _actor });
                SEntMan.EventBus.RaiseLocalEvent(_actor, (object) new CMUReconViewMessage(Vector2i.Zero)
                {
                    Actor = _actor,
                    UiKey = TacticalMapUserUi.Key,
                });
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var actor = CEntMan.GetEntity(SEntMan.GetNetEntity(_actor));
                var blip = CEntMan.GetComponent<TacticalMapUserComponent>(actor).GovforBlips[key];
                Assert.That(blip.ScoutReport!.Value.ContactType, Is.EqualTo(CMUScoutContactType.Vehicle));
                var report = blip.ScoutReport.Value;
                var label = CMUScoutReportPresentation.Label(report, report.ReportedAt + TimeSpan.FromSeconds(23));
                Assert.That(label, Does.Contain("Vehicle").And.Contain(report.Reporter).And.Contain("23s"));
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.TrackedContacts.Any(contact => contact.Blip.ScoutReport == report), Is.True);
            });
            await Server.WaitAssertion(() =>
            {
                var report = Reporter.Reports.Single();
                var info = report.Blip.ScoutReport!.Value;
                Reporter.Reports[0] = report with { Blip = report.Blip with
                {
                    ScoutReport = info with { ExpiresAt = SGameTiming.CurTime + TimeSpan.FromTicks(1) },
                } };
                _tactical.Update(0);
                Assert.That(Reporter.Reports, Has.Count.EqualTo(1));
                Reporter.Reports[0] = report with { Blip = report.Blip with
                {
                    ScoutReport = info with { ExpiresAt = SGameTiming.CurTime },
                } };
                _tactical.Update(0);
                Assert.That(Reporter.Reports, Is.Empty, "A report expires at its deadline, not the tick after.");
                Assert.That(_tactical.TryGetIntelBlipGrid(key, out _), Is.False);
            });
            await Pair.RunTicksSync(40);
            await Client.WaitAssertion(() =>
            {
                var actor = CEntMan.GetEntity(SEntMan.GetNetEntity(_actor));
                Assert.That(CEntMan.GetComponent<TacticalMapUserComponent>(actor).GovforBlips.ContainsKey(key), Is.False);
                var window = Client.ResolveDependency<IUserInterfaceManager>().WindowRoot.Children
                    .OfType<CMUReconstructionWindow>().Single();
                Assert.That(window.SurveyView.TrackedContacts.All(contact => contact.Blip.ScoutReport == null), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                SEntMan.System<SharedUserInterfaceSystem>().CloseUi(_actor, TacticalMapUserUi.Key, _actor);
                Server.PlayerMan.SetAttachedEntity(session, original);
            });
        }
    }
    #pragma warning restore RA0002
}
