#pragma warning disable RA0002 // Public status operations drive mutations; assertions inspect committed source identities.
using Content.Shared.Administration.Systems;
using Content.Shared.Alert;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class MedicalStatusRenewalLifecycleTest
{
    internal const string Drug = "StatusEffectCMUArrhythmia";
    private const string Independent = "StatusEffectCMUTachycardia";
    private static readonly ProtoId<AlertPrototype> ArrhythmiaAlert = "CMUArrhythmia";
    private static readonly ProtoId<AlertPrototype> TachycardiaAlert = "CMUTachycardia";

    [TestCase("set", "rejuvenate")]
    [TestCase("add", "queued")]
    [TestCase("update", "immediate")]
    public async Task FreshMedicationAfterRetirementGetsItsOwnIdentityAndDuration(string operation, string retirement)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid renewed = default;
        EntityUid independent = default;
        await pair.Server.WaitAssertion(() =>
        {
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            if (retirement == "rejuvenate") entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
            else if (retirement == "queued") Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
            else entities.DeleteEntity(old!.Value);
            if (retirement != "immediate")
            {
                Assert.That(entities.IsQueuedForDeletion(old!.Value), Is.True);
                Assert.That(status.TryGetStatusEffect(patient, Drug, out var readable), Is.True,
                    "The read-only query still exposes the retiring source until renewal or deletion.");
                Assert.That(readable, Is.EqualTo(old));
            }
            Assert.That(status.TrySetStatusEffectDuration(patient, Independent, out var other, TimeSpan.FromSeconds(60)), Is.True);
            independent = other!.Value;
            var now = pair.Server.Timing.CurTime;
            Assert.That(Renew(status, patient, operation, out var fresh), Is.True);
            renewed = fresh!.Value;
            Assert.That(renewed, Is.Not.EqualTo(old));
            Assert.That(entities.EntityExists(old!.Value), Is.False);
            Assert.That(entities.IsQueuedForDeletion(renewed), Is.False);
            Assert.That(entities.GetComponent<StatusEffectComponent>(renewed).EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(3)),
                "The retired source's remaining duration is not inherited by fresh medication.");
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
            Assert.That(Effect(status, patient, Independent), Is.EqualTo(independent));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.True);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() => Assert.That(Effect(entities.System<StatusEffectsSystem>(), patient, Drug), Is.EqualTo(renewed)));
        await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
        await pair.Server.WaitAssertion(() =>
        {
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.HasStatusEffect(patient, Drug), Is.False);
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.False);
            Assert.That(Effect(status, patient, Independent), Is.EqualTo(independent));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, TachycardiaAlert), Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("add")]
    [TestCase("set")]
    [TestCase("remove")]
    public async Task ExistingOnlyTimeAdjustmentCannotResurrectOrModifyRetiringMedication(string operation)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            var expiry = entities.GetComponent<StatusEffectComponent>(old!.Value).EndEffectTime;
            Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
            var adjusted = operation switch
            {
                "add" => status.TryAddTime(patient, Drug, TimeSpan.FromSeconds(7)),
                "set" => status.TrySetTime(patient, Drug, pair.Server.Timing.CurTime + TimeSpan.FromSeconds(7)),
                _ => status.TryRemoveTime(patient, Drug, TimeSpan.FromSeconds(7)),
            };
            Assert.That(adjusted, Is.False);
            Assert.That(entities.GetComponent<StatusEffectComponent>(old.Value).EndEffectTime, Is.EqualTo(expiry));
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
            Assert.That(entities.IsQueuedForDeletion(old.Value), Is.True);
            Assert.That(Renew(status, patient, "set", out var fresh), Is.True);
            Assert.That(fresh, Is.Not.EqualTo(old));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    private static bool Renew(StatusEffectsSystem status, EntityUid patient, string operation, out EntityUid? effect)
        => operation switch
        {
            "add" => status.TryAddStatusEffectDuration(patient, Drug, out effect, TimeSpan.FromSeconds(3)),
            "set" => status.TrySetStatusEffectDuration(patient, Drug, out effect, TimeSpan.FromSeconds(3)),
            _ => status.TryUpdateStatusEffectDuration(patient, Drug, out effect, TimeSpan.FromSeconds(3)),
        };

    private static EntityUid Effect(StatusEffectsSystem status, EntityUid patient, EntProtoId prototype)
    {
        Assert.That(status.TryGetStatusEffect(patient, prototype, out var effect), Is.True);
        return effect!.Value;
    }

    private static int CountSources(IEntityManager entities, EntityUid patient, string prototype)
    {
        var count = 0;
        foreach (var effect in entities.System<StatusEffectsSystem>().EnumerateStatusEffects((patient, null)))
            if (entities.GetComponent<MetaDataComponent>(effect.Owner).EntityPrototype?.ID == prototype) count++;
        return count;
    }
}
