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
    private const string Tissue = "StatusEffectCMUHeartArrhythmia";
    private static readonly ProtoId<AlertPrototype> ArrhythmiaAlert = "CMUArrhythmia";
    private static readonly ProtoId<AlertPrototype> TachycardiaAlert = "CMUTachycardia";

    [TestCase("add", "rejuvenate")]
    [TestCase("set", "rejuvenate")]
    [TestCase("update", "rejuvenate")]
    [TestCase("add", "queued")]
    [TestCase("set", "queued")]
    [TestCase("update", "queued")]
    [TestCase("add", "immediate")]
    [TestCase("set", "immediate")]
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

    [TestCase("add", 10)]
    [TestCase("set", 3)]
    [TestCase("update", 7)]
    public async Task RemovalCallbackReplacementIsReusedWithTheRequestedDurationContract(string operation, int seconds)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<MedicalStatusRenewalProbeSystem>();
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            var probe = entities.AddComponent<MedicalStatusRenewalProbeComponent>(old!.Value);
            probe.Mode = MedicalStatusRenewalMutation.ReplaceOnRemoval;
            Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
            var now = pair.Server.Timing.CurTime;
            Assert.That(Renew(status, patient, operation, out var renewed), Is.True);
            Assert.That(probe.Invocations, Is.EqualTo(1));
            Assert.That(renewed, Is.EqualTo(probe.Replacement));
            Assert.That(renewed, Is.Not.EqualTo(old));
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
            Assert.That(entities.GetComponent<StatusEffectComponent>(renewed!.Value).EndEffectTime,
                Is.EqualTo(now + TimeSpan.FromSeconds(seconds)));
            Assert.That(entities.System<AlertsSystem>().IsShowingAlert(patient, ArrhythmiaAlert), Is.True);
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

    [TestCase(false)]
    [TestCase(true)]
    public async Task RemovalCallbackDeletingPatientRejectsThePendingRenewal(bool queued)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<MedicalStatusRenewalProbeSystem>();
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            var probe = entities.AddComponent<MedicalStatusRenewalProbeComponent>(old!.Value);
            probe.Mode = queued ? MedicalStatusRenewalMutation.QueuePatientOnRemoval : MedicalStatusRenewalMutation.DeletePatientOnRemoval;
            Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
            Assert.That(Renew(status, patient, "set", out var renewed), Is.False);
            Assert.That(renewed, Is.Null);
            Assert.That(probe.Invocations, Is.EqualTo(1));
            Assert.That(CountSources(entities, patient, Drug), Is.Zero);
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() => Assert.That(pair.Server.EntMan.EntityExists(patient), Is.False));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovalCallbackQueuingItsReplacementStopsRenewalWithoutRetiringANewerSourceAgain()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<MedicalStatusRenewalProbeSystem>();
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            var probe = entities.AddComponent<MedicalStatusRenewalProbeComponent>(old!.Value);
            probe.Mode = MedicalStatusRenewalMutation.QueueReplacementOnRemoval;
            Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
            Assert.That(Renew(status, patient, "set", out var renewed), Is.False);
            Assert.That(renewed, Is.Null);
            Assert.That(probe.Invocations, Is.EqualTo(1));
            Assert.That(probe.Replacement, Is.Not.Null);
            Assert.That(entities.EntityExists(probe.Replacement!.Value), Is.True);
            Assert.That(entities.IsQueuedForDeletion(probe.Replacement.Value), Is.True);
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
        });
        await pair.RunTicksSync(2);
        await pair.Server.WaitAssertion(() =>
        {
            Assert.That(pair.Server.EntMan.System<StatusEffectsSystem>().HasStatusEffect(patient, Drug), Is.False);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("add")]
    [TestCase("set")]
    [TestCase("update")]
    public async Task PermissionCallbackCreatedSourceSupersedesTheOlderCreation(string operation)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<MedicalStatusRenewalProbeSystem>();
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            var probe = entities.AddComponent<MedicalStatusRenewalProbeComponent>(patient);
            probe.Mode = MedicalStatusRenewalMutation.ReplaceOnPermission;
            var now = pair.Server.Timing.CurTime;
            Assert.That(Renew(status, patient, operation, out var superseded), Is.False);
            Assert.That(superseded, Is.Null);
            Assert.That(probe.Invocations, Is.EqualTo(1));
            Assert.That(Effect(status, patient, Drug), Is.EqualTo(probe.Replacement));
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
            Assert.That(entities.GetComponent<StatusEffectComponent>(probe.Replacement!.Value).EndEffectTime,
                Is.EqualTo(now + TimeSpan.FromSeconds(7)));
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EndTimeCallbackReplacementDoesNotReceiveTheOldContinuation()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<MedicalStatusRenewalProbeSystem>();
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var status = entities.System<StatusEffectsSystem>();
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var old, TimeSpan.FromSeconds(15)), Is.True);
            var probe = entities.AddComponent<MedicalStatusRenewalProbeComponent>(old!.Value);
            probe.Mode = MedicalStatusRenewalMutation.ReplaceOnEndTime;
            var now = pair.Server.Timing.CurTime;
            Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var superseded, TimeSpan.FromSeconds(3),
                delay: TimeSpan.FromSeconds(2)), Is.False);
            Assert.That(superseded, Is.Null);
            Assert.That(probe.Invocations, Is.EqualTo(1));
            Assert.That(entities.EntityExists(old.Value), Is.False);
            Assert.That(CountSources(entities, patient, Drug), Is.EqualTo(1));
            Assert.That(Effect(status, patient, Drug), Is.EqualTo(probe.Replacement));
            var fresh = entities.GetComponent<StatusEffectComponent>(probe.Replacement!.Value);
            Assert.That(fresh.EndEffectTime, Is.EqualTo(now + TimeSpan.FromSeconds(7)));
            Assert.That(fresh.StartEffectTime, Is.EqualTo(now));
            Assert.That(fresh.Applied, Is.True);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ConnectedRenewalAndExpiryPreserveTheIndependentTissueAlert()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        EntityUid patient = default;
        EntityUid heart = default;
        EntityUid tissue = default;
        NetEntity patientNet = default;
        NetEntity renewedNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<HeartComponent>(patient, out heart), Is.True);
                var health = entities.GetComponent<OrganHealthComponent>(heart);
                var damagedThreshold = health.StageThresholds[OrganDamageStage.Damaged];
                var damage = new OrganDamagedEvent(patient, heart,
                    new DamageSpecifier { DamageDict = { ["Blunt"] = health.Current - damagedThreshold } },
                    OrganDamageSource.Direct);
                entities.EventBus.RaiseLocalEvent(heart, ref damage, broadcast: true);
                Assert.That(health.Stage, Is.EqualTo(OrganDamageStage.Damaged),
                    "The independent arrhythmia source requires damaged or failing attached heart tissue.");
                var status = entities.System<StatusEffectsSystem>();
                tissue = Effect(status, patient, Tissue);
                Assert.That(status.TrySetStatusEffectDuration(patient, Drug, TimeSpan.FromSeconds(15)), Is.True);
                patientNet = entities.GetNetEntity(patient);
            });
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() =>
            {
                var status = entities.System<StatusEffectsSystem>();
                var old = Effect(status, patient, Drug);
                Assert.That(status.TryRemoveStatusEffect(patient, Drug), Is.True);
                Assert.That(status.TrySetStatusEffectDuration(patient, Drug, out var renewed, TimeSpan.FromSeconds(3)), Is.True);
                Assert.That(renewed, Is.Not.EqualTo(old));
                Assert.That(Effect(status, patient, Tissue), Is.EqualTo(tissue));
                renewedNet = entities.GetNetEntity(renewed!.Value);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientPatient = clientEntities.GetEntity(patientNet);
                Assert.That(Effect(clientEntities.System<StatusEffectsSystem>(), clientPatient, Drug), Is.EqualTo(clientEntities.GetEntity(renewedNet)));
                Assert.That(CountSources(clientEntities, clientPatient, Drug), Is.EqualTo(1));
                Assert.That(clientEntities.System<AlertsSystem>().IsShowingAlert(clientPatient, ArrhythmiaAlert), Is.True);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3.3f));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientPatient = clientEntities.GetEntity(patientNet);
                Assert.That(clientEntities.System<StatusEffectsSystem>().HasStatusEffect(clientPatient, Drug), Is.False);
                Assert.That(clientEntities.System<StatusEffectsSystem>().HasStatusEffect(clientPatient, Tissue), Is.True);
                Assert.That(clientEntities.System<AlertsSystem>().IsShowingAlert(clientPatient, ArrhythmiaAlert), Is.True);
            });
            await pair.Server.WaitAssertion(() => entities.System<SharedOrganHealthSystem>().HealOrgan(heart, patient, 100));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.System<AlertsSystem>()
                .IsShowingAlert(pair.Client.EntMan.GetEntity(patientNet), ArrhythmiaAlert), Is.False));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, originalPlayer);
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
            });
        }
        await pair.RunUntilSynced();
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

