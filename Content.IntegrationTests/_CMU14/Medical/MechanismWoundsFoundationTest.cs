using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Examine;
using Content.Shared._CMU14.Medical.Trauma;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Server._CMU14.Medical.Examine;
using Content.Server._CMU14.Medical.Wounds;
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
using Content.Shared._CMU14.Medical.Shrapnel;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using System.Collections.Generic;
using System.Reflection;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class MechanismWoundsFoundationTest
{
    [Test]
    public async Task ProjectilePiercingCreatesBulletWound()
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

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);

                Assert.Multiple(() =>
                {
                    Assert.That(wounds.Wounds, Has.Count.EqualTo(1));
                    Assert.That(wounds.Mechanisms, Has.Count.EqualTo(1));
                    Assert.That(wounds.Mechanisms[0], Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(wounds.TreatmentQualities[0], Is.EqualTo(WoundTreatmentQuality.Untreated));
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
                    Assert.That(torsoReadout.WoundDescriptor, Is.EqualTo(WoundSize.Deep));
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

                Assert.Multiple(() =>
                {
                    Assert.That(wounds.Wounds, Has.Count.EqualTo(1));
                    Assert.That(wounds.Mechanisms[0], Is.EqualTo(WoundMechanism.Blast));
                    Assert.That(wounds.SecondaryMechanisms[0] & WoundMechanismFlags.Burn, Is.Not.EqualTo(WoundMechanismFlags.None));
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
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);

                Assert.Multiple(() =>
                {
                    Assert.That(wounds.Wounds, Has.Count.EqualTo(1));
                    Assert.That(wounds.Mechanisms, Has.Count.EqualTo(1));
                    Assert.That(wounds.Mechanisms[0], Is.EqualTo(WoundMechanism.Slash));
                    Assert.That(wounds.Wounds[0].Damage, Is.EqualTo(FixedPoint2.New(36)));
                    Assert.That(wounds.Sizes[0], Is.EqualTo(WoundSize.Gaping));
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

                Assert.Multiple(() =>
                {
                    Assert.That(wounds.Wounds, Has.Count.EqualTo(6));
                    Assert.That(wounds.Mechanisms, Has.Count.EqualTo(6));
                    Assert.That(wounds.SecondaryMechanisms, Has.Count.EqualTo(6));
                    Assert.That(wounds.TreatmentQualities, Has.Count.EqualTo(6));
                    Assert.That(wounds.Cleanup, Has.Count.EqualTo(6));
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
    public async Task AdequateTreatmentMarksQualityAndLeavesCleanup()
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

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(wounds.Cleanup[0], Is.Not.EqualTo(WoundCleanupFlags.None));

                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(wounds.Wounds[0].Treated, Is.True);
                    Assert.That(wounds.TreatmentQualities[0], Is.EqualTo(WoundTreatmentQuality.Adequate));
                    Assert.That(wounds.Cleanup[0], Is.Not.EqualTo(WoundCleanupFlags.None));
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
    public async Task OptimalTreatmentMarksQualityAndClearsCleanup()
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

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
                Assert.That(wounds.Cleanup[0], Is.Not.EqualTo(WoundCleanupFlags.None));

                Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(completed, Is.True);
                    Assert.That(wounds.Wounds[0].Treated, Is.True);
                    Assert.That(wounds.TreatmentQualities[0], Is.EqualTo(WoundTreatmentQuality.Optimal));
                    Assert.That(wounds.Cleanup[0], Is.EqualTo(WoundCleanupFlags.None));
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
    public void FieldTreatmentCapCombinesWoundBurdenAndClamps()
    {
        var wounds = new BodyPartWoundComponent();
        WoundsOf(wounds).Add(new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, false));
        SizesOf(wounds).Add(WoundSize.Deep);
        TreatmentQualitiesOf(wounds).Add(WoundTreatmentQuality.Untreated);
        CleanupOf(wounds).Add(WoundCleanupFlags.PoorClosure);

        WoundsOf(wounds).Add(new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, false));
        SizesOf(wounds).Add(WoundSize.Massive);
        TreatmentQualitiesOf(wounds).Add(WoundTreatmentQuality.Adequate);
        CleanupOf(wounds).Add(WoundCleanupFlags.CrushDebris);

        Assert.That(SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds), Is.EqualTo(0.58f).Within(0.001f));

        for (var i = 0; i < 4; i++)
        {
            WoundsOf(wounds).Add(new Wound(10, FixedPoint2.Zero, 0f, null, WoundType.Brute, false));
            SizesOf(wounds).Add(WoundSize.Massive);
            TreatmentQualitiesOf(wounds).Add(WoundTreatmentQuality.Adequate);
            CleanupOf(wounds).Add(WoundCleanupFlags.CrushDebris);
        }

        Assert.That(SharedCMUWoundsSystem.ComputeFieldTreatmentCap(wounds), Is.EqualTo(0.35f).Within(0.001f));
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
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            torso = GetBodyPart(entMan, human, BodyPartType.Torso);

            Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
            var wounds = entMan.GetComponent<BodyPartWoundComponent>(torso);
            var woundList = WoundsOf(wounds);
            woundList[0] = woundList[0] with { Damage = FixedPoint2.New(1) };

            Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);
            Assert.That(completed, Is.True);
            Assert.That(wounds.Cleanup[0], Is.EqualTo(WoundCleanupFlags.None));
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
    public async Task DetailedExamineShowsMechanismAndOptimalHintWithoutUntreatedCleanup()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var fractureSystem = entMan.System<SharedFractureSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 20),
                    impact: DamageImpact.MeleeSlash), Is.True);
                var fracture = entMan.EnsureComponent<FractureComponent>(torso);
                fractureSystem.SetSeverity((torso, fracture), FractureSeverity.Hairline);

                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("slash wound"));
                    Assert.That(text, Does.Contain("slash wound[/color]\n  [color=#ffd166]untreated[/color]\n  [color=#83c9ff]optimal: sealing dressing[/color]\n  [color=#ff5f5f]external bleeding: moderate[/color]"));
                    Assert.That(text, Does.Contain("optimal: sealing dressing"));
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
    public async Task DetailedExamineShowsCleanupOnlyForAdequateTreatment()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 10),
                    impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.True);

                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("slash wound[/color]\n  [color=#f0c85a]adequate treatment[/color]\n  [color=#d987ff]cleanup needed:"));
                    Assert.That(text, Does.Contain("adequate treatment"));
                    Assert.That(text, Does.Contain("cleanup needed"));
                    Assert.That(text, Does.Contain("dirty dressing"));
                    Assert.That(text, Does.Contain("optimal: sealing dressing"));
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
    public async Task DetailedExamineHidesOptimallyTreatedWounds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var woundsSystem = entMan.System<CMUWoundsSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 10),
                    impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(woundsSystem.TryTreatWound(torso, WoundTreatmentQuality.Optimal, out var completed), Is.True);
                Assert.That(completed, Is.True);

                var text = examine.GetDetailedExamineText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Is.EqualTo("No obvious injuries found."));
                    Assert.That(text, Does.Not.Contain("slash wound"));
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
    public async Task NormalExamineShowsTreatedWoundsAsTreated()
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

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 10),
                    impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(woundsSystem.TryTreatWound(torso, out var completed), Is.True);
                Assert.That(completed, Is.True);

                var examine = new ExaminedEvent(new FormattedMessage(), human, human, true, false);
                entMan.EventBus.RaiseLocalEvent(human, examine);
                var text = examine.GetTotalMessage().ToMarkup();

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("treated"));
                    Assert.That(text, Does.Contain("wound"));
                    Assert.That(text, Does.Not.Contain("[color=#ff4d4d]a small wound[/color]"));
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

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var head = GetBodyPart(entMan, human, BodyPartType.Head);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var leftArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Left);

                Assert.That(partHealth.TryApplyPartDamage(human, leftArm, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, head, Damage("Slash", 10), impact: DamageImpact.MeleeSlash), Is.True);

                var text = examine.GetDetailedExamineText(human);
                var headIndex = text.IndexOf("Head", StringComparison.Ordinal);
                var torsoIndex = text.IndexOf("Torso", StringComparison.Ordinal);
                var armIndex = text.IndexOf("Left arm", StringComparison.Ordinal);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("[bold][color=#9fc7ff]Head[/color][/bold]"));
                    Assert.That(text, Does.Contain("[color=#ffb86c]small slash wound[/color]"));
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
    public async Task DetailedExamineShortcutStartsInspectInjuriesDoAfter()
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
                Assert.That(examine.TryStartDetailedExamine(user, patient), Is.True);
                Assert.That(entMan.HasComponent<ActiveDoAfterComponent>(user), Is.True);
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
    public async Task InspectInjuriesUsesDistinctSiteColorFromOptimalTreatmentHint()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var rightArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Slash", 80), impact: DamageImpact.MeleeSlash), Is.True);
                Assert.That(partHealth.TryApplyPartDamage(human, rightArm, Damage("Slash", 20), impact: DamageImpact.MeleeSlash), Is.True);

                var text = examine.GetInspectInjuriesText(human);

                Assert.Multiple(() =>
                {
                    Assert.That(text, Does.Contain("[color=#83c9ff]Optimal Treatment: sealing dressing[/color]"));
                    Assert.That(text, Does.Contain("[color=#ff9f43]Massive Torso, Moderate Right arm[/color]"));
                    Assert.That(text, Does.Not.Contain("[color=#9fc7ff]Massive Torso, Moderate Right arm[/color]"));
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

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var examine = entMan.System<CMUMedicalExamineSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var rightArm = GetBodyPart(entMan, human, BodyPartType.Arm, BodyPartSymmetry.Right);

                Assert.That(partHealth.TryApplyPartDamage(human, rightArm, Damage("Slash", 80), impact: DamageImpact.MeleeSlash), Is.True);

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
                Assert.Multiple(() =>
                {
                    Assert.That(wounds.Mechanisms[0], Is.EqualTo(WoundMechanism.Bullet));
                    Assert.That(wounds.Cleanup[0] & WoundCleanupFlags.RetainedFragment, Is.EqualTo(WoundCleanupFlags.None));
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

    [Test]
    public async Task DetailedExamineVerbIsAvailableOnCMUHumans()
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
                Assert.That(ContainsVerb(local, "Inspect injuries"), Is.True);
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

    private static List<Wound> WoundsOf(BodyPartWoundComponent comp)
        => GetField<List<Wound>>(comp, nameof(BodyPartWoundComponent.Wounds));

    private static List<WoundSize> SizesOf(BodyPartWoundComponent comp)
        => GetField<List<WoundSize>>(comp, nameof(BodyPartWoundComponent.Sizes));

    private static List<WoundTreatmentQuality> TreatmentQualitiesOf(BodyPartWoundComponent comp)
        => GetField<List<WoundTreatmentQuality>>(comp, nameof(BodyPartWoundComponent.TreatmentQualities));

    private static List<WoundCleanupFlags> CleanupOf(BodyPartWoundComponent comp)
        => GetField<List<WoundCleanupFlags>>(comp, nameof(BodyPartWoundComponent.Cleanup));

    private static T GetField<T>(BodyPartWoundComponent comp, string name)
        => (T) typeof(BodyPartWoundComponent).GetField(name, BindingFlags.Instance | BindingFlags.Public)!.GetValue(comp)!;

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
