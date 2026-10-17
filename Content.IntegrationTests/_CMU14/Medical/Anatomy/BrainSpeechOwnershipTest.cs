#pragma warning disable RA0002 // Observe independently owned speech sources and committed organ state.
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class BrainSpeechOwnershipTest
{
    // Many eligible letters make the deterministic full-strength transform observable.
    private const string Speech = "Our cautious surgeon can assess unusual causes. " +
        "Our cautious surgeon can assess unusual causes. Our cautious surgeon can assess unusual causes.";

    [TestCase(false)]
    [TestCase(true)]
    public async Task BrainAndDirectSlurComposeIdenticallyWithOrderedAccents(bool timedSlur)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, brain) = SpawnPatient(entities);
            try
            {
                // Ratvarian explicitly orders itself before SlurredSystem. Its
                // character translation and slurring do not commute.
                var ratvarian = entities.AddComponent<RatvarianLanguageComponent>(patient);
                if (timedSlur)
                    Assert.That(entities.System<StatusEffectsSystem>().TryAddStatusEffectDuration(patient,
                        "StatusEffectSlurred", TimeSpan.FromSeconds(600)), Is.True);
                var withoutBodySlur = Speak(entities, patient);
                entities.AddComponent<SlurredAccentComponent>(patient);
                var direct = Speak(entities, patient);
                Assert.That(direct, Is.Not.EqualTo(withoutBodySlur));

                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                Assert.That(Speak(entities, patient), Is.EqualTo(direct), "two body owners still contribute one transform");
                entities.RemoveComponent<SlurredAccentComponent>(patient);
                AssertBrainSpeech(entities, patient, true);
                Assert.That(Speak(entities, patient), Is.EqualTo(direct),
                    "switching the body slur owner must preserve its position relative to ordered and relayed accents");

                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 100);
                Assert.That(Speak(entities, patient), Is.EqualTo(withoutBodySlur));
                Assert.That(entities.GetComponent<RatvarianLanguageComponent>(patient), Is.SameAs(ratvarian));
                entities.AddComponent<SlurredAccentComponent>(patient);
                Assert.That(Speak(entities, patient), Is.EqualTo(direct));
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PermanentAccentSurvivesBrainStagesWithoutDoubleTransformingSpeech()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, brain) = SpawnPatient(entities);
            try
            {
                var independent = entities.AddComponent<SlurredAccentComponent>(patient);
                var baseline = Speak(entities, patient);
                Assert.That(baseline, Is.Not.EqualTo(Speech));

                DamageToStage(entities, patient, brain, OrganDamageStage.Bruised);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent),
                    "A bruised brain never owned the permanent accent it previously removed.");
                AssertBrainSpeech(entities, patient, false);

                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                AssertBrainSpeech(entities, patient, true);
                Assert.That(Speak(entities, patient), Is.EqualTo(baseline),
                    "The brain marker must not apply a second body slur over a direct permanent source.");

                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 1);
                Assert.That(entities.GetComponent<OrganHealthComponent>(brain).Stage, Is.EqualTo(OrganDamageStage.Bruised));
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
                Assert.That(Speak(entities, patient), Is.EqualTo(baseline));

                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 100);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
                Assert.That(Speak(entities, patient), Is.EqualTo(baseline));
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ASourceAddedOrRemovedDuringBrainInjuryKeepsItsOwnLifetime()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, brain) = SpawnPatient(entities);
            try
            {
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                Assert.That(entities.HasComponent<SlurredAccentComponent>(patient), Is.False);
                var brainOnly = Speak(entities, patient);
                Assert.That(brainOnly, Is.Not.EqualTo(Speech));
                entities.AddComponent<SlurredAccentComponent>(patient);
                Assert.That(Speak(entities, patient), Is.EqualTo(brainOnly));
                entities.RemoveComponent<SlurredAccentComponent>(patient);
                AssertBrainSpeech(entities, patient, true);
                Assert.That(Speak(entities, patient), Is.EqualTo(brainOnly));
                Assert.That(entities.HasComponent<SlurredAccentComponent>(patient), Is.False,
                    "The brain must not recreate an independently removed component.");

                var laterSource = entities.AddComponent<SlurredAccentComponent>(patient);
                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 100);
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(laterSource));
                Assert.That(Speak(entities, patient), Is.EqualTo(brainOnly));
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("StatusEffectSlurred")]
    [TestCase("StatusEffectDrunk")]
    public async Task IndependentTimedSlurKeepsIdentityExpiryAndStrengthAcrossBrainRecovery(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, brain) = SpawnPatient(entities);
            try
            {
                var status = entities.System<StatusEffectsSystem>();
                var proto = new EntProtoId(prototype);
                Assert.That(status.TryAddStatusEffectDuration(patient, proto, out var effectUid,
                    TimeSpan.FromSeconds(600)), Is.True);
                var effect = effectUid!.Value;
                var independent = entities.GetComponent<StatusEffectComponent>(effect);
                var expiry = independent.EndEffectTime;
                var patientBaseline = Speak(entities, patient);
                // Direct the same public accent event to this source to observe its
                // own probability scale without the patient's brain contribution.
                var sourceBaseline = Speak(entities, effect);
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                AssertBrainSpeech(entities, patient, true);
                Assert.That(Speak(entities, effect), Is.EqualTo(sourceBaseline),
                    "An indefinite brain source must not enter the timed slur's maximum-duration calculation.");
                Assert.That(Speak(entities, patient), Is.Not.EqualTo(patientBaseline));
                Assert.That(independent.EndEffectTime, Is.EqualTo(expiry));

                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 100);
                AssertBrainSpeech(entities, patient, false);
                Assert.That(status.TryGetStatusEffect(patient, proto, out var stillApplied), Is.True);
                Assert.That(stillApplied, Is.EqualTo(effect));
                Assert.That(entities.GetComponent<StatusEffectComponent>(effect), Is.SameAs(independent));
                Assert.That(independent.EndEffectTime, Is.EqualTo(expiry));
                Assert.That(Speak(entities, effect), Is.EqualTo(sourceBaseline));
                Assert.That(Speak(entities, patient), Is.EqualTo(patientBaseline));
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TimedSourceExpiresWhileBrainSpeechImpairmentRemains()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid? effect = null;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                EntityUid brain;
                (patient, brain) = SpawnPatient(entities);
                Assert.That(entities.System<StatusEffectsSystem>().TryAddStatusEffectDuration(patient,
                    "StatusEffectSlurred", out effect, TimeSpan.FromSeconds(2)), Is.True);
                Assert.That(Speak(entities, effect!.Value), Is.EqualTo(Speech), "This short slur is below its strength threshold.");
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                Assert.That(Speak(entities, effect!.Value), Is.EqualTo(Speech), "Brain injury must not amplify even a subthreshold timed slur.");
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.EntityExists(effect!.Value), Is.False);
                AssertBrainSpeech(entities, patient, true);
                Assert.That(entities.HasComponent<SlurredAccentComponent>(patient), Is.False);
                Assert.That(Speak(entities, patient), Is.Not.EqualTo(Speech));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => { if (entities.EntityExists(patient)) entities.DeleteEntity(patient); });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeBrainRecoveryPreservesAnAccentAcquiredWhileBruised()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid brain = default;
        SlurredAccentComponent independent = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                (patient, brain) = SpawnPatient(entities);
                DamageToStage(entities, patient, brain, OrganDamageStage.Bruised);
                independent = entities.AddComponent<SlurredAccentComponent>(patient);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(11));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<OrganHealthComponent>(brain).Stage, Is.EqualTo(OrganDamageStage.Healthy));
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
                Assert.That(Speak(entities, patient), Is.Not.EqualTo(Speech));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => { if (entities.EntityExists(patient)) entities.DeleteEntity(patient); });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainExtractionReplacementAndMultipleDonorsReconcileOnlyTheirOwnSpeechSource()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var (patient, original) = SpawnPatient(entities);
            EntityUid donor = default;
            try
            {
                var independent = entities.AddComponent<SlurredAccentComponent>(patient);
                var body = entities.System<SharedBodySystem>();
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                Assert.That(index.TryGetOrganPart(original, out var head), Is.True);
                DamageToStage(entities, patient, original, OrganDamageStage.Damaged);
                Assert.That(body.RemoveOrgan(original), Is.True);
                AssertBrainSpeech(entities, patient, false);
                donor = entities.SpawnEntity("CMUOrganHumanBrain", MapCoordinates.Nullspace);
                Assert.That(body.InsertOrgan(head, donor, "brain"), Is.True);
                AssertBrainSpeech(entities, patient, false);

                entities.System<SharedOrganHealthSystem>().HealOrgan(original, patient, 1);
                Assert.That(entities.GetComponent<CMUBrainComponent>(original).ActionSpeedMultiplier, Is.EqualTo(0.9f),
                    "Resolving the actual patient must not stop a detached donor's own state from recovering.");
                AssertBrainSpeech(entities, patient, false);
                DamageToStage(entities, patient, original, OrganDamageStage.Damaged);
                AssertBrainSpeech(entities, patient, false, "A detached injured donor cannot impair the supplied patient.");

                Assert.That(index.TryGetBodyPart(patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var extraSite), Is.True);
                Assert.That(body.TryCreateOrganSlot(extraSite, "brain", out _), Is.True);
                Assert.That(body.InsertOrgan(extraSite, original, "brain"), Is.True);
                AssertBrainSpeech(entities, patient, true, "A damaged donor must contribute immediately on insertion.");
                DamageToStage(entities, patient, donor, OrganDamageStage.Damaged);
                entities.System<SharedOrganHealthSystem>().HealOrgan(original, patient, 100);
                AssertBrainSpeech(entities, patient, true, "Healing one brain cannot clear a second damaged donor's source.");
                Assert.That(body.RemoveOrgan(donor), Is.True);
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
            }
            finally
            {
                entities.DeleteEntity(patient);
                if (entities.EntityExists(original)) entities.DeleteEntity(original);
                if (entities.EntityExists(donor)) entities.DeleteEntity(donor);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CompleteRejuvenationClearsBrainSpeechAndPreservesPermanentAccent(bool removeBrain)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid brain = default;
        SlurredAccentComponent independent = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                (patient, brain) = SpawnPatient(entities);
                independent = entities.AddComponent<SlurredAccentComponent>(patient);
                Assert.That(entities.System<StatusEffectsSystem>().TryAddStatusEffectDuration(patient,
                    "StatusEffectSlurred", TimeSpan.FromSeconds(600)), Is.True);
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                if (removeBrain)
                    Assert.That(entities.System<SharedBodySystem>().RemoveOrgan(brain), Is.True);
                entities.EventBus.RaiseLocalEvent(patient, new RejuvenateEvent());
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<CMUBrainComponent>(patient, out var restored), Is.True);
                Assert.That(entities.GetComponent<OrganHealthComponent>(restored).Stage, Is.EqualTo(OrganDamageStage.Healthy));
            });
            await pair.RunTicksSync(2);
            await pair.Server.WaitAssertion(() =>
            {
                AssertBrainSpeech(entities, patient, false);
                Assert.That(entities.System<StatusEffectsSystem>().HasStatusEffect(patient, "StatusEffectSlurred"), Is.False,
                    "Full rejuvenation still removes independent timed debuffs through their own lifecycle.");
                Assert.That(entities.GetComponent<SlurredAccentComponent>(patient), Is.SameAs(independent));
                Assert.That(Speak(entities, patient), Is.Not.EqualTo(Speech));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (entities.EntityExists(patient)) entities.DeleteEntity(patient);
                if (entities.EntityExists(brain)) entities.DeleteEntity(brain);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrainSpeechReplicationSurvivesRecoveryAndClientAnatomyDeletion()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalPlayer = player.AttachedEntity;
        var entities = pair.Server.EntMan;
        EntityUid patient = default;
        EntityUid brain = default;
        NetEntity patientNet = default;
        NetEntity brainNet = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                pair.Server.PlayerMan.SetAttachedEntity(player, patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<CMUBrainComponent>(patient, out brain), Is.True);
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                patientNet = entities.GetNetEntity(patient);
                brainNet = entities.GetNetEntity(brain);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientPatient = clientEntities.GetEntity(patientNet);
                AssertBrainSpeech(clientEntities, clientPatient, true);
                Assert.That(clientEntities.HasComponent<SlurredAccentComponent>(clientPatient), Is.False);
                Assert.That(Speak(clientEntities, clientPatient), Is.Not.EqualTo(Speech));
            });
            await pair.Server.WaitAssertion(() =>
            {
                entities.AddComponent<SlurredAccentComponent>(patient);
                entities.System<SharedOrganHealthSystem>().HealOrgan(brain, patient, 100);
            });
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientPatient = clientEntities.GetEntity(patientNet);
                AssertBrainSpeech(clientEntities, clientPatient, false);
                Assert.That(clientEntities.HasComponent<SlurredAccentComponent>(clientPatient), Is.True);
                Assert.That(Speak(clientEntities, clientPatient), Is.Not.EqualTo(Speech));
            });
            await pair.Server.WaitAssertion(() =>
            {
                DamageToStage(entities, patient, brain, OrganDamageStage.Damaged);
                AssertBrainSpeech(entities, patient, true);
            });
            await pair.RunUntilSynced();
            await pair.Server.WaitAssertion(() => entities.DeleteEntity(brain));
            await pair.RunUntilSynced();
            await pair.Client.WaitAssertion(() =>
            {
                var clientEntities = pair.Client.EntMan;
                var clientPatient = clientEntities.GetEntity(patientNet);
                Assert.That(clientEntities.TryGetEntity(brainNet, out _), Is.False);
                AssertBrainSpeech(clientEntities, clientPatient, false);
                Assert.That(clientEntities.HasComponent<SlurredAccentComponent>(clientPatient), Is.True);
            });
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
        await pair.Client.WaitAssertion(() => Assert.That(pair.Client.EntMan.TryGetEntity(patientNet, out _), Is.False));
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid Brain) SpawnPatient(IEntityManager entities)
    {
        var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetOrgan<CMUBrainComponent>(patient, out var brain), Is.True);
        return (patient, brain);
    }

    private static void DamageToStage(IEntityManager entities, EntityUid patient, EntityUid brain, OrganDamageStage stage)
    {
        var health = entities.GetComponent<OrganHealthComponent>(brain);
        var damage = health.Current - health.StageThresholds[stage];
        Assert.That(damage, Is.GreaterThan(FixedPoint2.Zero));
        var ev = new OrganDamagedEvent(patient, brain,
            new DamageSpecifier { DamageDict = { ["Blunt"] = damage } }, OrganDamageSource.Direct);
        entities.EventBus.RaiseLocalEvent(brain, ref ev, broadcast: true);
        Assert.That(health.Stage, Is.EqualTo(stage));
    }

    private static string Speak(IEntityManager entities, EntityUid entity)
    {
        var accent = new AccentGetEvent(entity, Speech);
        entities.EventBus.RaiseLocalEvent(entity, ref accent);
        return accent.Message;
    }

    private static void AssertBrainSpeech(IEntityManager entities, EntityUid patient, bool impaired, string? message = null)
        => Assert.That(entities.HasComponent<CMUBrainSpeechImpairmentComponent>(patient), Is.EqualTo(impaired), message);
}
