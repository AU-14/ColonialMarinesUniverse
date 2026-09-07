#pragma warning disable RA0002 // Inspect committed state after public medical interactions.
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Bones.Events;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy;

[TestFixture]
public sealed class BoneRecoveryLifecycleTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FractureCallbackCannotRestoreWorkForARemovedOrReplacedBone(bool replace)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid arm = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            entities.System<CMUBoneRecoveryReentryProbeSystem>();
            (patient, arm) = SpawnInjury(entities);
            entities.AddComponent<CMUBoneRecoveryReentryProbeComponent>(arm).Replace = replace;
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.GetComponent<CMUBoneRecoveryReentryProbeComponent>(arm).Removed, Is.True);
            Assert.That(entities.HasComponent<BoneComponent>(arm), Is.EqualTo(replace));
            Assert.That(entities.HasComponent<BoneRecoveryComponent>(arm), Is.False);
            if (replace)
                AssertDeficit(entities, arm, 0);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ClearingHairlineFractureContinuesRecoveryUntilFullIntegrity()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid arm = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, arm) = SpawnInjury(entities);
            foreach (var (part, _) in entities.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient))
                Assert.That(entities.HasComponent<BoneRecoveryComponent>(part), Is.EqualTo(part == arm));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            Assert.That(entities.HasComponent<FractureComponent>(arm), Is.False);
            AssertRecovery(entities, arm, 1);
            Assert.That(entities.HasComponent<BoneRecoveryComponent>(arm), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertRecovery(entities, arm, 2);
            entities.System<SharedBoneSystem>().RestoreIntegrity(arm, entities.GetComponent<BoneComponent>(arm).IntegrityMax - 1);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertDeficit(entities, arm, 0);
            Assert.That(entities.HasComponent<BoneRecoveryComponent>(arm), Is.False);
            // Repeated full treatment is harmless; damage in the same tick starts
            // fresh work rather than inheriting an already-cancelled deadline.
            entities.System<SharedBoneSystem>().RestoreIntegrity(arm, entities.GetComponent<BoneComponent>(arm).IntegrityMax);
            Assert.That(entities.System<SharedBoneSystem>().DamageWeakestBone(patient, -1, false), Is.False);
            AssertDeficit(entities, arm, 0);
            entities.System<SharedBoneSystem>().RestoreIntegrity(arm, entities.GetComponent<BoneComponent>(arm).IntegrityMax - 1);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(9));
        await pair.Server.WaitAssertion(() => AssertDeficit(pair.Server.EntMan, arm, 1));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() =>
        {
            AssertDeficit(pair.Server.EntMan, arm, 0);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("stasis")]
    [TestCase("patient")]
    [TestCase("part")]
    [TestCase("both")]
    [TestCase("stasisAndPause")]
    public async Task RecoveryPreservesRemainingTimeAcrossIndependentFreezeSources(string kind)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid arm = default;
        await pair.Server.WaitAssertion(() => (patient, arm) = SpawnInjury(pair.Server.EntMan));
        await pair.RunTicksSync(pair.SecondsToTicks(4));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var metadata = entities.System<MetaDataSystem>();
            if (kind is "stasis" or "stasisAndPause")
                entities.EnsureComponent<CMInStasisComponent>(patient);
            if (kind is "patient" or "both" or "stasisAndPause")
                metadata.SetEntityPaused(patient, true);
            if (kind is "part" or "both" or "stasisAndPause")
                metadata.SetEntityPaused(arm, true);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(20));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertRecovery(entities, arm, 0);
            var metadata = entities.System<MetaDataSystem>();
            // Parent first intentionally exercises resume while the part's own
            // AutoPaused clock is still frozen.
            metadata.SetEntityPaused(patient, false);
            if (kind == "stasisAndPause")
                entities.RemoveComponent<CMInStasisComponent>(patient);
            metadata.SetEntityPaused(arm, false);
            if (kind == "stasis")
                entities.RemoveComponent<CMInStasisComponent>(patient);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await pair.Server.WaitAssertion(() => AssertRecovery(pair.Server.EntMan, arm, 0));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() =>
        {
            AssertRecovery(pair.Server.EntMan, arm, 1);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BriefStasisAndDetachedTimeDoNotBecomeRecoveryTime()
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid arm = default;
        EntityUid torso = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, arm) = SpawnInjury(entities);
            Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                new CMUMedicalBodyPartKey(BodyPartType.Torso, BodyPartSymmetry.None), out torso), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(4));
        await pair.Server.WaitPost(() => pair.Server.EntMan.EnsureComponent<CMInStasisComponent>(patient));
        await pair.RunTicksSync(pair.SecondsToTicks(0.4f));
        await pair.Server.WaitPost(() => pair.Server.EntMan.RemoveComponent<CMInStasisComponent>(patient));
        await pair.Server.WaitAssertion(() =>
            Assert.That(pair.Server.EntMan.System<SharedBodySystem>().RemoveOrgan(arm), Is.True));
        await pair.RunTicksSync(pair.SecondsToTicks(20));
        await pair.Server.WaitAssertion(() =>
        {
            AssertRecovery(pair.Server.EntMan, arm, 0);
            Assert.That(pair.Server.EntMan.System<SharedBodySystem>().AttachPart(torso, "left_arm", arm), Is.True);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(5));
        await pair.Server.WaitAssertion(() => AssertRecovery(pair.Server.EntMan, arm, 0));
        await pair.RunTicksSync(pair.SecondsToTicks(2));
        await pair.Server.WaitAssertion(() =>
        {
            AssertRecovery(pair.Server.EntMan, arm, 1);
            pair.Server.EntMan.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(FractureSeverity.Simple, false)]
    [TestCase(FractureSeverity.Compound, false)]
    [TestCase(FractureSeverity.Shattered, false)]
    [TestCase(FractureSeverity.Hairline, true)]
    public async Task RecoveryDoesNotBypassTreatmentRequirements(FractureSeverity severity, bool malunion)
    {
        await using var pair = await PoolManager.GetServerClient();
        EntityUid patient = default;
        EntityUid arm = default;
        FixedPoint2 original = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            (patient, arm) = SpawnInjury(entities, severity);
            original = entities.GetComponent<BoneComponent>(arm).Integrity;
            if (malunion)
                entities.EnsureComponent<CMUMalunionComponent>(arm);
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertIntegrity(entities, arm, original);
            entities.System<StatusEffectsSystem>().TrySetStatusEffectDuration(patient,
                "StatusEffectCMUBoneRegenBoost", TimeSpan.FromSeconds(20));
        });
        await pair.RunTicksSync(pair.SecondsToTicks(11));
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            AssertIntegrity(entities, arm,
                severity == FractureSeverity.Shattered || malunion ? original : original + 1);
            entities.DeleteEntity(patient);
        });
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Patient, EntityUid Arm) SpawnInjury(IEntityManager entities,
        FractureSeverity severity = FractureSeverity.Hairline)
    {
        var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
        Assert.That(entities.System<SharedBoneSystem>().SeedFracture(arm, severity), Is.True);
        return (patient, arm);
    }

    // A human arm overrides the component default Hairline threshold (65, not 80).
    // The configured starting point is independent of the one-unit recovery contract.
    private static void AssertRecovery(IEntityManager entities, EntityUid part, int recovered)
    {
        var bone = entities.GetComponent<BoneComponent>(part);
        AssertIntegrity(entities, part, bone.FractureThresholds[FractureSeverity.Hairline] + recovered);
    }
    private static void AssertDeficit(IEntityManager entities, EntityUid part, int deficit)
        => AssertIntegrity(entities, part, entities.GetComponent<BoneComponent>(part).IntegrityMax - deficit);
    private static void AssertIntegrity(IEntityManager entities, EntityUid part, FixedPoint2 expected)
        => Assert.That(entities.GetComponent<BoneComponent>(part).Integrity, Is.EqualTo(expected));
}

[RegisterComponent]
public sealed partial class CMUBoneRecoveryReentryProbeComponent : Component
{
    public bool Replace;
    public bool Removed;
}

public sealed partial class CMUBoneRecoveryReentryProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBoneRecoveryReentryProbeComponent, FractureSeverityChangedEvent>(OnFractureChanged);
    }

    private void OnFractureChanged(Entity<CMUBoneRecoveryReentryProbeComponent> ent,
        ref FractureSeverityChangedEvent args)
    {
        if (args.New != FractureSeverity.None)
            return;
        RemComp<BoneComponent>(ent.Owner);
        ent.Comp.Removed = true;
        if (ent.Comp.Replace)
            AddComp<BoneComponent>(ent.Owner);
    }
}
