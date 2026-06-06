using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.EntityEffects;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._CMU14.Medical.Organs.Stomach;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.Stabilizers;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.EffectConditions;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Content.Server.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Reflection;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class PainShockReworkTest
{
    [Test]
    public async Task ComminutedFractureAloneIsSeverePressureNotShock()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var frac = entMan.EnsureComponent<FractureComponent>(part);
                fracture.SetSeverity((part, frac), FractureSeverity.Comminuted);

                var profile = pain.ComputePainSourceProfile(human);
                var rawTier = PainTierThresholds.Get(PainTier.None, profile.Target, 0f, pain.ShockThreshold);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Target.Float(), Is.EqualTo(65f).Within(0.001f));
                    Assert.That(profile.RiseRate.Float(), Is.EqualTo(3.25f).Within(0.001f));
                    Assert.That(rawTier, Is.EqualTo(PainTier.Severe));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StackedSeriousSourcesCanReachShockPressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var fracture = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var frac = entMan.EnsureComponent<FractureComponent>(part);
                fracture.SetSeverity((part, frac), FractureSeverity.Comminuted);
                AddWound(entMan, part, WoundSize.Massive, treated: false);
                entMan.EnsureComponent<CMUEscharComponent>(part);

                var profile = pain.ComputePainSourceProfile(human);
                var rawTier = PainTierThresholds.Get(PainTier.None, profile.Target, 0f, pain.ShockThreshold);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Target.Float(), Is.EqualTo(95f).Within(0.001f));
                    Assert.That(profile.RiseRate.Float(), Is.EqualTo(4f).Within(0.001f));
                    Assert.That(rawTier, Is.EqualTo(PainTier.Shock));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingWoundsRemovesTheirPainFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var wounds = AddWound(entMan, part, WoundSize.Massive, treated: false);

                Assert.That(pain.ComputePainSourceProfile(human).Target.Float(), Is.EqualTo(50f).Within(0.001f));

                var woundList = WoundsOf(wounds);
                woundList[0] = woundList[0] with { Treated = true };
                Assert.That(pain.ComputePainSourceProfile(human).Target, Is.EqualTo(FixedPoint2.Zero));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelAddsPainPressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 3, 18f);

                var profile = pain.ComputePainSourceProfile(human);

                Assert.Multiple(() =>
                {
                    Assert.That(profile.Target.Float(), Is.GreaterThanOrEqualTo(18f));
                    Assert.That(profile.RiseRate.Float(), Is.GreaterThan(0f));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelExtractionRemovesFragments()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 5, 25f);

                var extractor = entMan.EnsureComponent<CMUShrapnelExtractorComponent>(tool);

                Assert.That(shrapnel.TryExtractShrapnel(human, (tool, extractor), out var removed), Is.True);
                Assert.That(removed, Is.EqualTo(2));
                Assert.That(entMan.GetComponent<CMUShrapnelComponent>(part).Fragments, Is.EqualTo(3));
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(tool);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelExtractionClearsRetainedFragmentCleanupWhenLastFragmentRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 2, 12f);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(part);
                Assert.That(HasRetainedFragmentCleanup(wounds), Is.True);

                var extractor = entMan.EnsureComponent<CMUShrapnelExtractorComponent>(tool);

                Assert.That(shrapnel.TryExtractShrapnel(human, (tool, extractor), out var removed), Is.True);
                Assert.That(removed, Is.EqualTo(2));
                Assert.That(entMan.HasComponent<CMUShrapnelComponent>(part), Is.False);
                Assert.That(HasRetainedFragmentCleanup(wounds), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(tool);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelExtractionVerbRequiresHeldExtractor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var verbs = entMan.System<VerbSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity("KitchenKnife", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, patient);
                shrapnel.AddShrapnel(part, 2, 12f);

                var withoutTool = verbs.GetLocalVerbs(patient, user, typeof(InteractionVerb), force: true);
                Assert.That(ContainsVerb(withoutTool, "Remove shrapnel"), Is.False);

                Assert.That(hands.TryPickupAnyHand(user, tool, checkActionBlocker: false), Is.True);

                var withTool = verbs.GetLocalVerbs(patient, user, typeof(InteractionVerb), force: true);
                Assert.That(ContainsVerb(withTool, "Remove shrapnel"), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
                entMan.DeleteEntity(tool);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShrapnelAndLegFracturesPulsePainOnMovement()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var fracture = entMan.System<SharedFractureSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 2, 12f);

                Assert.That(shrapnel.ComputeMovementPainPulse(human), Is.GreaterThan(0f));

                var leg = GetBodyPart(entMan, human, BodyPartType.Leg, BodyPartSymmetry.Left);
                var frac = entMan.EnsureComponent<FractureComponent>(leg);
                fracture.SetSeverity((leg, frac), FractureSeverity.Simple);

                Assert.That(shrapnel.ComputeMovementPainPulse(human), Is.GreaterThan(12f));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OrganPainProfilesAreOrganSpecific()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var heartPatient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var stomachPatient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                SetOrganStage<HeartComponent>(entMan, heartPatient, OrganDamageStage.Damaged);
                SetOrganStage<CMUStomachComponent>(entMan, stomachPatient, OrganDamageStage.Damaged);

                var heartProfile = pain.ComputePainSourceProfile(heartPatient);
                var stomachProfile = pain.ComputePainSourceProfile(stomachPatient);

                Assert.That(heartProfile.Target.Float(), Is.GreaterThan(stomachProfile.Target.Float()));
            }
            finally
            {
                entMan.DeleteEntity(heartPatient);
                entMan.DeleteEntity(stomachPatient);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StabilizedOrganReducesOrganPainPressure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var timing = server.ResolveDependency<Robust.Shared.Timing.IGameTiming>();
            var pain = entMan.System<SharedPainShockSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                SetOrganStage<LungsComponent>(entMan, human, OrganDamageStage.Damaged);

                var baseline = pain.ComputePainSourceProfile(human);
                var stabilized = entMan.EnsureComponent<CMUOrganStabilizedComponent>(human);
                SetField(stabilized, nameof(CMUOrganStabilizedComponent.Target), CMUOrganStabilizerTarget.Lungs);
                SetField(stabilized, nameof(CMUOrganStabilizedComponent.ExpiresAt), timing.CurTime + TimeSpan.FromMinutes(2));

                var suppressed = pain.ComputePainSourceProfile(human);

                Assert.That(suppressed.Target.Float(), Is.LessThan(baseline.Target.Float()));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OxyMasksShockAndWeakerMedsDoNotInheritItsStrength()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid human = default;
        await server.WaitPost(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();

            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var comp = entMan.EnsureComponent<PainShockComponent>(human);
            comp.Pain = 90;
            comp.PainTarget = 90;
            comp.CachedRiseRate = 0;

            pain.AddPainSuppressionProfile(human, 0.75f, 4, 1.25f, TimeSpan.FromSeconds(1));
            pain.AddPainSuppressionProfile(human, 0.50f, 2, 0.75f, TimeSpan.FromSeconds(30));
            pain.RefreshTier(human);

            Assert.Multiple(() =>
            {
                Assert.That(comp.RawTier, Is.EqualTo(PainTier.Shock));
                Assert.That(comp.Tier, Is.EqualTo(PainTier.None));
                Assert.That(pain.GetTierSuppression(human), Is.EqualTo(4));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var pain = entMan.System<SharedPainShockSystem>();
            var comp = entMan.GetComponent<PainShockComponent>(human);

            pain.RefreshTier(human);

            Assert.Multiple(() =>
            {
                Assert.That(comp.RawTier, Is.EqualTo(PainTier.Shock));
                Assert.That(comp.Tier, Is.EqualTo(PainTier.Moderate));
                Assert.That(pain.GetTierSuppression(human), Is.EqualTo(2));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DrugPainSuppressionWeakensAsPainRises()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var painSystem = entMan.System<SharedPainShockSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var pain = entMan.EnsureComponent<PainShockComponent>(human);
                pain.Pain = 90;
                pain.PainTarget = 90;
                pain.CachedRiseRate = 0;

                painSystem.AddPainSuppressionProfile(
                    human,
                    0.75f,
                    4,
                    1.25f,
                    TimeSpan.FromSeconds(30),
                    reductionDecreaseRate: 0.25f);
                painSystem.RefreshTier(human);

                Assert.Multiple(() =>
                {
                    Assert.That(pain.RawTier, Is.EqualTo(PainTier.Shock));
                    Assert.That(painSystem.GetTierSuppression(human), Is.EqualTo(3));
                    Assert.That(pain.Tier, Is.EqualTo(PainTier.Mild));
                    Assert.That(painSystem.GetAccumulationSuppression(human), Is.LessThan(0.75f));
                    Assert.That(painSystem.GetDecayBonus(human), Is.LessThan(1.25f));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrongPainkillerOverdosesApplyDrunkenness()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            AssertPainkillerHasDrunkOverdoseEffect(
                prototypes.Index<ReagentPrototype>("CMUTramadol"),
                FixedPoint2.New(30));

            AssertPainkillerHasDrunkOverdoseEffect(
                prototypes.Index<ReagentPrototype>("CMUOxycodone"),
                FixedPoint2.New(20));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EpinephrineAndInaprovalineReducePain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var epinephrine = AssertReagentHasPainSuppression(
                prototypes.Index<ReagentPrototype>("CMEpinephrine"),
                1);
            var inaprovaline = AssertReagentHasPainSuppression(
                prototypes.Index<ReagentPrototype>("CMInaprovaline"),
                2);

            Assert.Multiple(() =>
            {
                Assert.That(epinephrine.Additive, Is.True);
                Assert.That(inaprovaline.Additive, Is.True);
                Assert.That(inaprovaline.TierSuppression, Is.GreaterThan(epinephrine.TierSuppression));
                Assert.That(inaprovaline.DecayBonus, Is.GreaterThan(epinephrine.DecayBonus));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetFirstPart(IEntityManager entMan, EntityUid bodyUid)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, _) in body.GetBodyChildren(bodyUid))
        {
            if (entMan.HasComponent<BodyPartComponent>(partUid))
                return partUid;
        }

        Assert.Fail("Expected CMU human to have at least one body part.");
        return EntityUid.Invalid;
    }

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid bodyUid, BodyPartType type, BodyPartSymmetry symmetry)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type && part.Symmetry == symmetry)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }

    private static void SetOrganStage<TOrgan>(IEntityManager entMan, EntityUid bodyUid, OrganDamageStage stage)
        where TOrgan : IComponent
    {
        var organ = GetOrgan<TOrgan>(entMan, bodyUid);
        var health = entMan.GetComponent<OrganHealthComponent>(organ);
        SetField(health, nameof(OrganHealthComponent.Stage), stage);
    }

    private static EntityUid GetOrgan<TOrgan>(IEntityManager entMan, EntityUid bodyUid)
        where TOrgan : IComponent
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var organ in body.GetBodyOrgans(bodyUid))
        {
            if (entMan.HasComponent<TOrgan>(organ.Id))
                return organ.Id;
        }

        Assert.Fail($"Expected CMU human to have organ {typeof(TOrgan).Name}.");
        return EntityUid.Invalid;
    }

    private static BodyPartWoundComponent AddWound(IEntityManager entMan, EntityUid part, WoundSize size, bool treated)
    {
        var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
        WoundsOf(wounds).Add(new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, treated));
        SizesOf(wounds).Add(size);
        BandagesOf(wounds).Add(0);
        return wounds;
    }

    private static List<Wound> WoundsOf(BodyPartWoundComponent comp)
        => GetField<List<Wound>>(comp, "Wounds");

    private static List<WoundSize> SizesOf(BodyPartWoundComponent comp)
        => GetField<List<WoundSize>>(comp, "Sizes");

    private static List<int> BandagesOf(BodyPartWoundComponent comp)
        => GetField<List<int>>(comp, "Bandages");

    private static List<WoundCleanupFlags> CleanupOf(BodyPartWoundComponent comp)
        => GetField<List<WoundCleanupFlags>>(comp, nameof(BodyPartWoundComponent.Cleanup));

    private static bool HasRetainedFragmentCleanup(BodyPartWoundComponent comp)
    {
        foreach (var cleanup in CleanupOf(comp))
        {
            if ((cleanup & WoundCleanupFlags.RetainedFragment) != WoundCleanupFlags.None)
                return true;
        }

        return false;
    }

    private static T GetField<T>(BodyPartWoundComponent comp, string name)
        => (T) typeof(BodyPartWoundComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(comp)!;

    private static void SetField<TComponent, TValue>(TComponent comp, string name, TValue value)
        => typeof(TComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.SetValue(comp, value);

    private static bool ContainsVerb(IEnumerable<Verb> verbs, string text)
    {
        foreach (var verb in verbs)
        {
            if (verb.Text == text)
                return true;
        }

        return false;
    }

    private static void AssertPainkillerHasDrunkOverdoseEffect(ReagentPrototype reagent, FixedPoint2 min)
    {
        var metabolism = reagent.Metabolisms![new ProtoId<MetabolismGroupPrototype>("Medicine")];
        foreach (var effect in metabolism.Effects)
        {
            if (effect is not Drunk drunk || !drunk.SlurSpeech)
                continue;

            if (HasReagentThreshold(effect, min))
                return;
        }

        Assert.Fail($"{reagent.ID} must apply drunkenness at overdose threshold {min}.");
    }

    private static CMUApplyPainSuppressionEffect AssertReagentHasPainSuppression(
        ReagentPrototype reagent,
        int minTierSuppression)
    {
        var metabolism = reagent.Metabolisms![new ProtoId<MetabolismGroupPrototype>("Medicine")];
        foreach (var effect in metabolism.Effects)
        {
            if (effect is not CMUApplyPainSuppressionEffect suppression)
                continue;

            Assert.Multiple(() =>
            {
                Assert.That(suppression.TierSuppression, Is.GreaterThanOrEqualTo(minTierSuppression));
                Assert.That(suppression.DurationPerUnit, Is.GreaterThan(0f));
                Assert.That(suppression.ReductionDecreaseRate, Is.EqualTo(0f));
            });

            return suppression;
        }

        Assert.Fail($"{reagent.ID} must apply CMU pain suppression.");
        return default!;
    }

    private static bool HasReagentThreshold(EntityEffect effect, FixedPoint2 min)
    {
        if (effect.Conditions == null)
            return false;

        foreach (var condition in effect.Conditions)
        {
            if (condition is ReagentThreshold threshold && threshold.Min == min)
                return true;
        }

        return false;
    }
}
