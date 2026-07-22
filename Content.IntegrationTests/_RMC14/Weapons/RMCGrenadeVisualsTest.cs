using Content.Client.Trigger.Components;
using Content.Client.Trigger.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Client.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Weapons;

[TestFixture]
public sealed class RMCGrenadeVisualsTest : GameTest
{
    private static readonly string[] Grenades =
    [
        "CMGrenadeHighExplosive",
        "CMGrenadeFrag",
    ];

    [Test]
    public async Task GrenadesHavePrimedVisuals()
    {
        var client = Pair.Client;
        var spriteSystem = client.System<SpriteSystem>();

        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var prototype in Grenades)
                {
                    var grenade = client.EntMan.Spawn(prototype);
                    try
                    {
                        Assert.That(client.EntMan.HasComponent<TimerTriggerVisualsComponent>(grenade),
                            Is.True,
                            $"{prototype} has no timer visualizer.");

                        var sprite = client.EntMan.GetComponent<SpriteComponent>(grenade);
                        Assert.That(spriteSystem.LayerMapTryGet(
                                (grenade, sprite),
                                TriggerVisualLayers.Base,
                                out var layerId,
                                false),
                            Is.True,
                            $"{prototype} has no trigger visual layer.");

                        if (!spriteSystem.TryGetLayer((grenade, sprite), layerId, out var layer, false))
                        {
                            Assert.Fail($"{prototype} has no sprite layer for its trigger visuals.");
                            continue;
                        }

                        Assert.That(layer.ActualRsi.TryGetState("primed", out _),
                            Is.True,
                            $"{prototype} has no primed animation in {layer.ActualRsi.Path}.");
                    }
                    finally
                    {
                        client.EntMan.DeleteEntity(grenade);
                    }
                }
            });
        });
    }

    [Test, NonParallelizable]
    public async Task HedpRemainsPrimedUntilExplosionAppears()
    {
        await AssertPrimedUntilEffectAppears("CMGrenadeHighExplosive", "CMExplosionEffectGrenade");
    }

    [Test, NonParallelizable]
    public async Task HefaRemainsPrimedUntilExplosionAppears()
    {
        await AssertPrimedUntilEffectAppears("CMGrenadeFrag", "CMExplosionEffectGrenade");
    }

    [Test, NonParallelizable]
    public async Task HsdpRemainsVisibleUntilSmokeAppears()
    {
        await AssertPrimedUntilEffectAppears("CMGrenadeSmoke", "RMCSmoke");
    }

    private async Task AssertPrimedUntilEffectAppears(string grenadePrototype, string effectPrototype)
    {
        var map = await Pair.CreateTestMap();
        var playerManager = Server.ResolveDependency<IPlayerManager>();
        var session = playerManager.Sessions.Single();
        EntityUid grenade = default;
        NetEntity grenadeNet = default;

        await Server.WaitAssertion(() =>
        {
            var player = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(playerManager.SetAttachedEntity(session, player), Is.True);

            grenade = SEntMan.SpawnEntity(grenadePrototype, map.GridCoords);
            grenadeNet = SEntMan.GetNetEntity(grenade);

            var trigger = Server.System<TriggerSystem>();
            trigger.SetDelay(grenade, TimeSpan.FromSeconds(10));
            Assert.That(trigger.ActivateTimerTrigger(grenade), Is.True);
        });

        await RunUntilSynced();
        await RunTicksSync(1);
        await Client.WaitAssertion(() => Assert.That(GetGrenadeState(), Is.EqualTo("primed")));
        await Server.WaitAssertion(() =>
        {
            var trigger = Server.System<TriggerSystem>();
            var remaining = trigger.GetRemainingTime(grenade);
            Assert.That(remaining, Is.Not.Null);
            Assert.That(trigger.TryDelay(grenade, SGameTiming.TickPeriod * 2 - remaining!.Value), Is.True);
        });

        var effectSeen = false;
        for (var i = 0; i < 20 && !effectSeen; i++)
        {
            await RunTicksSync(1);
            await Client.WaitAssertion(() =>
            {
                var grenade = CEntMan.GetEntity(grenadeNet);
                var grenadeExists = CEntMan.EntityExists(grenade);
                effectSeen = CountEntities(effectPrototype) > 0;

                Assert.That(grenadeExists || effectSeen,
                    Is.True,
                    $"{grenadePrototype} disappeared before {effectPrototype} became visible.");

                if (!grenadeExists)
                    return;

                Assert.That(GetGrenadeState(),
                    Is.EqualTo("primed"),
                    $"{grenadePrototype} returned to its idle sprite before disappearing.");
            });
        }

        Assert.That(effectSeen, Is.True, $"{effectPrototype} never became visible.");

        int CountEntities(string prototype)
        {
            var count = 0;
            var query = CEntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out _, out var metadata))
            {
                if (!metadata.Deleted && metadata.EntityPrototype?.ID == prototype)
                    count++;
            }

            return count;
        }

        string GetGrenadeState()
        {
            var clientGrenade = CEntMan.GetEntity(grenadeNet);
            var sprite = CEntMan.GetComponent<SpriteComponent>(clientGrenade);
            Assert.That(Client.System<SpriteSystem>().LayerMapTryGet(
                    (clientGrenade, sprite),
                    TriggerVisualLayers.Base,
                    out var layerId,
                    false),
                Is.True);
            Assert.That(Client.System<SpriteSystem>().TryGetLayer(
                    (clientGrenade, sprite),
                    layerId,
                    out var layer,
                    false),
                Is.True);
            return layer.ActualState?.StateId.Name ?? string.Empty;
        }
    }
}
