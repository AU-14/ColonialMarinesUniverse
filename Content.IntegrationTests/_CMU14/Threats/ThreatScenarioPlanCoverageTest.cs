using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking.Presets;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.Scenario;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Threats;

[TestFixture]
public sealed class ThreatScenarioPlanCoverageTest : GameTest
{
    private static readonly string[] Presets =
    [
        "DistressSignal",
        "ColonyFall",
    ];

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
    };

    [Test]
    public async Task EnabledPlanetsHaveResolvableThreatVotesAndSpawnMarkers()
    {
        var componentFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var scenarioPlan = SEntMan.System<ScenarioPlanSystem>();
            var failures = new List<string>();

            foreach (var presetId in Presets)
            {
                var preset = SProtoMan.Index<GamePresetPrototype>(presetId);
                var votingChoices = SProtoMan.EnumeratePrototypes<VotingChoicesPrototype>()
                    .Single(choices => choices.Presets.Contains(presetId, StringComparer.OrdinalIgnoreCase));

                foreach (var planetId in preset.SupportedPlanets ?? [])
                {
                    if (!SProtoMan.TryIndex<EntityPrototype>(planetId, out var planetPrototype) ||
                        !planetPrototype.TryComp(out RMCPlanetMapPrototypeComponent planet, componentFactory))
                    {
                        failures.Add($"{presetId}/{planetId}: planet prototype could not be resolved.");
                        continue;
                    }

                    var report = scenarioPlan.ValidateVotingChoicesPrototypeCoverage(
                        votingChoices.ID,
                        presetId,
                        planetId,
                        planet.MapId,
                        50);
                    if (!report.IsValid ||
                        report.Plans.Count != 1 ||
                        report.Plans[0].DeferredForceChoices.Count == 0)
                    {
                        failures.Add($"{presetId}/{planetId} ({planet.MapId}): {report}");
                    }
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        });
    }
}
