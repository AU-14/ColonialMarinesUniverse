using System.Collections.Generic;
using System.Linq;
using Content.Server.AU14.Round;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared._RMC14.Rules;
using Content.Shared.AU14.util;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Round;

[TestFixture]
public sealed class AuRoundMigrationValidationTest
{
    private const string PrometheusPreset = "Prometheus";

    private static IEnumerable<TestCaseData> MigratedPresetCases()
    {
        yield return new TestCaseData(
                "ForceOnForce",
                new[] { "AuVoteRule", "PlatoonSpawn", "RemoveAllJobs", "AddGovfor", "AddOpfor" })
            .SetName("ForceOnForce migrated round setup resolves");

        yield return new TestCaseData(
                "Insurgency",
                new[] { "AuVoteRule", "PlatoonSpawn", "AddGovfor", "AddClf", "ColonyAntags", "InsWinConditionsRule" })
            .SetName("Insurgency migrated round setup resolves");

        yield return new TestCaseData(
                "DistressSignal",
                new[] { "AuVoteRule", "PlatoonSpawn", "RemoveAllJobs", "AddGovfor" })
            .SetName("DistressSignal migrated round setup resolves");

        yield return new TestCaseData(
                "ColonyFall",
                new[] { "AuVoteRule", "ColonyAntags" })
            .SetName("ColonyFall migrated round setup resolves");
    }

    private static IEnumerable<TestCaseData> MigratedRuleCases()
    {
        yield return new TestCaseData("AuVoteRule", new[] { "AuVoteRule", "Bioscan" });
        yield return new TestCaseData("PlatoonSpawn", new[] { "PlatoonSpawnRule" });
        yield return new TestCaseData("RemoveAllJobs", new[] { "RemoveJobsRule" });
        yield return new TestCaseData("AddGovfor", new[] { "AddJobsRule" });
        yield return new TestCaseData("AddOpfor", new[] { "AddJobsRule" });
        yield return new TestCaseData("AddClf", new[] { "AddJobsRule" });
        yield return new TestCaseData("ColonyAntags", new[] { "GameRule", "ColonyAntagsRule" });
    }

    [TestCaseSource(nameof(MigratedPresetCases))]
    public async Task MigratedRoundPresetsResolveRulesPlanetsAndMaps(string presetId, string[] expectedRules)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var componentFactory = server.ResolveDependency<IComponentFactory>();
            var errors = new List<string>();
            var preset = prototypes.Index<GamePresetPrototype>(presetId);

            foreach (var expectedRule in expectedRules)
            {
                if (!preset.Rules.Contains(expectedRule))
                    errors.Add($"{presetId} does not include expected rule {expectedRule}");
            }

            foreach (var ruleId in preset.Rules)
            {
                if (!prototypes.HasIndex<EntityPrototype>(ruleId))
                    errors.Add($"{presetId} references missing rule entity {ruleId}");
            }

            var planetIds = ResolvePresetPlanetIds(prototypes, preset).ToList();
            if (planetIds.Count == 0)
                errors.Add($"{presetId} has no supported planets or planet pool entries");

            foreach (var planetId in planetIds)
            {
                if (!prototypes.TryIndex<EntityPrototype>(planetId, out var planetProto))
                {
                    errors.Add($"{presetId} references missing planet entity {planetId}");
                    continue;
                }

                if (!planetProto.TryGetComponent<RMCPlanetMapPrototypeComponent>(out var planet, componentFactory))
                {
                    errors.Add($"{planetId} has no RMCPlanetMapPrototypeComponent");
                    continue;
                }

                if (!prototypes.HasIndex<GameMapPrototype>(planet.MapId))
                    errors.Add($"{planetId} references missing GameMapPrototype {planet.MapId}");
            }

            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        });

        await pair.CleanReturnAsync();
    }

    [TestCaseSource(nameof(MigratedRuleCases))]
    public async Task MigratedRoundRulesKeepExpectedComponents(string ruleId, string[] componentNames)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var rule = prototypes.Index<EntityPrototype>(ruleId);
            var missing = componentNames
                .Where(componentName => !rule.Components.ContainsKey(componentName))
                .ToList();

            Assert.That(missing, Is.Empty, $"{ruleId} is missing migrated components: {string.Join(", ", missing)}");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PrometheusAntagsAreHiddenFromPreferences()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            foreach (var antagId in new[] { "ExpedTrailerRole", "MonsterRole" })
            {
                var antag = prototypes.Index<AntagPrototype>(antagId);
                Assert.That(antag.SetPreference, Is.False, $"{antagId} should not appear in antag preferences.");
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task GameTickerPresetSelectionMirrorsAuRoundPreset()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
        });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var ticker = server.System<GameTicker>();
            var round = server.System<AuRoundSystem>();
            var preset = prototypes.Index<GamePresetPrototype>(PrometheusPreset);

            ticker.SetGamePreset(preset);

            Assert.That(ticker.Preset?.ID, Is.EqualTo(PrometheusPreset));
            Assert.That(round.SelectedPreset?.ID, Is.EqualTo(PrometheusPreset));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ForcePlanetMapCommandMirrorsAuRoundPlanet()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        string planetId = string.Empty;
        string mapId = string.Empty;

        await server.WaitPost(() =>
        {
            var planet = server.System<RMCPlanetSystem>().GetAllPlanets().First();
            planetId = planet.Proto.ID;
            mapId = planet.Comp.MapId;
        });

        await pair.WaitCommand($"forceplanetmap {planetId}");

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            Assert.That(round.GetSelectedPlanetId(), Is.EqualTo(planetId));
            Assert.That(round.GetSelectedPlanet()?.MapId, Is.EqualTo(mapId));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FactionCommandsAcceptNestedShipAlias()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        string shipId = string.Empty;

        await server.WaitPost(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            shipId = prototypes.EnumeratePrototypes<PlatoonPrototype>()
                .SelectMany(platoon => platoon.PossibleShips)
                .First();
        });

        await pair.WaitCommand($"setgovfor ship {shipId}");
        await pair.WaitCommand($"setopfor ship {shipId}");

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            Assert.That(round.GetSelectedGovforShip(), Is.EqualTo(shipId));
            Assert.That(round.GetSelectedOpforShip(), Is.EqualTo(shipId));
        });

        await pair.CleanReturnAsync();
    }

    private static IEnumerable<string> ResolvePresetPlanetIds(IPrototypeManager prototypes, GamePresetPrototype preset)
    {
        if (!string.IsNullOrWhiteSpace(preset.PlanetPool))
            return prototypes.Index<GamePlanetPoolPrototype>(preset.PlanetPool).Planets;

        return preset.SupportedPlanets ?? Enumerable.Empty<string>();
    }
}
