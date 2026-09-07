#pragma warning disable RA0002 // Inspect committed ledgers and model external component replacement at the public callback boundary.
using System.Linq;
using Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;
using Content.Server.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Stasis;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Round;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Injuries.Wounds;

[TestFixture]
public sealed class PassiveWoundRecoveryReentryTest
{
    [TestCase(RecoveryInterruption.Stasis)]
    [TestCase(RecoveryInterruption.Rejuvenate)]
    [TestCase(RecoveryInterruption.RejuvenateThenHit)]
    [TestCase(RecoveryInterruption.ReplaceWounds)]
    [TestCase(RecoveryInterruption.ClearRows)]
    [TestCase(RecoveryInterruption.RebuildIdenticalRows)]
    [TestCase(RecoveryInterruption.ReplacePartHealth)]
    [TestCase(RecoveryInterruption.ReplaceAnatomy)]
    [TestCase(RecoveryInterruption.DetachPart)]
    [TestCase(RecoveryInterruption.DeletePart)]
    [TestCase(RecoveryInterruption.DeletePatient)]
    public async Task AnAcceptedRecoveryRowCannotContinueIntoChangedPatientState(RecoveryInterruption interruption)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var entities = pair.Server.EntMan;
        var cleanup = new HashSet<EntityUid>();
        EntityUid patient = default, medic = default, gauze = default, part = default;
        BodyPartWoundComponent originalWounds = default!;
        BodyPartHealthComponent originalHealth = default!;
        BodyPartHealthComponent? replacementHealth = null;
        BodyPartComponent? replacementAnatomy = null;
        BodyPartWoundComponent? replacementWounds = null;
        PassiveWoundRecoveryProbeComponent probe = default!;
        CMUWoundEntry[] initialRows = [];
        CMUWoundEntry[] callbackRows = [];
        CMUWoundEntry[] replacementRows = [];
        FixedPoint2 healthAtCallback = default, damageBefore = default, damageAfterFirst = default, healthAfterFirst = default;
        FixedPoint2 replacementDebt = default, replacementCurrent = default, replacementAggregate = default;
        var ledger = entities.System<CMUWoundLedgerSystem>();
        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                entities.System<RegionalDamageProbeSystem>();
                entities.System<PassiveWoundRecoveryProbeSystem>();
                patient = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                medic = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                gauze = entities.SpawnEntity("CMGauze10", map.GridCoords);
                cleanup.UnionWith([patient, medic, gauze]);
                entities.EnsureComponent<CMInStasisComponent>(patient);
                Assert.That(entities.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(patient,
                    new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left), out part), Is.True);
                Hit(entities, patient, part, "Slash", 10, DamageImpact.SnaggingContact);
                Hit(entities, patient, part, "Piercing", 10,
                    new DamageImpact(DamageImpactDelivery.Contact, DamageImpactContact.Stab,
                        DamageImpactPenetration.None, DamageImpactEnergy.Low));
                originalWounds = entities.GetComponent<BodyPartWoundComponent>(part);
                originalHealth = entities.GetComponent<BodyPartHealthComponent>(part);
                Assert.That(ledger.GetEntries(originalWounds), Has.Count.EqualTo(2));
                Assert.That(ledger.GetEntries(originalWounds).Select(row => row.Mechanism),
                    Is.EquivalentTo(new[] { WoundMechanism.Slash, WoundMechanism.Stab }),
                    "Two hits with the same mechanism would merge and fail to exercise continuation to a later row.");

                var treater = entities.GetComponent<WoundTreaterComponent>(gauze);
                treater.InstantWoundTreatment = false;
                treater.InstantWoundTreatmentSkills.Clear();
                treater.WoundsTreatedPerUse = 1;
                entities.System<SkillsSystem>().SetSkill(medic, "RMCSkillMedical", 2);
                entities.System<TagSystem>().AddTag(medic, "InstantDoAfters");
                Assert.That(entities.System<SharedHandsSystem>().TryPickupAnyHand(medic, gauze, checkActionBlocker: false), Is.True);
                entities.System<SharedBodyZoneTargetingSystem>().SelectZone((medic, null), TargetBodyZone.LeftArm);
                // Accelerate only the actual DoAfter clock, not its completion event
                // or treatment effects. Repeats may treat the second row themselves.
                for (var attempt = 0; attempt < 4 && ledger.GetEntries(originalWounds).Any(row => !row.Wound.Treated); attempt++)
                {
                    var interact = new AfterInteractEvent(medic, gauze, patient, default, true);
                    entities.EventBus.RaiseLocalEvent(gauze, interact);
                    Assert.That(interact.Handled, Is.True);
                }
                initialRows = ledger.GetEntries(originalWounds).ToArray();
                Assert.That(initialRows, Has.Length.EqualTo(2));
                Assert.That(initialRows.All(row => row.Wound.Treated), Is.True);
                Assert.That(initialRows.All(row => row.Wound.Healed == FixedPoint2.Zero), Is.True);
                Assert.That(entities.System<SharedStackSystem>().GetCount(gauze), Is.LessThan(10),
                    "Real bandage treatment must have consumed supplies.");
                Assert.That(entities.HasComponent<CMUBandagePendingComponent>(medic), Is.False);
                AssertConservation(entities, patient);
                damageBefore = BruteDamage(entities, patient);
                Assert.That(damageBefore, Is.GreaterThan(FixedPoint2.Zero));
                probe = entities.AddComponent<PassiveWoundRecoveryProbeComponent>(patient);
                probe.Callback = () =>
                {
                    callbackRows = ledger.GetEntries(originalWounds).ToArray();
                    healthAtCallback = originalHealth.Current;
                    switch (interruption)
                    {
                        case RecoveryInterruption.Stasis:
                            entities.EnsureComponent<CMInStasisComponent>(patient);
                            break;
                        case RecoveryInterruption.Rejuvenate:
                            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
                            break;
                        case RecoveryInterruption.RejuvenateThenHit:
                            entities.System<RejuvenateSystem>().PerformRejuvenate(patient);
                            Hit(entities, patient, part, "Slash", 5, DamageImpact.SnaggingContact);
                            replacementWounds = entities.GetComponent<BodyPartWoundComponent>(part);
                            replacementRows = ledger.GetEntries(replacementWounds).ToArray();
                            replacementDebt = originalHealth.BodyDamage.GetTotal();
                            replacementCurrent = originalHealth.Current;
                            replacementAggregate = BruteDamage(entities, patient);
                            Assert.That(replacementWounds, Is.Not.SameAs(originalWounds));
                            Assert.That(entities.GetComponent<BodyPartHealthComponent>(part), Is.SameAs(originalHealth),
                                "Rejuvenation retains the health object, so identity alone cannot detect this reset.");
                            break;
                        case RecoveryInterruption.ReplaceWounds:
                            entities.RemoveComponent<BodyPartWoundComponent>(part);
                            // A real new injury creates the replacement ledger. The
                            // old loop must not heal or overwrite this untreated row.
                            Hit(entities, patient, part, "Slash", 5, DamageImpact.SnaggingContact);
                            replacementWounds = entities.GetComponent<BodyPartWoundComponent>(part);
                            replacementRows = ledger.GetEntries(replacementWounds).ToArray();
                            Assert.That(replacementWounds, Is.Not.SameAs(originalWounds));
                            break;
                        case RecoveryInterruption.ClearRows:
                            Assert.That(ledger.ClearEntries(originalWounds), Is.True);
                            break;
                        case RecoveryInterruption.RebuildIdenticalRows:
                            // A true remove/recreate operation can end with identical
                            // values. Value equality alone cannot validate ownership.
                            Assert.That(ledger.ClearEntries(originalWounds), Is.True);
                            foreach (var row in callbackRows)
                                Assert.That(ledger.AddEntry(originalWounds, row), Is.GreaterThanOrEqualTo(0));
                            break;
                        case RecoveryInterruption.ReplacePartHealth:
                            var debt = new DamageSpecifier(originalHealth.BodyDamage);
                            replacementDebt = debt.GetTotal();
                            entities.RemoveComponent<BodyPartHealthComponent>(part);
                            replacementHealth = entities.AddComponent<BodyPartHealthComponent>(part);
                            replacementHealth.Current = 17;
                            replacementHealth.Max = originalHealth.Max;
                            replacementHealth.BodyDamage = debt;
                            break;
                        case RecoveryInterruption.ReplaceAnatomy:
                            var anatomy = entities.GetComponent<BodyPartComponent>(part);
                            entities.RemoveComponent<BodyPartComponent>(part);
                            replacementAnatomy = entities.AddComponent<BodyPartComponent>(part);
                            replacementAnatomy.Body = patient;
                            replacementAnatomy.PartType = anatomy.PartType;
                            replacementAnatomy.Symmetry = anatomy.Symmetry;
                            replacementAnatomy.IsVital = anatomy.IsVital;
                            replacementAnatomy.Children = new(anatomy.Children);
                            replacementAnatomy.Organs = new(anatomy.Organs);
                            break;
                        case RecoveryInterruption.DetachPart:
                            var carrier = entities.System<DetachableOrganSystem>().Detach(part);
                            Assert.That(carrier, Is.Not.Null);
                            cleanup.Add(carrier!.Value);
                            break;
                        case RecoveryInterruption.DeletePart:
                            entities.DeleteEntity(part);
                            break;
                        case RecoveryInterruption.DeletePatient:
                            entities.DeleteEntity(patient);
                            break;
                    }
                };
                probe.Armed = true;
                entities.RemoveComponent<CMInStasisComponent>(patient);
                originalWounds.NextHealTick = TimeSpan.Zero;
                entities.System<CMUWoundsSystem>().Update(0.5f);

                Assert.That(probe.CallbackInvocations, Is.EqualTo(1));
                Assert.That(probe.HealingEvents, Is.EqualTo(1), "No second row may publish healing after the callback changes the operation owner/state.");
                Assert.That(callbackRows, Has.Length.EqualTo(2));
                Assert.That(callbackRows[0].Wound.Healed, Is.EqualTo(initialRows[0].Wound.Healed));
                Assert.That(callbackRows[1].Wound.Healed,
                    Is.EqualTo(initialRows[1].Wound.Healed + (FixedPoint2)SharedCMUWoundsSystem.HealPerSecond),
                    "The accepted row must already be committed when aggregate observers run.");

                switch (interruption)
                {
                    case RecoveryInterruption.Stasis:
                    case RecoveryInterruption.RebuildIdenticalRows:
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                        Assert.That(BruteDamage(entities, patient), Is.EqualTo(damageBefore - probe.Healing));
                        break;
                    case RecoveryInterruption.Rejuvenate:
                        Assert.That(entities.HasComponent<BodyPartWoundComponent>(part), Is.False);
                        Assert.That(BruteDamage(entities, patient), Is.EqualTo(FixedPoint2.Zero));
                        Assert.That(originalHealth.Current, Is.EqualTo(originalHealth.Max));
                        Assert.That(originalHealth.BodyDamage.GetTotal(), Is.EqualTo(FixedPoint2.Zero));
                        break;
                    case RecoveryInterruption.RejuvenateThenHit:
                        Assert.That(entities.GetComponent<BodyPartWoundComponent>(part), Is.SameAs(replacementWounds));
                        Assert.That(ledger.GetEntries(replacementWounds!), Is.EqualTo(replacementRows));
                        Assert.That(replacementRows, Has.Length.EqualTo(1));
                        Assert.That(replacementRows[0].Wound.Treated, Is.False);
                        Assert.That(replacementRows[0].Wound.Healed, Is.EqualTo(FixedPoint2.Zero));
                        Assert.That(originalHealth.BodyDamage.GetTotal(), Is.EqualTo(replacementDebt));
                        Assert.That(BruteDamage(entities, patient), Is.EqualTo(replacementAggregate));
                        Assert.That(replacementAggregate, Is.EqualTo(FixedPoint2.New(5)));
                        Assert.That(originalHealth.Current, Is.EqualTo(replacementCurrent),
                            "The old accepted wound must not spend structural recovery on a fresh injury after rejuvenation.");
                        break;
                    case RecoveryInterruption.ReplaceWounds:
                        Assert.That(entities.GetComponent<BodyPartWoundComponent>(part), Is.SameAs(replacementWounds));
                        Assert.That(ledger.GetEntries(replacementWounds!), Is.EqualTo(replacementRows));
                        Assert.That(replacementRows.All(row => !row.Wound.Treated && row.Wound.Healed == FixedPoint2.Zero), Is.True);
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows), "No write may target the retired component either.");
                        Assert.That(BruteDamage(entities, patient), Is.EqualTo(damageBefore - probe.Healing + FixedPoint2.New(5)));
                        break;
                    case RecoveryInterruption.ClearRows:
                        Assert.That(ledger.GetEntries(originalWounds), Is.Empty);
                        Assert.That(BruteDamage(entities, patient), Is.EqualTo(damageBefore - probe.Healing));
                        break;
                    case RecoveryInterruption.ReplacePartHealth:
                        Assert.That(entities.GetComponent<BodyPartHealthComponent>(part), Is.SameAs(replacementHealth));
                        Assert.That(replacementHealth!.Current, Is.EqualTo(FixedPoint2.New(17)));
                        Assert.That(replacementHealth.BodyDamage.GetTotal(), Is.EqualTo(replacementDebt));
                        Assert.That(originalHealth.Current, Is.EqualTo(healthAtCallback), "The retired health component must not be healed or dirtied.");
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                        break;
                    case RecoveryInterruption.ReplaceAnatomy:
                        Assert.That(entities.GetComponent<BodyPartComponent>(part), Is.SameAs(replacementAnatomy));
                        Assert.That(originalHealth.Current, Is.EqualTo(healthAtCallback));
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                        break;
                    case RecoveryInterruption.DetachPart:
                        Assert.That(entities.GetComponent<BodyPartComponent>(part).Body, Is.Not.EqualTo(patient));
                        Assert.That(originalHealth.Current, Is.EqualTo(healthAtCallback));
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                        break;
                    case RecoveryInterruption.DeletePart:
                        Assert.That(entities.EntityExists(part), Is.False);
                        Assert.That(originalHealth.Current, Is.EqualTo(healthAtCallback));
                        Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                        break;
                    case RecoveryInterruption.DeletePatient:
                        Assert.That(entities.EntityExists(patient), Is.False);
                        Assert.That(originalHealth.Current, Is.EqualTo(healthAtCallback));
                        break;
                }
                if (entities.EntityExists(patient)) AssertConservation(entities, patient);
                if (interruption == RecoveryInterruption.Stasis)
                {
                    damageAfterFirst = BruteDamage(entities, patient);
                    healthAfterFirst = originalHealth.Current;
                }
            });

            if (interruption == RecoveryInterruption.Stasis)
            {
                await pair.RunTicksSync(pair.SecondsToTicks(1.3f));
                await pair.Server.WaitAssertion(() =>
                {
                    Assert.That(probe.HealingEvents, Is.EqualTo(1));
                    Assert.That(ledger.GetEntries(originalWounds), Is.EqualTo(callbackRows));
                    Assert.That(BruteDamage(entities, patient), Is.EqualTo(damageAfterFirst));
                    Assert.That(originalHealth.Current, Is.EqualTo(healthAfterFirst));
                    entities.RemoveComponent<CMInStasisComponent>(patient);
                });
                await pair.RunTicksSync(pair.SecondsToTicks(1.3f));
                await pair.Server.WaitAssertion(() =>
                {
                    Assert.That(probe.HealingEvents, Is.GreaterThan(1));
                    Assert.That(ledger.GetEntries(originalWounds)[0].Wound.Healed, Is.GreaterThan(callbackRows[0].Wound.Healed));
                    Assert.That(BruteDamage(entities, patient), Is.LessThan(damageAfterFirst));
                    AssertConservation(entities, patient);
                    Assert.That(probe.CallbackInvocations, Is.EqualTo(1));
                });
            }
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                foreach (var uid in cleanup)
                    if (entities.EntityExists(uid)) entities.DeleteEntity(uid);
            });
        }
        await pair.CleanReturnAsync();
    }

    private static void Hit(IEntityManager entities, EntityUid patient, EntityUid part, string type, int amount, DamageImpact impact)
    {
        entities.EnsureComponent<RegionalDamageProbeComponent>(patient).Target = part;
        var applied = entities.System<DamageableSystem>().TryChangeDamage(patient,
            new DamageSpecifier { DamageDict = { [type] = amount } }, ignoreResistances: true, impact: impact);
        Assert.That(applied!.GetTotal(), Is.EqualTo(FixedPoint2.New(amount)));
    }

    private static FixedPoint2 BruteDamage(IEntityManager entities, EntityUid patient)
    {
        var damage = entities.System<DamageableSystem>().GetAllDamage(patient).DamageDict;
        return damage.GetValueOrDefault("Slash") + damage.GetValueOrDefault("Piercing") + damage.GetValueOrDefault("Blunt");
    }

    private static void AssertConservation(IEntityManager entities, EntityUid patient)
    {
        var damage = entities.System<DamageableSystem>().GetAllDamage(patient).DamageDict;
        foreach (var type in new[] { "Slash", "Piercing", "Blunt" })
        {
            var regional = FixedPoint2.Zero;
            foreach (var (part, _) in entities.System<CMUMedicalBodyIndexSystem>().GetBodyParts(patient))
                if (entities.TryGetComponent<BodyPartHealthComponent>(part, out var health))
                    regional += health.BodyDamage.DamageDict.GetValueOrDefault(type);
            Assert.That(damage.GetValueOrDefault(type), Is.EqualTo(regional), $"Aggregate {type} must equal attached regional attribution.");
        }
    }

    public enum RecoveryInterruption
    {
        Stasis,
        Rejuvenate,
        RejuvenateThenHit,
        ReplaceWounds,
        ClearRows,
        RebuildIdenticalRows,
        ReplacePartHealth,
        ReplaceAnatomy,
        DetachPart,
        DeletePart,
        DeletePatient,
    }
}

