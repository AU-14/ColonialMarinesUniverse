#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Projectiles;
using Content.Shared._RMC14.Projectiles.Penetration;
using Content.Shared.CCVar;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.Weapons;

[TestFixture]
[TestOf(typeof(SharedProjectileSystem))]
public sealed class ProjectileMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: damageContainer
  id: ProjectileMergeDamageContainer
  supportedTypes: [ Blunt ]

- type: Tag
  id: ProjectileMergeTargetTag

- type: entity
  id: ProjectileMergeTarget
  components:
  - type: Damageable
  - type: Injurable
    damageContainer: ProjectileMergeDamageContainer
  - type: Destructible
    thresholds:
    - trigger:
        !type:DamageTrigger
        damage: 10
      behaviors:
      - !type:DoActsBehavior
        acts: [ Destruction ]
  - type: Tag
    tags: [ ProjectileMergeTargetTag ]
  - type: ProjectileMergeProbe

- type: entity
  id: ProjectileMergePhysicsTarget
  parent: ProjectileMergeTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape: !type:PhysShapeCircle
          radius: 0.25
        density: 10
        layer: [ BulletImpassable ]
        hard: true

- type: entity
  id: ProjectileMergeComplex
  components:
  - type: Projectile
    damage:
      types:
        Blunt: 1
    deleteOnCollide: false
  - type: ComplexProjectileDamage
    damageOptions:
    - damage:
        types:
          Blunt: 7
      whitelist:
        tags: [ ProjectileMergeTargetTag ]
    - damage:
        types:
          Blunt: 9
      whitelist:
        tags: [ ProjectileMergeTargetTag ]
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      projectile:
        shape: !type:PhysShapeCircle
          radius: 0.25
        mask: [ BulletImpassable ]
        hard: false
  - type: ProjectileMergeProbe

- type: entity
  id: ProjectileMergePenetrating
  components:
  - type: Projectile
    damage:
      types:
        Blunt: 12
    penetrationThreshold: 20
    penetrationDamageTypeRequirement: [ Blunt ]
  - type: Physics
  - type: ProjectileMergeProbe

- type: entity
  id: ProjectileMergeUnsupportedPenetrating
  components:
  - type: Projectile
    damage:
      types:
        Piercing: 12
    penetrationThreshold: 20
    penetrationDamageTypeRequirement: [ Piercing ]
  - type: Physics
  - type: ProjectileMergeProbe

- type: entity
  id: ProjectileMergeRmcPenetrating
  components:
  - type: Projectile
    damage:
      types:
        Blunt: 3
  - type: RMCPenetratingProjectile
    range: 100
  - type: Physics
  - type: ProjectileMergeProbe
