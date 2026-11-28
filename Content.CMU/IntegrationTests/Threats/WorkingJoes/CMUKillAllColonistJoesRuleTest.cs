using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats.Rules;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.SSDIndicator;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Threats.WorkingJoes;

/// <summary>
/// The Working Joes shipment's colonist rule unlocks colony evacuation at its own evacuation threshold and ends the
/// round at its own percentage, both read from the prototype. Synthetics, Working Joes among them, are not counted.
/// </summary>
[TestFixture]
public sealed class CMUKillAllColonistJoesRuleTest
{
    private static readonly EntProtoId Rule = "CMUKillAllColonistJoesRule";
    private const string Human = "CMMobHuman";
    private const string ColonyJoe = "AU14MobWorkingJoeColony";
    private const int Colonists = 20;
    private static readonly ProtoId<NpcFactionPrototype> Colonist = "AUColonist";

    [Test, CancelAfter(180000)]
    public async Task EvacuationAndVictoryFollowTheRulesOwnThresholds()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var ticker = entities.System<GameTicker>();
            var mobs = entities.System<MobStateSystem>();
            var factions = entities.System<NpcFactionSystem>();
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));

            Assert.That(prototypes.Index(Rule)
                .TryGetComponent<KillAllColonistRuleComponent>(out var config, entities.ComponentFactory));
            Assert.That(config!.ColonyEvacThreshold, Is.InRange(1, 100), "the rule unlocks evacuation");
            Assert.That(config.Percent, Is.InRange(config.ColonyEvacThreshold, 100), "evacuation comes before the end");

            var colonists = new List<EntityUid>();
            for (var i = 0; i < Colonists; i++)
            {
                var colonist = entities.SpawnEntity(Human, map.GridCoords);
                factions.AddFaction(colonist, Colonist);
                // a mindless test body is SSD, and SSD colonists sit out of the count
                entities.RemoveComponent<SSDIndicatorComponent>(colonist);
                colonists.Add(colonist);
            }

            // a living colony Joe in the colonists' faction must not shift the thresholds
            var joe = entities.SpawnEntity(ColonyJoe, map.GridCoords);
            factions.AddFaction(joe, Colonist);

            Assert.That(ticker.StartGameRule(Rule, out var ruleEntity), Is.True);
            var rule = entities.GetComponent<KillAllColonistRuleComponent>(ruleEntity);

            var evacAt = FirstMeeting(Colonists, config.ColonyEvacThreshold);
            var endAt = FirstMeeting(Colonists, config.Percent);

            for (var killed = 1; killed <= endAt; killed++)
            {
                mobs.ChangeMobState(colonists[killed - 1], MobState.Dead);

                Assert.That(rule.ColonyEvacTriggered, Is.EqualTo(killed >= evacAt),
                    $"evacuation after {killed} of {Colonists} colonists dead");

                var expected = killed >= endAt ? GameRunLevel.PostRound : GameRunLevel.InRound;
                Assert.That(ticker.RunLevel, Is.EqualTo(expected), $"round state after {killed} of {Colonists} dead");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>The fewest dead out of <paramref name="total"/> that meet <paramref name="percent"/>.</summary>
    private static int FirstMeeting(int total, int percent)
    {
        return (total * percent + 99) / 100;
    }
}
