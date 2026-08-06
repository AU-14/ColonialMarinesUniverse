using System.Linq;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMUHealthScannerProjectionTest
{
    [Test]
    public async Task ScannerBuildsSkillGatedStatePerViewer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid scanner = default;
        EntityUid patient = default;
        EntityUid unskilled = default;
        EntityUid corpsman = default;

        await server.WaitPost(() =>
        {
            var entityManager = server.EntMan;
            scanner = entityManager.SpawnEntity("CMHealthAnalyzer", map.GridCoords);
            patient = entityManager.SpawnEntity("CMMobHuman", map.GridCoords);
            unskilled = entityManager.SpawnEntity("CMMobHuman", map.GridCoords);
            corpsman = entityManager.SpawnEntity("CMMobHuman", map.GridCoords);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var entityManager = server.EntMan;
            var scannerSystem = entityManager.System<HealthScannerSystem>();
            var medicalIndex = entityManager.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entityManager.System<SharedBodyPartHealthSystem>();
            var skills = entityManager.System<SkillsSystem>();

            try
            {
                skills.SetSkill(unskilled, "RMCSkillMedical", 0);
                skills.SetSkill(corpsman, "RMCSkillMedical", 2);

                var torso = medicalIndex.GetBodyParts(patient)
                    .Single(part => part.Comp.PartType == BodyPartType.Torso)
                    .Owner;
                var damage = new DamageSpecifier();
                damage.DamageDict["Slash"] = FixedPoint2.New(10);
                Assert.That(partHealth.TryApplyPartDamage(patient, torso, damage), Is.True);

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
                    Assert.That(
                        corpsmanState.CMUParts!.Values.Any(part =>
                            part.Type == BodyPartType.Torso && part.Current < part.Max),
                        Is.True);
                });
            }
            finally
            {
                entityManager.DeleteEntity(corpsman);
                entityManager.DeleteEntity(unskilled);
                entityManager.DeleteEntity(patient);
                entityManager.DeleteEntity(scanner);
            }
        });

        await pair.CleanReturnAsync();
    }
}
