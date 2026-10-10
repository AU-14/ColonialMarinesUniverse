using System.Collections.Generic;
using System.Numerics;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared._RMC14.Xenonids.Boxer;
using Content.Shared.Actions.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoBoxerTest
{
    [Test]
    public async Task BoxerHasNoFollowUpStrains()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        try
        {
            await server.WaitAssertion(() =>
            {
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var components = server.ResolveDependency<IComponentFactory>();

                Assert.That(prototypes.TryIndex<EntityPrototype>("CMXenoWarriorBoxer", out var boxer), Is.True);
                Assert.That(boxer!.TryComp<XenoEvolutionComponent>(out var evolution, components), Is.True);
                Assert.That(evolution!.Strains, Is.Empty,
                    "Boxer must not expose Bulwark or another Boxer strain after specializing.");
            });
        }
        finally
        {
            server.Dispose();
        }
    }

    private static readonly HashSet<string> PunchSounds =
    [
        "/Audio/Weapons/punch1.ogg",
        "/Audio/Weapons/punch2.ogg",
        "/Audio/Weapons/punch3.ogg",
        "/Audio/Weapons/punch4.ogg",
    ];

    [Test]
    public async Task BoxerAttacksPlayPunchSound()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityCoordinates gridCoords = default;

        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var grid = mapSystem.CreateGridEntity(mapId);
            gridCoords = new EntityCoordinates(grid, 0, 0);
        });

        EntityUid boxer = default;
        EntityUid target = default;
        var actions = new List<EntityUid>();

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                boxer = entMan.SpawnEntity("CMXenoWarriorBoxer", gridCoords.Offset(new Vector2(0.5f, 0.5f)));
                target = entMan.SpawnEntity("CMMobHuman", gridCoords.Offset(new Vector2(1.5f, 0.5f)));

                var punchAction = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                actions.Add(punchAction);
                var punchActionComp = entMan.EnsureComponent<ActionComponent>(punchAction);
                var beforePunch = GetAudioCount(entMan);
                var punch = new XenoBoxerPunchActionEvent
                {
                    Performer = boxer,
                    Action = (punchAction, punchActionComp),
                    Target = target,
                };
                entMan.EventBus.RaiseLocalEvent(boxer, punch);
                Assert.That(punch.Handled, Is.True);
                AssertPunchSound(entMan, beforePunch, "Punch");

                var jabAction = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                actions.Add(jabAction);
                var jabActionComp = entMan.EnsureComponent<ActionComponent>(jabAction);
                var beforeJab = GetAudioCount(entMan);
                var jab = new XenoBoxerJabActionEvent
                {
                    Performer = boxer,
                    Action = (jabAction, jabActionComp),
                    Target = target,
                };
                entMan.EventBus.RaiseLocalEvent(boxer, jab);
                Assert.That(jab.Handled, Is.True);
                AssertPunchSound(entMan, beforeJab, "Jab");

                var boxerComp = entMan.GetComponent<XenoBoxerComponent>(boxer);
                var uppercutAction = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                actions.Add(uppercutAction);
                var uppercutActionComp = entMan.EnsureComponent<ActionComponent>(uppercutAction);
                var beforeUppercut = GetAudioCount(entMan);
                var uppercut = new XenoBoxerUppercutActionEvent
                {
                    Performer = boxer,
                    Action = (uppercutAction, uppercutActionComp),
                    Target = target,
                };
                entMan.EventBus.RaiseLocalEvent(boxer, uppercut);
                Assert.That(uppercut.Handled, Is.True);
                AssertPunchSound(entMan, beforeUppercut, "Uppercut");
                Assert.That(boxerComp.KoMeter, Is.EqualTo(0));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var action in actions)
                {
                    if (server.EntMan.EntityExists(action))
                        server.EntMan.DeleteEntity(action);
                }

                if (server.EntMan.EntityExists(target))
                    server.EntMan.DeleteEntity(target);

                if (server.EntMan.EntityExists(boxer))
                    server.EntMan.DeleteEntity(boxer);
            });

            server.Dispose();
        }
    }

    private static int GetAudioCount(IEntityManager entMan)
    {
        var count = 0;
        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }

    private static void AssertPunchSound(IEntityManager entMan, int beforeCount, string attackName)
    {
        var afterCount = GetAudioCount(entMan);
        Assert.That(afterCount, Is.GreaterThan(beforeCount), $"Expected {attackName} to create an audio entity");

        var query = entMan.EntityQueryEnumerator<AudioComponent>();
        var hasPunchSound = false;
        while (query.MoveNext(out _, out var component))
        {
            if (PunchSounds.Contains(component.FileName))
            {
                hasPunchSound = true;
                break;
            }
        }

        Assert.That(hasPunchSound, Is.True, $"Expected {attackName} to use one of punch1.ogg-punch4.ogg");
    }
}
