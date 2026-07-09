using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMUHealthScannerProjectionTest
{
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
