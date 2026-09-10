using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Slow;
using Content.Shared._RMC14.Tackle;
using Content.Shared.Actions.Events;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Injuries.Trauma;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.CombatMode;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
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
            var badBloodSlow = entMan.GetComponent<SlowOnDamageComponent>(badBlood).SpeedModifierThresholds;
            Assert.Multiple(() =>
            {
                Assert.That(resilience!.PainAccumulationMultiplier, Is.EqualTo(0.5f));
                Assert.That(resilience.MinimumPenalizingFractureSeverity, Is.EqualTo(FractureSeverity.Simple));
                Assert.That(resilience.MovementPenaltyFloor, Is.EqualTo(1f));
                Assert.That(resilience.AimPenaltyCeiling, Is.EqualTo(1.35f));
                Assert.That(resilience.ActionSpeedPenaltyCeiling, Is.EqualTo(1.35f));
                Assert.That(entMan.HasComponent<CMUMedicalResilienceComponent>(badBlood), Is.False);
                Assert.That(entMan.HasComponent<SlowOnDamageComponent>(regular), Is.False);
                Assert.That(entMan.System<SharedPainShockSystem>().GetAccumulationMultiplier(regular), Is.EqualTo(0.5f));
                Assert.That(entMan.System<SharedPainShockSystem>().GetAccumulationMultiplier(badBlood), Is.EqualTo(1f));
                Assert.That(badBloodSlow, Contains.Key(FixedPoint2.New(160)));
            });

            entMan.DeleteEntity(regular);
            entMan.DeleteEntity(badBlood);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularHunterRejectsStunsAndRmcMovementDebuffs()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hunter = entities.SpawnEntity("CMUMobYautja", MapCoordinates.Nullspace);
            var stun = entities.System<SharedStunSystem>();
            var slow = entities.System<RMCSlowSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(stun.TryParalyze(hunter, TimeSpan.FromSeconds(5), true, force: true), Is.False);
                Assert.That(slow.TrySlowdown(hunter, TimeSpan.FromSeconds(5), ignoreDurationModifier: true), Is.False);
                Assert.That(slow.TrySuperSlowdown(hunter, TimeSpan.FromSeconds(5), ignoreDurationModifier: true), Is.False);
                Assert.That(slow.TryRoot(hunter, TimeSpan.FromSeconds(5)), Is.False);
                Assert.That(entities.HasComponent<StunnedComponent>(hunter), Is.False);
                Assert.That(entities.HasComponent<RMCSlowdownComponent>(hunter), Is.False);
                Assert.That(entities.HasComponent<RMCSuperSlowdownComponent>(hunter), Is.False);
                Assert.That(entities.HasComponent<RMCRootedComponent>(hunter), Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RegularHunterCannotBeDisarmedOrDropItemsWhenKnockedDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var hands = entities.System<SharedHandsSystem>();
            var hunter = entities.SpawnEntity("CMUMobYautja", map.GridCoords);
            var attacker = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var weapon = entities.SpawnEntity("CMUYautjaClanSword", map.GridCoords);
            Assert.That(hands.TryPickupAnyHand(hunter, weapon), Is.True);

            var attempt = new DisarmAttemptEvent(hunter, attacker, weapon);
            entities.EventBus.RaiseLocalEvent(hunter, ref attempt);
            var disarmed = new DisarmedEvent(hunter, attacker, 1f);
            entities.EventBus.RaiseLocalEvent(hunter, ref disarmed);
            var knockedDown = entities.System<SharedStunSystem>()
                .TryKnockdown(hunter, TimeSpan.FromSeconds(2), force: true);

            Assert.Multiple(() =>
            {
                Assert.That(attempt.Cancelled, Is.True);
                Assert.That(disarmed.Handled, Is.True);
                Assert.That(knockedDown, Is.True);
                Assert.That(hands.IsHolding(hunter, weapon), Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task XenoTackleHasHalfChanceAgainstRegularHunter()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var xeno = entities.SpawnEntity("CMXenoRunner", map.GridCoords);
            var targets = new List<EntityUid>();
            for (var i = 0; i < 32; i++)
            {
                var target = entities.SpawnEntity("CMMobHuman", map.GridCoords);
                entities.EnsureComponent<YautjaComponent>(target);
                targets.Add(target);
            }

            server.ResolveDependency<Robust.Shared.Random.IRobustRandom>().SetSeed(7041);
            var successes = 0;
            foreach (var target in targets)
            {
                var tackle = new CMDisarmEvent(xeno);
                entities.EventBus.RaiseLocalEvent(target, ref tackle);
                Assert.That(tackle.Handled, Is.True);
                if (entities.HasComponent<KnockedDownComponent>(target))
                    successes++;
            }

            Assert.That(successes, Is.InRange(7, 25));
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
