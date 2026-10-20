#pragma warning disable RA0002 // The fixture controls phase durations and inspects committed transport state.
using Content.Server.CMU14.Hospital;
using Content.Server.CMU14.ColonyEconomy;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Hospital;
using Content.Shared.CMU14.Round;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.Medical.Hospital;

[TestFixture]
public sealed class HospitalFtlRoundTripTest
{
    // CMU14: exercise each payment destination through the real settlement lifecycle.
    [TestCase(false, false, "AU14HospitalEmergencyComputer", null)]
    [TestCase(true, false, "CMUHospitalEmergencyComputerColony", null)]
    [TestCase(false, true, "CMUHospitalEmergencyComputerColony", null)]
    [TestCase(false, false, "CMUHospitalEmergencyComputerGovfor", "govfor")]
    [TestCase(false, false, "CMUHospitalEmergencyComputerOpfor", "opfor")]
    [Category("HospitalTransport"), CancelAfter(180000)]
    public async Task RealDeliveryAndPickupRetryCooldownAndSettleOnceAfterReturning(bool staleReservation, bool differentDeck,
        string computerPrototype, string? payoutFaction)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var computerDeck = differentDeck ? await pair.CreateTestMap() : map;
        var entities = server.EntMan;
        var originalArrivalTime = server.CfgMan.GetCVar(CCVars.FTLArrivalTime);
        var transportEntities = new HashSet<EntityUid>();
        var returnMaps = new Dictionary<EntityUid, EntityUid>();
        EntityUid computer = default;
        EntityUid actor = default;
        EntityUid patient = default;
        EntityUid landing = default;
        EntityUid delivery = default;
        EntityUid pickup = default;
        EntityUid pickupReturnMap = default;
        EntityUid? deckNetwork = null;
        HospitalEmergencyComputerComponent hospital = default!;
        var initialMaps = 0;
        var initialGrids = 0;
        var expectedReward = 0;
        var colonyBudget = server.System<ColonyBudgetSystem>();
        var originalBudget = 0f;
        var accounts = new Dictionary<string, EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                // Keep the production cooldown. Only flight animation/travel phases
                // are shortened; no successful FTL completion is injected by this test.
                Assert.That(server.CfgMan.GetCVar(CCVars.FTLCooldown), Is.EqualTo(60f));
                server.CfgMan.SetCVar(CCVars.FTLArrivalTime, 0.1f);
                if (differentDeck)
                {
                    var zLevels = server.System<CMUZLevelsSystem>();
                    var network = zLevels.CreateZNetwork();
                    deckNetwork = network.Owner;
                    Assert.That(zLevels.TryAddMapsIntoZNetwork(network,
                        new Dictionary<EntityUid, int> { [map.MapUid] = 0, [computerDeck.MapUid] = 1 }), Is.True);
                }
                originalBudget = colonyBudget.GetBudget();
                foreach (var faction in new[] { "govfor", "opfor", "unassigned" })
                {
                    var account = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                    var accountComp = entities.AddComponent<RequisitionsAccountComponent>(account);
                    accountComp.Faction = faction;
                    accountComp.Balance = 100;
                    accountComp.Started = true;
                    accounts.Add(faction, account);
                }
                computer = entities.SpawnEntity(computerPrototype, computerDeck.GridCoords);
                actor = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                entities.EnsureComponent<CMInStasisComponent>(actor);
                // Keep the landing shuttle away from the hospital and its patient.
                landing = entities.SpawnEntity("AU14HospitalDropshipLandingZone",
                    new EntityCoordinates(map.MapUid, new Vector2(50, 0)));
                hospital = entities.GetComponent<HospitalEmergencyComputerComponent>(computer);
                Assert.That(hospital.RequisitionsFaction, Is.EqualTo(payoutFaction));
                hospital.MinCasualties = hospital.MaxCasualties = 1;
                hospital.ShuttleStartupTime = 0.1f;
                // Retain an observable departure phase between one-second polls.
                hospital.ShuttleDepartureStartupTime = 1.1f;
                hospital.ShuttleTravelTime = 0.2f;
                initialMaps = NonTransitMapCount();
                initialGrids = entities.Count<MapGridComponent>();
                Assert.That(server.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1)), Is.EqualTo(1));
            });
            await WaitForPhase(HospitalEmergencyStatus.AwaitingApproval, 5);
            await server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving), hospital.TransportFailure);
                delivery = CaptureTransport();
                Assert.That(hospital.ExpectedDestination, Is.EqualTo(landing));
                patient = hospital.Patients.Single();
                // Treatment is exercised by other tests. Preserve a healthy patient
                // through the real transport clocks without adding ambient pathology.
                server.System<RejuvenateSystem>().PerformRejuvenate(patient);
                entities.EnsureComponent<CMInStasisComponent>(patient);
                Assert.That(server.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
                expectedReward = hospital.BaseRewardPerPatient + hospital.SeverityRewardBonus * hospital.Severity;
            });
            await WaitForPhase(HospitalEmergencyStatus.ManualUnloading, 10);
            await server.WaitAssertion(() =>
            {
                AssertLanded(delivery);
                Assert.That(entities.GetComponent<TransformComponent>(patient).GridUid, Is.EqualTo(delivery));
                server.System<SharedTransformSystem>().SetCoordinates(patient, map.GridCoords);
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyReleaseShuttleMsg { Actor = actor });
                AssertWaitingForCooldown(delivery);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(30));
            await server.WaitAssertion(() => AssertWaitingForCooldown(delivery));
            await WaitForPhase(HospitalEmergencyStatus.Treating, 45);
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                AssertLeaseReclaimed();
                Assert.That(entities.EntityExists(patient), Is.True);
                Assert.That(hospital.Patients, Does.Contain(patient));
                Assert.That(hospital.LastPayout, Is.Zero);
                Assert.That(CashTotal(), Is.Zero);
                AssertBudgets(0);
                Assert.That(server.System<HospitalEmergencySystem>().AssessDischarge(patient).Cleared, Is.True);
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyRequestPickupMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.PickupInbound), hospital.TransportFailure);
                pickup = CaptureTransport();
                pickupReturnMap = entities.GetComponent<TransformComponent>(hospital.ReturnDestination!.Value).MapUid!.Value;
                Assert.That(pickup, Is.Not.EqualTo(delivery));
                Assert.That(hospital.ExpectedDestination, Is.EqualTo(landing));
            });
            await WaitForPhase(HospitalEmergencyStatus.PickupBoarding, 10);
            await server.WaitAssertion(() =>
            {
                AssertLanded(pickup);
                server.System<SharedTransformSystem>().SetCoordinates(patient, new EntityCoordinates(pickup, Vector2.Zero));
            });
            // A later manual release still precedes the real sixty-second recharge.
            await pair.RunTicksSync(pair.SecondsToTicks(30));
            await server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyReleaseShuttleMsg { Actor = actor });
                AssertWaitingForCooldown(pickup);
            });
            await WaitForPhase(HospitalEmergencyStatus.ShuttleDeparting, 40);
            await server.WaitAssertion(() =>
            {
                Assert.That(hospital.ExpectedDestination, Is.EqualTo(hospital.ReturnDestination));
                // Even the correct shuttle completing on the wrong map cannot settle.
                var wrongMap = new FTLCompletedEvent(pickup, map.MapUid);
                entities.EventBus.RaiseEvent(EventSource.Local, ref wrongMap);
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.ShuttleDeparting));
                Assert.That(hospital.LastPayout, Is.Zero);
                Assert.That(CashTotal(), Is.Zero);
                AssertBudgets(0);
                Assert.That(entities.IsQueuedForDeletion(patient), Is.False);
            });
            await WaitForPhase(HospitalEmergencyStatus.RewardReady, 10);
            await pair.RunTicksSync(2);
            await server.WaitAssertion(() =>
            {
                AssertLeaseReclaimed();
                Assert.That(entities.EntityExists(patient), Is.False);
                Assert.That(hospital.Patients, Is.Empty);
                Assert.That(hospital.LastMissedInjuries, Is.Zero);
                Assert.That(hospital.LastPayout, Is.EqualTo(expectedReward));
                Assert.That(CashTotal(), Is.Zero, "Hospital payments go directly to the budget.");
                AssertBudgets(expectedReward);

                var duplicate = new FTLCompletedEvent(pickup, pickupReturnMap);
                entities.EventBus.RaiseEvent(EventSource.Local, ref duplicate);
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyReleaseShuttleMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.RewardReady));
                Assert.That(hospital.LastPayout, Is.EqualTo(expectedReward));
                Assert.That(CashTotal(), Is.Zero);
                AssertBudgets(expectedReward); // Repeated completion/release cannot issue a second payment.
            });

            // The next incident is the third shuttle call, using the same landing pad.
            await server.WaitAssertion(() =>
            {
                if (staleReservation)
                    server.System<SharedDropshipSystem>().SetDestinationShip(landing, pickup);
                server.System<HospitalEmergencySystem>().SetNextIncidentDelay(TimeSpan.FromSeconds(1));
            });
            await WaitForPhase(HospitalEmergencyStatus.AwaitingApproval, 5);
            await server.WaitAssertion(() =>
            {
                entities.EventBus.RaiseLocalEvent(computer, new HospitalEmergencyApproveLandingMsg { Actor = actor });
                Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.Arriving), hospital.TransportFailure);
                delivery = CaptureTransport();
                patient = hospital.Patients.Single();
                entities.EnsureComponent<CMInStasisComponent>(patient);
            });
            await WaitForPhase(HospitalEmergencyStatus.ManualUnloading, 10);
            await server.WaitAssertion(() => AssertLanded(delivery));
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.CfgMan.SetCVar(CCVars.FTLArrivalTime, originalArrivalTime);
                colonyBudget.SetBudget(originalBudget);
                foreach (var account in accounts.Values)
                    entities.DeleteEntity(account);
                foreach (var uid in transportEntities.Append(patient).Append(computer).Append(actor).Append(landing))
                {
                    if (entities.EntityExists(uid))
                        entities.DeleteEntity(uid);
                }
                if (deckNetwork is { } network && entities.EntityExists(network))
                    entities.DeleteEntity(network);
            });
        }
        await pair.CleanReturnAsync();

        int NonTransitMapCount() => entities.Count<MapComponent>() - entities.Count<FTLMapComponent>();

        void AssertBudgets(int payout)
        {
            Assert.That(colonyBudget.GetBudget(), Is.EqualTo(originalBudget + (payoutFaction == null ? payout : 0)));
            foreach (var (faction, account) in accounts)
                Assert.That(entities.GetComponent<RequisitionsAccountComponent>(account).Balance,
                    Is.EqualTo(100 + (faction == payoutFaction ? payout : 0)), $"Unexpected payment to {faction}.");
        }

        int CashTotal()
        {
            var total = 0;
            var query = entities.EntityQueryEnumerator<StackComponent, MetaDataComponent, TransformComponent>();
            while (query.MoveNext(out _, out var stack, out var metadata, out var transform))
            {
                if (metadata.EntityPrototype?.ID == "RMCSpaceCash" && transform.MapUid == computerDeck.MapUid)
                    total += stack.Count;
            }
            return total;
        }

        EntityUid CaptureTransport()
        {
            Assert.That(hospital.ActiveShuttle, Is.Not.Null);
            Assert.That(hospital.ReturnDestination, Is.Not.Null);
            var shuttle = hospital.ActiveShuttle!.Value;
            transportEntities.Add(shuttle);
            transportEntities.Add(hospital.ReturnDestination!.Value);
            transportEntities.UnionWith(hospital.TransportRoots);
            var returnMap = entities.GetComponent<TransformComponent>(hospital.ReturnDestination.Value).MapUid;
            Assert.That(returnMap, Is.Not.Null);
            Assert.That(returnMap, Is.Not.EqualTo(map.MapUid));
            Assert.That(hospital.TransportRoots, Does.Contain(returnMap!.Value));
            returnMaps.Add(shuttle, returnMap.Value);
            Assert.That(entities.HasComponent<FTLComponent>(shuttle), Is.True);
            var navigation = entities.EntityQueryEnumerator<DropshipNavigationComputerComponent, WhitelistedShuttleComponent, TransformComponent>();
            var foundNavigation = false;
            while (navigation.MoveNext(out _, out _, out var whitelist, out var transform))
            {
                if (transform.GridUid != shuttle)
                    continue;
                foundNavigation = true;
                Assert.That(whitelist.AutoReturn, Is.False,
                    "Hospital transports must not be controlled by the generic inactivity return timer.");
            }
            Assert.That(foundNavigation, Is.True);
            return shuttle;
        }

        void AssertLanded(EntityUid shuttle)
        {
            Assert.That(entities.GetComponent<TransformComponent>(shuttle).MapUid, Is.EqualTo(map.MapUid));
            Assert.That(entities.GetComponent<DropshipComponent>(shuttle).Destination, Is.EqualTo(landing));
            Assert.That(entities.GetComponent<FTLComponent>(shuttle).State, Is.EqualTo(FTLState.Cooldown));
            Assert.That(hospital.ExpectedDestination, Is.Null);
            var returnMarker = entities.GetComponent<TransformComponent>(hospital.ReturnDestination!.Value);
            Assert.That(returnMarker.MapUid, Is.EqualTo(returnMaps[shuttle]),
                "The return marker must remain on the leased home map when its original grid departs.");
            Assert.That(returnMarker.GridUid, Is.Null);
        }

        void AssertWaitingForCooldown(EntityUid shuttle)
        {
            Assert.That(hospital.Status, Is.EqualTo(HospitalEmergencyStatus.WaitingForDeparture));
            Assert.That(hospital.ActiveShuttle, Is.EqualTo(shuttle));
            Assert.That(entities.GetComponent<FTLComponent>(shuttle).State, Is.EqualTo(FTLState.Cooldown));
            Assert.That(hospital.Patients, Does.Contain(patient));
            Assert.That(entities.IsQueuedForDeletion(patient), Is.False);
            Assert.That(hospital.LastPayout, Is.Zero);
            Assert.That(CashTotal(), Is.Zero);
        }

        void AssertLeaseReclaimed()
        {
            Assert.That(hospital.ActiveShuttle, Is.Null);
            Assert.That(hospital.ReturnDestination, Is.Null);
            Assert.That(hospital.ExpectedDestination, Is.Null);
            Assert.That(hospital.TransportRoots, Is.Empty);
            foreach (var uid in transportEntities)
                Assert.That(entities.EntityExists(uid), Is.False, $"Transport lease entity {uid} survived verified return.");
            Assert.That(entities.Count<MapGridComponent>(), Is.EqualTo(initialGrids));
            Assert.That(NonTransitMapCount(), Is.EqualTo(initialMaps));
        }

        async Task WaitForPhase(HospitalEmergencyStatus expected, int maximumSeconds)
        {
            var observed = HospitalEmergencyStatus.Idle;
            var failure = string.Empty;
            for (var second = 0; second <= maximumSeconds; second++)
            {
                await server.WaitPost(() =>
                {
                    observed = hospital.Status;
                    failure = hospital.TransportFailure;
                });
                if (observed == expected)
                    return;
                if (second < maximumSeconds)
                    await pair.RunTicksSync(pair.SecondsToTicks(1));
            }
            Assert.Fail($"Hospital did not reach {expected} within {maximumSeconds} simulated seconds; state={observed}, failure={failure}");
        }
    }
}
