using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.TacticalMap;

[TestFixture]
public sealed class RMCAdminObserverPrototypeTest : GameTest
{
    private static readonly EntProtoId[] AdminObservers =
    [
        "AdminObserver",
        "RMCAdminObserver",
    ];

    [Test]
    public async Task AdminObserversHaveValidTacticalMapActions()
    {
        var componentFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            foreach (var id in AdminObservers)
            {
                Assert.That(SProtoMan.TryIndex<EntityPrototype>(id, out var prototype), Is.True,
                    $"{id} failed to load.");
                Assert.That(prototype!.TryComp<TacticalMapUserComponent>(out var tacticalMap, componentFactory), Is.True,
                    $"{id} does not have a tactical-map user component.");
                Assert.That(SProtoMan.HasIndex<EntityPrototype>(tacticalMap!.ActionId), Is.True,
                    $"{id} references missing tactical-map action {tacticalMap.ActionId}.");
            }
        });
    }
}
