#pragma warning disable RA0002 // Observe exact operation handles, fixture tool policy, and committed treatment state.
using System.Linq;
using Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Body;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Injuries.Wounds.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Treatment;

[TestFixture]
public sealed class BandageOperationContextTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task UnskilledSelfUseAndAfterInteractCannotTreatWoundsOrArteries(bool selfUse)
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var medic = Human(entities);
            var patient = selfUse ? medic : Human(entities);
            var tool = Tool(entities, medic, instant: true, requiredSkill: 2);
            try
            {
                entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 0);
                var part = Part(entities, patient);
                AddWound(entities, part);
                entities.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding = ExternalBleedTier.Arterial;
                entities.GetComponent<WoundTreaterComponent>(tool).CMUStopsArterialBleeding = true;
                if (selfUse)
                {
                    var use = new UseInHandEvent(medic);
                    entities.EventBus.RaiseLocalEvent(tool, use);
                    Assert.That(use.Handled, Is.True);
                }
                else
                    Interact(entities, medic, patient, tool);
                AssertUntreated(entities, part);
                Assert.That(entities.GetComponent<BodyPartWoundComponent>(part).ExternalBleeding, Is.EqualTo(ExternalBleedTier.Arterial));
                Assert.That(Count(entities, tool), Is.EqualTo(10));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            }
            finally
            {
                Delete(entities, medic, patient, tool);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LosingRequiredSkillDuringDoAfterPreventsEveryEffectAndConsumption()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic, requiredSkill: 2);
                part = Part(entities, patient);
                AddWound(entities, part);
                Interact(entities, medic, patient, tool);
                Assert.That(Pending(entities, medic), Is.EqualTo(1));
                entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 0);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(5));
            await pair.Server.WaitAssertion(() =>
            {
                AssertUntreated(entities, part);
                Assert.That(Count(entities, tool), Is.EqualTo(10));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ConcurrentPatientsKeepTheirOwnContextWhenAnEarlierOperationEnds(bool cancelFirst)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, first = default, second = default, tool = default, firstPart = default, secondPart = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                first = Human(entities);
                second = Human(entities);
                tool = Tool(entities, medic);
                firstPart = Part(entities, first);
                secondPart = Part(entities, second);
                AddWound(entities, firstPart);
                AddWound(entities, secondPart);
                Interact(entities, medic, first, tool);
                var firstOperation = Operation(entities, medic, first);
                Interact(entities, medic, second, tool);
                Assert.That(Pending(entities, medic), Is.EqualTo(2));
                if (cancelFirst)
                {
                    entities.System<SharedDoAfterSystem>().Cancel(firstOperation.DoAfter.Id);
                    Assert.That(Pending(entities, medic), Is.EqualTo(1), "Cancelling an old patient must retain the newer operation handle.");
                }
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(Entry(entities, firstPart).Wound.Treated, Is.EqualTo(!cancelFirst));
                Assert.That(Entry(entities, secondPart).Wound.Treated, Is.True);
                Assert.That(Count(entities, tool), Is.EqualTo(cancelFirst ? 9 : 8));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, first, second, tool));
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task QueuedTreatmentDoesNotFollowASeveredSiteOrRetargetAnotherLimb(bool instant)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, detached = default, selected = default, other = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic, instant);
                selected = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Right);
                other = Part(entities, patient, BodyPartType.Arm, BodyPartSymmetry.Left);
                AddWound(entities, selected);
                AddWound(entities, other);
                // No fresh selection: instant treatment uses the real deferred search path.
                Interact(entities, medic, patient, tool);
                var operation = Operation(entities, medic, patient);
                Assert.That(operation.Part, Is.EqualTo(entities.GetNetEntity(selected)));
                Assert.That(operation.ApplyInstantTreatment, Is.EqualTo(instant));
                var carrier = entities.System<DetachableOrganSystem>().Detach(selected);
                Assert.That(carrier, Is.Not.Null);
                detached = carrier!.Value;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                AssertUntreated(entities, selected);
                AssertUntreated(entities, other);
                Assert.That(Count(entities, tool), Is.EqualTo(10));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool, detached));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MechanismMaskControlsSelectionTimingAndTheActualTreatedRow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic);
                entities.GetComponent<WoundTreaterComponent>(tool).CMUMechanisms = WoundMechanismFlags.Slash;
                part = Part(entities, patient);
                AddWound(entities, part, WoundMechanism.Crush, 45, WoundSize.CutMassive);
                AddWound(entities, part, WoundMechanism.Slash);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.Chest);
                Interact(entities, medic, patient, tool);
                Assert.That(Operation(entities, medic, patient).Args.Delay, Is.EqualTo(TimeSpan.FromSeconds(1.5)),
                    "An excluded massive wound must not determine the selected small slash's treatment time.");
            });
            await pair.RunTicksSync(pair.SecondsToTicks(3));
            await pair.Server.WaitAssertion(() =>
            {
                var entries = entities.System<CMUWoundLedgerSystem>().GetEntries(entities.GetComponent<BodyPartWoundComponent>(part));
                Assert.That(entries.Single(row => row.Mechanism == WoundMechanism.Crush).Wound.Treated, Is.False);
                Assert.That(entries.Single(row => row.Mechanism == WoundMechanism.Slash).Wound.Treated, Is.True);
                Assert.That(Count(entities, tool), Is.EqualTo(9));
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task DeletingTheQueuedPatientOrToolRetiresOnlyItsOwnOperation(bool deletePatient)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic);
                part = Part(entities, patient);
                AddWound(entities, part);
                Interact(entities, medic, patient, tool);
                Assert.That(Pending(entities, medic), Is.EqualTo(1));
                entities.DeleteEntity(deletePatient ? patient : tool);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(4));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
                if (deletePatient)
                    Assert.That(Count(entities, tool), Is.EqualTo(10));
                else
                    AssertUntreated(entities, part);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReentrantCancellationChargesTheCommittedDressingAndPreventsFurtherHealing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<BandageCompletionProbeSystem>();
                entities.System<RegionalDamageProbeSystem>();
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic);
                part = Part(entities, patient);
                entities.AddComponent<RegionalDamageProbeComponent>(patient).Target = part;
                entities.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Slash"] = 10 } }, ignoreResistances: true);
                // Stasis prevents elapsed passive recovery from obscuring the completion boundary.
                entities.AddComponent<CMInStasisComponent>(patient);
                var treater = entities.GetComponent<WoundTreaterComponent>(tool);
                treater.WoundsTreatedPerUse = 6;
                treater.Damage = -5;
                treater.UnskilledDamage = -5;
                var probe = entities.AddComponent<BandageCompletionProbeComponent>(patient);
                Interact(entities, medic, patient, tool);
                probe.Cancel = Operation(entities, medic, patient).DoAfter.Id;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(5));
            await pair.Server.WaitAssertion(() =>
            {
                var probe = entities.GetComponent<BandageCompletionProbeComponent>(patient);
                Assert.That(probe.Invocations, Is.EqualTo(1));
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.EqualTo(probe.DamageAtCallback));
                Assert.That(Entry(entities, part).Wound.Treated, Is.True);
                Assert.That(Count(entities, tool), Is.EqualTo(9), "A committed dressing incurs exactly one use even when later healing is cancelled.");
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReentrantNewTreatmentReplacesTheOldRepeatWithoutLosingItsHandle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        CMUBandageDoAfterEvent first = default!;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<BandageCompletionProbeSystem>();
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic);
                part = Part(entities, patient);
                AddWound(entities, part);
                AddWound(entities, part, WoundMechanism.Crush);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.Chest);
                var probe = entities.AddComponent<BandageCompletionProbeComponent>(patient);
                probe.RestartMedic = medic;
                probe.RestartTool = tool;
                Interact(entities, medic, patient, tool);
                first = Operation(entities, medic, patient);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.GetComponent<BandageCompletionProbeComponent>(patient).Invocations, Is.EqualTo(1));
                Assert.That(first.Repeat, Is.False);
                Assert.That(Pending(entities, medic), Is.EqualTo(1));
                Assert.That(Operation(entities, medic, patient), Is.Not.SameAs(first));
                Assert.That(Count(entities, tool), Is.EqualTo(9));
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
                Assert.That(Count(entities, tool), Is.EqualTo(8));
                var entries = entities.System<CMUWoundLedgerSystem>().GetEntries(entities.GetComponent<BodyPartWoundComponent>(part));
                Assert.That(entries.All(entry => entry.Wound.Treated), Is.True);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    [TestCase("stasis")]
    [TestCase("body")]
    [TestCase("part")]
    public async Task ACommittedDressingOnlyRecoversWhileItsTissueIsActive(string suspension)
    {
        await using var pair = await PoolManager.GetServerClient();
        var entities = pair.Server.EntMan;
        EntityUid medic = default, patient = default, tool = default, part = default;
        FixedPoint2 damageBefore = default, healedBefore = default;
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<RegionalDamageProbeSystem>();
                medic = Human(entities);
                patient = Human(entities);
                tool = Tool(entities, medic, instant: true);
                part = Part(entities, patient);
                entities.AddComponent<RegionalDamageProbeComponent>(patient).Target = part;
                entities.System<DamageableSystem>().TryChangeDamage(patient,
                    new DamageSpecifier { DamageDict = { ["Slash"] = 10 } }, ignoreResistances: true);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.Chest);
                Interact(entities, medic, patient, tool);
                Assert.That(Entry(entities, part).Wound.Treated, Is.True);
                Assert.That(Count(entities, tool), Is.EqualTo(9));
                if (suspension == "stasis")
                    entities.AddComponent<CMInStasisComponent>(patient);
                else
                    entities.System<MetaDataSystem>().SetEntityPaused(suspension == "body" ? patient : part, true);
                damageBefore = entities.System<DamageableSystem>().GetTotalDamage(patient);
                healedBefore = Entry(entities, part).Wound.Healed;
            });
            await pair.RunTicksSync(pair.SecondsToTicks(5));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.EqualTo(damageBefore));
                Assert.That(Entry(entities, part).Wound.Healed, Is.EqualTo(healedBefore));
                if (suspension == "stasis")
                    entities.RemoveComponent<CMInStasisComponent>(patient);
                else
                    entities.System<MetaDataSystem>().SetEntityPaused(suspension == "body" ? patient : part, false);
            });
            await pair.RunTicksSync(pair.SecondsToTicks(2));
            await pair.Server.WaitAssertion(() =>
            {
                Assert.That(entities.System<DamageableSystem>().GetTotalDamage(patient), Is.LessThan(damageBefore));
                Assert.That(Entry(entities, part).Wound.Healed, Is.GreaterThan(healedBefore));
                Assert.That(Count(entities, tool), Is.EqualTo(9));
            });
        }
        finally
        {
            await pair.Server.WaitPost(() => Delete(entities, medic, patient, tool));
        }
        await pair.CleanReturnAsync();
    }

    private static EntityUid Human(IEntityManager entities)
        => entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

    private static EntityUid Tool(IEntityManager entities, EntityUid medic, bool instant = false, int requiredSkill = 0)
    {
        var tool = entities.SpawnEntity("CMGauze10", MapCoordinates.Nullspace);
        var treater = entities.GetComponent<WoundTreaterComponent>(tool);
        treater.InstantWoundTreatment = instant;
        treater.InstantWoundTreatmentSkills.Clear();
        treater.CanUseUnskilled = requiredSkill == 0;
        treater.Skills.Clear();
        treater.Skills["RMCSkillMedical"] = requiredSkill;
        entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
        Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, tool, checkActionBlocker: false), Is.True);
        return tool;
    }

    private static EntityUid Part(IEntityManager entities, EntityUid patient,
        BodyPartType type = BodyPartType.Torso, BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
            new CMUMedicalBodyPartKey(type, symmetry), out var part), Is.True);
        return part;
    }

    private static void AddWound(IEntityManager entities, EntityUid part, WoundMechanism mechanism = WoundMechanism.Slash,
        int damage = 10, WoundSize size = WoundSize.CutSmall)
    {
        var wounds = entities.EnsureComponent<BodyPartWoundComponent>(part);
        Assert.That(entities.System<CMUWoundLedgerSystem>().AddEntry(wounds, new CMUWoundEntry(
            new Wound(damage, FixedPoint2.Zero, 0, null, WoundType.Brute, false), size, 0,
            mechanism, WoundMechanismFlags.None, WoundTreatmentQuality.Untreated, WoundCleanupFlags.None)), Is.GreaterThanOrEqualTo(0));
    }

    private static void Interact(IEntityManager entities, EntityUid medic, EntityUid patient, EntityUid tool)
    {
        var ev = new AfterInteractEvent(medic, tool, patient, default, true);
        entities.EventBus.RaiseLocalEvent(tool, ev);
        Assert.That(ev.Handled, Is.True);
    }

    private static CMUBandageDoAfterEvent Operation(IEntityManager entities, EntityUid medic, EntityUid patient)
        => entities.GetComponent<CMUBandagePendingComponent>(medic).Operations.Single(ev => ev.Patient == entities.GetNetEntity(patient));

    private static int Pending(IEntityManager entities, EntityUid medic)
        => entities.GetComponent<CMUBandagePendingComponent>(medic).Operations.Count;

    private static int Count(IEntityManager entities, EntityUid tool)
        => entities.System<SharedStackSystem>().GetCount(tool);

    private static CMUWoundEntry Entry(IEntityManager entities, EntityUid part)
        => entities.System<CMUWoundLedgerSystem>().GetEntries(entities.GetComponent<BodyPartWoundComponent>(part))[0];

    private static void AssertUntreated(IEntityManager entities, EntityUid part)
        => Assert.That(Entry(entities, part).Wound.Treated, Is.False);

    private static void Delete(IEntityManager entities, params EntityUid[] values)
    {
        foreach (var value in values)
            if (entities.EntityExists(value)) entities.DeleteEntity(value);
    }
}

[RegisterComponent]
public sealed partial class BandageCompletionProbeComponent : Component
{
    public DoAfterId? Cancel;
    public EntityUid? RestartMedic;
    public EntityUid? RestartTool;
    public int Invocations;
    public FixedPoint2 DamageAtCallback;
}

public sealed partial class BandageCompletionProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WoundTreatedEvent>(OnTreated);
    }

    private void OnTreated(ref WoundTreatedEvent args)
    {
        if (!TryComp<BandageCompletionProbeComponent>(args.Body, out var probe) || probe.Invocations != 0)
            return;
        probe.Invocations++;
        probe.DamageAtCallback = EntityManager.System<DamageableSystem>().GetTotalDamage(args.Body);
        if (probe.Cancel is { } cancel)
            EntityManager.System<SharedDoAfterSystem>().Cancel(cancel, force: true);
        if (probe.RestartMedic is { } medic && probe.RestartTool is { } tool)
        {
            var interact = new AfterInteractEvent(medic, tool, args.Body, default, true);
            RaiseLocalEvent(tool, interact);
            Assert.That(interact.Handled, Is.True);
        }
    }
}
