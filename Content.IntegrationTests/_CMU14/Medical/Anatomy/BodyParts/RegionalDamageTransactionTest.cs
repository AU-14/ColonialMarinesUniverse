#pragma warning disable RA0002 // Regression assertions inspect the committed damage and anatomy ledgers.

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared.Alert;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

[TestFixture]
public sealed class RegionalDamageTransactionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMMobHuman
  id: RegionalDamageTransactionHuman
  components:
  - type: RegionalDamageProbe
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task NestedDamageRetainsExactSiteAndReturnsOnlyItsOwnDelta(bool afterCommit)
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            try
            {
                var torso = Part(human, BodyPartType.Torso);
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                probe.Target = torso;
                probe.NestedTarget = arm;
                probe.NestAfterCommit = afterCommit;
                var applied = Server.System<DamageableSystem>().TryChangeDamage(human, Damage("Slash", 11), ignoreResistances: true);

                Assert.Multiple(() =>
                {
                    Assert.That(applied!.GetTotal(), Is.EqualTo(FixedPoint2.New(11)));
                    Assert.That(Debt(torso, "Slash"), Is.EqualTo(FixedPoint2.New(11)));
                    Assert.That(Debt(torso, "Piercing"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(Debt(arm, "Piercing"), Is.EqualTo(FixedPoint2.New(7)));
                    Assert.That(Debt(arm, "Slash"), Is.EqualTo(FixedPoint2.Zero));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [Test]
    public async Task TargetedRepairCannotHealAnotherSiteOrAnotherDamageType()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            try
            {
                var damageable = Server.System<DamageableSystem>();
                var health = Server.System<SharedBodyPartHealthSystem>();
                var torso = Part(human, BodyPartType.Torso);
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                probe.Target = torso;
                damageable.TryChangeDamage(human, Damage("Slash", 15), ignoreResistances: true);
                probe.Target = arm;
                damageable.TryChangeDamage(human, Damage("Piercing", 10), ignoreResistances: true);
                damageable.TryChangeDamage(human, Damage("Heat", 5), ignoreResistances: true);
                var torsoBefore = SEntMan.GetComponent<BodyPartHealthComponent>(torso).Current;

                Assert.That(health.HealPartDamage(human, arm, "Brute", FixedPoint2.New(100)), Is.EqualTo(FixedPoint2.New(10)));
                Assert.That(health.HealPartDamage(human, arm, "Brute", FixedPoint2.New(100)), Is.EqualTo(FixedPoint2.Zero));
                var aggregate = damageable.GetAllDamage(human);
                Assert.Multiple(() =>
                {
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Slash"), Is.EqualTo(FixedPoint2.New(15)));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.New(5)));
                    Assert.That(SEntMan.GetComponent<BodyPartHealthComponent>(torso).Current, Is.EqualTo(torsoBefore));
                    Assert.That(Debt(arm, "Heat"), Is.EqualTo(FixedPoint2.New(5)));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [Test]
    public async Task ForeignPartDamageAndCancelledOrWrongPatientSeveranceDoNotCommit()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            var other = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var before = SEntMan.GetComponent<BodyPartHealthComponent>(arm).Current;
                Assert.That(Server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(other, arm, Damage("Slash", 10)), Is.False);
                var cancelled = new BodyPartSeverAttemptEvent(human, arm, BodyPartType.Arm, Surgical: true) { Cancelled = true };
                SEntMan.EventBus.RaiseLocalEvent(arm, ref cancelled);
                var foreign = new BodyPartSeverAttemptEvent(other, arm, BodyPartType.Arm, Surgical: true);
                SEntMan.EventBus.RaiseLocalEvent(arm, ref foreign);
                Assert.Multiple(() =>
                {
                    Assert.That(cancelled.Succeeded, Is.False);
                    Assert.That(foreign.Succeeded, Is.False);
                    Assert.That(SEntMan.GetComponent<BodyPartComponent>(arm).Body, Is.EqualTo(human));
                    Assert.That(SEntMan.GetComponent<BodyPartHealthComponent>(arm).Current, Is.EqualTo(before));
                    Assert.That(Server.System<RegionalDamageProbeSystem>().SeveredParts, Does.Not.Contain(arm));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                SEntMan.DeleteEntity(other);
            }
        });
    }

    [Test]
    public async Task CommittedSeveranceRemovesTheCompleteSubtreeContributionAndPreservesDetachedInjury()
    {
        await Server.WaitAssertion(() =>
        {
            var observer = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            EntityUid? detachedBody = null;
            try
            {
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var hand = Part(human, BodyPartType.Hand, BodyPartSymmetry.Right);
                var torso = Part(human, BodyPartType.Torso);
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                var damageable = Server.System<DamageableSystem>();
                var armSlot = Server.System<CMUMedicalBodyIndexSystem>().GetBodyPartSlots(torso).Single(slot => slot.Part == arm).SlotId;
                probe.Target = torso;
                damageable.TryChangeDamage(human, Damage("Slash", 13), ignoreResistances: true);
                probe.Target = arm;
                damageable.TryChangeDamage(human, Damage("Piercing", 11), ignoreResistances: true);
                probe.Target = hand;
                damageable.TryChangeDamage(human, Damage("Heat", 7), ignoreResistances: true);
                var attempt = new BodyPartSeverAttemptEvent(human, arm, BodyPartType.Arm, Surgical: true);
                SEntMan.EventBus.RaiseLocalEvent(arm, ref attempt);
                detachedBody = attempt.DetachedBody;
                var aggregate = damageable.GetAllDamage(human);
                Assert.Multiple(() =>
                {
                    Assert.That(attempt.Succeeded, Is.True);
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Slash"), Is.EqualTo(FixedPoint2.New(13)));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(Debt(arm, "Piercing"), Is.EqualTo(FixedPoint2.New(11)));
                    Assert.That(Debt(hand, "Heat"), Is.EqualTo(FixedPoint2.New(7)));
                    Assert.That(SEntMan.GetComponent<BodyPartComponent>(hand).Body, Is.EqualTo(detachedBody));
                    Assert.That(observer.SeveredParts, Does.Contain(arm));
                    Assert.That(observer.SeveredParts, Does.Contain(hand));
                });

                var statuses = Server.System<StatusEffectsSystem>();
                Assert.That(statuses.HasStatusEffect(human, "StatusEffectCMUMissingHandRight"), Is.True);
                Assert.That(Server.System<SharedBodySystem>().AttachPart(torso, armSlot, arm), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(statuses.HasStatusEffect(human, "StatusEffectCMUMissingArmRight"), Is.False);
                    Assert.That(statuses.HasStatusEffect(human, "StatusEffectCMUMissingHandRight"), Is.False);
                    Assert.That(damageable.GetAllDamage(human).DamageDict.GetValueOrDefault("Piercing"), Is.EqualTo(FixedPoint2.New(11)));
                    Assert.That(damageable.GetAllDamage(human).DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.New(7)));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                if (detachedBody is { } detached)
                    SEntMan.DeleteEntity(detached);
            }
        });
    }

    [TestCase(BodyPartType.Arm)]
    [TestCase(BodyPartType.Leg)]
    public async Task SameTickSeverAttachAndSeverPreservesTheLatestMissingSubtreeStatus(BodyPartType rootType)
    {
        EntityUid human = default;
        EntityUid root = default;
        EntityUid torso = default;
        string slotId = string.Empty;
        var detachedBodies = new List<EntityUid>();
        var statuses = new List<(string Status, string Alert, EntityUid Retired, EntityUid Current)>();
        var childType = rootType == BodyPartType.Arm ? BodyPartType.Hand : BodyPartType.Foot;
        try
        {
            await Server.WaitAssertion(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                root = Part(human, rootType, BodyPartSymmetry.Right);
                torso = Part(human, BodyPartType.Torso);
                slotId = Server.System<CMUMedicalBodyIndexSystem>().GetBodyPartSlots(torso)
                    .Single(slot => slot.Part == root).SlotId;
                var statusEffects = Server.System<StatusEffectsSystem>();
                var first = new BodyPartSeverAttemptEvent(human, root, rootType, Surgical: true);
                SEntMan.EventBus.RaiseLocalEvent(root, ref first);
                Assert.That(first.Succeeded, Is.True);
                detachedBodies.Add(first.DetachedBody!.Value);
                var retired = new Dictionary<BodyPartType, EntityUid>();
                foreach (var type in new[] { rootType, childType })
                {
                    Assert.That(statusEffects.TryGetStatusEffect(human, $"StatusEffectCMUMissing{type}Right", out var effect), Is.True);
                    retired[type] = effect!.Value;
                }

                Assert.That(Server.System<SharedBodySystem>().AttachPart(torso, slotId, root), Is.True);
                var second = new BodyPartSeverAttemptEvent(human, root, rootType, Surgical: true);
                SEntMan.EventBus.RaiseLocalEvent(root, ref second);
                Assert.That(second.Succeeded, Is.True);
                detachedBodies.Add(second.DetachedBody!.Value);
                foreach (var type in new[] { rootType, childType })
                {
                    var status = $"StatusEffectCMUMissing{type}Right";
                    Assert.That(statusEffects.TryGetStatusEffect(human, status, out var effect), Is.True);
                    Assert.That(effect, Is.Not.EqualTo(retired[type]), "A committed attachment left the old missing-site source alive.");
                    statuses.Add((status, $"CMUMissing{type}", retired[type], effect!.Value));
                }
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var statusEffects = Server.System<StatusEffectsSystem>();
                var alerts = Server.System<AlertsSystem>();
                foreach (var (status, alert, retired, current) in statuses)
                {
                    Assert.That(SEntMan.EntityExists(retired), Is.False);
                    Assert.That(SEntMan.EntityExists(current), Is.True);
                    Assert.That(statusEffects.HasStatusEffect(human, status), Is.True,
                        "The deletion flush removed the renewed missing-site source.");
                    Assert.That(alerts.IsShowingAlert(human, alert), Is.True);
                }
                Assert.That(Server.System<SharedBodySystem>().AttachPart(torso, slotId, root), Is.True);
                foreach (var (status, _, _, _) in statuses)
                    Assert.That(statusEffects.HasStatusEffect(human, status), Is.False);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                var alerts = Server.System<AlertsSystem>();
                foreach (var (_, alert, _, _) in statuses)
                    Assert.That(alerts.IsShowingAlert(human, alert), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(human))
                    SEntMan.DeleteEntity(human);
                foreach (var detached in detachedBodies)
                {
                    if (SEntMan.EntityExists(detached))
                        SEntMan.DeleteEntity(detached);
                }
            });
        }
    }

    [Test]
    public async Task MixedImpactKeepsBruteAndBurnRowsSeparate()
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            try
            {
                var torso = Part(human, BodyPartType.Torso);
                SEntMan.GetComponent<RegionalDamageProbeComponent>(human).Target = torso;
                var mixed = Damage("Slash", 20);
                mixed.DamageDict["Heat"] = FixedPoint2.New(10);
                Server.System<DamageableSystem>().TryChangeDamage(human, mixed, ignoreResistances: true);
                var wounds = SEntMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = Server.System<CMUWoundLedgerSystem>().GetEntries(wounds);
                Assert.Multiple(() =>
                {
                    // Torso structural resistance turns the actual 20 Slash into 18 wound damage.
                    Assert.That(entries.Where(entry => entry.Wound.Type == WoundType.Brute).Sum(entry => entry.Wound.Damage.Float()), Is.EqualTo(18));
                    Assert.That(entries.Where(entry => entry.Wound.Type == WoundType.Burn).Sum(entry => entry.Wound.Damage.Float()), Is.EqualTo(10));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    private EntityUid Part(EntityUid human, BodyPartType type, BodyPartSymmetry symmetry = BodyPartSymmetry.None)
    {
        Assert.That(Server.System<CMUMedicalBodyIndexSystem>().TryGetBodyPart(human,
            new CMUMedicalBodyPartKey(type, symmetry), out var part), Is.True);
        return part;
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task GenericHealingOrOverwriteCannotLeaveRobotDebtThatRepairsFreshOrganicInjury(bool overwrite)
    {
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            try
            {
                var damageable = Server.System<DamageableSystem>();
                var partHealth = Server.System<SharedBodyPartHealthSystem>();
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var torso = Part(human, BodyPartType.Torso);
                SEntMan.EnsureComponent<CMURoboticLimbComponent>(arm);
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                probe.Target = arm;
                damageable.TryChangeDamage(human, Damage("Slash", 10), ignoreResistances: true);
                if (overwrite)
                    damageable.SetDamage(human, Damage("Slash", 0));
                else
                    damageable.TryChangeDamage(human, Damage("Slash", -100), ignoreResistances: true);
                Assert.That(Debt(arm, "Slash"), Is.EqualTo(FixedPoint2.New(overwrite ? 0 : 10)));
                probe.Target = torso;
                damageable.TryChangeDamage(human, Damage("Slash", 8), ignoreResistances: true);
                var torsoBefore = SEntMan.GetComponent<BodyPartHealthComponent>(torso).Current;
                partHealth.HealPartDamage(human, arm, "Brute", FixedPoint2.New(100));
                Assert.Multiple(() =>
                {
                    Assert.That(damageable.GetAllDamage(human).DamageDict.GetValueOrDefault("Slash"), Is.EqualTo(FixedPoint2.New(8)));
                    Assert.That(Debt(torso, "Slash"), Is.EqualTo(FixedPoint2.New(8)));
                    Assert.That(SEntMan.GetComponent<BodyPartHealthComponent>(torso).Current, Is.EqualTo(torsoBefore));
                });
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [Test]
    public async Task RandomIncomingHitsReachBothSidesOfPairedLimbs()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var damageable = Server.System<DamageableSystem>();
                for (var i = 0; i < 400; i++)
                    damageable.TryChangeDamage(human,
                        new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(0.01) } }, ignoreResistances: true);
                foreach (var type in new[] { BodyPartType.Arm, BodyPartType.Leg })
                {
                    foreach (var side in new[] { BodyPartSymmetry.Left, BodyPartSymmetry.Right })
                        Assert.That(Debt(Part(human, type, side), "Slash"), Is.GreaterThan(FixedPoint2.Zero), $"{side} {type}");
                }
            }
            finally
            {
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [Test]
    public async Task ExactGroinAimKeepsGroinZoneInsteadOfCanonicalizingToChest()
    {
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<RegionalDamageProbeSystem>();
            var human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
            var attacker = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            try
            {
                var aim = SEntMan.GetComponent<BodyZoneTargetingComponent>(attacker);
                aim.MeleeAccuracy = 1f;
                Server.System<SharedBodyZoneTargetingSystem>().SelectZone(attacker, TargetBodyZone.GroinPelvis);
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                // Accuracy is intentionally capped at 95%; allow called-shot misses without accepting chest substitution.
                for (var i = 0; i < 20 && probe.LastZone != TargetBodyZone.GroinPelvis; i++)
                    Server.System<DamageableSystem>().TryChangeDamage(human,
                        new DamageSpecifier { DamageDict = { ["Slash"] = FixedPoint2.New(0.01) } },
                        ignoreResistances: true, origin: attacker);
                Assert.That(probe.LastZone, Is.EqualTo(TargetBodyZone.GroinPelvis));
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                SEntMan.DeleteEntity(attacker);
            }
        });
    }

    [Test]
    public async Task RestoringFracturedBoneRemovesItsInternalBleedingAfterCommittedRecovery()
    {
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var setter = SEntMan.SpawnEntity("CMBonesetter", MapCoordinates.Nullspace);
            var splint = SEntMan.SpawnEntity("CMUSplintItem", MapCoordinates.Nullspace);
            try
            {
                var arm = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var bones = Server.System<SharedBoneSystem>();
                Assert.That(bones.SeedFracture(arm, FractureSeverity.Shattered), Is.True);
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(arm), Is.True);

                // Shattered fractures require surgical realignment before medicine can
                // mend the remaining splinted injury. The raw integrity setter does not
                // commit a fracture-stage change by itself.
                var step = Server.System<SharedCMSurgerySystem>().GetSingleton("CMUSurgeryStepRealignShatteredBone")!.Value;
                var realign = new CMSurgeryStepEvent(human, human, arm, new List<EntityUid> { setter }) { Used = setter };
                Assert.That(Server.System<SharedCMUSurgerySystem>().TryExecuteStep(step, ref realign),
                    Is.EqualTo(CMUSurgeryStepOutcome.Succeeded));
                Assert.That(SEntMan.GetComponent<FractureComponent>(arm).Severity, Is.EqualTo(FractureSeverity.Compound));
                Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                Assert.That(Server.System<SharedCMUSplintItemSystem>().ApplySplintToPart(
                    (splint, SEntMan.GetComponent<CMUSplintItemComponent>(splint)), arm), Is.True);
                var bone = SEntMan.GetComponent<BoneComponent>(arm);
                Assert.That(bones.ChemicallyMendFractures(human, bone.IntegrityMax), Is.EqualTo(1));
                Assert.Multiple(() =>
                {
                    Assert.That(bone.Integrity, Is.EqualTo(bone.IntegrityMax));
                    Assert.That(SEntMan.HasComponent<FractureComponent>(arm), Is.False);
                    Assert.That(SEntMan.HasComponent<InternalBleedingComponent>(arm), Is.False);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(setter);
                SEntMan.DeleteEntity(splint);
                SEntMan.DeleteEntity(human);
            }
        });
    }

    [TestCase(BodyPartType.Torso, false)]
    [TestCase(BodyPartType.Head, false)]
    [TestCase(BodyPartType.Torso, true)]
    public async Task TreatedResistantWoundsRecoverTheirOwnPoolWithoutHealingAnotherRegion(BodyPartType site, bool anatomyOnly)
    {
        EntityUid human = default;
        EntityUid part = default;
        EntityUid otherPart = default;
        FixedPoint2 otherHealth = default;
        FixedPoint2 initialBruteWound = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                _ = Server.System<RegionalDamageProbeSystem>();
                human = SEntMan.SpawnEntity("RegionalDamageTransactionHuman", MapCoordinates.Nullspace);
                part = Part(human, site);
                otherPart = Part(human, BodyPartType.Arm, BodyPartSymmetry.Right);
                var damageable = Server.System<DamageableSystem>();
                var probe = SEntMan.GetComponent<RegionalDamageProbeComponent>(human);
                probe.Target = otherPart;
                damageable.TryChangeDamage(human,
                    new DamageSpecifier { DamageDict = { ["Slash"] = 8, ["Heat"] = 7 } }, ignoreResistances: true);
                otherHealth = SEntMan.GetComponent<BodyPartHealthComponent>(otherPart).Current;
                probe.Target = part;
                var injury = new DamageSpecifier { DamageDict = { ["Slash"] = 6, ["Heat"] = 3 } };
                if (anatomyOnly)
                    Assert.That(Server.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(human, part, injury), Is.True);
                else
                    damageable.TryChangeDamage(human, injury, ignoreResistances: true);

                var wounds = SEntMan.GetComponent<BodyPartWoundComponent>(part);
                initialBruteWound = Server.System<CMUWoundLedgerSystem>().GetEntries(wounds).Single(e => e.Wound.Type == WoundType.Brute).Wound.Damage;
                Assert.That(initialBruteWound.Float(), Is.EqualTo(site == BodyPartType.Torso ? 5.4f : 5.1f).Within(0.011f),
                    "Use the real torso/head structural resistance rather than a synthetic wound amount.");
                var treatment = Server.System<SharedCMUWoundsSystem>();
                Assert.That(treatment.TryTreatWounds(part, WoundType.Brute, 6, out var bruteTreated), Is.True);
                Assert.That(treatment.TryTreatWounds(part, WoundType.Burn, 6, out var burnTreated), Is.True);
                Assert.That(bruteTreated, Is.EqualTo(1));
                Assert.That(burnTreated, Is.EqualTo(1));
            });

            await Pair.RunTicksSync(Pair.SecondsToTicks(2f));
            await Server.WaitAssertion(() =>
            {
                var wounds = SEntMan.GetComponent<BodyPartWoundComponent>(part);
                var brute = Server.System<CMUWoundLedgerSystem>().GetEntries(wounds).Single(e => e.Wound.Type == WoundType.Brute).Wound;
                var expected = anatomyOnly ? 0f : 6f * (brute.Damage - brute.Healed).Float() / initialBruteWound.Float();
                Assert.That(brute.Healed, Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(Debt(part, "Slash").Float(), Is.EqualTo(expected).Within(0.02f));
                Assert.That(Debt(otherPart, "Slash"), Is.EqualTo(FixedPoint2.New(8)));
                Assert.That(Debt(otherPart, "Heat"), Is.EqualTo(FixedPoint2.New(7)));
            });

            await Pair.RunTicksSync(Pair.SecondsToTicks(20f));
            await Server.WaitAssertion(() =>
            {
                var aggregate = Server.System<DamageableSystem>().GetAllDamage(human);
                var health = SEntMan.GetComponent<BodyPartHealthComponent>(part);
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<BodyPartWoundComponent>(part), Is.False);
                    Assert.That(Debt(part, "Slash"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(Debt(part, "Heat"), Is.EqualTo(FixedPoint2.Zero));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Slash"), Is.EqualTo(FixedPoint2.New(8)));
                    Assert.That(aggregate.DamageDict.GetValueOrDefault("Heat"), Is.EqualTo(FixedPoint2.New(7)));
                    Assert.That(health.Current, Is.EqualTo(health.Max), "Anatomy-only wounds still restore structural health.");
                    Assert.That(SEntMan.GetComponent<BodyPartHealthComponent>(otherPart).Current, Is.EqualTo(otherHealth));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() => SEntMan.DeleteEntity(human));
        }
    }

    [Test]
    public async Task ReplicatedPatientDeletionDoesNotCreateNewOrganFailureStatusChildren()
    {
        var player = Pair.Player!;
        var originalAttached = player.AttachedEntity;
        EntityUid human = default;
        NetEntity humanNet = default;
        NetEntity[] anatomy = [];
        try
        {
            await Server.WaitPost(() =>
            {
                human = originalAttached is { } original
                    ? SEntMan.SpawnEntity("CMMobHuman", SEntMan.GetComponent<TransformComponent>(original).Coordinates)
                    : SEntMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                humanNet = SEntMan.GetNetEntity(human);
                anatomy = Server.System<CMUMedicalBodyIndexSystem>().GetOrgans(human)
                    .Select(organ => SEntMan.GetNetEntity(organ.Owner)).ToArray();
                Server.PlayerMan.SetAttachedEntity(player, human);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.TryGetEntity(humanNet, out var clientHuman), Is.True);
                Assert.That(CEntMan.GetComponent<TransformComponent>(clientHuman!.Value).ChildCount, Is.GreaterThan(1),
                    "The test must exercise a replicated hierarchy, not delete an unseen patient.");
            });

            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(player, originalAttached);
                SEntMan.DeleteEntity(human);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.TryGetEntity(humanNet, out _), Is.False);
                foreach (var organ in anatomy)
                    Assert.That(CEntMan.TryGetEntity(organ, out _), Is.False);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(player, originalAttached);
                if (SEntMan.EntityExists(human))
                    SEntMan.DeleteEntity(human);
            });
        }
    }

    private FixedPoint2 Debt(EntityUid part, string type)
        => SEntMan.GetComponent<BodyPartHealthComponent>(part).BodyDamage.DamageDict.GetValueOrDefault(type);

    private static DamageSpecifier Damage(string type, int amount)
        => new() { DamageDict = { [type] = FixedPoint2.New(amount) } };
}

[RegisterComponent]
public sealed partial class RegionalDamageProbeComponent : Component
{
    public EntityUid? Target;
    public EntityUid? NestedTarget;
    public bool NestAfterCommit;
    public TargetBodyZone? LastZone;
}

public sealed partial class RegionalDamageProbeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    public readonly HashSet<EntityUid> SeveredParts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RegionalDamageProbeComponent, HitLocationResolveEvent>(OnResolve);
        SubscribeLocalEvent<RegionalDamageProbeComponent, DamageModifyAfterResistEvent>(OnAfterResist);
        SubscribeLocalEvent<RegionalDamageProbeComponent, DamageChangedEvent>(OnChanged);
        SubscribeLocalEvent<BodyPartSeveredEvent>(OnSevered);
    }

    private void OnResolve(Entity<RegionalDamageProbeComponent> ent, ref HitLocationResolveEvent args)
    {
        if (ent.Comp.Target is not { } target)
            return;
        args.ResolvedPartEntity = target;
        args.ResolvedPart = Comp<BodyPartComponent>(target).PartType;
        args.Handled = true;
    }

    private void OnAfterResist(Entity<RegionalDamageProbeComponent> ent, ref DamageModifyAfterResistEvent args)
    {
        if (!ent.Comp.NestAfterCommit)
            NestedDamage(ent);
    }

    private void OnChanged(Entity<RegionalDamageProbeComponent> ent, ref DamageChangedEvent args)
    {
        ent.Comp.LastZone = args.TargetZone;
        if (ent.Comp.NestAfterCommit)
            NestedDamage(ent);
    }

    private void NestedDamage(Entity<RegionalDamageProbeComponent> ent)
    {
        if (ent.Comp.NestedTarget is not { } nested)
            return;
        ent.Comp.NestedTarget = null;
        var previous = ent.Comp.Target;
        ent.Comp.Target = nested;
        _damageable.TryChangeDamage(ent.Owner,
            new DamageSpecifier { DamageDict = { ["Piercing"] = FixedPoint2.New(7) } }, ignoreResistances: true);
        ent.Comp.Target = previous;
    }

    private void OnSevered(ref BodyPartSeveredEvent args) => SeveredParts.Add(args.Part);
}
