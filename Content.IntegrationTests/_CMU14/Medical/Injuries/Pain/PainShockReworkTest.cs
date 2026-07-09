using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Prototypes;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.EffectConditions;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Content.Server.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Reflection;

namespace Content.IntegrationTests._CMU14.Medical.Injuries.Pain;

[TestFixture]
public sealed class PainShockReworkTest
{
    private static readonly ProtoId<ReagentPrototype> Paracetamol = "CMUParacetamol";
    private static readonly ProtoId<ReagentPrototype> Tramadol = "CMUTramadol";
    private static readonly ProtoId<ReagentPrototype> Sleen = "CMUSleen";
    private static readonly ProtoId<ReagentPrototype> Oxycodone = "CMUOxycodone";
    private static readonly ProtoId<ReagentPrototype> Soporific = "CMUSoporific";
    private static readonly ProtoId<ReagentPrototype> Epinephrine = "CMEpinephrine";
    private static readonly ProtoId<ReagentPrototype> Inaprovaline = "CMInaprovaline";

    [Test]
    public async Task ShatteredFractureAloneIsSeverePressureNotShock()
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
                fracture.SetSeverity((part, frac), FractureSeverity.Shattered);

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
                fracture.SetSeverity((part, frac), FractureSeverity.Shattered);
                AddWound(entMan, part, WoundSize.CutMassive, treated: false);
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
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var pain = entMan.System<SharedPainShockSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                var wounds = AddWound(entMan, part, WoundSize.CutMassive, treated: false);

                Assert.That(pain.ComputePainSourceProfile(human).Target.Float(), Is.EqualTo(50f).Within(0.001f));

