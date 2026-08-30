using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.HUD;
using Content.Shared._RMC14.Medical.HUD.Components;
using Content.Shared.Body.Part;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics.Holocards;

[TestFixture]
public sealed class AutoHolocardSystemTest
{
    [Test]
    public async Task ClearsIndicatorAfterLastInjuryHeals()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid fracturedArm = default;
        EntityUid bleedingArm = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var index = entMan.System<CMUMedicalBodyIndexSystem>();
            var fractures = entMan.System<SharedFractureSystem>();
            var wounds = entMan.System<SharedCMUWoundsSystem>();
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            fracturedArm = GetPart(index, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            bleedingArm = GetPart(index, patient, BodyPartType.Arm, BodyPartSymmetry.Right);

            var fracture = entMan.EnsureComponent<FractureComponent>(fracturedArm);
            fractures.SetSeverity((fracturedArm, fracture), FractureSeverity.Simple);
            wounds.SeedInternalBleed(bleedingArm, "blunt", 0.5f);

            Assert.That(
                entMan.GetComponent<HolocardStateComponent>(patient).HolocardStatus,
                Is.EqualTo(HolocardStatus.Trauma));
        });

        await server.WaitPost(() =>
        {
            server.EntMan.System<SharedCMUWoundsSystem>().ClearInternalBleed(bleedingArm);
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(
                server.EntMan.GetComponent<HolocardStateComponent>(patient).HolocardStatus,
                Is.EqualTo(HolocardStatus.Trauma));
        });

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var fracture = entMan.GetComponent<FractureComponent>(fracturedArm);
            entMan.System<SharedFractureSystem>()
                .SetSeverity((fracturedArm, fracture), FractureSeverity.None);
        });
        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(
                entMan.GetComponent<HolocardStateComponent>(patient).HolocardStatus,
                Is.EqualTo(HolocardStatus.None));
            entMan.DeleteEntity(patient);
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetPart(
        CMUMedicalBodyIndexSystem index,
        EntityUid body,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        Assert.That(index.TryGetBodyPart(body, new CMUMedicalBodyPartKey(type, symmetry), out var part), Is.True);
        return part;
    }
}
