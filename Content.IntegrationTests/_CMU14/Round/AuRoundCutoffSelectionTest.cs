#nullable enable

using System;
using System.Collections.Generic;
using Content.Server.AU14.Round;
using Content.Server.AU14.Scenario;
using Content.Server.GameTicking.Presets;
using Content.Shared.AU14.util;
using Content.Shared.CMU.Round;
using Content.Shared.GameTicking;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests._CMU14.Round;

[TestFixture]
public sealed class AuRoundCutoffSelectionTest
{
    private const int PlayerCount = 40;
    private const string FixedFactionPresetId = "CMUTestFixedFactionPreset";
    private const string FixedBothSidesPresetId = "CMUTestFixedBothSidesPreset";
    private const string MissingAsrsPresetId = "CMUTestMissingAsrsPreset";
    private const string ShepherdsPridePlanetId = "AUPlanetShepherdsPride";
    private static readonly ProtoId<PlatoonPrototype> HazopsPlatoon = "HAZOPS";
    private static readonly ProtoId<GamePresetPrototype> JailbreakPreset = "Jailbreak";
    private static readonly ProtoId<GamePresetPrototype> PrometheusPreset = "Prometheus";
    private static readonly ProtoId<PlatoonPrototype> RmcPlatoon = "RMC";
    private static readonly ProtoId<PlatoonPrototype> UppPlatoon = "UPP";
    private static readonly ProtoId<PlatoonPrototype> UscmPlatoon = "USCM";
    private static readonly ProtoId<PlatoonPrototype> WeyuPlatoon = "WEYU";

    private const string DuplicateAsrsProfile = """
        - type: entity
          id: CMUTestDuplicateUSCMAsrsProfile
          categories: [ HideSpawnMenu ]
          components:
          - type: RoundForceAsrsProfile
            forceId: USCM
        """;