";

    [SidedDependency(Side.Server)] private ProjectileSystem _projectiles = default!;

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 2f)]
    public async Task ComplexSelectionPrecedesHandledHitAndServerShellExposesSharedApi()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ProjectileMergeProbeSystem>();
            var shooter = SEntMan.SpawnEntity(null, map.GridCoords);
            var projectile = SEntMan.SpawnEntity("ProjectileMergeComplex", map.GridCoords);
            var handledTarget = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var hitTarget = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var component = SEntMan.GetComponent<ProjectileComponent>(projectile);
            var physics = SEntMan.GetComponent<PhysicsComponent>(projectile);
            var probe = SEntMan.GetComponent<ProjectileMergeProbeComponent>(projectile);

            Assert.That(_projectiles, Is.TypeOf<ProjectileSystem>(),
                "The Server ProjectileSystem must remain the injectable shell over SharedProjectileSystem.");
            _projectiles.SetShooter(projectile, component, shooter);
            Assert.That(component.Shooter, Is.EqualTo(shooter));

            probe.HandleHit = true;
            _projectiles.ProjectileCollide((projectile, component, physics), handledTarget);

            Assert.Multiple(() =>
            {
                Assert.That(probe.Order, Is.EqualTo(new[] { "before", "hit" }));
                AssertDamage(probe.HitDamage, "Blunt", 14,
                    "the first matching Complex option must be selected before the universal projectile modifier");
                AssertDamage(SEntMan.GetComponent<DamageableComponent>(handledTarget).Damage, "Blunt", 0,
                    "ProjectileHitEvent.Handled must short-circuit damage and the After event");
                Assert.That(probe.AfterHits, Is.Zero);
                Assert.That(component.ProjectileSpent, Is.False);
            });

            probe.Reset();
            _projectiles.ProjectileCollide((projectile, component, physics), hitTarget);

            Assert.Multiple(() =>
            {
                Assert.That(probe.Order, Is.EqualTo(new[] { "before", "hit", "after" }));
                AssertDamage(probe.HitDamage, "Blunt", 14);
                AssertDamage(SEntMan.GetComponent<DamageableComponent>(hitTarget).Damage, "Blunt", 14);
                Assert.That(SEntMan.GetComponent<ProjectileMergeProbeComponent>(hitTarget).DamageChanges, Is.EqualTo(1));
                Assert.That(probe.BeforeHits, Is.EqualTo(1));
                Assert.That(probe.ProjectileHits, Is.EqualTo(1));
                Assert.That(probe.AfterHits, Is.EqualTo(1));
            });
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task SharedStartCollideAppliesExactlyOneProjectileEffect()
    {
        var map = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ProjectileMergeProbeSystem>();
            target = SEntMan.SpawnEntity("ProjectileMergePhysicsTarget", map.GridCoords);
            projectile = SEntMan.SpawnEntity("ProjectileMergeComplex", map.GridCoords);
        });

        await Server.WaitRunTicks(3);
        await Server.WaitAssertion(() =>
        {
            var projectileProbe = SEntMan.GetComponent<ProjectileMergeProbeComponent>(projectile);
            var targetProbe = SEntMan.GetComponent<ProjectileMergeProbeComponent>(target);
            Assert.Multiple(() =>
            {
                Assert.That(projectileProbe.BeforeHits, Is.EqualTo(1));
                Assert.That(projectileProbe.ProjectileHits, Is.EqualTo(1));
                Assert.That(projectileProbe.AfterHits, Is.EqualTo(1));
                Assert.That(targetProbe.DamageChanges, Is.EqualTo(1));
                AssertDamage(SEntMan.GetComponent<DamageableComponent>(target).Damage, "Blunt", 7,
                    "one Shared StartCollide subscription must produce one damage application");
            });
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task BasePenetrationUsesActualDamageRemainingThresholdAndFinalSpentState()
    {
        var map = await Pair.CreateTestMap();
        EntityUid projectile = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ProjectileMergeProbeSystem>();
            projectile = SEntMan.SpawnEntity("ProjectileMergePenetrating", map.GridCoords);
            var first = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var second = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var component = SEntMan.GetComponent<ProjectileComponent>(projectile);
            var physics = SEntMan.GetComponent<PhysicsComponent>(projectile);

            _projectiles.ProjectileCollide((projectile, component, physics), first);
            Assert.Multiple(() =>
            {
                Assert.That(component.PenetrationAmount, Is.EqualTo((FixedPoint2) 10));
                Assert.That(component.ProjectileSpent, Is.False,
                    "damage meeting the remaining destruction threshold must permit penetration below the budget");
                Assert.That(SEntMan.EntityExists(projectile), Is.True);
            });

            _projectiles.ProjectileCollide((projectile, component, physics), second);
            Assert.Multiple(() =>
            {
                Assert.That(component.PenetrationAmount, Is.EqualTo((FixedPoint2) 20));
                Assert.That(component.ProjectileSpent, Is.True,
                    "reaching the penetration budget must win the final deletion gate");
            });
        });

        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(projectile), Is.False));

        await Server.WaitAssertion(() =>
        {
            var unsupported = SEntMan.SpawnEntity("ProjectileMergeUnsupportedPenetrating", map.GridCoords);
            var target = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var component = SEntMan.GetComponent<ProjectileComponent>(unsupported);
            var physics = SEntMan.GetComponent<PhysicsComponent>(unsupported);

            _projectiles.ProjectileCollide((unsupported, component, physics), target);
            Assert.Multiple(() =>
            {
                AssertDamage(SEntMan.GetComponent<DamageableComponent>(target).Damage, "Blunt", 0);
                Assert.That(component.PenetrationAmount, Is.EqualTo(FixedPoint2.Zero),
                    "penetration must use the filtered actual delta, not the attempted unsupported damage");
                Assert.That(component.ProjectileSpent, Is.True);
            });
        });
    }

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.PlaytestProjectileDamageModifier), 1f)]
    public async Task RmcPenetrationRecordsOnceResetsSpentAndControlsFinalDeletion()
    {
        var map = await Pair.CreateTestMap();
        EntityUid projectile = default;

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<ProjectileMergeProbeSystem>();
            projectile = SEntMan.SpawnEntity("ProjectileMergeRmcPenetrating", map.GridCoords);
            var target = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            var component = SEntMan.GetComponent<ProjectileComponent>(projectile);
            var penetrating = SEntMan.GetComponent<RMCPenetratingProjectileComponent>(projectile);
            var physics = SEntMan.GetComponent<PhysicsComponent>(projectile);
            var targetDamage = SEntMan.GetComponent<DamageableComponent>(target);

            _projectiles.ProjectileCollide((projectile, component, physics), target);
            Assert.Multiple(() =>
            {
                Assert.That(component.ProjectileSpent, Is.False,
                    "AfterProjectileHitEvent must be able to reopen the final deletion gate while range remains");
                Assert.That(SEntMan.EntityExists(projectile), Is.True);
                Assert.That(penetrating.HitTargetIds, Has.Count.EqualTo(1));
                AssertDamage(targetDamage.Damage, "Blunt", 3);
            });

            var firstDamage = targetDamage.Damage.DamageDict["Blunt"];
            var probe = SEntMan.GetComponent<ProjectileMergeProbeComponent>(projectile);
            probe.Reset();
            _projectiles.ProjectileCollide((projectile, component, physics), target);
            Assert.Multiple(() =>
            {
                Assert.That(probe.BeforeHits, Is.EqualTo(1));
                Assert.That(probe.ProjectileHits, Is.EqualTo(1));
                Assert.That(probe.AfterHits, Is.Zero,
                    "a repeated target must be Handled before damage, spent state, and AfterProjectileHit");
                Assert.That(targetDamage.Damage.DamageDict["Blunt"], Is.EqualTo(firstDamage));
                Assert.That(penetrating.HitTargetIds, Has.Count.EqualTo(1));
            });

            penetrating.Range = -1;
            var finalTarget = SEntMan.SpawnEntity("ProjectileMergeTarget", map.GridCoords);
            _projectiles.ProjectileCollide((projectile, component, physics), finalTarget);
            Assert.That(component.ProjectileSpent, Is.True,
                "an exhausted RMC range must leave the base spent state intact for final deletion");
        });

        await Server.WaitRunTicks(1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.EntityExists(projectile), Is.False));
    }

    private static void AssertDamage(DamageSpecifier? damage, string type, int expected, string? message = null)
    {
        Assert.That(damage, Is.Not.Null, message);
        var amount = damage!.DamageDict.TryGetValue(type, out var value) ? value : FixedPoint2.Zero;
        Assert.That(amount, Is.EqualTo((FixedPoint2) expected), message);
    }
}

