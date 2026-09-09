#pragma warning disable RA0002 // Fixture controls travel duration and inspects owned transport state.
using Content.Server.CMU14.Hospital;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Administration.Systems;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Round;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalTransportRecoveryTest
{
    [Test, Category("HospitalTransport"), Timeout(240000)]
    public async Task ConsoleLossPreservesNonmanifestPeopleAndOffersARealReturnAfterTheyBoard()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        var arrivalTime = server.CfgMan.GetCVar(CCVars.FTLArrivalTime);
        var reusable = server.CfgMan.GetCVar(RMCCVars.ThirdPartyDropshipReusable);
        var cleanup = new HashSet<EntityUid>();
        EntityUid computer = default, actor = default, landing = default, shuttle = default;
        EntityUid patient = default, passenger = default, stranded = default, belonging = default, leaseUid = default, returnMap = default;
        HospitalEmergencyComputerComponent hospital = default!;
        HospitalTransportLeaseComponent lease = default!;
        try
        {
            await server.WaitAssertion(() =>
            {
                // Keep both production sixty-second cooldowns. Only animations
                // and travel are shortened; each arrival is real engine FTL.
                Assert.That(server.CfgMan.GetCVar(CCVars.FTLCooldown), Is.EqualTo(60f));
                server.CfgMan.SetCVar(CCVars.FTLArrivalTime, 0.1f);
                server.CfgMan.SetCVar(RMCCVars.ThirdPartyDropshipReusable, false);
                computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                entities.EnsureComponent<CMInStasisComponent>(actor);
                landing = entities.SpawnEntity("AU14HospitalDropshipLandingZone",
                    new EntityCoordinates(map.MapUid, new Vector2(50, 0)));
                cleanup.UnionWith([computer, actor, landing]);
                hospital = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
                hospital.MinCasualties = hospital.MaxCasualties = 1;
                hospital.ShuttleStartupTime = 0.1f;
                hospital.ShuttleDepartureStartupTime = 1.1f;
                hospital.ShuttleTravelTime = 0.2f;
                server.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1));
            });
            await WaitUntil(() => hospital.Status == HospitalEmergencyStatus.AwaitingApproval, 5, "incident approval");
            await server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving), hospital.TransportFailure);
                shuttle = hospital.ActiveShuttle!.Value;
                leaseUid = entities.GetComponent<HospitalTransportShuttleComponent>(shuttle).Lease;
                lease = entities.GetComponent<HospitalTransportLeaseComponent>(leaseUid);
                returnMap = entities.GetComponent<TransformComponent>(hospital.ReturnDestination!.Value).MapUid!.Value;
                cleanup.UnionWith(lease.Roots);
                cleanup.UnionWith([shuttle, leaseUid, hospital.ReturnDestination.Value]);
                patient = hospital.Patients.Single();
                cleanup.Add(patient);
                server.System<RejuvenateSystem>().PerformRejuvenate(patient);
                entities.EnsureComponent<CMInStasisComponent>(patient);
            });
            await WaitUntil(() => hospital.Status == HospitalEmergencyStatus.ManualUnloading, 10, "hospital arrival");
            await server.WaitAssertion(() =>
            {
                server.System<SharedTransformSystem>().SetCoordinates(patient, map.GridCoords);
                passenger = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(shuttle, Vector2.Zero));
                // A patient marker is not permission to discard its live owner.
                // This person was never in the console's manifest and is ashore
                // on the leased return map when the console disappears.
                stranded = entities.SpawnEntity("CMMobHuman", new EntityCoordinates(returnMap, new Vector2(30, 0)));
                belonging = entities.SpawnEntity("CMScalpel", new EntityCoordinates(returnMap, new Vector2(31, 0)));
                entities.System<MetaDataSystem>().SetEntityPaused(belonging, true);
                entities.EnsureComponent<HospitalPatientComponent>(stranded).SourceComputer = computer;
                entities.EnsureComponent<CMInStasisComponent>(passenger);
                entities.EnsureComponent<CMInStasisComponent>(stranded);
                cleanup.UnionWith([passenger, stranded, belonging]);
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyReleaseShuttleMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.WaitingForDeparture));
            });
            await WaitUntil(() => hospital.Status == HospitalEmergencyStatus.ShuttleDeparting, 70, "return departure");
            await server.WaitAssertion(() =>
            {
                entities.DeleteEntity(computer);
                Assert.That(lease.Retiring, Is.True);
                Assert.That(lease.Computer, Is.Null);
                Assert.That(entities.IsQueuedForDeletion(passenger), Is.False);
                Assert.That(entities.IsQueuedForDeletion(returnMap), Is.False);
            });
            await WaitUntil(() => entities.GetComponent<TransformComponent>(shuttle).MapUid == returnMap &&
                                  entities.GetComponent<FTLComponent>(shuttle).State == FTLState.Cooldown,
                10, "orphan transport return");
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<PreventFTLComponent>(shuttle), Is.False,
                    "Generic nonreusable transport retirement must not strand this hospital lease.");
                Assert.That(HasNavigation(), Is.True);
                Assert.That(entities.EntityExists(stranded), Is.True);
                Assert.That(CashTotal(), Is.Zero);
            });
            await WaitUntil(() => !entities.HasComponent<FTLComponent>(shuttle), 70, "real recovery cooldown");
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(shuttle).MapUid, Is.EqualTo(returnMap));
                Assert.That(server.System<HospitalEmergencySystem>().RequestHospitalTransportRecovery(shuttle), Is.False,
                    "Recovery cannot abandon a living person elsewhere on its owned map.");
                Assert.That(entities.IsQueuedForDeletion(stranded), Is.False);
                Assert.That(entities.IsQueuedForDeletion(returnMap), Is.False);
                server.System<SharedTransformSystem>().SetCoordinates(stranded, new EntityCoordinates(shuttle, Vector2.Zero));
                Assert.That(server.System<HospitalEmergencySystem>().RequestHospitalTransportRecovery(shuttle), Is.False,
                    "Boarding all people cannot abandon a foreign item still ashore.");
                server.System<SharedTransformSystem>().SetCoordinates(belonging, new EntityCoordinates(shuttle, Vector2.Zero));
                Assert.That(server.System<HospitalEmergencySystem>().RequestHospitalTransportRecovery(shuttle), Is.True);
                Assert.That(entities.HasComponent<FTLComponent>(shuttle), Is.True);
            });
            await WaitUntil(() => entities.GetComponent<TransformComponent>(shuttle).MapUid == map.MapUid &&
                                  entities.GetComponent<FTLComponent>(shuttle).State == FTLState.Cooldown,
                10, "recovery hospital arrival");
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(passenger), Is.True);
                Assert.That(entities.EntityExists(stranded), Is.True);
                server.System<SharedTransformSystem>().SetCoordinates(passenger, map.GridCoords);
                server.System<SharedTransformSystem>().SetCoordinates(stranded, map.GridCoords);
                server.System<SharedTransformSystem>().SetCoordinates(belonging, map.GridCoords);
                Assert.That(CashTotal(), Is.Zero, "Console loss/recovery never creates a discharge payout.");
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(shuttle), Is.False);
                Assert.That(entities.EntityExists(leaseUid), Is.False);
                Assert.That(entities.EntityExists(returnMap), Is.False);
                Assert.That(entities.EntityExists(patient), Is.True);
                Assert.That(entities.EntityExists(passenger), Is.True);
                Assert.That(entities.EntityExists(stranded), Is.True);
                Assert.That(entities.EntityExists(belonging), Is.True);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(CCVars.FTLArrivalTime, arrivalTime);
                server.CfgMan.SetCVar(RMCCVars.ThirdPartyDropshipReusable, reusable);
                foreach (var uid in cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.CleanReturnAsync();

        bool HasNavigation()
        {
            var query = entities.EntityQueryEnumerator<DropshipNavigationComputerComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var transform))
                if (transform.GridUid == shuttle) return true;
            return false;
        }

        int CashTotal()
        {
            var total = 0;
            var query = entities.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var stack, out var metadata, out var transform))
                if (metadata.EntityPrototype?.ID == hospital.CashPrototype.Id && transform.MapUid == map.MapUid)
                    total += stack.Count;
            return total;
        }

        async Task WaitUntil(Func<bool> predicate, int seconds, string phase)
        {
            for (var second = 0; second <= seconds; second++)
            {
                var complete = false;
                await server.WaitPost(() => complete = predicate());
                if (complete) return;
                if (second < seconds) await pair.RunTicksSync(pair.SecondsToTicks(1));
            }
            Assert.Fail($"Timed out during {phase}; hospital={hospital.Status}, lease={lease?.Failure}");
        }
    }

    [TestCase(false), TestCase(true), Category("HospitalTransport"), Timeout(120000)]
    public async Task AnActualArrivalCannotCommitAgainstAChangedDestinationOrLandingPosition(bool displaceShuttle)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var arrivalTime = pair.Server.CfgMan.GetCVar(CCVars.FTLArrivalTime);
        var cleanup = new HashSet<EntityUid>();
        var probe = entities.System<HospitalArrivalIdentityProbeSystem>();
        EntityUid computer = default, actor = default, landing = default, shuttle = default;
        HospitalEmergencyComputerComponent hospital = default!;
        HospitalTransportLeaseComponent lease = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                pair.Server.CfgMan.SetCVar(CCVars.FTLArrivalTime, 0.1f);
                computer = entities.SpawnEntity("AU14HospitalEmergencyComputer", map.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                landing = entities.SpawnEntity("AU14HospitalDropshipLandingZone",
                    new EntityCoordinates(map.MapUid, new Vector2(50, 0)));
                cleanup.UnionWith([computer, actor, landing]);
                hospital = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
                hospital.MinCasualties = hospital.MaxCasualties = 1;
                hospital.ShuttleStartupTime = 0.1f;
                hospital.ShuttleTravelTime = 0.2f;
                entities.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
            await pair.Server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving), hospital.TransportFailure);
                shuttle = hospital.ActiveShuttle!.Value;
                var leaseUid = entities.GetComponent<HospitalTransportShuttleComponent>(shuttle).Lease;
                lease = entities.GetComponent<HospitalTransportLeaseComponent>(leaseUid);
                cleanup.UnionWith(lease.Roots);
                cleanup.UnionWith(hospital.Patients);
                cleanup.UnionWith([shuttle, leaseUid, hospital.ReturnDestination!.Value]);
                foreach (var patient in hospital.Patients)
                    entities.EnsureComponent<CMInStasisComponent>(patient);
                probe.Shuttle = shuttle;
                probe.Destination = landing;
                probe.Invocations = 0;
                probe.DisplaceShuttle = displaceShuttle;
                probe.Enabled = true;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(probe.Invocations, Is.EqualTo(1));
                Assert.That(entities.GetComponent<TransformComponent>(shuttle).MapUid, Is.EqualTo(map.MapUid));
                Assert.That(entities.GetComponent<FTLComponent>(shuttle).State, Is.EqualTo(FTLState.Cooldown));
                if (!displaceShuttle)
                    Assert.That(entities.GetComponent<DropshipDestinationComponent>(landing), Is.Not.SameAs(lease.Flight!.DestinationComponent));
                else
                    Assert.That(entities.System<SharedTransformSystem>().GetMapCoordinates(shuttle).Position,
                        Is.Not.EqualTo(lease.Flight!.DestinationPosition.Position));
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving),
                    "Matching entity and map IDs do not validate a replaced launch destination.");
                Assert.That(hospital.ExpectedDestination, Is.EqualTo(landing));
                Assert.That(hospital.LastPayout, Is.Zero);
                Assert.That(hospital.Patients, Has.Count.EqualTo(1));
                Assert.That(hospital.Patients.All(patient => !entities.IsQueuedForDeletion(patient)), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                probe.Enabled = false;
                pair.Server.CfgMan.SetCVar(CCVars.FTLArrivalTime, arrivalTime);
                foreach (var uid in cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.CleanReturnAsync();
    }
}

public sealed class HospitalArrivalIdentityProbeSystem : EntitySystem
{
    public bool Enabled;
    public EntityUid Shuttle;
    public EntityUid Destination;
    public int Invocations;
    public bool DisplaceShuttle;

    public override void Initialize()
        => SubscribeLocalEvent<FTLCompletedEvent>(OnCompleted, before: [typeof(HospitalEmergencySystem)]);

    private void OnCompleted(ref FTLCompletedEvent args)
    {
        if (!Enabled || args.Entity != Shuttle) return;
        Enabled = false;
        Invocations++;
        if (DisplaceShuttle)
        {
            var transform = Transform(Shuttle);
            EntityManager.System<SharedTransformSystem>().SetCoordinates(Shuttle, transform.Coordinates.Offset(new Vector2(20, 0)));
            return;
        }
        RemComp<DropshipDestinationComponent>(Destination);
        var replacement = AddComp<DropshipDestinationComponent>(Destination);
        replacement.Ship = Shuttle;
    }
}
