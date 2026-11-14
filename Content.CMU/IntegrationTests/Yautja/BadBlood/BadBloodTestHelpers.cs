using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats;
using Content.Shared.Mobs;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.UnitTesting;

namespace Content.IntegrationTests.CMU14.Yautja.BadBlood;

public sealed class BadBloodTestParty
{
    public EntityUid Leader;
    public readonly List<EntityUid> Grunts = new();

    // set by StartBadBloodRule once the Bad Blood rule prototype exists
    public EntityUid? Rule;

    public IEnumerable<EntityUid> All => Grunts.Prepend(Leader);
}

// shared setup for Bad Blood tests: a Leader and grunts with minds on a test map, the way the threat system leaves them
public static class BadBloodTestHelpers
{
    public const string LeaderPrototype = "CMUMobYautjaBadBloodLeader";
    public const string GruntPrototype = "CMUMobYautjaBadBloodGrunt";
    public const string ThreatMarkerPrototype = "threatentityspawnmarker";

    public static readonly ProtoId<JobPrototype> LeaderJob = "AU14JobThreatLeader";
    public static readonly ProtoId<JobPrototype> MemberJob = "AU14JobThreatMember";
    public static readonly EntProtoId ThreatMindRole = "MindRoleThreat";

    // the Bad Blood rule entity, the clan's state for the round
    public static readonly EntProtoId RulePrototype = "CMUBadBloodCampRule";

    public static async Task<BadBloodTestParty> SpawnParty(
        TestPair pair,
        TestMapData map,
        int grunts = 3,
        bool attachMinds = true,
        bool startRule = true)
    {
        var server = pair.Server;
        var party = new BadBloodTestParty();

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            for (var i = 0; i <= grunts; i++)
            {
                var coords = map.GridCoords.Offset(new Vector2(i, 0));
                entMan.SpawnEntity(ThreatMarkerPrototype, coords);

                var leader = i == 0;
                var body = entMan.SpawnEntity(leader ? LeaderPrototype : GruntPrototype, coords);
                entMan.EnsureComponent<ThreatComponent>(body).ObjectiveJob = leader ? LeaderJob : MemberJob;

                if (leader)
                    party.Leader = body;
                else
                    party.Grunts.Add(body);

                if (attachMinds)
                    AttachThreatMind(server, body);
            }
        });

        if (startRule)
            await StartBadBloodRule(pair, party);

        return party;
    }

    // extension hook: later Bad Blood nodes put their rule setup here so every test that spawns a party gets it.
    // the rule picks the party's Leader when it starts. a started rule is global state: call EndBadBloodRule in a
    // finally block unless the pair is dirty
    public static async Task StartBadBloodRule(TestPair pair, BadBloodTestParty party)
    {
        var server = pair.Server;
        await server.WaitPost(() =>
        {
            if (!server.ProtoMan.HasIndex<EntityPrototype>(RulePrototype.Id))
                return;

            if (server.System<GameTicker>().StartGameRule(RulePrototype, out var rule))
                party.Rule = rule;
        });
    }

    // ends the rule StartBadBloodRule started, so a pooled server does not keep a clan from an earlier test
    public static async Task EndBadBloodRule(TestPair pair, BadBloodTestParty party)
    {
        var server = pair.Server;
        await server.WaitPost(() =>
        {
            if (party.Rule is { } rule && server.EntMan.EntityExists(rule))
                server.System<GameTicker>().EndGameRule(rule);

            party.Rule = null;
        });
    }

    // a mind with no player behind it, holding the threat mind role. call on the server thread
    public static EntityUid AttachThreatMind(RobustIntegrationTest.ServerIntegrationInstance server, EntityUid body)
    {
        var minds = server.System<SharedMindSystem>();
        var mind = minds.CreateMind(null, server.EntMan.GetComponent<MetaDataComponent>(body).EntityName);
        minds.TransferTo(mind, body);
        server.System<SharedRoleSystem>().MindAddRole(mind, ThreatMindRole, silent: true);
        return mind;
    }

    public static Task Kill(TestPair pair, EntityUid body)
    {
        var server = pair.Server;
        return server.WaitPost(() => server.System<MobStateSystem>().ChangeMobState(body, MobState.Dead));
    }

    // the body keeps its mind but nobody is driving it
    public static Task MakeSsd(TestPair pair, EntityUid body)
    {
        var server = pair.Server;
        return server.WaitPost(() =>
        {
            var minds = server.System<SharedMindSystem>();
            if (minds.GetMind(body) is { } mind)
                minds.SetUserId(mind, null);
        });
    }

    public static Task AdvanceClock(TestPair pair, float seconds)
    {
        return pair.RunSeconds(seconds);
    }
}