public enum MedicalStatusRenewalMutation : byte
{
    None,
    ReplaceOnRemoval,
    QueueReplacementOnRemoval,
    DeletePatientOnRemoval,
    QueuePatientOnRemoval,
    ReplaceOnPermission,
    ReplaceOnEndTime,
}

[RegisterComponent]
public sealed partial class MedicalStatusRenewalProbeComponent : Component
{
    public MedicalStatusRenewalMutation Mode;
    public int Invocations;
    public EntityUid? Replacement;
}

public sealed partial class MedicalStatusRenewalProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<MedicalStatusRenewalProbeComponent, StatusEffectRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<MedicalStatusRenewalProbeComponent, BeforeStatusEffectAddedEvent>(OnPermission);
        SubscribeLocalEvent<MedicalStatusRenewalProbeComponent, StatusEffectEndTimeUpdatedEvent>(OnEndTime);
    }

    private void OnRemoved(Entity<MedicalStatusRenewalProbeComponent> ent, ref StatusEffectRemovedEvent args)
    {
        var mode = ent.Comp.Mode;
        if (mode is not (MedicalStatusRenewalMutation.ReplaceOnRemoval or MedicalStatusRenewalMutation.QueueReplacementOnRemoval or
            MedicalStatusRenewalMutation.DeletePatientOnRemoval or MedicalStatusRenewalMutation.QueuePatientOnRemoval)) return;
        ent.Comp.Mode = MedicalStatusRenewalMutation.None;
        ent.Comp.Invocations++;
        if (mode == MedicalStatusRenewalMutation.DeletePatientOnRemoval) Del(args.Target);
        else if (mode == MedicalStatusRenewalMutation.QueuePatientOnRemoval) QueueDel(args.Target);
        else
        {
            CreateReplacement(ent.Comp, args.Target);
            if (mode == MedicalStatusRenewalMutation.QueueReplacementOnRemoval) QueueDel(ent.Comp.Replacement);
        }
    }

    private void OnPermission(Entity<MedicalStatusRenewalProbeComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (ent.Comp.Mode != MedicalStatusRenewalMutation.ReplaceOnPermission || args.Effect.Id != MedicalStatusRenewalLifecycleTest.Drug) return;
        ent.Comp.Mode = MedicalStatusRenewalMutation.None;
        ent.Comp.Invocations++;
        CreateReplacement(ent.Comp, ent.Owner);
    }

    private void OnEndTime(Entity<MedicalStatusRenewalProbeComponent> ent, ref StatusEffectEndTimeUpdatedEvent args)
    {
        if (ent.Comp.Mode != MedicalStatusRenewalMutation.ReplaceOnEndTime) return;
        ent.Comp.Mode = MedicalStatusRenewalMutation.None;
        ent.Comp.Invocations++;
        Del(ent.Owner);
        CreateReplacement(ent.Comp, args.Target);
    }

    private void CreateReplacement(MedicalStatusRenewalProbeComponent probe, EntityUid patient)
    {
        Assert.That(EntityManager.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient,
            MedicalStatusRenewalLifecycleTest.Drug, out var replacement, TimeSpan.FromSeconds(7)), Is.True);
        probe.Replacement = replacement;
    }
}
