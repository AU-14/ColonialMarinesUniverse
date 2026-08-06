using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Medical.Examine;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.HealthExaminable;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMULegacyMedicalExamineTest
{
    [Test]
    public async Task CmuPatientDoesNotShowLegacyDamageOrBleedingExamineText()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid examiner = default;

        await server.WaitAssertion(() =>
        {
            patient = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            examiner = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.HasComponent<CMUHumanMedicalComponent>(patient), Is.True);
                Assert.That(server.EntMan.HasComponent<HealthExaminableComponent>(patient), Is.True);
                Assert.That(server.EntMan.HasComponent<RMCMedicalExamineComponent>(patient), Is.True);
            });

            var damage = new DamageSpecifier();
            damage.DamageDict["Piercing"] = FixedPoint2.New(101);
            server.EntMan.System<DamageableSystem>().SetDamage((patient, null), damage);

            var bloodstream = server.EntMan.System<SharedBloodstreamSystem>();
            Assert.That(bloodstream.TryModifyBleedAmount((patient, null), 1), Is.True);

            var examine = server.EntMan.System<Content.Server.Examine.ExamineSystem>();
            var text = examine.GetExamineText(patient, examiner).ToString();

            Assert.Multiple(() =>
            {
                Assert.That(text, Does.Not.Contain("completely covered in massive"));
                Assert.That(text, Does.Not.Contain("dripping blood"));
                Assert.That(text, Does.Not.Contain("bleeding wounds on"));
            });

            server.EntMan.DeleteEntity(examiner);
            server.EntMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}
