#pragma warning disable RA0002 // Observe replicated and committed medical state.
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain.Penalties;
using Content.Shared.Body.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Damage;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class OrganVisionOwnershipTest
{
    [Test]
    public async Task OrganRecoveryPreservesIndependentEyeDamageAndReplicatesVisibleBlur()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var original = player.AttachedEntity;
        EntityUid patient = default;
        EntityUid eye = default;
        NetEntity networkPatient = default;
        float initialAim = 0;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                networkPatient = entities.GetNetEntity(patient);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out eye), Is.True);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                initialAim = entities.GetComponent<CMUAimAccuracyComponent>(patient).SwayMultiplier;
                var health = entities.GetComponent<OrganHealthComponent>(eye);
                var injury = new OrganDamagedEvent(patient, eye,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = health.Max - health.StageThresholds[OrganDamageStage.Damaged] } },
                    OrganDamageSource.Direct);
                entities.EventBus.RaiseLocalEvent(eye, ref injury, broadcast: true);
                Assert.That(entities.GetComponent<BlindableComponent>(patient).EyeDamage, Is.Zero);
                Assert.That(entities.GetComponent<BlurryVisionComponent>(patient).Magnitude, Is.GreaterThan(0));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() => AssertCachedAim(pair.Server.EntMan, patient, initialAim * 1.1f));
            await pair.RunTicksSync(20);
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientPatient = entities.GetEntity(networkPatient);
                Assert.That(entities.GetComponent<BlurryVisionComponent>(clientPatient).Magnitude, Is.GreaterThan(0));
                AssertCachedAim(entities, clientPatient, initialAim * 1.1f);
            });
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var blindness = entities.System<BlindableSystem>();
                // Independent eye damage can heal to its floor and then recur while
                // the organ remains damaged. Organ recovery must never subtract it.
                blindness.AdjustEyeDamage(patient, 5);
                blindness.AdjustEyeDamage(patient, -100);
                blindness.AdjustEyeDamage(patient, 4);
                var before = entities.GetComponent<BlindableComponent>(patient).EyeDamage;
                entities.System<SharedOrganHealthSystem>().HealOrgan(eye, patient, 100);
                Assert.That(entities.HasComponent<CMUOrganVisionImpairmentComponent>(patient), Is.False);
                Assert.That(entities.GetComponent<BlindableComponent>(patient).EyeDamage, Is.EqualTo(before));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() => AssertCachedAim(pair.Server.EntMan, patient, initialAim));
            await pair.RunTicksSync(20);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.EntMan.HasComponent<CMUOrganVisionImpairmentComponent>(
                    pair.Client.EntMan.GetEntity(networkPatient)), Is.False);
                AssertCachedAim(pair.Client.EntMan, pair.Client.EntMan.GetEntity(networkPatient), initialAim);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => pair.Server.PlayerMan.SetAttachedEntity(player, original));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ExtractingAnInjuredEyeRetiresItsCachedAimPenalty()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid eye = default;
        float initialAim = 0;
        await pair.Server.WaitPost(() => patient = pair.Server.EntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace));
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            initialAim = entities.GetComponent<CMUAimAccuracyComponent>(patient).SwayMultiplier;
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<EyesComponent>(patient, out eye), Is.True);
            var health = entities.GetComponent<OrganHealthComponent>(eye);
            var injury = new OrganDamagedEvent(patient, eye,
                new DamageSpecifier { DamageDict = { ["Blunt"] = health.Max - health.StageThresholds[OrganDamageStage.Failing] } },
                OrganDamageSource.Direct);
            entities.EventBus.RaiseLocalEvent(eye, ref injury, broadcast: true);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertCachedAim(entities, patient, initialAim * 1.3f);
            Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(eye), Is.True);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertCachedAim(entities, patient, initialAim);
            entities.DeleteEntity(patient);
            entities.DeleteEntity(eye);
        });
        await pair.CleanReturnAsync();
    }

    private static void AssertCachedAim(IEntityManager entities, EntityUid patient, float expected)
    {
        var aim = entities.GetComponent<CMUAimAccuracyComponent>(patient);
        Assert.That(aim.SwayMultiplier, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(aim.SpreadMultiplier, Is.EqualTo(expected).Within(0.0001f));
    }
}
