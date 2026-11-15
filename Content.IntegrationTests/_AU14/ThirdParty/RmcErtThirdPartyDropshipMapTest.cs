using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Utility;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Threats;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.Threats;
using Content.Shared.Shuttles.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.IntegrationTests.CMU14.ThirdParty;

[TestFixture]
public sealed class RmcErtThirdPartyDropshipMapTest
{

    private static readonly ProtoId<ThirdPartyPrototype> MissionariesParty = "MissionariesParty";

    [Test]
    public async Task ThirdPartyShuttleSpawnWaitsForManualLaunch()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var volunteers = await server.AddDummySessions(2);
        await pair.RunUntilSynced();

        EntityUid genericDestination = EntityUid.Invalid;
        EntityUid returnedDestination = EntityUid.Invalid;
        EntityUid thirdPartyDestination = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var thirdPartySystem = server.System<ThirdPartySystem>();
            var thirdParty = prototypes.Index<ThirdPartyPrototype>(MissionariesParty);
            var partySpawn = prototypes.Index<PartySpawnPrototype>(thirdParty.PartySpawn);

            genericDestination = entities.SpawnEntity("CMDropshipDestination", map.GridCoords);

            returnedDestination = entities.SpawnEntity("CMDropshipDestinationThirdPartyReturn", map.GridCoords);
            var returnDestination = entities.EnsureComponent<ThirdPartyDropshipReturnDestinationComponent>(
                returnedDestination);
            returnDestination.Shuttle = EntityUid.Invalid;

            thirdPartyDestination = entities.SpawnEntity("CMDropshipDestinationThirdPartyWhitelist", map.GridCoords);

            thirdPartySystem.SpawnThirdParty(thirdParty, partySpawn, false);
            var interest = server.System<ForceInterestSystem>();
            var force = interest.GetForces(volunteers[0]).Single();
            Assert.That(force.RequiredPlayers, Is.EqualTo(volunteers.Length));
            foreach (var volunteer in volunteers)
                interest.SetInterest(volunteer, force.Identifier, true);
            Assert.That(interest.GetForces(volunteers[0]).Single().InterestedPlayers, Is.EqualTo(volunteers.Length));
        });
        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            EntityUid? spawnedDropship = null;
            EntityUid? spawnedReturnDestination = null;
            var dropshipQuery = entities.EntityQueryEnumerator<DropshipComponent>();
            while (dropshipQuery.MoveNext(out var uid, out var dropship))
            {
                if (dropship.Destination is not { } destination ||
                    !entities.TryGetComponent(destination,
                        out ThirdPartyDropshipReturnDestinationComponent returnDestination) ||
                    returnDestination.Shuttle != uid)
                {
                    continue;
                }

                spawnedDropship = uid;
                spawnedReturnDestination = destination;
                break;
            }

            Assert.That(spawnedDropship, Is.Not.Null);
            Assert.That(spawnedReturnDestination, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(genericDestination).Ship,
                    Is.Null,
                    "Generic dropship destinations must not be selected for strict third-party shuttles.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(returnedDestination).Ship,
                    Is.Null,
                    "Returned third-party holding vectors must not be reused as active landing destinations.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(thirdPartyDestination).Ship,
                    Is.Null,
                    "The spawned third-party shuttle must not auto-launch to the active third-party landing destination.");
                Assert.That(
                    entities.GetComponent<DropshipDestinationComponent>(spawnedReturnDestination.Value).Ship,
                    Is.EqualTo(spawnedDropship.Value),
                    "The spawned third-party shuttle should stay parked at its generated deep-space return destination.");
                Assert.That(
                    entities.HasComponent<FTLComponent>(spawnedDropship.Value),
                    Is.False,
                    "The spawned third-party shuttle should wait for a manual launch instead of entering FTL.");
            });
        });

        await pair.CleanReturnAsync();
    }

}
