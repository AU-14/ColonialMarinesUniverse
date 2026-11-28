#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Threats;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats;
using Content.Shared.Mind;
using Content.Shared.NPC.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

/// <summary><see cref="CMUThreatBodyOptionsComponent"/> keeps the generic threat stamps off a seated body.</summary>
[TestFixture]
public sealed class ThreatBodyOptionsTest
{
    private static readonly ProtoId<JobPrototype> ThreatMemberJob = "AU14JobThreatMember";

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestThreatOptionsBody
          components:
          - type: MindContainer
          - type: CMUThreatBodyOptions
            skipThreatFaction: true
            skipThreatMindRole: true
        - type: entity
          id: CMUTestThreatPlainBody
          components:
          - type: MindContainer
        - type: partySpawn
          id: CMUTestThreatOptionsSpawn
          leadersToSpawn: {}
          gruntsToSpawn:
            CMUTestThreatOptionsBody: 1
        - type: partySpawn
          id: CMUTestThreatPlainSpawn
          leadersToSpawn: {}
          gruntsToSpawn:
            CMUTestThreatPlainBody: 1
        - type: threat
          id: CMUTestThreatOptionsThreat
          roundstartspawns: CMUTestThreatOptionsSpawn
        - type: threat
          id: CMUTestThreatPlainThreat
          roundstartspawns: CMUTestThreatPlainSpawn
        """;

    [TestCase("CMUTestThreatOptionsThreat", true)]
    [TestCase("CMUTestThreatPlainThreat", false)]
    public async Task BodyOptionsSkipThreatFactionAndMindRole(string threatId, bool skipped)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var player = server.PlayerMan.Sessions.Single();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            entMan.System<GameTicker>().JoinAsObserver(player);
            entMan.SpawnEntity("threatmemberspawnmarker", map.GridCoords);

            var assignedJobs = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>
            {
                [player.UserId] = (ThreatMemberJob, EntityUid.Invalid),
            };

            entMan.System<ThreatSystem>().SpawnThreatFromVote(
                server.ProtoMan.Index<ThreatPrototype>(threatId),
                map.MapId,
                assignedJobs,
                [player.UserId],
                playerCount: 16);
        });

        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var mindId = entMan.System<SharedMindSystem>().GetMind(player.UserId);
            Assert.That(mindId, Is.Not.Null);
            var mind = entMan.GetComponent<MindComponent>(mindId!.Value);
            Assert.That(mind.OwnedEntity, Is.Not.Null, "the voter must be seated in the threat body");
            var body = mind.OwnedEntity!.Value;

            var hasThreatFaction = entMan.TryGetComponent(body, out NpcFactionMemberComponent? factions)
                && factions.Factions.Contains("THREAT");
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ThreatComponent>(body), Is.EqualTo(!skipped));
                Assert.That(hasThreatFaction, Is.EqualTo(!skipped));
                Assert.That(MindHasRolePrototype(entMan, mind, "MindRoleThreat"), Is.EqualTo(!skipped));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static bool MindHasRolePrototype(IEntityManager entMan, MindComponent mind, string prototype)
    {
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (entMan.TryGetComponent(role, out MetaDataComponent? meta) && meta.EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}
