using System.Linq;
using Content.Server._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.Surgery;

[TestFixture]
public sealed class CMUBodyScannerProjectionTest
{
    [Test]
    public async Task ScannerProjectionCarriesTypedSeverityAndRanges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;

        await server.WaitPost(() =>
        {
            patient = server.EntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        });

        // Publish spawn-time anatomy before testing revision-based cache invalidation.
        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var damageable = entMan.System<DamageableSystem>();
            var medicalIndex = entMan.System<CMUMedicalBodyIndexSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var readout = entMan.System<CMUBodyScannerReadoutSystem>();

            try
            {
                var initial = readout.BuildScanLines(patient);
                var healthyDamage = initial.Single(line => line.Kind == CMUBodyScannerScanKind.Damage);
                var healthyPart = initial.First(line => line.Kind == CMUBodyScannerScanKind.BodyPart);
                var repeatedPart = readout.BuildScanLines(patient)
                    .First(line => line.Kind == CMUBodyScannerScanKind.BodyPart);

                Assert.Multiple(() =>
                {
                    Assert.That(healthyDamage.Severity, Is.EqualTo(CMUBodyScannerScanSeverity.Stable));
                    Assert.That(healthyDamage.Title, Is.Not.Empty);
                    Assert.That(healthyPart.HasRange, Is.True);
                    Assert.That(healthyPart.Maximum, Is.GreaterThan(0f));
                    Assert.That(healthyPart.Current, Is.EqualTo(healthyPart.Maximum));
                    Assert.That(repeatedPart, Is.SameAs(healthyPart));
                });

                Assert.That(medicalIndex.TryGetBodyPart(
                    patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                    out var arm), Is.True);
                var partDamage = new DamageSpecifier();
                partDamage.DamageDict["Heat"] = FixedPoint2.New(10);
                Assert.That(partHealth.TryApplyPartDamage(patient, arm, partDamage), Is.True);

                var changedPart = readout.BuildScanLines(patient)
                    .First(line => line.Kind == CMUBodyScannerScanKind.BodyPart);
                Assert.That(changedPart, Is.Not.SameAs(healthyPart));

                var damage = new DamageSpecifier();
                damage.DamageDict["Blunt"] = FixedPoint2.New(20);
                damageable.TryChangeDamage(patient, damage, ignoreResistances: true);

                var injured = readout.BuildScanLines(patient);
                var injuredDamage = injured.Single(line => line.Kind == CMUBodyScannerScanKind.Damage);

                Assert.That(injuredDamage.Severity, Is.EqualTo(CMUBodyScannerScanSeverity.Warning));
            }
            finally
            {
                entMan.DeleteEntity(patient);
            }
        });

        await pair.CleanReturnAsync();
    }
}