    [TestPrototypes]
    private static readonly string FixedFactionPreset = $"""
        - type: GamePlanetPool
          id: CMUTestFixedFactionPlanetPool
          planets:
          - AUPlanetShepherdsPride

        - type: platoon
          id: CMUTestMissingAsrsPlatoon
          name: CMU missing ASRS profile test
          possibleships:
          - USSBushRedux

        - type: entity
          id: CMUTestMissingAsrsPlanet
          components:
          - type: RMCPlanetMapPrototype
            map: /Maps/_CMU14/sheperds.yml
            mapId: Sheperds
            platoonsGovfor:
            - CMUTestMissingAsrsPlatoon
            defaultgovfor: CMUTestMissingAsrsPlatoon

        - type: GamePlanetPool
          id: CMUTestMissingAsrsPlanetPool
          planets:
          - CMUTestMissingAsrsPlanet

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

        - type: gamePreset
          id: {MissingAsrsPresetId}
          name: CMU missing ASRS profile test
          description: Tests catalog validation before the round plan is committed.
          showInVote: false
          usesGovforPlatoon: true
          planetPool: CMUTestMissingAsrsPlanetPool
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
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

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
    public async Task FreezeCommitsStablePerSideAsrsCatalogsUntilReset()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var round = server.System<AuRoundSystem>();
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforBeforeFreeze),
                    Is.False);
                Assert.That(govforBeforeFreeze, Is.Null);
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforBeforeFreeze),
                    Is.False);
                Assert.That(opforBeforeFreeze, Is.Null);
            });

            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));
            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Govfor, prototypes.Index(WeyuPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyForce(RoundSide.Opfor, prototypes.Index(UppPlatoon)),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
            });

            var selection = director.FreezeSelection(PlayerCount, FixedBothSidesPresetId);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforCatalog),
                Is.True);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforCatalog),
                Is.True);
            Assert.That(govforCatalog, Is.Not.Null);
            Assert.That(opforCatalog, Is.Not.Null);

            var pouch = new RoundAsrsOfferId("LargeMagazinePouches");
            Assert.That(govforCatalog!.TryGetOffer(pouch, out var govforPouch), Is.True);
            Assert.That(opforCatalog!.TryGetOffer(pouch, out var opforPouch), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(selection.GovforAssignment?.Force, Is.EqualTo(govforCatalog.Force));
                Assert.That(selection.OpforAssignment?.Force, Is.EqualTo(opforCatalog.Force));
                Assert.That(govforPouch!.Crate.Id, Is.EqualTo("RMCCrateClothingMagazinePouchesLargePMC"));
                Assert.That(opforPouch!.Crate.Id, Is.EqualTo("RMCCrateClothingMagazinePouchesLarge"));
            });

            Assert.That(
                director.FreezeSelection(PlayerCount, FixedBothSidesPresetId),
                Is.EqualTo(selection));
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var repeatedGovforCatalog),
                Is.True);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var repeatedOpforCatalog),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(repeatedGovforCatalog, Is.SameAs(govforCatalog));
                Assert.That(repeatedOpforCatalog, Is.SameAs(opforCatalog));
            });

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforAfterReset),
                    Is.False);
                Assert.That(govforAfterReset, Is.Null);
                Assert.That(
                    director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforAfterReset),
                    Is.False);
                Assert.That(opforAfterReset, Is.Null);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InvalidAsrsProfilesDoNotLatchSelectionOrCatalogs()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Destructive = true });
        var server = pair.Server;

        var changed = new Dictionary<Type, HashSet<string>>();
        server.ProtoMan.LoadString(DuplicateAsrsProfile, changed: changed);
        await server.WaitPost(() => server.ProtoMan.ReloadPrototypes(changed));

        await server.WaitAssertion(() =>
        {
            var director = server.System<CMURoundDirectorSystem>();
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());

            var missing = Assert.Throws<InvalidOperationException>(() =>
                director.FreezeSelection(PlayerCount, MissingAsrsPresetId));
            Assert.That(missing!.Message, Does.Contain("has no ASRS profile"));
            AssertDirectorHasNoCommittedPlan(director);

            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            var duplicate = Assert.Throws<InvalidOperationException>(() =>
                director.FreezeSelection(PlayerCount, FixedBothSidesPresetId));
            Assert.That(duplicate!.Message, Does.Contain("has multiple ASRS profiles"));
            AssertDirectorHasNoCommittedPlan(director);
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertDirectorHasNoCommittedPlan(CMURoundDirectorSystem director)
    {
        Assert.Multiple(() =>
        {
            Assert.That(director.Selection, Is.Null);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Govfor, out var govforCatalog),
                Is.False);
            Assert.That(govforCatalog, Is.Null);
            Assert.That(
                director.TryGetCommittedAsrsCatalog(RoundSide.Opfor, out var opforCatalog),
                Is.False);
            Assert.That(opforCatalog, Is.Null);
        });
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
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
            round.SetPreset(prototypes.Index<GamePresetPrototype>(FixedBothSidesPresetId));

            Assert.Multiple(() =>
            {
                Assert.That(
                    director.TrySetLegacyPlanet(ShepherdsPridePlanetId),
                    Is.EqualTo(CMURoundSelectionMutationResult.Applied));
                Assert.That(
                    director.TrySetLegacyPlanet("CMUMissingPlanet"),
                    Is.EqualTo(CMURoundSelectionMutationResult.InvalidSelection));
                Assert.That(round.GetSelectedPlanetId(), Is.EqualTo(ShepherdsPridePlanetId));
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
                    director.TrySetLegacyPlanet("AUPlanetLV747"),
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
                Assert.That(committed.PlanetId, Is.EqualTo(ShepherdsPridePlanetId));
                Assert.That(round.GetSelectedPlanetId(), Is.EqualTo(ShepherdsPridePlanetId));
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
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
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
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RoundRestartCleanupEvent());
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
