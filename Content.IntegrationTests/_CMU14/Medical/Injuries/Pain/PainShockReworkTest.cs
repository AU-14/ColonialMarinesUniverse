using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Treatment.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Stomach;
using Content.Shared.CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.EntityConditions.Conditions;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.EntityEffects.Effects.StatusEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Metabolism;
using Content.Shared.Verbs;
using Content.Server.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Reflection;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Pain;

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
    private static readonly ProtoId<MetabolismStagePrototype> Bloodstream = "Bloodstream";

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

}
