using Content.Client.Overlays;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CMU14.Yautja;
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

}
