using Content.Server._RMC14.Medical.IV;
using Content.Shared._RMC14.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.CMU14.Medical.Blood;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.CMU14.Medical.Blood;

[TestFixture]
public sealed class CMUBloodMismatchTest
{
    [Test]
    public async Task IncompatibleBloodPoisonsRecipient()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid patient = default;
        EntityUid pack = default;
        EntityUid iv = default;

        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            patient = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            pack = entMan.SpawnEntity("CMUBloodPackFullOPositive", map.GridCoords);
            iv = entMan.SpawnEntity("CMIV", map.GridCoords);
            Assert.That(entMan.GetComponent<CMUBloodPackTypeComponent>(pack).Type, Is.EqualTo(CMUBloodType.OPositive),
                "The O+ pack spawned without its blood type.");

            var bloodType = entMan.EnsureComponent<CMUBloodTypeComponent>(patient);
            bloodType.Type = CMUBloodType.ANegative;

            var bloodstreamSystem = entMan.System<BloodstreamSystem>();
            var bloodstream = entMan.GetComponent<BloodstreamComponent>(patient);
            bloodstreamSystem.TryModifyBloodLevel((patient, bloodstream), FixedPoint2.New(-100));

            var containers = entMan.System<SharedContainerSystem>();
            var ivDrip = entMan.GetComponent<IVDripComponent>(iv);
            Assert.That(containers.TryGetContainer(iv, ivDrip.Slot, out var slot), Is.True);
            Assert.That(containers.Insert(pack, slot), Is.True);

            ivDrip.AttachedTo = patient;
            ivDrip.Injecting = true;
            ivDrip.TransferAt = TimeSpan.Zero;
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var label = entMan.GetComponent<CMUBloodPackTypeComponent>(pack);
            var damage = entMan.System<DamageableSystem>().GetAllDamage((patient, null));
            var solutions = entMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(pack, "pack", out _, out var packSolution), Is.True);

            TestContext.Out.WriteLine($"label {label.Type} mixed {label.Mixed}; pack volume {packSolution!.Volume}; damage {string.Join(", ", damage.DamageDict.Where(d => d.Value > 0).Select(d => $"{d.Key}={d.Value}"))}");

            Assert.That(packSolution.Volume, Is.LessThan(FixedPoint2.New(300)), "Nothing left the pack.");
            Assert.That(damage.DamageDict.GetValueOrDefault("Poison"), Is.GreaterThan(FixedPoint2.Zero),
                "Incompatible blood didn't poison the recipient.");
        });

        await pair.CleanReturnAsync();
    }
}
