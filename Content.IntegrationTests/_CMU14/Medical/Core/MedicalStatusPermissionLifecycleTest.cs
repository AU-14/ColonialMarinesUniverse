using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Core;

[TestFixture]
public sealed class MedicalStatusPermissionLifecycleTest
{
    private const string Effect = "StatusEffectCMUArrhythmia";

    [TestCase("add", false)]
    [TestCase("add", true)]
    [TestCase("set", false)]
    [TestCase("set", true)]
    [TestCase("update", false)]
    [TestCase("update", true)]
    public async Task PermissionCannotAddMedicationToADeletedPatient(string operation, bool queued)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<MedicalStatusPermissionDeletionProbeSystem>();
                patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                var status = entities.System<StatusEffectsSystem>();
                Assert.That(status.TryGetStatusEffect(patient, Effect, out _), Is.False);
                var before = CountEffects(entities);
                var probe = entities.AddComponent<MedicalStatusPermissionDeletionProbeComponent>(patient);
                probe.Queued = queued;
                EntityUid? effect;
                var applied = operation switch
                {
                    "add" => status.TryAddStatusEffectDuration(patient, Effect, out effect, TimeSpan.FromSeconds(3)),
                    "set" => status.TrySetStatusEffectDuration(patient, Effect, out effect, TimeSpan.FromSeconds(3)),
                    _ => status.TryUpdateStatusEffectDuration(patient, Effect, out effect, TimeSpan.FromSeconds(3)),
                };
                Assert.That(probe.Invocations, Is.EqualTo(1), "The real permission boundary must execute.");
                Assert.That(applied, Is.False);
                Assert.That(effect, Is.Null);
                Assert.That(CountEffects(entities), Is.EqualTo(before), "No orphan medication may be spawned.");
                Assert.That(queued ? entities.IsQueuedForDeletion(patient) : !entities.EntityExists(patient), Is.True);
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() => Assert.That(entities.EntityExists(patient), Is.False));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
            });
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ForceDoesNotBypassRetiringPatientIdentity(bool queued)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        await pair.Server.WaitAssertion(() =>
        {
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            if (queued) entities.QueueDeleteEntity(patient);
            else entities.DeleteEntity(patient);
            Assert.That(entities.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient, Effect,
                out var effect, TimeSpan.FromSeconds(3), force: true), Is.False);
            Assert.That(effect, Is.Null);
        });
        await pair.RunTicksSync(2);
        await pair.CleanReturnAsync();
    }

    private static int CountEffects(IEntityManager entities)
    {
        var count = 0;
        var query = entities.AllEntityQueryEnumerator<StatusEffectComponent, MetaDataComponent>();
        while (query.MoveNext(out _, out _, out var metadata))
            if (metadata.EntityPrototype?.ID == Effect) count++;
        return count;
    }
}

[RegisterComponent]
public sealed partial class MedicalStatusPermissionDeletionProbeComponent : Component
{
    public bool Queued;
    public int Invocations;
}

public sealed partial class MedicalStatusPermissionDeletionProbeSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<MedicalStatusPermissionDeletionProbeComponent, BeforeStatusEffectAddedEvent>(OnPermission);

    private void OnPermission(Entity<MedicalStatusPermissionDeletionProbeComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (args.Effect.Id != "StatusEffectCMUArrhythmia") return;
        ent.Comp.Invocations++;
        if (ent.Comp.Queued) QueueDel(ent.Owner);
        else Del(ent.Owner);
    }
}
