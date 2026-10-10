using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Despoiler;
using Content.Shared._RMC14.Xenonids.Projectile.Spit;
using Content.Shared._RMC14.Xenonids.Projectile.Spit.Charge;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUDespoilerAcidTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUDespoilerAcidTestTarget
          components:
          - type: Damageable
          - type: Injurable
          - type: MobState
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              body:
                shape: !type:PhysShapeCircle
                  radius: 0.3
                layer: [MobLayer]
                mask: [MobMask]
        """;

    [Test]
    public async Task AcidTicksStayOnScheduleUntilExpiry()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        float duration = 0;
        float expectedDamage = 0;
        await Server.WaitAssertion(() =>
        {
            var caster = SEntMan.SpawnEntity("RMCXenoDespoiler", map.GridCoords.Offset(new Vector2(4, 0)));
            target = SEntMan.SpawnEntity("CMUDespoilerAcidTestTarget", map.GridCoords);
            var settings = SEntMan.GetComponent<XenoDespoilerComponent>(caster).AcidTiers[1];
            duration = (float) settings.Duration.TotalSeconds;
            expectedDamage = (float) settings.Damage.GetTotal() * duration;
            Server.System<XenoDespoilerAcidSystem>().ApplyAcid(target, caster, 2);
        });

        await Pair.RunSeconds(duration + 2);
        await Server.WaitAssertion(() =>
        {
            Assert.That((float) Server.System<DamageableSystem>().GetTotalDamage(target), Is.EqualTo(expectedDamage).Within(0.01f),
                "frame delays must not accumulate and discard the final scheduled damage ticks");
            Assert.That(SEntMan.HasComponent<UserAcidedComponent>(target), Is.False, "acid must stop after its duration");
            Assert.That(Server.System<XenoDespoilerAcidSystem>().ConsumeAcidTier(target), Is.Zero,
                "expired acid must not leave a finishing-stab bonus behind");
        });
    }

    [Test]
    public async Task StrongAcidSurvivesWeakerApplicationsUntilResisted()
    {
        var map = await Pair.CreateTestMap();
        EntityUid caster = default;
        EntityUid target = default;
        float weakDamage = 0;
        float beforeWeakerApplication = 0;
        TimeSpan expiry = default;
        await Server.WaitAssertion(() =>
        {
            caster = SEntMan.SpawnEntity("RMCXenoDespoiler", map.GridCoords.Offset(new Vector2(4, 0)));
            target = SEntMan.SpawnEntity("CMUDespoilerAcidTestTarget", map.GridCoords);
            Server.System<XenoDespoilerAcidSystem>().ApplyAcid(target, caster);
        });
        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            weakDamage = (float) Server.System<DamageableSystem>().GetTotalDamage(target);
            var acid = Server.System<XenoDespoilerAcidSystem>();
            acid.ApplyAcid(target, caster, 2);
            acid.ApplyAcid(target, caster, 3);
            expiry = SEntMan.GetComponent<UserAcidedComponent>(target).ExpiresAt;
        });
        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            beforeWeakerApplication = (float) Server.System<DamageableSystem>().GetTotalDamage(target);
            Server.System<XenoDespoilerAcidSystem>().ApplyAcid(target, caster);
            Server.System<XenoSpitSystem>().SetAcidCombo(target, TimeSpan.FromSeconds(40), null, default, 2);
            var strong = SEntMan.GetComponent<UserAcidedComponent>(target);
            Assert.That(strong.Tier, Is.EqualTo(3));
            Assert.That(strong.ExpiresAt, Is.EqualTo(expiry), "slashes must not refresh strong acid indefinitely");
        });
        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            var total = (float) Server.System<DamageableSystem>().GetTotalDamage(target);
            Assert.That(total - beforeWeakerApplication, Is.GreaterThan(weakDamage),
                "upgraded acid must keep its stronger damage ticks after a weaker application");
            var acid = Server.System<XenoDespoilerAcidSystem>();
            Assert.That(acid.ConsumeAcidTier(target), Is.EqualTo(4), "Tier 3 keeps the strongest finishing-stab bonus");
            var resists = SEntMan.GetComponent<UserAcidedComponent>(target).ResistsNeeded;
            for (var i = 0; i < resists; i++)
                Server.System<XenoSpitSystem>().Resist(target, interactionAlreadyValidated: true);
        });
        await Pair.RunSeconds(0.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<UserAcidedComponent>(target), Is.False);
            Assert.That(Server.System<XenoDespoilerAcidSystem>().ConsumeAcidTier(target), Is.Zero);
        });
    }

    [Test]
    public async Task EmpoweredOozingWoundsAppliesTierTwoOnCollision()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        EntityUid control = default;
        float initialDamage = 0;
        float initialControlDamage = 0;
        await Server.WaitAssertion(() =>
        {
            var caster = SEntMan.SpawnEntity("RMCXenoDespoiler", map.GridCoords.Offset(new Vector2(4, 0)));
            target = SEntMan.SpawnEntity("CMUDespoilerAcidTestTarget", map.GridCoords);
            control = SEntMan.SpawnEntity("CMUDespoilerAcidTestTarget", map.GridCoords.Offset(new Vector2(2, 0)));
            var spray = SEntMan.SpawnEntity("RMCEffectDespoilerAcidSprayEmpowered", map.GridCoords);
            SEntMan.GetComponent<XenoDespoilerAcidSprayComponent>(spray).Caster = caster;
            var normal = SEntMan.SpawnEntity("RMCEffectDespoilerAcidSpray", map.GridCoords.Offset(new Vector2(2, 0)));
            SEntMan.GetComponent<XenoDespoilerAcidSprayComponent>(normal).Caster = caster;
        });
        await Pair.RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<UserAcidedComponent>(target).Tier, Is.EqualTo(2));
            Assert.That(SEntMan.GetComponent<UserAcidedComponent>(control).Tier, Is.EqualTo(1));
            initialDamage = (float) Server.System<DamageableSystem>().GetTotalDamage(target);
            initialControlDamage = (float) Server.System<DamageableSystem>().GetTotalDamage(control);
        });
        await Pair.RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var damage = Server.System<DamageableSystem>();
            var empoweredTicks = (float) damage.GetTotalDamage(target) - initialDamage;
            var normalTicks = (float) damage.GetTotalDamage(control) - initialControlDamage;
            Assert.That(empoweredTicks, Is.GreaterThan(normalTicks), "empowered spray must apply stronger lingering damage");
        });
    }
}
