using System;
using Content.Shared.Administration.Systems;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.CMU14.Medical.Treatment.Recovery;

[TestFixture]
public sealed class CMUMedicalHemostasisResetTest
{
    [Test]
    public async Task TraumaDressingControlsArteryAfterPreparedGauzeAlreadyTreatedTheWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var user = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entities.SpawnEntity("CMUSealingGauze6", MapCoordinates.Nullspace);
            var dressing = entities.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);
            try
            {
                server.System<SkillsSystem>().SetSkill(user, "RMCSkillMedical", 2);
                server.System<SharedBodyZoneTargetingSystem>().SelectZone((user, null), TargetBodyZone.Chest);
                var part = Part(server.System<CMUMedicalBodyIndexSystem>(), patient, BodyPartType.Torso);
                Assert.That(server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                    patient, part, Slash(80), impact: DamageImpact.MeleeSlash), Is.True);

                var gauzeUse = new AfterInteractEvent(user, gauze, patient, default, true);
                entities.EventBus.RaiseLocalEvent(gauze, gauzeUse);
                var wounds = entities.GetComponent<BodyPartWoundComponent>(part);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                Assert.That(server.System<CMUWoundLedgerSystem>().GetEntries(wounds)[0].Wound.Treated, Is.True);

                var before = entities.GetComponent<StackComponent>(dressing).Count;
                var dressingUse = new AfterInteractEvent(user, dressing, patient, default, true);
                entities.EventBus.RaiseLocalEvent(dressing, dressingUse);
                Assert.Multiple(() =>
                {
                    Assert.That(dressingUse.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                    Assert.That(entities.GetComponent<StackComponent>(dressing).Count, Is.EqualTo(before - 1));
                });
                entities.EventBus.RaiseLocalEvent(dressing, new AfterInteractEvent(user, dressing, patient, default, true));
                Assert.That(entities.GetComponent<StackComponent>(dressing).Count, Is.EqualTo(before - 1));
            }
            finally
            {
                entities.DeleteEntity(gauze);
                entities.DeleteEntity(dressing);
                entities.DeleteEntity(patient);
                entities.DeleteEntity(user);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TourniquetOccludesNewDistalWoundsAndRemovalResumesBloodLoss()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid arm = default;
        EntityUid hand = default;
        var initialBlood = 0f;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var index = server.System<CMUMedicalBodyIndexSystem>();
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            arm = Part(index, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            hand = Part(index, patient, BodyPartType.Hand, BodyPartSymmetry.Left);
            var tourniquet = entities.SpawnEntity("AU14Tourniquet", MapCoordinates.Nullspace);
            Assert.That(server.System<SharedCMUTourniquetSystem>().ApplyTourniquetToPart(
                (tourniquet, entities.GetComponent<CMUTourniquetItemComponent>(tourniquet)), arm), Is.True);
            Assert.That(server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                patient, hand, Slash(10), impact: DamageImpact.MeleeSlash), Is.True);
            server.System<BloodstreamSystem>().TrySetBleedAmount((patient, null), 0f);
            initialBlood = server.System<BloodstreamSystem>().GetBloodLevel((patient, null));
            Assert.That(entities.GetComponent<BodyPartWoundComponent>(hand).ExternalBleeding, Is.Not.EqualTo(ExternalBleedTier.None));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.1f));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BloodstreamSystem>().GetBloodLevel((patient, null)), Is.EqualTo(initialBlood).Within(0.00001f));
            server.EntMan.RemoveComponent<CMUTourniquetComponent>(arm);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(1.1f));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<BloodstreamSystem>().GetBloodLevel((patient, null)), Is.LessThan(initialBlood));
            server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RejuvenationClearsSourcesAndRemainsHealthyAfterFormerMalunionDeadline()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid patient = default;
        EntityUid part = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            part = Part(server.System<CMUMedicalBodyIndexSystem>(), patient, BodyPartType.Arm, BodyPartSymmetry.Left);
            server.System<SharedCMUShrapnelSystem>().AddShrapnel(part, 3, 5);
            server.System<SharedCMUWoundsSystem>().SeedSurgicalInternalBleed(part);
            server.System<SharedCMUWoundsSystem>().SuppressInternalBleed(part);
            server.System<SharedCMUSurgicalTraitSystem>().EnsureTrait(part, CMUSurgicalTrait.VascularTear);
            server.System<SharedPainShockSystem>().AddPainPulse(patient, 100);
            var postOp = entities.EnsureComponent<CMUPostOpBoneSetComponent>(part);
            postOp.MalunionCheckAt = server.ResolveDependency<IGameTiming>().CurTime + TimeSpan.FromSeconds(0.1);
            postOp.MalunionChance = 1;
            server.System<SharedCMUSplintItemSystem>().SchedulePostOpMalunion(part, postOp);
            foreach (var status in new[] { "StatusEffectCMUAnesthesia", "StatusEffectCMURecoveringSurgery", "StatusEffectCMUFentanylHaze" })
                server.System<StatusEffectsSystem>().TryUpdateStatusEffectDuration(patient, status, TimeSpan.FromMinutes(1));

            server.System<RejuvenateSystem>().PerformRejuvenate(patient);
        });

        await pair.RunTicksSync(pair.SecondsToTicks(1.2f));
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            server.System<SharedCMUWoundsSystem>().RecomputeInternalBleed(part);
            var pain = entities.GetComponent<PainShockComponent>(patient);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<CMUShrapnelComponent>(part), Is.False);
                Assert.That(entities.HasComponent<CMUSurgicalInternalBleedingComponent>(part), Is.False);
                Assert.That(entities.HasComponent<CMUInternalBleedingSuppressedComponent>(part), Is.False);
                Assert.That(entities.HasComponent<InternalBleedingComponent>(part), Is.False);
                Assert.That(entities.HasComponent<CMUPostOpBoneSetComponent>(part), Is.False);
                Assert.That(entities.HasComponent<CMUMalunionComponent>(part), Is.False);
                Assert.That(entities.HasComponent<FractureComponent>(part), Is.False);
                Assert.That(server.System<SharedCMUSurgicalTraitSystem>().CountTraits(part), Is.Zero);
                Assert.That(pain.Pain.Float(), Is.Zero);
                Assert.That(pain.PainTarget.Float(), Is.Zero);
                Assert.That(pain.Tier, Is.EqualTo(PainTier.None));
                Assert.That(server.System<SharedCMUShrapnelSystem>().ComputeMovementPainPulse(patient), Is.Zero);
            });
            foreach (var status in new[] { "StatusEffectCMUAnesthesia", "StatusEffectCMURecoveringSurgery", "StatusEffectCMUFentanylHaze" })
                Assert.That(server.System<StatusEffectsSystem>().HasStatusEffect(patient, status), Is.False);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid Part(CMUMedicalBodyIndexSystem index, EntityUid patient, BodyPartType type, BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        Assert.That(index.TryGetBodyPart(patient, new(type, symmetry), out var part), Is.True);
        return part;
    }

    private static DamageSpecifier Slash(int amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Slash"] = amount;
        return damage;
    }
}
