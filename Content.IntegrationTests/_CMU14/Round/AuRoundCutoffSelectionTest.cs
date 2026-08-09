#nullable enable

using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking.Presets;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class AuRoundCutoffSelectionTest
{
    private const int PlayerCount = 40;
    private const string FixedFactionPresetId = "CMUTestFixedFactionPreset";
    private static readonly ProtoId<GamePresetPrototype> JailbreakPreset = "Jailbreak";
    private static readonly ProtoId<GamePresetPrototype> PrometheusPreset = "Prometheus";

    [TestPrototypes]
    private static readonly string FixedFactionPreset = $"""
        - type: GamePlanetPool
          id: CMUTestFixedFactionPlanetPool
          planets:
          - AUPlanetShepherdsPride

        - type: gamePreset
          id: {FixedFactionPresetId}
          name: CMU fixed faction test
          description: Tests pool-only fixed-faction round planning.
          showInVote: false
          usesGovforPlatoon: true
          threatSelectionMode: PostRoundstartVote
          usesThreatSpawnDelay: true
          planetPool: CMUTestFixedFactionPlanetPool
          rules: []
        """;

    [TestCase(
        "DistressSignal",
        "AUPlanetTrijent",
        "AUTrijentMap",
        "RMC",
        "USSBushRedux",
        null,
        null,
        CmuThreatSelectionMode.PostRoundstartVote,
        false)]
    [TestCase(
        "ColonyFall",
        "AUPlanetShepherdsPride",
        "Sheperds",
        null,
        null,
        null,
        null,
        CmuThreatSelectionMode.PostRoundstartVote,
        true)]
    [TestCase(
        "Insurgency",
        "AUPlanetShepherdsPride",
        "Sheperds",
        "USCM",
        "USSBushRedux",
        null,
        null,
        CmuThreatSelectionMode.Disabled,
        false)]
    [TestCase(
        "CMDistressSignal",
        null,
        null,
        null,
        null,
        null,
        null,
        CmuThreatSelectionMode.Disabled,
        false)]
    public async Task CutoffUsesPrototypeBackedDefaults(
        string presetId,
        string? expectedPlanetId,
        string? expectedMapId,
        string? expectedGovforPlatoonId,
        string? expectedGovforShipId,
        string? expectedOpforPlatoonId,
        string? expectedOpforShipId,
        CmuThreatSelectionMode expectedThreatSelectionMode,
        bool expectedThreatSpawnDelay)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            round.ResetLobbySelection();
            round.FinalizeVoteSequence(PlayerCount, presetId);

            var selectedPresetId = round.SelectedPreset?.ID ?? presetId;
            var selection = round.CaptureRoundPlanSelection(
                PlayerCount,
                selectedPresetId,
                round.SelectedThreat?.ID);

            Assert.Multiple(() =>
            {
                Assert.That(selection.PresetId, Is.EqualTo(presetId));
                Assert.That(selection.PlanetId, Is.EqualTo(expectedPlanetId));
                Assert.That(selection.MapId, Is.EqualTo(expectedMapId));
                Assert.That(selection.GovforPlatoonId, Is.EqualTo(expectedGovforPlatoonId));
                Assert.That(selection.GovforShipId, Is.EqualTo(expectedGovforShipId));
                Assert.That(selection.OpforPlatoonId, Is.EqualTo(expectedOpforPlatoonId));
                Assert.That(selection.OpforShipId, Is.EqualTo(expectedOpforShipId));
                Assert.That(selection.SelectedThreatId, Is.Null);
                Assert.That(round.SelectedPreset?.ThreatSelectionMode, Is.EqualTo(expectedThreatSelectionMode));
                Assert.That(
                    round.UsesPostRoundstartThreatVote(),
                    Is.EqualTo(expectedThreatSelectionMode == CmuThreatSelectionMode.PostRoundstartVote));
                Assert.That(round.SelectedPreset?.UsesThreatSpawnDelay, Is.EqualTo(expectedThreatSpawnDelay));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PoolOnlyFixedFactionFlowsIntoScenarioPlan()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            round.ResetLobbySelection();
            round.FinalizeVoteSequence(PlayerCount, FixedFactionPresetId);

            var selection = round.CaptureRoundPlanSelection(
                PlayerCount,
                FixedFactionPresetId,
                round.SelectedThreat?.ID);
            var plan = server.System<ScenarioPlanSystem>()
                .GeneratePlans(selection.ToScenarioPlanRequest())
                .Single();
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            Assert.Multiple(() =>
            {
                Assert.That(selection.PlanetId, Is.EqualTo("AUPlanetShepherdsPride"));
                Assert.That(selection.MapId, Is.EqualTo("Sheperds"));
                Assert.That(selection.GovforPlatoonId, Is.EqualTo("USCM"));
                Assert.That(selection.GovforShipId, Is.EqualTo("USSBushRedux"));
                Assert.That(selection.OpforPlatoonId, Is.Null);
                Assert.That(selection.OpforShipId, Is.Null);
                Assert.That(round.UsesPostRoundstartThreatVote(), Is.True);
                Assert.That(round.SelectedPreset?.UsesThreatSpawnDelay, Is.True);
                Assert.That(plan.PlanetId, Is.EqualTo(selection.PlanetId));
                Assert.That(
                    plan.Forces.Any(force =>
                        force.ForceId == "GovforPlatoon:USCM" &&
                        force.SourcePrototypeId == "USCM"),
                    Is.True);
                Assert.That(
                    plan.DeferredForceChoices.Any(choice => choice.ChoiceId == "GovforPlatoon"),
                    Is.False);
                Assert.That(
                    plan.DeferredForceChoices.Any(choice =>
                        choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal)),
                    Is.True);
                Assert.That(
                    plan.DeferredForceChoices
                        .Where(choice => choice.ChoiceId.StartsWith("DeferredThreat:", StringComparison.Ordinal))
                        .SelectMany(choice => choice.Candidates)
                        .All(force => force.Timing.HasDelay),
                    Is.True);
                Assert.That(
                    prototypes.Index(PrometheusPreset).ThreatSelectionMode,
                    Is.EqualTo(CmuThreatSelectionMode.PreRoundstart));
                Assert.That(
                    prototypes.Index(JailbreakPreset).ThreatSelectionMode,
                    Is.EqualTo(CmuThreatSelectionMode.PreRoundstart));
            });
        });

        await pair.CleanReturnAsync();
    }
}
