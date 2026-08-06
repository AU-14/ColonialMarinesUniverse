using Content.Server._CMU14.Medical.Diagnostics.Examine;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Medical.Diagnostics;

[TestFixture]
public sealed class CMUDetailedMedicalExamineEntryPointTest
{
    [Test]
    public async Task DetailedExamineStartsForCmuPatient()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid patient = default;
        EntityUid examiner = default;

        await server.WaitPost(() =>
        {
            patient = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            examiner = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var examine = entities.System<CMUDetailedMedicalExamineSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(examine.TryStartDetailedExamine(examiner, patient), Is.True);
                Assert.That(entities.HasComponent<ActiveDoAfterComponent>(examiner), Is.True);
            });

            entities.DeleteEntity(examiner);
            entities.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }
}
