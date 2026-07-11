using System.Linq;
using Content.Server._CMU14.Medical.Treatment.FieldCare;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FieldCare;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical.Treatment.FieldCare;

[TestFixture]
public sealed class CMUMedicalFieldMixingTest
{
    [TestCase(0, 5)]
    [TestCase(1, 5)]
    [TestCase(2, 2)]
    [TestCase(3, 1)]
    [TestCase(4, 1)]
    public async Task IngredientCostScalesWithMedicalSkill(int skill, int expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var system = server.EntMan.System<CMUMedicalFieldMixingSystem>();
            Assert.That(system.ResolveIngredientUnitCost(skill), Is.EqualTo(expected));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalTwoConsumesTwoIngredientUnitsAndOnePackedTraumaBase()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 2);

                Assert.That(system.TryMixTreatment(user, ingredient, baseItem, out product), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(48));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(49));
                    Assert.That(product, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(product!.Value).EntityPrototype?.ID, Is.EqualTo("CMTraumaKit1"));
                });
            }
            finally
            {
                entMan.DeleteEntity(user);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(baseItem);
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CoagulantCannotUseGauzeBase()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMGauze10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 0);

                Assert.That(system.TryMixTreatment(user, ingredient, baseItem, out product), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(50));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(10));
                    Assert.That(product, Is.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(user);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(baseItem);
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TraumaFoamCannotUsePackedTraumaBase()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUTraumaFoam", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 4);

                Assert.That(system.TryMixTreatment(user, ingredient, baseItem, out product), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(50));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(50));
                    Assert.That(product, Is.Null);
                });
            }
            finally
            {
                entMan.DeleteEntity(user);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(baseItem);
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalCraftingOptionsListHeldIngredientAndBasePair()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 2);
                Assert.That(hands.TryPickupAnyHand(user, ingredient, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, baseItem, checkActionBlocker: false), Is.True);

                var options = system.GetCraftableOptions(user).ToArray();

                Assert.That(options, Has.Length.EqualTo(1));
                Assert.Multiple(() =>
                {
                    Assert.That(options[0].Family, Is.EqualTo(CMUFieldTreatmentFamily.Hemostatic));
                    Assert.That(options[0].BaseKind, Is.EqualTo(CMUFieldTreatmentBaseKind.TraumaDressing));
                    Assert.That(options[0].Product, Is.EqualTo("CMTraumaKit1"));
                    Assert.That(options[0].IngredientCost, Is.EqualTo(2));
                });
            }
            finally
            {
                entMan.DeleteEntity(baseItem);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalCraftingMenuCraftsTraumaDressingFromHeldStacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 4);
                Assert.That(hands.TryPickupAnyHand(user, ingredient, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, baseItem, checkActionBlocker: false), Is.True);

                Assert.That(system.TryCraftAvailableTreatment(
                    user,
                    CMUFieldTreatmentFamily.Hemostatic,
                    CMUFieldTreatmentBaseKind.TraumaDressing,
                    out product), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(49));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(49));
                    Assert.That(product, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(product!.Value).EntityPrototype?.ID, Is.EqualTo("CMTraumaKit1"));
                });
            }
            finally
            {
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
                entMan.DeleteEntity(baseItem);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BurnGelAndPackedTraumaBaseCraftsBaseBurnKit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUBurnGel", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 4);

                Assert.That(system.TryMixTreatment(user, ingredient, baseItem, out product), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(49));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(49));
                    Assert.That(product, Is.Not.Null);
                    Assert.That(entMan.GetComponent<MetaDataComponent>(product!.Value).EntityPrototype?.ID, Is.EqualTo("CMBurnKit1"));
                });
            }
            finally
            {
                entMan.DeleteEntity(user);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(baseItem);
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalCraftingMenuFailurePreservesIngredientAndBaseStacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();
            var skills = entMan.System<SkillsSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder1", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 0);
                Assert.That(hands.TryPickupAnyHand(user, ingredient, checkActionBlocker: false), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, baseItem, checkActionBlocker: false), Is.True);

                Assert.That(system.TryCraftAvailableTreatment(
                    user,
                    CMUFieldTreatmentFamily.Hemostatic,
                    CMUFieldTreatmentBaseKind.Gauze,
                    out product), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(product, Is.Null);
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(1));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(50));
                });
            }
            finally
            {
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
                entMan.DeleteEntity(baseItem);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalCraftingMenuRejectsXenoUsers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var system = entMan.System<CMUMedicalFieldMixingSystem>();

            var user = entMan.SpawnEntity("CMXenoRunner", MapCoordinates.Nullspace);
            var ingredient = entMan.SpawnEntity("CMUCoagulantPowder", MapCoordinates.Nullspace);
            var baseItem = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);

            EntityUid? product = null;
            try
            {
                Assert.That(system.TryCraftAvailableTreatment(
                    user,
                    CMUFieldTreatmentFamily.Hemostatic,
                    CMUFieldTreatmentBaseKind.Gauze,
                    out product), Is.False);

                Assert.Multiple(() =>
                {
                    Assert.That(product, Is.Null);
                    Assert.That(entMan.GetComponent<StackComponent>(ingredient).Count, Is.EqualTo(50));
                    Assert.That(entMan.GetComponent<StackComponent>(baseItem).Count, Is.EqualTo(50));
                });
            }
            finally
            {
                if (product is { } mixed && entMan.EntityExists(mixed))
                    entMan.DeleteEntity(mixed);
                entMan.DeleteEntity(baseItem);
                entMan.DeleteEntity(ingredient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MechanismTreatmentOnlyTreatsMatchingWoundAsAdequate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 20),
                    impact: DamageImpact.Projectile), Is.True);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 20),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                var bulletIndex = FindMechanism(ledger, wounds, WoundMechanism.Bullet);
                var slashIndex = FindMechanism(ledger, wounds, WoundMechanism.Slash);

                Assert.That(bulletIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(slashIndex, Is.GreaterThanOrEqualTo(0));

                Assert.That(woundsSystem.TryTreatWounds(
                    torso,
                    WoundType.Brute,
                    1,
                    out var treated,
                    mechanismMask: WoundMechanismFlags.Bullet,
                    quality: WoundTreatmentQuality.Optimal), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(treated, Is.EqualTo(1));
                    Assert.That(entries[bulletIndex].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[bulletIndex].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entries[slashIndex].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                    Assert.That(entries[slashIndex].Wound.Treated, Is.False);
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
    public async Task PlainGauzeStopsSurfaceBleedingWithoutTreatingWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    arm,
                    Damage("Slash", 20),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(arm);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Moderate));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                    Assert.That(entries[0].Wound.Treated, Is.False);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(49));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlainGauzeCannotStopArterialBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);

            try
            {
                var arm = GetBodyPart(entMan, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    arm,
                    Damage("Slash", 80),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(arm);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                    Assert.That(entries[0].Wound.Treated, Is.False);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(50));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlainGauzeStopsTorsoBleedingWithoutTreatingWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Slash", 20),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Moderate));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                    Assert.That(entries[0].Wound.Treated, Is.False);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(49));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlainTraumaDressingStopsArterialBleedingAfterDoAfterWithoutTreatingWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid user = default;
        EntityUid patient = default;
        EntityUid torso = default;
        EntityUid trauma = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            trauma = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);
            Assert.That(hands.TryPickupAnyHand(user, trauma, checkActionBlocker: false), Is.True);

            torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
            Assert.That(partHealth.TryApplyPartDamage(
                patient,
                torso,
                Damage("Slash", 80),
                impact: DamageImpact.MeleeSlash), Is.True);

            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));

            var interact = new AfterInteractEvent(user, trauma, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(trauma, interact);

            Assert.Multiple(() =>
            {
                Assert.That(interact.Handled, Is.True);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(50));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(2f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var entries = ledger.GetEntries(wounds);

            Assert.Multiple(() =>
            {
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                Assert.That(entries[0].Wound.Treated, Is.False);
                Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(49));
            });

            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlainTraumaDressingStopsArterialBleedingInstantlyForCorpsman()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var skills = entMan.System<SkillsSystem>();

            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var trauma = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 2);
                Assert.That(hands.TryPickupAnyHand(user, trauma, checkActionBlocker: false), Is.True);

                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Slash", 80),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));

                var interact = new AfterInteractEvent(user, trauma, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(trauma, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                    Assert.That(entries[0].Wound.Treated, Is.False);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
                    Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(49));
                });
            }
            finally
            {
                entMan.DeleteEntity(trauma);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedGauzeTreatsWoundAndStopsNonArterialBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUSealingGauze6", MapCoordinates.Nullspace);

            try
            {
                targeting.SelectZone((user, null), TargetBodyZone.Chest);
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Slash", 20),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Moderate));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(9));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedGauzeDoesNotRetargetTreatedWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUSealingGauze6", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Slash", 10),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.True);
                Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(10));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CleanupTreatmentDoesNotRetargetTreatedWoundsAcrossDamageTypes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var antiseptic = entMan.SpawnEntity("CMUAntisepticGauze6", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Heat", 10),
                    impact: new DamageImpact(DamageImpactDelivery.Contact, DamageImpactContact.Burn, DamageImpactPenetration.None, DamageImpactEnergy.Medium)), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.True);
                Assert.That(entries[0].Wound.Type, Is.EqualTo(WoundType.Burn));
                Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));

                var interact = new AfterInteractEvent(user, antiseptic, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(antiseptic, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(entries[0].Cleanup & WoundCleanupFlags.DirtyDressing, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entries[0].Cleanup & WoundCleanupFlags.CharredTissue, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entMan.GetComponent<StackComponent>(antiseptic).Count, Is.EqualTo(10));
                });
            }
            finally
            {
                entMan.DeleteEntity(antiseptic);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedGauzeTreatsWoundButCannotStopArterialBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var targeting = entMan.System<SharedBodyZoneTargetingSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUSealingGauze6", MapCoordinates.Nullspace);

            try
            {
                targeting.SelectZone((user, null), TargetBodyZone.Chest);
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(partHealth.TryApplyPartDamage(
                    patient,
                    torso,
                    Damage("Slash", 80),
                    impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));

                var interact = new AfterInteractEvent(user, gauze, patient, default, true);
                entMan.EventBus.RaiseLocalEvent(gauze, interact);

                Assert.Multiple(() =>
                {
                    Assert.That(interact.Handled, Is.True);
                    Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(9));
                });
            }
            finally
            {
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedTraumaDressingTreatsWoundAndStopsArterialBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        EntityUid user = default;
        EntityUid patient = default;
        EntityUid torso = default;
        EntityUid trauma = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var hands = entMan.System<SharedHandsSystem>();

            user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            trauma = entMan.SpawnEntity("CMUSealingTraumaDressing4", MapCoordinates.Nullspace);
            Assert.That(hands.TryPickupAnyHand(user, trauma, checkActionBlocker: false), Is.True);

            torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
            Assert.That(partHealth.TryApplyPartDamage(
                patient,
                torso,
                Damage("Slash", 80),
                impact: DamageImpact.MeleeSlash), Is.True);

            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));

            var interact = new AfterInteractEvent(user, trauma, patient, default, true);
            entMan.EventBus.RaiseLocalEvent(trauma, interact);

            Assert.Multiple(() =>
            {
                Assert.That(interact.Handled, Is.True);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(6));
            });
        });

        await pair.RunTicksSync(pair.SecondsToTicks(6f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var entries = ledger.GetEntries(wounds);

            Assert.Multiple(() =>
            {
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
                Assert.That(entries[0].Wound.Treated, Is.True);
                Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(5));
            });

            entMan.DeleteEntity(patient);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task Au14HemostaticGauzeMatchesCmuHemostaticTreatment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var cmu = entMan.SpawnEntity("CMUHemostaticGauze1", MapCoordinates.Nullspace);
            var au14 = entMan.SpawnEntity("AU14HemostaticGauze", MapCoordinates.Nullspace);

            try
            {
                var expected = entMan.GetComponent<WoundTreaterComponent>(cmu);
                var actual = entMan.GetComponent<WoundTreaterComponent>(au14);

                Assert.Multiple(() =>
                {
                    Assert.That(actual.Wound, Is.EqualTo(expected.Wound));
                    Assert.That(actual.Treats, Is.EqualTo(expected.Treats));
                    Assert.That(actual.InstantWoundTreatment, Is.EqualTo(expected.InstantWoundTreatment));
                    Assert.That(actual.WoundsTreatedPerUse, Is.EqualTo(expected.WoundsTreatedPerUse));
                    Assert.That(actual.Group, Is.EqualTo(expected.Group));
                    Assert.That(actual.Consumable, Is.EqualTo(expected.Consumable));
                    Assert.That(actual.CanUseUnskilled, Is.EqualTo(expected.CanUseUnskilled));
                    Assert.That(actual.CMUMechanisms, Is.EqualTo(expected.CMUMechanisms));
                    Assert.That(actual.CMUTreatmentQuality, Is.EqualTo(expected.CMUTreatmentQuality));
                    Assert.That(actual.CMUCleanupClears, Is.EqualTo(expected.CMUCleanupClears));
                    Assert.That(actual.CMUStopsArterialBleeding, Is.EqualTo(expected.CMUStopsArterialBleeding));
                });
            }
            finally
            {
                entMan.DeleteEntity(cmu);
                entMan.DeleteEntity(au14);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PreparedTreatmentStacksUseUpdatedLimits()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var packedGauze = entMan.SpawnEntity("CMUPlainGauze10", MapCoordinates.Nullspace);
            var packedTrauma = entMan.SpawnEntity("CMUPlainTraumaDressing10", MapCoordinates.Nullspace);
            var gauze = entMan.SpawnEntity("CMUHemostaticGauze6", MapCoordinates.Nullspace);
            var trauma = entMan.SpawnEntity("CMUHemostaticTraumaDressing4", MapCoordinates.Nullspace);

            try
            {
                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<StackComponent>(packedGauze).Count, Is.EqualTo(50));
                    Assert.That(entMan.GetComponent<StackComponent>(packedTrauma).Count, Is.EqualTo(50));
                    Assert.That(entMan.GetComponent<StackComponent>(gauze).Count, Is.EqualTo(10));
                    Assert.That(entMan.GetComponent<StackComponent>(trauma).Count, Is.EqualTo(6));
                });
            }
            finally
            {
                entMan.DeleteEntity(packedGauze);
                entMan.DeleteEntity(packedTrauma);
                entMan.DeleteEntity(gauze);
                entMan.DeleteEntity(trauma);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetBodyPart(
        IEntityManager entMan,
        EntityUid bodyUid,
        BodyPartType type,
        BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType != type)
                continue;
            if (symmetry != BodyPartSymmetry.None && part.Symmetry != symmetry)
                continue;

            return partUid;
        }

        Assert.Fail($"Expected CMU human to have {symmetry} {type}.");
        return EntityUid.Invalid;
    }

    private static int FindMechanism(
        CMUWoundLedgerSystem ledger,
        BodyPartWoundComponent wounds,
        WoundMechanism mechanism)
    {
        var entries = ledger.GetEntries(wounds);
        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i].Mechanism == mechanism)
                return i;
        }

        return -1;
    }

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }
}
