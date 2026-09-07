#pragma warning disable RA0002 // Fixture ownership/setup fields; interactions use production lifecycle and BUI APIs.
using Content.Server.CMU14.Hospital;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Hospital;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalTransportOwnershipTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task ConsoleRetirementPreservesPausedForeignContentMovedDuringMapInitialization(bool passenger)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var move = entities.System<HospitalMapInitForeignContentProbeSystem>();
        var safety = entities.System<HospitalTransportSafetyProbeSystem>();
        var cleanup = new HashSet<EntityUid>();
        EntityUid computer = default, actor = default, foreign = default, shuttle = default, leaseUid = default;
        HospitalEmergencyComputerComponent hospital = default!;
        HospitalTransportLeaseComponent lease = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                var landing = entities.SpawnEntity("AU14HospitalDropshipLandingZone",
                    new EntityCoordinates(map.MapUid, new Vector2(50, 0)));
                // The minimal live mob deliberately has no unpaused anatomy or
                // clothing descendants that could accidentally protect its map.
                foreign = entities.SpawnEntity(passenger ? null : "CMScalpel", map.GridCoords);
                if (passenger) entities.EnsureComponent<MobStateComponent>(foreign);
                cleanup.UnionWith([computer, actor, landing, foreign]);
                hospital = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
                hospital.MinCasualties = hospital.MaxCasualties = 1;
                entities.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1));
                move.Foreign = foreign;
                move.OriginalMap = map.MapUid;
                move.Invocations = 0;
                move.Enabled = true;
                safety.Enabled = true;
                safety.Throw = false;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
            await pair.Server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.WaitingForArrival));
                shuttle = hospital.ActiveShuttle!.Value;
                leaseUid = entities.GetComponent<HospitalTransportShuttleComponent>(shuttle).Lease;
                lease = entities.GetComponent<HospitalTransportLeaseComponent>(leaseUid);
                cleanup.UnionWith(lease.Roots);
                cleanup.UnionWith(hospital.Patients);
                cleanup.UnionWith([shuttle, leaseUid, hospital.ReturnDestination!.Value]);
                Assert.That(move.Invocations, Is.EqualTo(1));
                Assert.That(entities.GetComponent<TransformComponent>(foreign).GridUid, Is.EqualTo(shuttle));
                // Map loading unpauses the loaded map after MapInit. Establish an
                // individually paused occupant at the actual retirement boundary.
                entities.System<MetaDataSystem>().SetEntityPaused(foreign, true);
                Assert.That(entities.GetComponent<MetaDataComponent>(foreign).EntityPaused, Is.True);
                Assert.That(lease.AuthoredEntities, Does.Not.Contain(foreign),
                    "MapInit moving a pre-existing object cannot transfer its ownership to the map loader.");
                foreach (var patient in hospital.Patients)
                    entities.System<SharedTransformSystem>().SetCoordinates(patient, map.GridCoords);
                entities.DeleteEntity(computer);
                entities.System<HospitalEmergencySystem>().Update(0f);
                Assert.That(lease.Retiring, Is.True);
                Assert.That(entities.IsQueuedForDeletion(shuttle), Is.False);
                Assert.That(entities.IsQueuedForDeletion(foreign), Is.False);
                Assert.That(lease.Roots.All(root => !entities.IsQueuedForDeletion(root)), Is.True);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(foreign), Is.True);
                Assert.That(entities.EntityExists(shuttle), Is.True);
                Assert.That(entities.EntityExists(leaseUid), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                move.Enabled = false;
                safety.Enabled = false;
                foreach (var uid in cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DeletingAWrongConsoleCannotRemoveAnotherHospitalsPatientOrLeaseOwnership()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var owner = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var wrong = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var leaseUid = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var ownerComp = entities.GetComponent<HospitalEmergencyComputerComponent>(owner);
            var wrongComp = entities.GetComponent<HospitalEmergencyComputerComponent>(wrong);
            var patientComp = entities.EnsureComponent<HospitalPatientComponent>(patient);
            patientComp.SourceComputer = owner;
            ownerComp.Patients.Add(patient);
            wrongComp.Patients.Add(patient);
            var lease = entities.AddComponent<HospitalTransportLeaseComponent>(leaseUid);
            lease.Shuttle = map.Grid.Owner;
            lease.Computer = owner;
            lease.Controller = ownerComp;
            lease.HospitalMap = map.MapUid;
            lease.HospitalMapComponent = entities.GetComponent<MapComponent>(map.MapUid);
            entities.EnsureComponent<HospitalTransportShuttleComponent>(map.Grid.Owner).Lease = leaseUid;
            ownerComp.ActiveShuttle = wrongComp.ActiveShuttle = map.Grid.Owner;
            try
            {
                entities.DeleteEntity(wrong);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<HospitalPatientComponent>(patient), Is.SameAs(patientComp));
                    Assert.That(patientComp.SourceComputer, Is.EqualTo(owner));
                    Assert.That(lease.Computer, Is.EqualTo(owner));
                    Assert.That(lease.Controller, Is.SameAs(ownerComp));
                    Assert.That(lease.Retiring, Is.False);
                    Assert.That(entities.IsQueuedForDeletion(patient), Is.False);
                    Assert.That(entities.IsQueuedForDeletion(map.Grid.Owner), Is.False);
                });
            }
            finally
            {
                ownerComp.ActiveShuttle = null;
                entities.RemoveComponent<HospitalTransportShuttleComponent>(map.Grid.Owner);
                entities.DeleteEntity(leaseUid);
                entities.DeleteEntity(owner);
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClosedHospitalConsolePublishesOnlyWhenAViewerOpensIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var console = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
            var actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var comp = entities.GetComponent<HospitalEmergencyComputerComponent>(console);
            var ui = entities.System<UserInterfaceSystem>();
            try
            {
                comp.NextUiRefreshAt = TimeSpan.Zero;
                entities.System<HospitalEmergencySystem>().Update(0f);
                Assert.That(ui.TryGetUiState<HospitalEmergencyComputerBuiState>(console, HospitalEmergencyComputerUi.Key, out _), Is.False);
                Assert.That(ui.TryOpenUi(console, HospitalEmergencyComputerUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<HospitalEmergencyComputerBuiState>(console, HospitalEmergencyComputerUi.Key, out var initial), Is.True);
                ui.CloseUi(console, HospitalEmergencyComputerUi.Key, actor);
                comp.Casualties = 7;
                comp.NextUiRefreshAt = TimeSpan.Zero;
                entities.System<HospitalEmergencySystem>().Update(0f);
                Assert.That(ui.TryGetUiState<HospitalEmergencyComputerBuiState>(console, HospitalEmergencyComputerUi.Key, out var closed), Is.True);
                Assert.That(closed, Is.SameAs(initial), "No viewer means no rebuilt clinical/BUI projection.");
                Assert.That(ui.TryOpenUi(console, HospitalEmergencyComputerUi.Key, actor), Is.True);
                Assert.That(ui.TryGetUiState<HospitalEmergencyComputerBuiState>(console, HospitalEmergencyComputerUi.Key, out var reopened), Is.True);
                Assert.That(reopened!.Casualties, Is.EqualTo(7));
            }
            finally
            {
                entities.DeleteEntity(console);
                entities.DeleteEntity(actor);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FtlSetupVetoOrExceptionRetainsPreparedPatientsAndRetriesWithoutAFalseCommit(bool throwDuringSetup)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var cleanup = new HashSet<EntityUid>();
        EntityUid computer = default;
        EntityUid actor = default;
        EntityUid shuttle = default;
        HospitalEmergencyComputerComponent hospital = default!;
        HospitalTransportLeaseComponent lease = default!;
        var safety = entities.System<HospitalTransportSafetyProbeSystem>();
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                var landing = entities.SpawnEntity("AU14HospitalDropshipLandingZone",
                    new EntityCoordinates(map.MapUid, new Vector2(50, 0)));
                cleanup.UnionWith([computer, actor, landing]);
                hospital = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
                hospital.MinCasualties = hospital.MaxCasualties = 1;
                hospital.ShuttleStartupTime = 1;
                hospital.ShuttleTravelTime = 1;
                entities.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.AwaitingApproval));
                safety.Enabled = true;
                safety.Invocations = 0;
                safety.Throw = throwDuringSetup;
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.ActiveShuttle, Is.Not.Null);
                shuttle = hospital.ActiveShuttle!.Value;
                var leaseUid = entities.GetComponent<HospitalTransportShuttleComponent>(shuttle).Lease;
                lease = entities.GetComponent<HospitalTransportLeaseComponent>(leaseUid);
                cleanup.UnionWith(hospital.TransportRoots);
                cleanup.UnionWith(hospital.Patients);
                cleanup.UnionWith([shuttle, leaseUid, hospital.ReturnDestination!.Value]);
                Assert.Multiple(() =>
                {
                    Assert.That(safety.Invocations, Is.EqualTo(1));
                    Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.WaitingForArrival));
                    Assert.That(hospital.ExpectedDestination, Is.Null);
                    Assert.That(lease.Flight, Is.Null);
                    Assert.That(entities.HasComponent<FTLComponent>(shuttle), Is.False);
                    Assert.That(hospital.Patients, Has.Count.EqualTo(1));
                    Assert.That(hospital.Patients.All(patient => !entities.IsQueuedForDeletion(patient)), Is.True);
                    Assert.That(entities.GetComponent<DropshipComponent>(shuttle).Destination, Is.EqualTo(hospital.ReturnDestination));
                    Assert.That(entities.GetComponent<DropshipDestinationComponent>(hospital.ReturnDestination.Value).Ship, Is.EqualTo(shuttle));
                    Assert.That(hospital.LastPayout, Is.Zero);
                });
                safety.Enabled = false;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2.2f));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(hospital.ActiveShuttle, Is.EqualTo(shuttle));
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving), hospital.TransportFailure);
                Assert.That(lease.Flight, Is.Not.Null);
                Assert.That(entities.HasComponent<FTLComponent>(shuttle), Is.True);
                Assert.That(hospital.Patients, Has.Count.EqualTo(1), "Retry cannot generate another batch of patients.");
                Assert.That(hospital.LastPayout, Is.Zero);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                safety.Enabled = false;
                foreach (var uid in cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.CleanReturnAsync();
    }
}

