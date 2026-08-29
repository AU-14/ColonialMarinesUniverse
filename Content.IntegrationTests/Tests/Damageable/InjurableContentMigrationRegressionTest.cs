using Content.Client.Overlays;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Materials;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Damageable;

[TestFixture]
[TestOf(typeof(InjurableComponent))]
public sealed class InjurableContentMigrationRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: InjurableContentMigrationStateTarget
          components:
          - type: Damageable
          - type: Injurable
            damageContainer: Biological
            healthBarThreshold: 3
        """;

    [Test]
    [RunOnSide(Side.Server)]
    public void EveryContentDamageableUsesTheInjurableSuccessor()
    {
        var missing = SProtoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(prototype => !Pair.IsTestEntityPrototype(prototype.ID))
            .Where(prototype => prototype.Components.ContainsKey("Damageable"))
            .Where(prototype => !prototype.Components.ContainsKey("Injurable"))
            .Select(prototype => prototype.ID)
            .Order()
            .ToArray();

        Assert.That(missing, Is.Empty,
            $"Every effective Damageable prototype needs Injurable to store and filter damage: {string.Join(", ", missing)}");
    }

    [Test]
    [RunOnSide(Side.Server)]
    public void ForkContainersThresholdsAndYautjaFilterRemainExact()
    {
        var expectedContainers = new (string Source, string Representative, string Container)[]
        {
            ("RMCBaseMobSpeciesOrganic", "CMMobHuman", "Biological"),
            ("CMSheetGlassBase", "CMSheetGlass", "Inorganic"),
            ("CMUYautjaBracerShield", "CMUYautjaBracerShield", "Shield"),
            ("RMCAirScrubberPortable", "RMCAirScrubberPortable", "StructuralInorganic"),
            ("RMCTentBase", "RMCTentUNMCStandard", "StructuralMarine"),
            ("CMBaseWallXeno", "WallXenoResin", "StructuralXeno"),
            ("CMXenoBase", "CMXenoDrone", "Xeno"),
        };

        using (Assert.EnterMultipleScope())
        {
            foreach (var (source, representative, expectedContainer) in expectedContainers)
            {
                var prototype = SProtoMan.Index<EntityPrototype>(representative);
                var injurable = (InjurableComponent) prototype.Components["Injurable"].Component;
                Assert.That(injurable.DamageContainer?.Id, Is.EqualTo(expectedContainer),
                    $"{source} via concrete {representative}");
            }

            var glass = SProtoMan.Index<EntityPrototype>("CMSheetGlass");
            Assert.That(glass.Components.ContainsKey("Material"), Is.True);
            Assert.That(glass.Components.ContainsKey("Damageable"), Is.True);
            Assert.That(glass.Components.ContainsKey("Injurable"), Is.True);
            Assert.That(glass.Components["Material"].Component, Is.TypeOf<MaterialComponent>());
            var glassDamageable = (DamageableComponent) glass.Components["Damageable"].Component;
            Assert.That(glassDamageable.DamageModifierSetId?.Id, Is.EqualTo("Glass"));

            AssertThreshold("RMCSimpleMob", "RMCMobCat", 2);
            AssertThreshold("RMCBaseMobSpeciesOrganic", "CMMobHuman", 5);
            AssertThreshold("CMUMobCarpBase", "CMUMobCarpInvasive", 2);
            AssertThreshold("CMUMobYautja", "CMUMobYautja", 5);

            var hunter = SProtoMan.Index<RandomHumanoidSettingsPrototype>("CMUYautjaHunter");
            Assert.That(hunter.Components, Is.Not.Null);
            var hunterDamageable = (DamageableComponent) hunter.Components!["Damageable"].Component;
            var hunterInjurable = (InjurableComponent) hunter.Components["Injurable"].Component;
            Assert.That(hunterDamageable.DamageModifierSetId?.Id, Is.EqualTo("CMUYautja"));
            Assert.That(hunterInjurable.DamageContainer?.Id, Is.EqualTo("Biological"));
            Assert.That(hunterInjurable.HealthBarThreshold, Is.EqualTo(FixedPoint2.New(5)));

            var gun = SProtoMan.Index<EntityPrototype>("CMUYautjaHealingGun");
            var healing = (YautjaHealingGunComponent) gun.Components["YautjaHealingGun"].Component;
            Assert.That(healing.DamageContainers?.Select(container => container.Id),
                Is.EqualTo(new[] { "Biological" }));
            Assert.That(healing.DamageContainers, Does.Contain(hunterInjurable.DamageContainer!.Value));
            var shield = (InjurableComponent) SProtoMan
                .Index<EntityPrototype>("CMUYautjaBracerShield")
                .Components["Injurable"].Component;
            Assert.That(healing.DamageContainers, Does.Not.Contain(shield.DamageContainer!.Value));
        }
    }

    [Test]
    public void HealthBarThresholdOnlyHidesAliveTargetsBelowMinimum()
    {
        Assert.Multiple(() =>
        {
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(true, FixedPoint2.New(1), null), Is.False);
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(true, FixedPoint2.New(1), FixedPoint2.New(2)), Is.True);
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(true, FixedPoint2.New(2), FixedPoint2.New(2)), Is.False);
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(true, FixedPoint2.New(4), FixedPoint2.New(5)), Is.True);
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(true, FixedPoint2.New(5), FixedPoint2.New(5)), Is.False);
            Assert.That(EntityHealthBarOverlay.ShouldHideHealthBar(false, FixedPoint2.New(1), FixedPoint2.New(2)), Is.False);
        });
    }

    [Test]
    public async Task HealthBarThresholdIsPartOfTheNetworkedInjurableState()
    {
        var map = await Pair.CreateTestMap();
        EntityUid target = default;
        NetEntity targetNet = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                target = SEntMan.SpawnEntity("InjurableContentMigrationStateTarget", map.GridCoords);
                targetNet = SEntMan.GetNetEntity(target);
                var injurable = SEntMan.GetComponent<InjurableComponent>(target);
                Assert.That(injurable.HealthBarThreshold, Is.EqualTo(FixedPoint2.New(3)));

                var state = SEntMan.GetComponentState(SEntMan.EventBus, injurable, null, GameTick.Zero);
                Assert.That(state, Is.Not.Null);
                var thresholdField = state!.GetType().GetField(nameof(InjurableComponent.HealthBarThreshold));
                Assert.That(thresholdField, Is.Not.Null,
                    "AutoNetworkedField must generate a HealthBarThreshold state member");
                Assert.That(thresholdField!.GetValue(state), Is.EqualTo(FixedPoint2.New(3)));
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(targetNet);
                var injurable = CEntMan.GetComponent<InjurableComponent>(clientTarget);
                Assert.That(injurable.HealthBarThreshold, Is.EqualTo(FixedPoint2.New(3)));
            });

            await Server.WaitAssertion(() =>
            {
                var injurable = SEntMan.GetComponent<InjurableComponent>(target);
                injurable.HealthBarThreshold = FixedPoint2.New(7);
                SEntMan.Dirty(target, injurable);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientTarget = CEntMan.GetEntity(targetNet);
                Assert.That(CEntMan.GetComponent<InjurableComponent>(clientTarget).HealthBarThreshold,
                    Is.EqualTo(FixedPoint2.New(7)),
                    "live threshold changes must be sent through the generated Injurable state");
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(target))
                    SEntMan.DeleteEntity(target);
            });
        }
    }

    private void AssertThreshold(string source, string representative, int expected)
    {
        var prototype = SProtoMan.Index<EntityPrototype>(representative);
        var injurable = (InjurableComponent) prototype.Components["Injurable"].Component;
        Assert.That(injurable.HealthBarThreshold, Is.EqualTo(FixedPoint2.New(expected)),
            $"{source} via concrete {representative}");
    }
}
