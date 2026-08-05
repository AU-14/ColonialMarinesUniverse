using Content.Client.PowerCell;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Power.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14.Power;

/// <summary>
/// Tests that the RMC recharger visual states exist in its configured RSI.
/// </summary>
[TestFixture]
public sealed class RMCRechargerVisualsTest : GameTest
{
    private static readonly EntProtoId Recharger = "RMCRecharger";

    [Test]
    public async Task ConfiguredStatesExist()
    {
        var client = Pair.Client;
        var spriteSystem = client.System<SpriteSystem>();

        await client.WaitAssertion(() =>
        {
            var uid = client.EntMan.Spawn(Recharger);
            try
            {
                var sprite = client.EntMan.GetComponent<SpriteComponent>(uid);
                var visuals = client.EntMan.GetComponent<PowerChargerVisualsComponent>(uid);

                Assert.Multiple(() =>
                {
                    AssertStateExists(spriteSystem,
                        uid,
                        sprite,
                        PowerChargerVisualLayers.Base,
                        visuals.EmptyState);
                    AssertStateExists(spriteSystem,
                        uid,
                        sprite,
                        PowerChargerVisualLayers.Base,
                        visuals.OccupiedState);

#pragma warning disable RA0002 // This regression test intentionally inspects visualizer-owned prototype configuration.
                    foreach (var status in Enum.GetValues<CellChargerStatus>())
                    {
                        if (!visuals.LightStates.TryGetValue(status, out var state))
                        {
                            Assert.Fail($"{Recharger} has no light state configured for {status}.");
                            continue;
                        }

                        AssertStateExists(spriteSystem,
                            uid,
                            sprite,
                            PowerChargerVisualLayers.Light,
                            state);
                    }
#pragma warning restore RA0002
                });
            }
            finally
            {
                client.EntMan.DeleteEntity(uid);
            }
        });
    }

    private static void AssertStateExists(
        SpriteSystem spriteSystem,
        EntityUid uid,
        SpriteComponent sprite,
        PowerChargerVisualLayers layerKey,
        string state)
    {
        if (!spriteSystem.LayerMapTryGet((uid, sprite), layerKey, out var layerId, false))
        {
            Assert.Fail($"{Recharger} has PowerChargerVisualsComponent but no {layerKey} layer map.");
            return;
        }

        if (!spriteSystem.TryGetLayer((uid, sprite), layerId, out var layer, false))
        {
            Assert.Fail($"{Recharger} has PowerChargerVisualsComponent but no {layerKey} sprite layer.");
            return;
        }

        Assert.That(layer.ActualRsi.TryGetState(state, out _),
            $"{Recharger} has PowerChargerVisualsComponent, but {layer.ActualRsi.Path} doesn't have state {state}.");
    }
}