public sealed class HospitalMapInitForeignContentProbeSystem : EntitySystem
{
    public bool Enabled;
    public EntityUid Foreign;
    public EntityUid OriginalMap;
    public int Invocations;

    public override void Initialize()
        => SubscribeLocalEvent<MapGridComponent, MapInitEvent>(OnMapInit);

    private void OnMapInit(Entity<MapGridComponent> ent, ref MapInitEvent args)
    {
        if (!Enabled || Transform(ent).MapUid == OriginalMap) return;
        Enabled = false;
        Invocations++;
        EntityManager.System<SharedTransformSystem>().SetCoordinates(Foreign, new EntityCoordinates(ent.Owner, Vector2.Zero));
        EntityManager.System<MetaDataSystem>().SetEntityPaused(Foreign, true);
    }
}

public sealed class HospitalTransportSafetyProbeSystem : EntitySystem
{
    public bool Enabled;
    public bool Throw;
    public int Invocations;

    public override void Initialize()
        => SubscribeLocalEvent<ShuttleFTLSafetyEvent>(OnSafety);

    private void OnSafety(ref ShuttleFTLSafetyEvent args)
    {
        if (!Enabled || args.Phase != ShuttleFTLSafetyPhase.Setup || !HasComp<HospitalTransportShuttleComponent>(args.Shuttle))
            return;
        Invocations++;
        if (Throw)
            throw new InvalidOperationException("Hospital transport regression: interrupted FTL setup.");
        args.Cancelled = true;
        args.Reason = "Hospital transport regression: setup veto.";
    }
}