                var entry = ledger.GetEntries(wounds)[0];
                Assert.That(ledger.TryUpdateEntry(wounds, 0, entry with
                {
                    Wound = entry.Wound with { Treated = true },
                    TreatmentQuality = WoundTreatmentQuality.Adequate,
                }), Is.True);
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
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 2, 12f);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(part);
                Assert.That(HasRetainedFragmentCleanup(ledger, wounds), Is.True);

                var extractor = entMan.EnsureComponent<CMUShrapnelExtractorComponent>(tool);

                Assert.That(shrapnel.TryExtractShrapnel(human, (tool, extractor), out var removed), Is.True);
                Assert.That(removed, Is.EqualTo(2));
                Assert.That(entMan.HasComponent<CMUShrapnelComponent>(part), Is.False);
                Assert.That(HasRetainedFragmentCleanup(ledger, wounds), Is.False);
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
    public async Task ShrapnelExtractionAlternativeVerbRequiresHeldExtractor()
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

                var withoutTool = verbs.GetLocalVerbs(patient, user, typeof(AlternativeVerb), force: true);
                Assert.That(ContainsVerb(withoutTool, "Remove shrapnel"), Is.False);

                Assert.That(hands.TryPickupAnyHand(user, tool, checkActionBlocker: false), Is.True);

                var withTool = verbs.GetLocalVerbs(patient, user, typeof(AlternativeVerb), force: true);
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
    public async Task UseInHandExtractorStartsSelfShrapnelRemoval()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = entMan.System<SharedHandsSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity("KitchenKnife", MapCoordinates.Nullspace);

            try
            {
                var part = GetFirstPart(entMan, human);
                shrapnel.AddShrapnel(part, 2, 12f);

                Assert.That(hands.TryPickupAnyHand(human, tool, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryUseItemInHand(human), Is.True);
                Assert.That(entMan.TryGetComponent<DoAfterComponent>(human, out var doAfter), Is.True);
                Assert.That(HasActiveShrapnelExtractionDoAfter(doAfter!), Is.True);
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
    public async Task ShrapnelExtractionDoAfterRepeatsUntilFragmentsAreGone()
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

                entMan.EnsureComponent<CMUShrapnelExtractorComponent>(tool);
                var ev = new CMUShrapnelExtractDoAfterEvent();
                ev.DoAfter = new DoAfter(
                    0,
                    new DoAfterArgs(entMan, human, TimeSpan.FromSeconds(1), ev, tool, target: human, used: tool),
                    TimeSpan.Zero);

                entMan.EventBus.RaiseLocalEvent(tool, ev);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<CMUShrapnelComponent>(part).Fragments, Is.EqualTo(3));
                    Assert.That(ev.Repeat, Is.True);
                    Assert.That(ev.PreSelectedPart, Is.Not.Null);
                });

                entMan.EventBus.RaiseLocalEvent(tool, ev);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<CMUShrapnelComponent>(part).Fragments, Is.EqualTo(1));
                    Assert.That(ev.Repeat, Is.True);
                    Assert.That(ev.PreSelectedPart, Is.Not.Null);
                });

                entMan.EventBus.RaiseLocalEvent(tool, ev);
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUShrapnelComponent>(part), Is.False);
                    Assert.That(ev.Repeat, Is.False);
                    Assert.That(ev.PreSelectedPart, Is.Null);
                });
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
    public async Task ShrapnelExtractionDamageDoesNotFractureOrCauseInternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaPierceBoneChance, 1f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaPierceVascularChance, 1f);

            var entMan = server.EntMan;
            var hitLocation = entMan.System<SharedHitLocationSystem>();
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var tool = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso, BodyPartSymmetry.None);
                shrapnel.AddShrapnel(torso, 1, 12f);
                AssertNoFracturesOrInternalBleeding(entMan, human);

                hitLocation.SetForcedHit(human, BodyPartType.Torso);
                var extractor = entMan.EnsureComponent<CMUShrapnelExtractorComponent>(tool);
                SetField(extractor, nameof(CMUShrapnelExtractorComponent.DamageOnExtract), FixedPoint2.New(100));
                SetField(extractor, nameof(CMUShrapnelExtractorComponent.PainPenalty), 0f);

                Assert.That(shrapnel.TryExtractShrapnel(human, (tool, extractor), out var removed, human, torso), Is.True);
                Assert.That(removed, Is.EqualTo(1));
                AssertNoFracturesOrInternalBleeding(entMan, human);
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

                var shrapnelOnlyPulse = shrapnel.ComputeMovementPainPulse(human);
                Assert.That(shrapnelOnlyPulse, Is.EqualTo(1f).Within(0.001f));

                var leg = GetBodyPart(entMan, human, BodyPartType.Leg, BodyPartSymmetry.Left);
                var frac = entMan.EnsureComponent<FractureComponent>(leg);
                fracture.SetSeverity((leg, frac), FractureSeverity.Simple);

                Assert.That(shrapnel.ComputeMovementPainPulse(human), Is.EqualTo(shrapnelOnlyPulse + 8f).Within(0.001f));
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
                prototypes.Index(Tramadol),
                FixedPoint2.New(30));

            AssertPainkillerHasDrunkOverdoseEffect(
                prototypes.Index(Oxycodone),
                FixedPoint2.New(20));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChemistryPainkillersMapToSurgeryPainControl()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();

            var paracetamol = prototypes.Index(Paracetamol);
            var tramadol = prototypes.Index(Tramadol);
            var sleen = prototypes.Index(Sleen);
            var oxycodone = prototypes.Index(Oxycodone);
            var soporific = prototypes.Index(Soporific);

            var paracetamolSuppression = AssertMedicinePainSuppression(paracetamol);
            var tramadolSuppression = AssertMedicinePainSuppression(tramadol);
            var sleenSuppression = AssertMedicinePainSuppression(sleen);
            var oxycodoneSuppression = AssertMedicinePainSuppression(oxycodone);
            var soporificSuppression = AssertMedicinePainSuppression(soporific);

            Assert.Multiple(() =>
            {
                Assert.That(paracetamol.Overdose, Is.EqualTo(FixedPoint2.New(60)));
                Assert.That(paracetamol.CriticalOverdose, Is.EqualTo(FixedPoint2.New(100)));
                Assert.That(tramadol.Overdose, Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(tramadol.CriticalOverdose, Is.EqualTo(FixedPoint2.New(50)));
                Assert.That(oxycodone.Overdose, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(oxycodone.CriticalOverdose, Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(sleen.Overdose, Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(sleen.CriticalOverdose, Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(soporific.Overdose, Is.EqualTo(FixedPoint2.New(30)));
                Assert.That(soporific.CriticalOverdose, Is.EqualTo(FixedPoint2.New(50)));

                Assert.That(paracetamolSuppression.TierSuppression, Is.EqualTo(1));
                Assert.That(tramadolSuppression.TierSuppression, Is.EqualTo(2));
                Assert.That(sleenSuppression.TierSuppression, Is.EqualTo(3));
                Assert.That(oxycodoneSuppression.TierSuppression, Is.EqualTo(4));
                Assert.That(soporificSuppression.TierSuppression, Is.EqualTo(5));

                Assert.That(IsEnoughForSurgeryPainControl(paracetamolSuppression), Is.False);
                Assert.That(IsEnoughForSurgeryPainControl(tramadolSuppression), Is.True);
                Assert.That(IsEnoughForSurgeryPainControl(sleenSuppression), Is.True);
                Assert.That(IsEnoughForSurgeryPainControl(oxycodoneSuppression), Is.True);
                Assert.That(IsEnoughForSurgeryPainControl(soporificSuppression), Is.True);
            });

            AssertReagentAppliesStatusEffect(soporific, "StatusEffectDrowsiness");
            AssertReagentAppliesStatusEffect(soporific, "StatusEffectForcedSleeping");
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
                prototypes.Index(Epinephrine),
                1);
            var inaprovaline = AssertReagentHasPainSuppression(
                prototypes.Index(Inaprovaline),
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
        var ledger = entMan.System<CMUWoundLedgerSystem>();
        var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(part);
        Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
            new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, treated),
            size,
            0,
            WoundMechanism.Generic,
            WoundMechanismFlags.None,
            treated ? WoundTreatmentQuality.Adequate : WoundTreatmentQuality.Untreated,
            WoundCleanupFlags.None)), Is.GreaterThanOrEqualTo(0));
        return wounds;
    }

    private static bool HasRetainedFragmentCleanup(CMUWoundLedgerSystem ledger, BodyPartWoundComponent comp)
    {
        foreach (var entry in ledger.GetEntries(comp))
        {
            if ((entry.Cleanup & WoundCleanupFlags.RetainedFragment) != WoundCleanupFlags.None)
                return true;
        }

        return false;
    }

    private static bool HasActiveShrapnelExtractionDoAfter(DoAfterComponent comp)
    {
        foreach (var doAfter in comp.DoAfters.Values)
        {
            if (!doAfter.Cancelled &&
                !doAfter.Completed &&
                doAfter.Args.Event is CMUShrapnelExtractDoAfterEvent)
            {
                return true;
            }
        }

        return false;
    }

    private static void AssertNoFracturesOrInternalBleeding(IEntityManager entMan, EntityUid bodyUid)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, _) in body.GetBodyChildren(bodyUid))
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<FractureComponent>(partUid), Is.False);
                Assert.That(entMan.HasComponent<InternalBleedingComponent>(partUid), Is.False);
            });
        }
    }

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

    private static CMUApplyPainSuppressionEffect AssertMedicinePainSuppression(ReagentPrototype reagent)
    {
        var metabolism = GetMedicineMetabolism(reagent);
        foreach (var effect in metabolism.Effects)
        {
            if (effect is CMUApplyPainSuppressionEffect suppression)
                return suppression;
        }

        Assert.Fail($"{reagent.ID} must apply CMU pain suppression.");
        return default!;
    }

    private static bool IsEnoughForSurgeryPainControl(CMUApplyPainSuppressionEffect suppression)
        => suppression.AccumulationSuppression >= 0.5f || suppression.TierSuppression >= 2;

    private static void AssertReagentAppliesStatusEffect(ReagentPrototype reagent, EntProtoId effectProto)
    {
        var metabolism = GetMedicineMetabolism(reagent);
        foreach (var effect in metabolism.Effects)
        {
            if (effect is ModifyStatusEffect statusEffect && statusEffect.EffectProto == effectProto)
                return;
        }

        Assert.Fail($"{reagent.ID} must apply status effect {effectProto}.");
    }

    private static ReagentEffectsEntry GetMedicineMetabolism(ReagentPrototype reagent)
    {
        Assert.That(reagent.Metabolisms, Is.Not.Null, reagent.ID);
        Assert.That(reagent.Metabolisms!.TryGetValue(new ProtoId<MetabolismGroupPrototype>("Medicine"), out var metabolism), Is.True, reagent.ID);
        return metabolism!;
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
