#nullable enable

using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking.Presets;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class AuRoundCutoffSelectionTest
{
    private const int PlayerCount = 40;
    private const string FixedFactionPresetId = "CMUTestFixedFactionPreset";
    private const string FixedBothSidesPresetId = "CMUTestFixedBothSidesPreset";
    private static readonly ProtoId<PlatoonPrototype> HazopsPlatoon = "HAZOPS";
    private static readonly ProtoId<GamePresetPrototype> JailbreakPreset = "Jailbreak";
    private static readonly ProtoId<GamePresetPrototype> PrometheusPreset = "Prometheus";
    private static readonly ProtoId<PlatoonPrototype> RmcPlatoon = "RMC";
    private static readonly ProtoId<PlatoonPrototype> UppPlatoon = "UPP";
    private static readonly ProtoId<PlatoonPrototype> UscmPlatoon = "USCM";

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

        - type: gamePreset
          id: {FixedBothSidesPresetId}
          name: CMU fixed both-sides test
          description: Tests committed typed assignments for both military sides.
          showInVote: false
          usesGovforPlatoon: true
          usesOpforPlatoon: true
          planetPool: CMUTestFixedFactionPlanetPool
          rules: []
        """;

    [Test]
    public async Task FixedBothSidesCommitTypedDefaultAssignments()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            round.ResetLobbySelection();

            var selection = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);

            Assert.Multiple(() =>
            {
                Assert.That(
                    selection.GovforAssignment,
                    Is.EqualTo(
                        new RoundForceAssignment(
                            RoundSide.Govfor,
                            new RoundForceId("USCM"),
                            "USSBushRedux")));
                Assert.That(
                    selection.OpforAssignment,
                    Is.EqualTo(
                        new RoundForceAssignment(
                            RoundSide.Opfor,
                            new RoundForceId("UPP"),
                            "USSBushRedux")));
                Assert.That(director.Selection, Is.EqualTo(selection));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreFreezeOverridesCommitAndPostFreezeOverridesAreRejected()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            var platoons = server.System<PlatoonSpawnRuleSystem>();
            round.ResetLobbySelection();
            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Govfor,
                        prototypes.Index(RmcPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Opfor,
                        prototypes.Index(HazopsPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Govfor, "USSBushRedux"),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Opfor, "USSBushRedux"),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            });

            var committed = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Govfor,
                        prototypes.Index(UscmPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetLegacyForce(
                        RoundSide.Opfor,
                        prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Govfor, "LaterGovforShip"),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    director.TrySetMainShip(RoundSide.Opfor, "LaterOpforShip"),
                    Is.EqualTo(CMURoundSelectionMutationResult.SelectionFrozen));
                Assert.That(
                    committed.GovforAssignment,
                    Is.EqualTo(new RoundForceAssignment(
                        RoundSide.Govfor,
                        new RoundForceId("RMC"),
                        "USSBushRedux")));
                Assert.That(
                    committed.OpforAssignment,
                    Is.EqualTo(new RoundForceAssignment(
                        RoundSide.Opfor,
                        new RoundForceId("HAZOPS"),
                        "USSBushRedux")));
                Assert.That(director.Selection, Is.EqualTo(committed));
                Assert.That(platoons.SelectedGovforPlatoon?.ID, Is.EqualTo("RMC"));
                Assert.That(platoons.SelectedOpforPlatoon?.ID, Is.EqualTo("HAZOPS"));
                Assert.That(round.GetSelectedGovforShip(), Is.EqualTo("USSBushRedux"));
                Assert.That(round.GetSelectedOpforShip(), Is.EqualTo("USSBushRedux"));
            });
        });

        await pair.CleanReturnAsync();
    }

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
