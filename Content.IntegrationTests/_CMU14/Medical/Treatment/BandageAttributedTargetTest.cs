#pragma warning disable RA0002 // Observe exact selected operation and committed regional state.
using System.Linq;
using Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Medical.Treatment;

[TestFixture]
public sealed class BandageAttributedTargetTest
{
    [Test]
    public async Task SurgicalLineSkipsEarlierHeatDebtAndHealsOnlyTheLaterSlashSite()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, line = default, right = default, left = default;
        FixedPoint2 rightBefore = default, leftBefore = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                patient = Human(entities);
                line = HeldTool(entities, medic, "CMSurgicalLine");
                right = Arm(entities, patient, BodyPartSymmetry.Right);
                left = Arm(entities, patient, BodyPartSymmetry.Left);
                Hit(entities, patient, right, "Heat", 20);
                Hit(entities, patient, left, "Slash", 20);
                // Remove the independent hemostasis work so this interaction
                // exercises damage-only auto-selection, not the bleeding fallback.
                var wounds = entities.System<SharedCMUWoundsSystem>();
                wounds.StopSurfaceBleedingOnPart(right);
                Assert.That(wounds.StopSurfaceBleedingOnPart(left), Is.True);
                entities.AddComponent<CMInStasisComponent>(patient);
                var health = entities.System<SharedBodyPartHealthSystem>();
                Assert.That(health.GetAttributedDamage(right, "Heat"), Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(health.GetAttributedDamage(right, "Slash"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(health.GetAttributedDamage(left, "Slash"), Is.EqualTo(FixedPoint2.New(20)));
                rightBefore = entities.GetComponent<BodyPartHealthComponent>(right).Current;
                leftBefore = entities.GetComponent<BodyPartHealthComponent>(left).Current;
                // No user zone selection: right arm precedes left arm in the real search order.
                Interact(entities, medic, patient, line);
                var operation = entities.GetComponent<CMUBandagePendingComponent>(medic).Operations.Single();
                Assert.That(operation.Part, Is.EqualTo(entities.GetNetEntity(left)));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                var health = entities.System<SharedBodyPartHealthSystem>();
                var damage = entities.System<DamageableSystem>().GetAllDamage(patient);
                Assert.Multiple(() =>
                {
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(right).Current, Is.EqualTo(rightBefore));
                    Assert.That(entities.GetComponent<BodyPartHealthComponent>(left).Current, Is.EqualTo(leftBefore + FixedPoint2.New(10)));
                    Assert.That(health.GetAttributedDamage(right, "Heat"), Is.EqualTo(FixedPoint2.New(20)));
                    Assert.That(health.GetAttributedDamage(left, "Slash"), Is.EqualTo(FixedPoint2.New(10)));
                    Assert.That(damage.DamageDict["Heat"], Is.EqualTo(FixedPoint2.New(20)));
                    Assert.That(damage.DamageDict["Slash"], Is.EqualTo(FixedPoint2.New(10)));
                    Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
                });
                Assert.That(entities.System<CMUWoundLedgerSystem>()
                    .GetEntries(entities.GetComponent<BodyPartWoundComponent>(left)).All(row => !row.Wound.Treated), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, line));
        }
        await pair.CleanReturnAsync();
    }

    private static EntityUid Human(IEntityManager entities)
        => entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

    private static EntityUid HeldTool(IEntityManager entities, EntityUid medic, string prototype)
    {
        var tool = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
        entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
        Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, tool, checkActionBlocker: false), Is.True);
        return tool;
    }

    private static EntityUid Arm(IEntityManager entities, EntityUid patient, BodyPartSymmetry symmetry)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(BodyPartType.Arm, symmetry), out var part), Is.True);
        return part;
    }

    private static void Hit(IEntityManager entities, EntityUid patient, EntityUid part, string type, int amount)
    {
        entities.System<RegionalDamageProbeSystem>();
        entities.EnsureComponent<RegionalDamageProbeComponent>(patient).Target = part;
        var applied = entities.System<DamageableSystem>().TryChangeDamage(patient,
            new DamageSpecifier { DamageDict = { [type] = amount } }, ignoreResistances: true,
            impact: DamageImpact.SnaggingContact);
        Assert.That(applied!.GetTotal(), Is.EqualTo(FixedPoint2.New(amount)));
    }

    private static void Interact(IEntityManager entities, EntityUid medic, EntityUid patient, EntityUid tool)
    {
        var interact = new AfterInteractEvent(medic, tool, patient, default, true);
        entities.EventBus.RaiseLocalEvent(tool, interact);
        Assert.That(interact.Handled, Is.True);
    }

    private static void Delete(IEntityManager entities, params EntityUid[] values)
    {
        foreach (var value in values)
            if (entities.EntityExists(value)) entities.DeleteEntity(value);
    }
}
