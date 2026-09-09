#pragma warning disable RA0002 // Inspect wound and part projections after actual burn, dressing and surgery interactions.
using System.Linq;
using Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Administration.Systems;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Wounds;

[TestFixture]
public sealed class EscharRecoveryPolicyTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FieldDressingRecoversBurnTissueWhileOnlyCommittedDebridementRemovesEschar(bool cancelFirst)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        EntityUid patient = default, medic = default, arm = default, kit = default, scalpel = default;
        BodyPartHealthComponent health = default!;
        FixedPoint2 healthAfterDressing = default, damageAfterDressing = default;
        var ledger = entities.System<CMUWoundLedgerSystem>();
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                medic = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                kit = entities.SpawnEntity("CMBurnKit10", map.GridCoords);
                scalpel = entities.SpawnEntity("CMScalpel", map.GridCoords);
                arm = BurnArm(entities, patient);
                health = entities.GetComponent<BodyPartHealthComponent>(arm);
                var wounds = entities.GetComponent<BodyPartWoundComponent>(arm);
                Assert.That(ledger.GetEntries(wounds).All(row => !row.Wound.Treated), Is.True);
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.True,
                    "Use the real production burn threshold, not a seeded surgical marker.");
                entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
                entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillSurgery", 3);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.LeftArm);
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, kit), Is.True);
                var treater = entities.GetComponent<WoundTreaterComponent>(kit);
                treater.InstantWoundTreatment = false;
                treater.InstantWoundTreatmentSkills.Clear();
                entities.System<TagSystem>().AddTag(medic, "InstantDoAfters");
                for (var attempt = 0; attempt < 6 && ledger.GetEntries(wounds).Any(row => !row.Wound.Treated); attempt++)
                {
                    var interaction = new AfterInteractEvent(medic, kit, patient, default, true);
                    entities.EventBus.RaiseLocalEvent(kit, interaction);
                    Assert.That(interaction.Handled, Is.True);
                }
                Assert.That(ledger.GetEntries(wounds).All(row => row.Wound.Treated), Is.True);
                Assert.That(entities.System<SharedStackSystem>().GetCount(kit), Is.LessThan(10));
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.True);
                healthAfterDressing = health.Current;
                damageAfterDressing = BurnDamage(entities, patient);
                Assert.That(damageAfterDressing, Is.GreaterThan(FixedPoint2.Zero));
                entities.System<TagSystem>().RemoveTag(medic, "InstantDoAfters");
                Assert.That(entities.System<SharedHandsSystem>().TryDrop(medic, kit), Is.True);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(6));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(health.Current, Is.GreaterThan(healthAfterDressing), "Eschar does not block treated wound recovery.");
                Assert.That(BurnDamage(entities, patient), Is.LessThan(damageAfterDressing));
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.True);
                Assert.That(entities.System<SharedPainSourceProfileSystem>().ComputePainSourceProfile(patient).Target,
                    Is.EqualTo(FixedPoint2.New(55)), "The distinct eschar pain source survives field recovery.");
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, scalpel), Is.True);
                entities.System<StandingStateSystem>().Down(patient, playSound: false, dropHeldItems: false, force: true);
                entities.System<SharedPainShockSystem>().AddPainSuppressionProfile(patient, 1f, 4, 0f, TimeSpan.FromSeconds(30));
                StartDebridement(entities, patient, medic, arm, scalpel);
                if (cancelFirst)
                    Assert.That(entities.System<SharedHandsSystem>().TryDrop(medic, scalpel), Is.True);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(6));
            if (cancelFirst)
            {
                await pair.Server.WaitAssertion(() =>
                {
                    Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.True,
                        "Dropping the exact surgical tool must cancel the pending effect.");
                    Assert.That(entities.System<SharedPainSourceProfileSystem>().ComputePainSourceProfile(patient).Target,
                        Is.EqualTo(FixedPoint2.New(55)));
                    Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, scalpel), Is.True);
                    StartDebridement(entities, patient, medic, arm, scalpel);
                });
                await pair.RunTicksSync(pair.SecondsToTicks(6));
            }
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.False);
                Assert.That(entities.HasComponent<CMUSurgeryArmedStepComponent>(patient), Is.False);
                Assert.That(entities.System<SharedHandsSystem>().IsHolding(medic, scalpel), Is.True);
                Assert.That(BurnDamage(entities, patient), Is.GreaterThan(FixedPoint2.Zero),
                    "Debridement clears its surgical condition rather than rejuvenating the remaining burn injury.");
                Assert.That(ledger.GetEntries(entities.GetComponent<BodyPartWoundComponent>(arm)).All(row => row.Wound.Treated), Is.True);
                Assert.That(entities.System<SharedPainSourceProfileSystem>().ComputePainSourceProfile(patient).Target,
                    Is.EqualTo(FixedPoint2.Zero));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                foreach (var entity in new[] { medic, patient, kit, scalpel })
                    if (entities.EntityExists(entity)) entities.DeleteEntity(entity);
            });
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompleteRejuvenationClearsBurnDamageWoundsAndEscharTogether()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var arm = BurnArm(entities, patient);
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.True);
                entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
                var health = entities.GetComponent<BodyPartHealthComponent>(arm);
                Assert.That(entities.HasComponent<CMUEscharComponent>(arm), Is.False);
                Assert.That(entities.HasComponent<BodyPartWoundComponent>(arm), Is.False);
                Assert.That(health.Current, Is.EqualTo(health.Max));
                Assert.That(BurnDamage(entities, patient), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entities.System<SharedBodyPartHealthSystem>().GetOutstandingBodyDamage(arm), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(entities.System<SharedPainSourceProfileSystem>().ComputePainSourceProfile(patient).Target, Is.EqualTo(FixedPoint2.Zero));
            }
            finally
            {
                entities.DeleteEntity(patient);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static EntityUid BurnArm(IEntityManager entities, EntityUid patient)
    {
        entities.System<RegionalDamageProbeSystem>();
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new(BodyPartType.Arm, BodyPartSymmetry.Left), out var arm), Is.True);
        entities.EnsureComponent<RegionalDamageProbeComponent>(patient).Target = arm;
        var burn = new DamageSpecifier();
        burn.DamageDict["Heat"] = 30;
        var impact = new DamageImpact(DamageImpactDelivery.Contact, DamageImpactContact.Burn,
            DamageImpactPenetration.None, DamageImpactEnergy.Low);
        var applied = entities.System<DamageableSystem>().TryChangeDamage(patient, burn, ignoreResistances: true, impact: impact);
        Assert.That(applied?.GetTotal(), Is.EqualTo(FixedPoint2.New(30)));
        return arm;
    }

    private static FixedPoint2 BurnDamage(IEntityManager entities, EntityUid patient)
        => entities.System<DamageableSystem>().GetAllDamage(patient).DamageDict.GetValueOrDefault("Heat");

    private static void StartDebridement(IEntityManager entities, EntityUid patient, EntityUid medic, EntityUid arm, EntityUid scalpel)
    {
        var flow = entities.System<CMUSurgeryFlowSystem>();
        var armed = flow.TryArmStep(medic, patient, arm, "CMUSurgeryDebrideEschar", 0,
            BodyPartType.Arm, BodyPartSymmetry.Left);
        Assert.That(armed, Is.Not.Null);
        Assert.That(flow.TryHandleArmedToolUse(patient, armed!, medic, scalpel, patient, out var handled, out var started), Is.True);
        Assert.That(handled && started, Is.True, "Exercise the real held-tool DoAfter and its commit validation.");
    }
}
