using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Trauma;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class TraumaContactIntegrationTest
{
    [Test]
    public async Task LowDamageBallisticCanMissDeepStructures()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            ForceBallistic(server.CfgMan, bone: 0f, organ: 0f, vascular: 0f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 40), tool: projectile), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.EqualTo(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.EqualTo(organBefore));
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BallisticCanHitOrganAndVascularWithoutBone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            ForceBallistic(server.CfgMan, bone: 0f, organ: 1f, vascular: 1f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 40), tool: projectile), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.EqualTo(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.LessThan(organBefore));
                    Assert.That(entMan.TryGetComponent<InternalBleedingComponent>(torso, out var ib), Is.True);
                    Assert.That(ib!.Source, Is.EqualTo("vascular:Ballistic"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BallisticTorsoOrganPassThroughIsBoostedButHeadIsNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trauma = entMan.System<SharedCMUTraumaSystem>();
            var projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                var damage = Damage("Piercing", 75);

                var torso = trauma.CreateContactResult(BodyPartType.Torso, damage, true, tool: projectile);
                var head = trauma.CreateContactResult(BodyPartType.Head, damage, true, tool: projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(torso.OrganContact, Is.True);
                    Assert.That(head.OrganContact, Is.True);
                    Assert.That(torso.HighEnergy, Is.True);
                    Assert.That(head.HighEnergy, Is.True);
                    Assert.That(torso.OrganPassThrough, Is.EqualTo(head.OrganPassThrough * 1.3f).Within(0.0001f));
                });
            }
            finally
            {
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HighDamageBallisticForcesBoneAndOrganButNotDirectVascular()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            ForceBallistic(server.CfgMan, bone: 0f, organ: 0f, vascular: 0f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 75), tool: projectile), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.LessThan(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.LessThan(organBefore));
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RifleBallisticBoneContactCanFractureInOneHit()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            ForceBallistic(server.CfgMan, bone: 1f, organ: 0f, vascular: 0f);

            var entMan = server.EntMan;
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                entMan.EnsureComponent<ProjectileComponent>(projectile);
                var torso = GetBodyPart(entMan, human, BodyPartType.Arm);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Piercing", 35), tool: projectile), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.TryGetComponent<FractureComponent>(torso, out var fracture), Is.True);
                    Assert.That(fracture!.Severity, Is.EqualTo(FractureSeverity.Hairline));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SlashCanHitOrganAndVascularWithoutBone()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaMeleeHighDamageThreshold, 100f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaSlashBoneChance, 0f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaSlashOrganChance, 1f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaSlashVascularChance, 1f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(partHealth.TryApplyPartDamage(
                    human,
                    torso,
                    Damage("Slash", 35),
                    impact: DamageImpact.XenoRendingSlash(3)), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.EqualTo(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.LessThan(organBefore));
                    Assert.That(entMan.TryGetComponent<InternalBleedingComponent>(torso, out var ib), Is.True);
                    Assert.That(ib!.Source, Is.EqualTo("vascular:Slash"));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("CMXenoWarrior")]
    [TestCase("CMXenoRavager")]
    public async Task TierTwoAndThreeXenoTorsoOrganPassThroughIsBoostedButHeadIsNot(string xenoPrototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var trauma = entMan.System<SharedCMUTraumaSystem>();
            var xeno = entMan.SpawnEntity(xenoPrototype, MapCoordinates.Nullspace);

            try
            {
                var damage = Damage("Slash", 45);
                var torso = trauma.CreateContactResult(BodyPartType.Torso, damage, true, origin: xeno, tool: null);
                var head = trauma.CreateContactResult(BodyPartType.Head, damage, true, origin: xeno, tool: null);

                Assert.Multiple(() =>
                {
                    Assert.That(torso.OrganContact, Is.True);
                    Assert.That(head.OrganContact, Is.True);
                    Assert.That(torso.HighEnergy, Is.True);
                    Assert.That(head.HighEnergy, Is.True);
                    Assert.That(torso.OrganPassThrough, Is.EqualTo(head.OrganPassThrough * 1.3f).Within(0.0001f));
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BluntCanDamageBoneWithoutDirectOrganOrVascularContact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaMeleeHighDamageThreshold, 100f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntBoneChance, 1f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntOrganChance, 0f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntVascularChance, 0f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(partHealth.TryApplyPartDamage(human, torso, Damage("Blunt", 40)), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.LessThan(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.EqualTo(organBefore));
                    Assert.That(entMan.HasComponent<InternalBleedingComponent>(torso), Is.False);
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
    public async Task ExplosiveMechanismBypassesContactRolls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            ForceBallistic(server.CfgMan, bone: 0f, organ: 0f, vascular: 0f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntBoneChance, 0f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntOrganChance, 0f);
            server.CfgMan.SetCVar(CMUMedicalCCVars.TraumaBluntVascularChance, 0f);

            var entMan = server.EntMan;
            var body = entMan.System<SharedBodySystem>();
            var partHealth = entMan.System<SharedBodyPartHealthSystem>();
            var human = entMan.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);

            try
            {
                var torso = GetBodyPart(entMan, human, BodyPartType.Torso);
                var boneBefore = entMan.GetComponent<BoneComponent>(torso).Integrity;
                var organBefore = OrganTotal(entMan, body, torso);

                Assert.That(
                    partHealth.TryApplyPartDamage(
                        human,
                        torso,
                        Damage("Blunt", 30),
                        mechanism: CMUTraumaMechanism.Explosive),
                    Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.GetComponent<BoneComponent>(torso).Integrity, Is.LessThan(boneBefore));
                    Assert.That(OrganTotal(entMan, body, torso), Is.LessThan(organBefore));
                });
            }
            finally
            {
                entMan.DeleteEntity(human);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static void ForceBallistic(IConfigurationManager cfg, float bone, float organ, float vascular)
    {
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileHighDamageThreshold, 60f);
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileHeadChance, bone);
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileTorsoChance, bone);
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileArmChance, bone);
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileLegChance, bone);
        cfg.SetCVar(CMUMedicalCCVars.BoneProjectileOtherChance, bone);
        cfg.SetCVar(CMUMedicalCCVars.TraumaBallisticTorsoOrganChance, organ);
        cfg.SetCVar(CMUMedicalCCVars.TraumaBallisticVascularChance, vascular);
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

    private static FixedPoint2 OrganTotal(IEntityManager entMan, SharedBodySystem body, EntityUid part)
    {
        var total = FixedPoint2.Zero;
        foreach (var (organId, _) in body.GetPartOrgans(part))
        {
            if (entMan.TryGetComponent<OrganHealthComponent>(organId, out var organ))
                total += organ.Current;
        }

        return total;
    }

    private static DamageSpecifier Damage(string type, int amount)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = FixedPoint2.New(amount);
        return damage;
    }
}
