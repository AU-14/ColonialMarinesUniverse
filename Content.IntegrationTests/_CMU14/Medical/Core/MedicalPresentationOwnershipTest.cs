using Content.Client.Damage;
using Content.Shared.Alert;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class MedicalPresentationOwnershipTest
{
    [Test]
    public async Task ConcurrentMedicalAlertsSurviveUnrelatedConditionRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var effects = entities.System<StatusEffectsSystem>();
            var alerts = entities.System<AlertsSystem>();
            effects.TrySetStatusEffectDuration(patient, "StatusEffectCMUCardiacArrest", null);
            effects.TrySetStatusEffectDuration(patient, "StatusEffectCMURenalFailure", null);
            effects.TrySetStatusEffectDuration(patient, "StatusEffectCMUMissingArmLeft", null);
            Assert.Multiple(() =>
            {
                Assert.That(alerts.IsShowingAlert(patient, "CMUCardiacArrest"), Is.True);
                Assert.That(alerts.IsShowingAlert(patient, "CMURenalFailure"), Is.True);
                Assert.That(alerts.IsShowingAlert(patient, "CMUMissingArm"), Is.True);
            });
            Assert.That(effects.TryRemoveStatusEffect(patient, "StatusEffectCMURenalFailure"), Is.True);
        });
        // Status removal queues deletion; inspect alerts after that lifecycle commits.
        await pair.RunTicksSync(1);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var alerts = entities.System<AlertsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(alerts.IsShowingAlert(patient, "CMUCardiacArrest"), Is.True);
                Assert.That(alerts.IsShowingAlert(patient, "CMURenalFailure"), Is.False);
                Assert.That(alerts.IsShowingAlert(patient, "CMUMissingArm"), Is.True);
            });
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RegionalVisualOwnershipReleasesWithoutOverwritingIndependentDisable(bool independentDisable)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var original = player.AttachedEntity;
        EntityUid patient = default;
        NetEntity networkPatient = default;
        try
        {
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                entities.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = 40 } }, ignoreResistances: true);
                networkPatient = entities.GetNetEntity(patient);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
            });
            await pair.RunTicksSync(20);
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientPatient = entities.GetEntity(networkPatient);
                var visuals = entities.GetComponent<DamageVisualsComponent>(clientPatient);
                Assert.That(visuals.MedicalOverride, Is.True);
                Assert.That(visuals.Disabled, Is.False);
            });
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<SharedAppearanceSystem>()
                .SetData(patient, DamageVisualizerKeys.Disabled, independentDisable));
            await pair.RunTicksSync(20);
            await pair.Server.WaitPost(() => pair.Server.EntMan.RemoveComponent<CMUHumanMedicalComponent>(patient));
            await pair.RunTicksSync(20);
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientPatient = entities.GetEntity(networkPatient);
                var visuals = entities.GetComponent<DamageVisualsComponent>(clientPatient);
                Assert.That(visuals.MedicalOverride, Is.False);
                Assert.That(visuals.Disabled, Is.EqualTo(independentDisable));
                if (!independentDisable)
                    AssertAggregateBruteVisible(entities, clientPatient, visuals);
            });
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<SharedAppearanceSystem>()
                .SetData(patient, DamageVisualizerKeys.Disabled, false));
            // Repeat ownership with unchanged aggregate damage: cached thresholds
            // must not prevent the previously hidden layers becoming visible again.
            await pair.Server.WaitPost(() => pair.Server.EntMan.EnsureComponent<CMUHumanMedicalComponent>(patient));
            await pair.RunTicksSync(20);
            await pair.Server.WaitPost(() => pair.Server.EntMan.RemoveComponent<CMUHumanMedicalComponent>(patient));
            await pair.RunTicksSync(20);
            await pair.Client.WaitAssertion(() =>
            {
                var entities = pair.Client.EntMan;
                var clientPatient = entities.GetEntity(networkPatient);
                AssertAggregateBruteVisible(entities, clientPatient, entities.GetComponent<DamageVisualsComponent>(clientPatient));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => pair.Server.PlayerMan.SetAttachedEntity(player, original));
        }
        await pair.CleanReturnAsync();
    }

    private static void AssertAggregateBruteVisible(IEntityManager entities, EntityUid patient, DamageVisualsComponent visuals)
    {
        var sprite = entities.GetComponent<SpriteComponent>(patient);
        var spriteSystem = entities.System<SpriteSystem>();
        var visible = 0;
        foreach (var layer in visuals.TargetLayerMapKeys)
        {
            if (spriteSystem.LayerMapTryGet((patient, sprite), $"{layer}Brute", out var index, false) && sprite[index].Visible)
                visible++;
        }
        Assert.That(visible, Is.GreaterThan(0), "Aggregate damage layers stayed hidden after medical ownership ended.");
    }
}