[RegisterComponent]
public sealed partial class ProjectileMergeProbeComponent : Component
{
    public readonly List<string> Order = new();
    public bool HandleHit;
    public int BeforeHits;
    public int ProjectileHits;
    public int AfterHits;
    public int DamageChanges;
    public DamageSpecifier? HitDamage;

    public void Reset()
    {
        Order.Clear();
        HandleHit = false;
        BeforeHits = 0;
        ProjectileHits = 0;
        AfterHits = 0;
        DamageChanges = 0;
        HitDamage = null;
    }
}

public sealed class ProjectileMergeProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProjectileMergeProbeComponent, BeforeProjectileHitEvent>(OnBeforeHit);
        SubscribeLocalEvent<ProjectileMergeProbeComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<ProjectileMergeProbeComponent, AfterProjectileHitEvent>(OnAfterHit);
        SubscribeLocalEvent<ProjectileMergeProbeComponent, DamageChangedEvent>(OnDamageChanged);
    }

    private static void OnBeforeHit(Entity<ProjectileMergeProbeComponent> ent, ref BeforeProjectileHitEvent args)
    {
        ent.Comp.Order.Add("before");
        ent.Comp.BeforeHits++;
    }

    private static void OnProjectileHit(Entity<ProjectileMergeProbeComponent> ent, ref ProjectileHitEvent args)
    {
        ent.Comp.Order.Add("hit");
        ent.Comp.ProjectileHits++;
        ent.Comp.HitDamage = args.Damage.Clone();
        if (ent.Comp.HandleHit)
            args.Handled = true;
    }

    private static void OnAfterHit(Entity<ProjectileMergeProbeComponent> ent, ref AfterProjectileHitEvent args)
    {
        ent.Comp.Order.Add("after");
        ent.Comp.AfterHits++;
    }

    private static void OnDamageChanged(Entity<ProjectileMergeProbeComponent> ent, ref DamageChangedEvent args)
    {
        ent.Comp.DamageChanges++;
    }
}

#pragma warning restore RA0002
