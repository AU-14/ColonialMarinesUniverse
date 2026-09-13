using Content.IntegrationTests.Fixtures;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Morgue;
using Content.Shared.Morgue.Components;
using Content.Shared.Standing;
using Content.Shared.Storage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Medical;

[TestFixture]
[TestOf(typeof(EntityStorageLayingDownOverrideSystem))]
public sealed class MorgueLayingDownOverrideMergeRegressionTest : GameTest
{
    private static readonly EntProtoId EnabledStorage = "MorgueLayingOverrideEnabled";
    private static readonly EntProtoId DisabledStorage = "MorgueLayingOverrideDisabled";
    private static readonly EntProtoId Actor = "MorgueLayingOverrideActor";

    private static readonly EntProtoId[] DefaultEnabledPrototypes =
    [
        "BodyBag",
        "Morgue",
        "Crematorium",
        "CMBodyBag",
        "CMMorgue",
    ];

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: MorgueLayingOverrideEnabled
  components:
  - type: EntityStorageLayingDownOverride

- type: entity
  id: MorgueLayingOverrideDisabled
  components:
  - type: EntityStorageLayingDownOverride
    enabled: false

- type: entity
  id: MorgueLayingOverrideActor
  components:
  - type: StandingState
  - type: MobState
  - type: Appearance
";

    [Test]
    public async Task DefaultFiltersOnlyStandingWhileDisabledAcceptsEveryContentKind()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            _ = server.System<EntityStorageLayingDownOverrideSystem>();
            var entities = server.EntMan;
            var standingSystem = server.System<StandingStateSystem>();
            var mobState = server.System<MobStateSystem>();

            var enabledStorage = entities.SpawnEntity(EnabledStorage, map.GridCoords);
            var disabledStorage = entities.SpawnEntity(DisabledStorage, map.GridCoords);
            var standing = entities.SpawnEntity(Actor, map.GridCoords);
            var down = entities.SpawnEntity(Actor, map.GridCoords);
            var critical = entities.SpawnEntity(Actor, map.GridCoords);
            var dead = entities.SpawnEntity(Actor, map.GridCoords);
            var nonMob = entities.SpawnEntity(null, map.GridCoords);

            Assert.That(standingSystem.Down(down, playSound: false, force: true), Is.True);
            mobState.ChangeMobState(critical, MobState.Critical);
            mobState.ChangeMobState(dead, MobState.Dead);
            Assert.Multiple(() =>
            {
                Assert.That(standingSystem.IsDown(down), Is.True);
                Assert.That(standingSystem.IsDown(critical), Is.True,
                    "entering critical must use the upstream down-state path");
                Assert.That(standingSystem.IsDown(dead), Is.True,
                    "entering dead must use the upstream down-state path");
                Assert.That(standingSystem.IsDown(nonMob), Is.False,
                    "entities without StandingState are deliberately not treated as standing mobs");
            });

            var allContents = new[] { standing, down, critical, dead, nonMob };
            var enabledContents = allContents.ToHashSet();
            var enabledEvent = new StorageBeforeCloseEvent(null, enabledContents, []);
            entities.EventBus.RaiseLocalEvent(enabledStorage, ref enabledEvent);
            Assert.Multiple(() =>
            {
                Assert.That(enabledContents, Does.Not.Contain(standing));
                Assert.That(enabledContents, Does.Contain(down));
                Assert.That(enabledContents, Does.Contain(critical));
                Assert.That(enabledContents, Does.Contain(dead));
                Assert.That(enabledContents, Does.Contain(nonMob));
            });

            var disabledContents = allContents.ToHashSet();
            var disabledEvent = new StorageBeforeCloseEvent(null, disabledContents, []);
            entities.EventBus.RaiseLocalEvent(disabledStorage, ref disabledEvent);
            Assert.That(disabledContents, Is.EquivalentTo(allContents),
                "enabled:false is the RMC stasis-bag opt-out and must accept a standing occupant");
        });
    }

    [Test]
    public async Task LivePrototypeGraphHasFiveDefaultFiltersAndOneStasisOptOut()
    {
        var server = Pair.Server;
        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.ResolveDependency<IComponentFactory>();

            foreach (var id in DefaultEnabledPrototypes)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.That(prototype.TryGetComponent<EntityStorageLayingDownOverrideComponent>(
                    out var component,
                    factory), Is.True, id.Id);
                Assert.That(component!.Enabled, Is.True, id.Id);
            }

            var stasis = prototypes.Index<EntityPrototype>("CMStasisBag");
            Assert.That(stasis.TryGetComponent<EntityStorageLayingDownOverrideComponent>(
                out var stasisComponent,
                factory), Is.True);
            Assert.That(stasisComponent!.Enabled, Is.False,
                "CMStasisBag must remain the sole standing-occupant opt-out");
        });
    }
}