[RegisterComponent]
public sealed partial class PassiveWoundRecoveryProbeComponent : Component
{
    public Action? Callback;
    public bool Armed;
    public bool InsideCallback;
    public int HealingEvents;
    public int CallbackInvocations;
    public FixedPoint2 Healing;
}

public sealed partial class PassiveWoundRecoveryProbeSystem : EntitySystem
{
    public override void Initialize()
        => SubscribeLocalEvent<PassiveWoundRecoveryProbeComponent, DamageChangedEvent>(OnDamage);

    private void OnDamage(Entity<PassiveWoundRecoveryProbeComponent> ent, ref DamageChangedEvent args)
    {
        if (!ent.Comp.Armed || ent.Comp.InsideCallback || args.DamageDelta is not { } delta) return;
        var brute = delta.DamageDict.GetValueOrDefault("Slash") + delta.DamageDict.GetValueOrDefault("Piercing") +
                    delta.DamageDict.GetValueOrDefault("Blunt");
        if (brute >= FixedPoint2.Zero) return;
        ent.Comp.HealingEvents++;
        ent.Comp.Healing -= brute;
        if (ent.Comp.Callback is not { } callback) return;
        ent.Comp.Callback = null;
        ent.Comp.CallbackInvocations++;
        ent.Comp.InsideCallback = true;
        try { callback(); }
        finally { ent.Comp.InsideCallback = false; }
    }
}
