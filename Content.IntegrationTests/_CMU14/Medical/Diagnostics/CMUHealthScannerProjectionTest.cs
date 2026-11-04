using System.IO;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMUHealthScannerProjectionTest
{
    [TestPrototypes]
    private static readonly string ChemicalPrototypes = string.Concat(Enumerable.Range(0, 65).Select(i => $"""
        - type: reagent
          parent: Water
          id: CMUScannerVisibleTest{i}
          unknown: false
          overdose: 3

        """)) + """
        - type: reagent
          parent: Water
          id: CMUScannerHiddenTestA
          unknown: true
        - type: reagent
          parent: Water
          id: CMUScannerHiddenTestB
          unknown: true
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task SerializedScannerStateHidesUnknownIdentityQuantityAndInstanceData(bool mixed)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var scanner = em.SpawnEntity("CMHealthAnalyzer", MapCoordinates.Nullspace);
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var bloodstream = em.System<SharedRMCBloodstreamSystem>();
                var solutions = em.System<SharedSolutionContainerSystem>();
                var scans = em.System<HealthScannerSystem>();
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                Assert.That(bloodstream.TryGetChemicalSolution(patient, out var solution, out _), Is.True);
                solutions.SetCapacity(solution, 2000);
                List<ReagentData> firstData = [new DnaData { DNA = "private-first-donor" }];
                List<ReagentData> secondData = [new DnaData { DNA = "private-second-donor" }];
                if (mixed)
                {
                    Assert.That(solutions.TryAddReagent(solution, "CMUScannerVisibleTest0", 2, data: firstData), Is.True);
                    Assert.That(solutions.TryAddReagent(solution, "CMUScannerVisibleTest0", 2, data: secondData), Is.True);
                }
                Assert.That(solutions.TryAddReagent(solution, "CMUScannerHiddenTestA", 7, data: firstData), Is.True);
                var first = scans.BuildStateForViewer(scanner, patient, patient);
                var firstBytes = SerializeState(serializer, first);

                Assert.That(solutions.RemoveReagent(solution, "CMUScannerHiddenTestA", 7, firstData), Is.EqualTo((FixedPoint2) 7));
                Assert.That(solutions.TryAddReagent(solution, "CMUScannerHiddenTestB", 19, data: secondData), Is.True);
                var second = scans.BuildStateForViewer(scanner, patient, patient);
                var secondBytes = SerializeState(serializer, second);
                Assert.That(secondBytes, Is.EqualTo(firstBytes),
                    "Changing only hidden identity, amount and DNA must not change the serialized viewer payload.");
                using var stream = new MemoryStream(secondBytes);
                var delivered = serializer.Deserialize<HealthScannerStateMessage>(stream).State;
                Assert.Multiple(() =>
                {
                    Assert.That(delivered.UnknownChemicals, Is.True);
                    Assert.That(delivered.OmittedKnownChemicals, Is.Zero);
                    Assert.That(delivered.KnownChemicals.Count, Is.EqualTo(mixed ? 1 : 0));
                });
                if (mixed)
                {
                    Assert.That(delivered.KnownChemicals[0], Is.EqualTo(
                        new HealthScannerChemicalReadout("CMUScannerVisibleTest0", 4, true)),
                        "Known DNA variants must aggregate before overdose evaluation and wire serialization.");
                }
            }
            finally
            {
                em.DeleteEntity(patient);
                em.DeleteEntity(scanner);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SerializedScannerChemicalRowsAreBoundedAndKeepOverdoseWarningsFirst()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.EntMan;
            var scanner = em.SpawnEntity("CMHealthAnalyzer", MapCoordinates.Nullspace);
            var patient = em.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var solutions = em.System<SharedSolutionContainerSystem>();
                Assert.That(em.System<SharedRMCBloodstreamSystem>().TryGetChemicalSolution(patient, out var solution, out _), Is.True);
                solutions.SetCapacity(solution, 2000);
                for (var i = 0; i < 65; i++)
                    Assert.That(solutions.TryAddReagent(solution, $"CMUScannerVisibleTest{i}", i == 64 ? 4 : 1), Is.True);
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                var state = em.System<HealthScannerSystem>().BuildStateForViewer(scanner, patient, patient);
                using var stream = new MemoryStream(SerializeState(serializer, state));
                var delivered = serializer.Deserialize<HealthScannerStateMessage>(stream).State;
                Assert.Multiple(() =>
                {
                    Assert.That(delivered.KnownChemicals.Count, Is.EqualTo(HealthScannerBuiState.MaximumChemicalReadouts));
                    Assert.That(delivered.OmittedKnownChemicals, Is.EqualTo(1));
                    Assert.That(delivered.UnknownChemicals, Is.False);
                    Assert.That(delivered.KnownChemicals[0], Is.EqualTo(
                        new HealthScannerChemicalReadout("CMUScannerVisibleTest64", 4, true)));
                });
            }
            finally
            {
                em.DeleteEntity(patient);
                em.DeleteEntity(scanner);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static byte[] SerializeState(IRobustSerializer serializer, HealthScannerBuiState state)
    {
        using var stream = new MemoryStream();
        serializer.Serialize(stream, new HealthScannerStateMessage(state));
        return stream.ToArray();
    }

    [Test]
    public async Task ScannerBuildsSkillGatedStatePerViewer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var scannerSystem = entMan.System<HealthScannerSystem>();
            var skills = entMan.System<SkillsSystem>();
            var scanner = entMan.SpawnEntity("CMHealthAnalyzer", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var unskilled = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var corpsman = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(unskilled, "RMCSkillMedical", 0);
                skills.SetSkill(corpsman, "RMCSkillMedical", 2);

                var unskilledState = scannerSystem.BuildStateForViewer(scanner, patient, unskilled);
                var corpsmanState = scannerSystem.BuildStateForViewer(scanner, patient, corpsman);

                Assert.Multiple(() =>
                {
                    Assert.That(unskilledState, Is.Not.SameAs(corpsmanState));
                    Assert.That(unskilledState.CMUParts, Is.Not.Null.And.Not.Empty);
                    Assert.That(unskilledState.CMUOrgans, Is.Null);
                    Assert.That(unskilledState.CMUFractures, Is.Null);
                    Assert.That(corpsmanState.CMUParts, Is.Not.Null.And.Not.Empty);
                    Assert.That(corpsmanState.CMUOrgans, Is.Not.Null.And.Not.Empty);
                    Assert.That(corpsmanState.CMUFractures, Is.Not.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(corpsman);
                entMan.DeleteEntity(unskilled);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(scanner);
            }
        });

        await pair.CleanReturnAsync();
    }
}
