using System.Collections;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Round;
using Content.Server.CMU14.Threats;
using Content.Server.CMU14.Yautja;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats;

/// <summary>
/// FLOW-121 wiring: the ballot really consults <c>blockedGameRules</c>, and the hunt dice really consult the
/// selected threat.
/// </summary>
public sealed class ThreatBallotBlockTest : GameTest
{
    private static readonly ProtoId<ThreatPrototype> BlockedThreat = "CMUTestBallotBlockedThreat";
    private static readonly ProtoId<ThreatPrototype> OpenThreat = "CMUTestBallotOpenThreat";
    private static readonly ProtoId<ThreatPrototype> BadBloodThreat = "BadBloodClanThreat";
    private static readonly EntProtoId HuntRule = "CMUYautjaPredatorRound";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestBallotBody
          components:
          - type: MindContainer
        - type: partySpawn
          id: CMUTestBallotSpawn
          leadersToSpawn:
            CMUTestBallotBody: 1
        - type: threat
          id: CMUTestBallotBlockedThreat
          roundstartspawns: CMUTestBallotSpawn
          blockedGameRules:
          - CMUYautjaPredatorRound
        - type: threat
          id: CMUTestBallotOpenThreat
          roundstartspawns: CMUTestBallotSpawn
        """;

    [Test]
    public async Task ThreatBlockedByAnAddedHuntIsLeftOffTheBallot()
    {
        await Server.WaitAssertion(() =>
        {
            var ticker = SEntMan.System<GameTicker>();
            Assert.That(SEntMan.System<YautjaPredatorRoundSystem>().HuntActive, Is.False);

            Assert.That(BallotCandidates(), Is.EquivalentTo(new[] { BlockedThreat.Id, OpenThreat.Id }),
                "with no hunt added both threats must be candidates, or the test proves nothing");

            var hunt = ticker.AddGameRule(HuntRule);
            try
            {
                Assert.That(BallotCandidates(), Is.EquivalentTo(new[] { OpenThreat.Id }),
                    "a threat that blocks the added hunt rule must stay off the ballot");
            }
            finally
            {
                // pooled servers are reused: a hunt left behind blocks threats in later tests
                SEntMan.DeleteEntity(hunt);
            }

            Assert.That(BallotCandidates(), Is.EquivalentTo(new[] { BlockedThreat.Id, OpenThreat.Id }));
        });
    }

    [Test]
    public async Task HuntDiceAreSkippedWhileABlockingThreatIsSelected()
    {
        await Server.WaitAssertion(() =>
        {
            var round = SEntMan.System<YautjaPredatorRoundSystem>();
            var auRound = SEntMan.System<AuRoundSystem>();
            var cfg = Server.ResolveDependency<IConfigurationManager>();
            var rollHunt = typeof(YautjaPredatorRoundSystem).GetMethod("RollHunt",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var rolledHunt = typeof(YautjaPredatorRoundSystem).GetField("_rolledHunt",
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            var chance = cfg.GetCVar(YautjaPredatorRoundCVars.InsurgencyChance);
            Assert.That(round.HuntActive, Is.False);

            try
            {
                // a certain roll, so only the selected threat can stop the hunt
                cfg.SetCVar(YautjaPredatorRoundCVars.InsurgencyChance, 1f);

                auRound.SetSelectedThreat(Server.ProtoMan.Index(BadBloodThreat));
                rollHunt.Invoke(round, new object?[] { YautjaHuntRoll.InsurgencyPreset });
                Assert.That(round.HuntActive, Is.False, "the dice must not start a hunt the selected threat blocks");

                auRound.SetSelectedThreat(null);
                rollHunt.Invoke(round, new object?[] { YautjaHuntRoll.InsurgencyPreset });
                Assert.That(round.HuntActive, Is.True, "the same roll must start a hunt once nothing blocks it");
            }
            finally
            {
                // pooled servers are reused: none of this may outlive the test
                cfg.SetCVar(YautjaPredatorRoundCVars.InsurgencyChance, chance);
                auRound.SetSelectedThreat(null);
                rolledHunt.SetValue(round, null);
                var ticker = SEntMan.System<GameTicker>();
                foreach (var rule in ticker.GetAddedGameRules().ToList())
                {
                    if (SEntMan.GetComponent<MetaDataComponent>(rule).EntityPrototype?.ID == HuntRule.Id)
                        ticker.EndGameRule(rule);
                }
            }

            Assert.That(round.HuntActive, Is.False);
        });
    }

    // the ids ThreatVoteSystem would put on the ballot for a planet that allows both test threats
    private List<string> BallotCandidates()
    {
        // the planet component is only readable from outside its system, so the list is filled by reflection
        var planet = new RMCPlanetMapPrototypeComponent();
        typeof(RMCPlanetMapPrototypeComponent).GetField("AllowedThreats")!
            .SetValue(planet, new List<ProtoId<ThreatPrototype>> { BlockedThreat, OpenThreat });

        var build = typeof(ThreatVoteSystem).GetMethod("BuildLegacyCandidates",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var candidates = (IEnumerable) build.Invoke(SEntMan.System<ThreatVoteSystem>(),
            new object[] { planet, YautjaHuntRoll.InsurgencyPreset, 10 })!;

        var ids = new List<string>();
        foreach (var candidate in candidates)
        {
            var threat = (ThreatPrototype) candidate.GetType().GetProperty("Threat")!.GetValue(candidate)!;
            ids.Add(threat.ID);
        }

        return ids;
    }
}
