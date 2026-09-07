using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._RMC14.Armor;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaFrontlineBalanceTest
{
    private static readonly string[] RegularMasks =
    {
        "CMUYautjaMask",
        "CMUYautjaMaskAncient",
        "CMUYautjaMaskElitePlated",
        "CMUYautjaMaskPred01Bone",
    };

    private static readonly string[] ExcludedMasks =
    {
        "CMUYautjaMaskBadBloodBane",
        "CMUYautjaMaskThrallBone",
    };

    [Test]
    public async Task RegularMasksHaveFrontlineArmorAndExcludedMasksDoNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;

            foreach (var prototype in RegularMasks)
            {
                var mask = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                Assert.That(entMan.TryGetComponent(mask, out CMArmorComponent? armor), Is.True, prototype);
                Assert.Multiple(() =>
                {
                    Assert.That(armor!.Melee, Is.EqualTo(25), prototype);
                    Assert.That(armor.Bullet, Is.EqualTo(30), prototype);
                    Assert.That(armor.Bio, Is.EqualTo(25), prototype);
                    Assert.That(armor.ExplosionArmor, Is.EqualTo(10), prototype);
                });
                entMan.DeleteEntity(mask);
            }

            foreach (var prototype in ExcludedMasks)
            {
                var mask = entMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
                Assert.That(entMan.HasComponent<CMArmorComponent>(mask), Is.False, prototype);
                entMan.DeleteEntity(mask);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularHunterKeepsAuditedHealthThresholds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var hunter = server.EntMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var thresholds = server.EntMan.GetComponent<MobThresholdsComponent>(hunter).Thresholds;

            Assert.Multiple(() =>
            {
                Assert.That(thresholds.Keys, Does.Contain(FixedPoint2.New(240)));
                Assert.That(thresholds.Keys, Does.Contain(FixedPoint2.New(340)));
            });

            server.EntMan.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MedicalResilienceIsRegularOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var regular = entMan.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var badBlood = entMan.SpawnEntity("CMUMobYautjaBadBloodGrunt", MapCoordinates.Nullspace);

            Assert.That(entMan.TryGetComponent(regular, out CMUMedicalResilienceComponent? resilience), Is.True);
            var regularSlow = entMan.GetComponent<SlowOnDamageComponent>(regular).SpeedModifierThresholds;
            var badBloodSlow = entMan.GetComponent<SlowOnDamageComponent>(badBlood).SpeedModifierThresholds;
            Assert.Multiple(() =>
            {
                Assert.That(resilience!.PainAccumulationMultiplier, Is.EqualTo(0.5f));
                Assert.That(resilience.MinimumPenalizingFractureSeverity, Is.EqualTo(FractureSeverity.Simple));
                Assert.That(resilience.MovementPenaltyFloor, Is.EqualTo(0.8f));
                Assert.That(resilience.AimPenaltyCeiling, Is.EqualTo(1.35f));
                Assert.That(resilience.ActionSpeedPenaltyCeiling, Is.EqualTo(1.35f));
                Assert.That(entMan.HasComponent<CMUMedicalResilienceComponent>(badBlood), Is.False);
                Assert.That(entMan.System<SharedPainShockSystem>().GetAccumulationMultiplier(regular), Is.EqualTo(0.5f));
                Assert.That(entMan.System<SharedPainShockSystem>().GetAccumulationMultiplier(badBlood), Is.EqualTo(1f));
                Assert.That(regularSlow, Contains.Key(FixedPoint2.New(200)));
                Assert.That(regularSlow, Does.Not.ContainKey(FixedPoint2.New(160)));
                Assert.That(badBloodSlow, Contains.Key(FixedPoint2.New(160)));
            });

            entMan.DeleteEntity(regular);
            entMan.DeleteEntity(badBlood);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularHunterCanStillFractureAndBleed()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var index = entities.System<CMUMedicalBodyIndexSystem>();
            Assert.That(index.TryGetBodyPart(hunter,
                new CMUMedicalBodyPartKey(BodyPartType.Arm, BodyPartSymmetry.Left),
                out var arm), Is.True);

            Assert.That(entities.System<SharedBoneSystem>().SeedFracture(arm, FractureSeverity.Simple), Is.True);
            Assert.That(entities.System<SharedBodyPartHealthSystem>().TryApplyPartDamage(
                hunter,
                arm,
                new DamageSpecifier { DamageDict = { ["Slash"] = 20 } },
                impact: DamageImpact.MeleeSlash,
                mechanism: CMUTraumaMechanism.Slash), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<FractureComponent>(arm).Severity,
                    Is.AtLeast(FractureSeverity.Simple));
                Assert.That(entities.TryGetComponent(arm, out BodyPartWoundComponent? wounds), Is.True);
                Assert.That(entities.System<CMUWoundLedgerSystem>()
                    .GetEntries(wounds!)
                    .Any(entry => entry.Wound.Bloodloss > 0f), Is.True);
                Assert.That(entities.GetComponent<BloodstreamComponent>(hunter).MaxBleedAmount, Is.GreaterThan(0f));
            });

            entities.DeleteEntity(hunter);
        });

        await pair.CleanReturnAsync();
    }
}
