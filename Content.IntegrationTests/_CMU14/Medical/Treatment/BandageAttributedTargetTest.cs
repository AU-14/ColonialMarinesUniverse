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
    private static readonly ProtoId<TagPrototype> InstantDoAfters = "InstantDoAfters";

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

    [Test]
    public async Task NestedSynchronousTreatmentCannotOrphanTheOriginalPatientsRepeat()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, other = default, gauze = default, part = default,
            repeatPart = default, otherPart = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<BandageNestedStartProbeSystem>();
                medic = Human(entities);
                patient = Human(entities);
                other = Human(entities);
                gauze = HeldTool(entities, medic, "CMGauze10");
                var treater = entities.GetComponent<WoundTreaterComponent>(gauze);
                treater.InstantWoundTreatment = false;
                treater.InstantWoundTreatmentSkills.Clear();
                treater.WoundsTreatedPerUse = 1;
                part = Arm(entities, patient, BodyPartSymmetry.Left);
                repeatPart = Arm(entities, patient, BodyPartSymmetry.Right);
                otherPart = Arm(entities, other, BodyPartSymmetry.Left);
                Hit(entities, patient, part, "Slash", 5);
                // Same-mechanism hits on one region deliberately merge into one
                // wound. A second site supplies real work for the outer repeat.
                Hit(entities, patient, repeatPart, "Slash", 5);
                Hit(entities, other, otherPart, "Slash", 5);
                entities.AddComponent<CMInStasisComponent>(patient);
                entities.AddComponent<CMInStasisComponent>(other);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.LeftArm);
                var probe = entities.AddComponent<BandageNestedStartProbeComponent>(patient);
                probe.Callback = () =>
                {
                    var tags = entities.System<TagSystem>();
                    Assert.That(tags.AddTag(medic, InstantDoAfters), Is.True);
                    try
                    {
                        Interact(entities, medic, other, gauze);
                        // The nested synchronous operation completed and removed the
                        // old empty owner; the outer operation must reacquire it.
                        Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
                    }
                    finally
                    {
                        tags.RemoveTag(medic, InstantDoAfters);
                    }
                };
                Interact(entities, medic, patient, gauze);
            });
            // Observe the first completion before advancing through its repeat.
            // The actual small-wound/skill delay can complete both within two seconds.
            var completedFirst = false;
            for (var tick = 0; tick < pair.SecondsToTicks(2) && !completedFirst; tick++)
            {
                await pair.RunTicksSync(1);
                await pair.Server.WaitPost(() => completedFirst =
                    entities.GetComponent<BandageNestedStartProbeComponent>(patient).Invocations != 0);
            }
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<BandageNestedStartProbeComponent>(patient).Invocations, Is.EqualTo(1));
                var pending = entities.GetComponent<CMUBandagePendingComponent>(medic);
                Assert.That(pending.Operations.Count, Is.EqualTo(1));
                Assert.That(pending.Operations.Single().Patient, Is.EqualTo(entities.GetNetEntity(patient)));
                Assert.That(pending.Operations.Single().Part, Is.EqualTo(entities.GetNetEntity(repeatPart)));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2));
            await pair.Server.WaitAssertion(() =>
            {
                var ledger = entities.System<CMUWoundLedgerSystem>();
                var rows = ledger.GetEntries(entities.GetComponent<BodyPartWoundComponent>(part));
                Assert.That(rows.Single().Wound.Treated, Is.True);
                Assert.That(ledger.GetEntries(entities.GetComponent<BodyPartWoundComponent>(repeatPart)).Single().Wound.Treated, Is.True);
                Assert.That(ledger.GetEntries(entities.GetComponent<BodyPartWoundComponent>(otherPart)).Single().Wound.Treated, Is.True);
                Assert.That(entities.System<SharedStackSystem>().GetCount(gauze), Is.EqualTo(7));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, other, gauze));
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

[RegisterComponent]
public sealed partial class BandageNestedStartProbeComponent : Component
{
    public Action? Callback;
    public int Invocations;
}

public sealed partial class BandageNestedStartProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundTreatedEvent>(OnTreated);
    }

    private void OnTreated(ref WoundTreatedEvent args)
    {
        if (!TryComp<BandageNestedStartProbeComponent>(args.Body, out var probe) || probe.Invocations != 0)
            return;
        probe.Invocations++;
        probe.Callback?.Invoke();
    }
}
