using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared._RMC14.Weapons.Ranged;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Medical;

[TestFixture]
public sealed class ProjectileImpactProfilePrototypeTest : GameTest
{
    [Test]
    [RunOnSide(Side.Server)]
    public void EveryCmuRmcAmmunitionProjectileHasExplicitProjectileImpactProfile()
    {
        var missing = SProtoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(IsCmuRmcAmmunitionFamily)
            .Where(proto => proto.Components.ContainsKey(nameof(ProjectileComponent).Replace("Component", string.Empty)))
            .Where(proto => !proto.Components.TryGetValue(nameof(DamageImpactProfileComponent).Replace("Component", string.Empty), out var impactData) ||
                            !((DamageImpactProfileComponent) impactData.Component).Projectile.IsSpecified)
            .Select(proto => proto.ID)
            .Order()
            .ToArray();

        Assert.That(missing, Is.Empty,
            $"Every RMC/CMU ammunition family must resolve an explicit projectile impact profile: {string.Join(", ", missing)}");
    }

    private static bool IsCmuRmcAmmunitionFamily(EntityPrototype prototype)
        => prototype.Components.ContainsKey(nameof(RMCBulletComponent).Replace("Component", string.Empty)) ||
           prototype.ID is "TribalArrow" or "CMUYautjaSpikeProjectile" ||
           prototype.ID.StartsWith("CMUYautjaPlasma");
}
