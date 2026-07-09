using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Diagnostics.Examine;
using Content.Shared._CMU14.Medical.Injuries.Trauma;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Injuries.Wounds.Events;
using Content.Server._CMU14.Medical.Diagnostics.Examine;
using Content.Server._CMU14.Medical.Injuries.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Server.Verbs;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Collections.Generic;

namespace Content.IntegrationTests._CMU14.Medical.Injuries.Wounds;

[TestFixture]
public sealed class MechanismWoundsFoundationTest
{
    [Test]
    public async Task LedgerEntryStaysCoherentAcrossAddTreatAndRemove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(torso);
                var wound = new Wound(12, FixedPoint2.Zero, 1f, TimeSpan.FromSeconds(10), WoundType.Brute, false);

                Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
                    wound,
                    WoundSize.CutSmall,
                    0,
                    WoundMechanism.Bullet,
                    WoundMechanismFlags.Fragment,
                    WoundTreatmentQuality.Untreated,
                    WoundCleanupFlags.RetainedFragment | WoundCleanupFlags.DirtyDressing)), Is.EqualTo(0));

                Assert.That(woundsSystem.TryTreatWound(
                    torso,
                    WoundTreatmentQuality.Optimal,
                    out var completed,
                    wounds,
                    type: WoundType.Brute,
                    mechanismMask: WoundMechanismFlags.Bullet,
                    cleanupClears: WoundCleanupFlags.DirtyDressing), Is.True);

                var entries = ledger.GetEntries(wounds);
                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Wound.Damage, Is.EqualTo(wound.Damage));
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].Size, Is.EqualTo(WoundSize.CutSmall));
                    Assert.That(entries[0].Bandages, Is.EqualTo(WoundSizeProfile.BandagesRequired(WoundSize.CutSmall, wound.Damage.Float())));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(entries[0].SecondaryMechanisms, Is.EqualTo(WoundMechanismFlags.Fragment));
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
                });

                Assert.That(ledger.TryRemoveEntry(wounds, 0), Is.True);
                Assert.That(ledger.GetEntries(wounds), Is.Empty);
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ProjectilePiercingCreatesBulletWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 30),
                    impact: DamageImpact.Projectile), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Untreated));
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
    public async Task HealthScannerBodyChipReadoutIncludesWoundMechanism()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 30),
                    impact: DamageImpact.Projectile), Is.True);

                var state = new HealthScannerBuiState(
                    entMan.GetNetEntity(human),
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    null,
                    null,
                    false);
                var ev = new HealthScannerBuildStateEvent(human, human, null, state);
                entMan.EventBus.RaiseLocalEvent(human, ref ev);

                Assert.That(state.CMUParts, Is.Not.Null);
                var torsoReadout = FindReadout(state, BodyPartType.Torso, BodyPartSymmetry.None);

                Assert.Multiple(() =>
                {
                    Assert.That(torsoReadout.WoundDescriptor, Is.EqualTo(WoundSize.CutFlesh));
                    Assert.That(torsoReadout.WoundMechanism, Is.EqualTo(WoundMechanism.Bullet));
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
    public async Task ExplosionCreatesBlastWoundWithBurnSecondary()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage(("Blunt", 30), ("Heat", 30)),
                    mechanism: CMUTraumaMechanism.Explosive,
                    impact: DamageImpact.Explosion), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Blast));
                    Assert.That(entries[0].SecondaryMechanisms & WoundMechanismFlags.Burn, Is.Not.EqualTo(WoundMechanismFlags.None));
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
    public async Task RepeatedSameMechanismMergesIntoLargerWound()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);

                Assert.Multiple(() =>
                {
                    Assert.That(entries, Has.Count.EqualTo(1));
                    Assert.That(entries[0].Mechanism, Is.EqualTo(WoundMechanism.Slash));
                    Assert.That(entries[0].Wound.Damage, Is.EqualTo(FixedPoint2.New(36)));
                    Assert.That(entries[0].Size, Is.EqualTo(WoundSize.CutFlesh));
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
    public async Task DistinctMechanismsRespectSixRecordCap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 10), impact: DamageImpact.Projectile), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 10), impact: new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Stab, DamageImpactPenetration.Medium, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Blunt", 10), impact: new DamageImpact(DamageImpactDelivery.Melee, DamageImpactContact.Crush, DamageImpactPenetration.None, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Heat", 10), impact: new DamageImpact(DamageImpactDelivery.Contact, DamageImpactContact.Burn, DamageImpactPenetration.None, DamageImpactEnergy.Medium)), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Blunt", 10), mechanism: CMUTraumaMechanism.Explosive, impact: DamageImpact.Explosion), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: new DamageImpact(DamageImpactDelivery.Thrown, DamageImpactContact.Fragment, DamageImpactPenetration.Low, DamageImpactEnergy.Medium)), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(ledger.GetEntries(wounds), Has.Count.EqualTo(6));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NewWoundCreatesLimbExternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.Moderate));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TreatingAnyWoundOnLimbClearsExternalBleeding()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(wounds.ExternalBleeding, Is.Not.EqualTo(ExternalBleedTier.None));

                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.False);

                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NewDamageCanRestartExternalBleedingAfterTreatment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(woundsSystem.TryTreatWound(torso, out _), Is.True);
                Assert.That(wounds.ExternalBleeding, Is.EqualTo(ExternalBleedTier.None));

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 20), impact: DamageImpact.Projectile), Is.True);

                Assert.That(wounds.ExternalBleeding, Is.Not.EqualTo(ExternalBleedTier.None));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WoundTreatmentClearsCleanupAndUsesTreatedState()
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

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(entries[0].Cleanup, Is.Not.EqualTo(WoundCleanupFlags.None));

                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
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
    public async Task WoundTreatmentClearsAllCleanupFlags()
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

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entry = ledger.GetEntries(wounds)[0];
                Assert.That(ledger.TryUpdateEntry(wounds, 0, entry with
                {
                    Cleanup = WoundCleanupFlags.PoorClosure | WoundCleanupFlags.DirtyDressing,
                }), Is.True);

                Assert.That(woundsSystem.TryTreatWound(
                    torso,
                    out var completed,
                    cleanupClears: WoundCleanupFlags.DirtyDressing), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(ledger.GetEntries(wounds)[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
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
    public async Task OptimalTreatmentRequestFallsBackToNormalTreatedState()
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

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entries = ledger.GetEntries(wounds);
                Assert.That(entries[0].Cleanup, Is.Not.EqualTo(WoundCleanupFlags.None));

                Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(entries[0].Wound.Treated, Is.True);
                    Assert.That(entries[0].TreatmentQuality, Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(entries[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
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
    public async Task FieldTreatmentCapCombinesWoundBurdenAndClamps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var ledger = server.EntMan.System<CMUWoundLedgerSystem>();
            var wounds = new BodyPartWoundComponent();

            Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
                new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, false),
                WoundSize.CutDeep,
                0,
                WoundMechanism.Generic,
                WoundMechanismFlags.None,
                WoundTreatmentQuality.Untreated,
                WoundCleanupFlags.PoorClosure)), Is.EqualTo(0));
            Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
                new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, true),
                WoundSize.CutMassive,
                WoundSizeProfile.BandagesRequired(WoundSize.CutMassive, 10f),
                WoundMechanism.Generic,
                WoundMechanismFlags.None,
                WoundTreatmentQuality.Adequate,
                WoundCleanupFlags.CrushDebris)), Is.EqualTo(1));

            Assert.That(SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds), Is.EqualTo(0.88f).Within(0.001f));

            for (var i = 0; i < 4; i++)
            {
                Assert.That(ledger.AddEntry(wounds, new CMUWoundEntry(
                    new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, false),
                    WoundSize.CutMassive,
                    0,
                    WoundMechanism.Generic,
                    WoundMechanismFlags.None,
                    WoundTreatmentQuality.Untreated,
                    WoundCleanupFlags.CrushDebris)), Is.EqualTo(i + 2));
            }

            Assert.That(SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds), Is.EqualTo(0.35f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CleanWoundRecordsAreRemovedAfterRecovery()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var prototypes = server.ResolveDependency<IPrototypeManager>();

        EntityUid human = default;
        EntityUid torso = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var entry = ledger.GetEntries(wounds)[0];
            Assert.That(ledger.TryUpdateEntry(wounds, 0, entry with
            {
                Wound = entry.Wound with { Damage = FixedPoint2.New(1) },
            }), Is.True);

            Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);
            Assert.That(completed, Is.True);
            Assert.That(ledger.GetEntries(wounds)[0].Cleanup, Is.EqualTo(WoundCleanupFlags.None));
        });

        await pair.RunTicksSync(pair.SecondsToTicks(3f));

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            Assert.That(entMan.HasComponent<BodyPartWoundComponent>(torso), Is.False);

            var damageable = entMan.GetComponent<DamageableComponent>(human);
            var brute = DamageInGroup(prototypes, damageable.Damage, "Brute");
            var health = entMan.GetComponent<BodyPartHealthComponent>(torso);
            Assert.Multiple(() =>
            {
                Assert.That(brute, Is.EqualTo(FixedPoint2.Zero));
                Assert.That(health.Current, Is.EqualTo(health.Max));
            });

            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetailedExamineShowsMechanismAndTreatmentStateWithoutOptimalHint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var fractureSystem = entMan.System<SharedFractureSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(
                human,
                torso,
                Damage("Slash", 20),
                impact: DamageImpact.MeleeSlash), Is.True);
            var fracture = entMan.EnsureComponent<FractureComponent>(torso);
            fractureSystem.SetSeverity((torso, fracture), FractureSeverity.Hairline);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("deep cut"));
                    Assert.That(text, Does.Contain("deep cut[/color]\n  [color=#ffd166]untreated[/color]\n  [color=#ff5f5f]external bleeding: moderate[/color]"));
                    Assert.That(text, Does.Not.Contain("optimal:"));
                    Assert.That(text, Does.Not.Contain("adequate treatment"));
                    Assert.That(text, Does.Not.Contain("cleanup needed"));
                    Assert.That(text, Does.Contain("external bleeding: moderate"));
                    Assert.That(text, Does.Not.Contain("bone:"));
                    Assert.That(text, Does.Not.Contain("organ"));
                    Assert.That(text, Does.Not.Contain("internal bleeding"));
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
    public async Task DetailedExamineShowsTreatedWoundsWithoutCleanupOrQualityLabels()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(
                human,
                torso,
                Damage("Slash", 10),
                impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
            Assert.That(completed, Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("cut[/color]\n  [color=#7bd88f]treated[/color]"));
                    Assert.That(text, Does.Not.Contain("adequate treatment"));
                    Assert.That(text, Does.Not.Contain("cleanup needed"));
                    Assert.That(text, Does.Not.Contain("dirty dressing"));
                    Assert.That(text, Does.Not.Contain("optimal:"));
                    Assert.That(text, Does.Not.Contain("bone:"));
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
    public async Task DetailedExamineShowsOptimalRequestsAsNormalTreatedWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(
                human,
                torso,
                Damage("Slash", 10),
                impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);
            Assert.That(completed, Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("cut[/color]\n  [color=#7bd88f]treated[/color]"));
                    Assert.That(text, Does.Not.Contain("optimal treatment"));
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
    public async Task NormalExamineSummarizesTreatedWoundsWithoutTreatmentQuality()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
            var wounds = entMan.EnsureComponent<BodyPartWoundComponent>(torso);

            AddVisibleWound(ledger, wounds, WoundSize.CutMassive, WoundTreatmentQuality.Adequate);
            AddVisibleWound(ledger, wounds, WoundSize.CutDeep, WoundTreatmentQuality.Adequate);
            AddVisibleWound(ledger, wounds, WoundSize.CutMassive, WoundTreatmentQuality.Optimal);
            AddVisibleWound(ledger, wounds, WoundSize.CutSmall, WoundTreatmentQuality.Optimal);
            var changed = new BodyPartWoundsChangedEvent(torso, false);
            entMan.EventBus.RaiseEvent(EventSource.Local, ref changed);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            try
            {
                var examine = new ExaminedEvent(new FormattedMessage(), human, human, true, false);
                entMan.EventBus.RaiseLocalEvent(human, examine);
                var text = examine.GetTotalMessage().ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("wounds treated"));
                    Assert.That(text, Does.Not.Contain("adequately treated"));
                    Assert.That(text, Does.Not.Contain("optimally treated"));
                    Assert.That(text, Does.Not.Contain("massive wound"));
                    Assert.That(text, Does.Not.Contain("moderate wound"));
                    Assert.That(text, Does.Not.Contain("small wound"));
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
    public async Task NormalExamineShowsSimpleFracturesButHidesHairlines()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var fractureSystem = entMan.System<SharedFractureSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var leftArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Left);
                var rightArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);

                var simple = entMan.EnsureComponent<FractureComponent>(leftArm);
                fractureSystem.SetSeverity((leftArm, simple), FractureSeverity.Simple);

                var hairline = entMan.EnsureComponent<FractureComponent>(rightArm);
                fractureSystem.SetSeverity((rightArm, hairline), FractureSeverity.Hairline);

                var examine = new ExaminedEvent(new FormattedMessage(), human, human, true, false);
                entMan.EventBus.RaiseLocalEvent(human, examine);
                var text = examine.GetTotalMessage().ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("Left arm"));
                    Assert.That(text, Does.Contain("simple fracture"));
                    Assert.That(text, Does.Not.Contain("Right arm"));
                    Assert.That(text, Does.Not.Contain("hairline fracture"));
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
    public async Task DetailedExamineOrdersHeadTorsoThenLimbsAndUsesMarkup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var head = GetBodyPart(entMan, human, BodyPartType.Head);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
            var leftArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Left);

            Assert.That(partHealth.TryApplyPartDamage(human, leftArm, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(partHealth.TryApplyPartDamage(human, head, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetDetailedExamineText(human);
                var headIndex = text.IndexOf("Head", StringComparison.Ordinal);
                var torsoIndex = text.IndexOf("Torso", StringComparison.Ordinal);
                var armIndex = text.IndexOf("Left arm", StringComparison.Ordinal);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("[bold][color=#9fc7ff]Head[/color][/bold]"));
                    Assert.That(text, Does.Contain("[color=#ffb86c]ripped cut[/color]"));
                    Assert.That(headIndex, Is.GreaterThanOrEqualTo(0));
                    Assert.That(torsoIndex, Is.GreaterThan(headIndex));
                    Assert.That(armIndex, Is.GreaterThan(torsoIndex));
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
    public async Task DetailedExamineShortcutDoesNotStartInspectInjuriesDoAfter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUDetailedMedicalExamineSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                Assert.That(examine.TryStartDetailedExamine(user, patient), Is.False);
                Assert.That(entMan.HasComponent<ActiveDoAfterComponent>(user), Is.False);
                CancelActiveDoAfters(entMan, user);
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetailedExamineUsesCorpsmanDelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUDetailedMedicalExamineSystem>();
            var skills = entMan.System<SkillsSystem>();
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                skills.SetSkill(user, "RMCSkillMedical", 0);
                Assert.That(examine.GetExamineDelay(user), Is.EqualTo(TimeSpan.FromSeconds(2)));

                skills.SetSkill(user, "RMCSkillMedical", 2);
                Assert.That(examine.GetExamineDelay(user), Is.EqualTo(TimeSpan.FromSeconds(0.4)));
            }
            finally
            {
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InspectInjuriesListsSitesWithoutOptimalTreatmentHint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
            var rightArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);

            Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 80), impact: DamageImpact.MeleeSlash), Is.True);
            Assert.That(partHealth.TryApplyPartDamage(human, rightArm, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetInspectInjuriesText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("[color=#ff9f43]Massive Torso, Moderate Right arm[/color]"));
                    Assert.That(text, Does.Not.Contain("Optimal Treatment"));
                    Assert.That(text, Does.Not.Contain("optimal:"));
                    Assert.That(text, Does.Not.Contain("[color=#83c9ff]Massive Torso, Moderate Right arm[/color]"));
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
    public async Task InspectInjuriesListsArterialBleedsByPart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        EntityUid human = default;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var rightArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);

            Assert.That(partHealth.TryApplyPartDamage(human, rightArm, Damage("Slash", 80), impact: DamageImpact.MeleeSlash), Is.True);
        });

        await pair.RunTicksSync(1);

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var examine = entMan.System<CMUMedicalExamineSystem>();

            try
            {
                var text = examine.GetInspectInjuriesText(human);

                Assert.That(text, Does.Contain("[bold][color=#ff5f5f]Arterial Bleeding[/color][/bold]\n  [color=#ff5f5f]Right arm[/color]"));
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BulletWoundsDoNotDefaultToRetainedFragments()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Piercing", 20),
                    impact: DamageImpact.Projectile), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entry = ledger.GetEntries(wounds)[0];
                Assert.Multiple(() =>
                {
                    Assert.That(entry.Mechanism, Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(entry.Cleanup & WoundCleanupFlags.RetainedFragment, Is.EqualTo(WoundCleanupFlags.None));
                    Assert.That(entMan.HasComponent<CMUShrapnelComponent>(torso), Is.False);
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
    public async Task ActualShrapnelAddsExtractionVerbWithKnife()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var shrapnel = entMan.System<SharedCMUShrapnelSystem>();
            var hands = entMan.System<SharedHandsSystem>();
            var verbs = entMan.System<VerbSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var knife = entMan.SpawnEntity("KitchenKnife", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, patient, BodyPartType.Torso);
                Assert.That(shrapnel.AddShrapnel(torso, 1, 10f), Is.True);
                Assert.That(hands.TryPickupAnyHand(user, knife, checkActionBlocker: false), Is.True);

                var local = verbs.GetLocalVerbs(patient, user, typeof(InteractionVerb), force: true);
                Assert.That(ContainsVerb(local, "Remove shrapnel"), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(knife);
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("XenoHedgehogSpikeProjectileSpread")]
    [TestCase("XenoHedgehogSpikeProjectileSpreadShort")]
    public async Task HedgehogSpikeProjectilesAddShrapnel(string projectilePrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var ledger = entMan.System<CMUWoundLedgerSystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(projectilePrototype, MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var damage = entMan.GetComponent<ProjectileComponent>(projectile).Damage;

                Assert.That(partHealth.TryApplyPartDamage(human, torso, damage, tool: projectile), Is.True);
                Assert.That(entMan.TryGetComponent<CMUShrapnelComponent>(torso, out var shrapnel), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                var entry = ledger.GetEntries(wounds)[0];
                Assert.Multiple(() =>
                {
                    Assert.That(shrapnel!.Fragments, Is.EqualTo(1));
                    Assert.That(entry.Cleanup & WoundCleanupFlags.RetainedFragment, Is.Not.EqualTo(WoundCleanupFlags.None));
                });
            }
            finally
            {
                entMan.DeleteEntity(projectile);
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DetailedExamineVerbIsNotAvailableOnCMUHumans()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var verbs = entMan.System<VerbSystem>();
            var patient = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var user = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var local = verbs.GetLocalVerbs(patient, user, typeof(InteractionVerb), force: true);
                Assert.That(ContainsVerb(local, "Inspect injuries"), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(patient);
                entMan.DeleteEntity(user);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static EntityUid GetBodyPart(IEntityManager entMan, EntityUid bodyUid, BodyPartType type)
    {
        var body = entMan.System<SharedBodySystem>();
        foreach (var (partUid, part) in body.GetBodyChildren(bodyUid))
        {
            if (part.PartType == type)
                return partUid;
        }

        Assert.Fail($"Expected CMU human to have {type}.");
        return EntityUid.Invalid;
    }

    private static CMUBodyPartReadout FindReadout(
        HealthScannerBuiState state,
        BodyPartType type,
        BodyPartSymmetry symmetry)
    {
        Assert.That(state.CMUParts, Is.Not.Null);
        foreach (var readout in state.CMUParts!.Values)
        {
            if (readout.Type == type && readout.Symmetry == symmetry)
                return readout;
        }

        Assert.Fail($"Expected scanner body readout for {type} {symmetry}.");
        return default;
    }

    private static EntityUid GetBodyPart(
        IEntityManager entMan,
        EntityUid bodyUid,
        BodyPartType type,
        BodyPartSymmetry symmetry)
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

    private static DamageSpecifier Damage(string type, FixedPoint2 amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        return damage;
    }

    private static DamageSpecifier Damage(params (string Type, FixedPoint2 Amount)[] entries)
    {
        var damage = new DamageSpecifier();
        foreach (var (type, amount) in entries)
        {
            damage.DamageDict[type] = amount;
        }

        return damage;
    }

    private static FixedPoint2 DamageInGroup(IPrototypeManager prototypes, DamageSpecifier damage, string groupId)
    {
        var group = prototypes.Index<DamageGroupPrototype>(groupId);
        return damage.TryGetDamageInGroup(group, out var total) ? total : FixedPoint2.Zero;
    }

    private static void AddVisibleWound(
        CMUWoundLedgerSystem ledger,
        BodyPartWoundComponent comp,
        WoundSize size,
        WoundTreatmentQuality quality)
    {
        Assert.That(ledger.AddEntry(comp, new CMUWoundEntry(
            new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, true),
            size,
            WoundSizeProfile.BandagesRequired(size, 10f),
            WoundMechanism.Slash,
            WoundMechanismFlags.None,
            quality,
            quality == WoundTreatmentQuality.Adequate
                ? WoundCleanupFlags.PoorClosure
                : WoundCleanupFlags.None)), Is.GreaterThanOrEqualTo(0));
    }

    private static bool ContainsVerb(IEnumerable<Verb> verbs, string text)
    {
        foreach (var verb in verbs)
        {
            if (verb.Text == text)
                return true;
        }

        return false;
    }

    private static void CancelActiveDoAfters(IEntityManager entMan, EntityUid user)
    {
        if (!entMan.TryGetComponent<DoAfterComponent>(user, out var doAfters))
            return;

        var doAfterSystem = entMan.System<SharedDoAfterSystem>();
        foreach (var id in new List<ushort>(doAfters.DoAfters.Keys))
        {
            doAfterSystem.Cancel(user, id, doAfters);
        }
    }
}
