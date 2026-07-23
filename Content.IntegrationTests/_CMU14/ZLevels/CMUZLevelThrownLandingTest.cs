using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels;
using Content.Shared.Gravity;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests._CMU14.ZLevels;

[TestFixture]
[NonParallelizable]
public sealed class CMUZLevelThrownLandingTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
    };

    [Test]
    public async Task DisabledZLevelsPreserveNormalLandingEventOverHole()
    {
        await OverrideCVar(Side.Server, CMUZLevelsCVars.Enabled, false, sync: false);
        var lifecycle = SEntMan.System<CMUZNetworkLifecycleSystem>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var throwing = SEntMan.System<ThrownItemSystem>();
        var observer = SEntMan.System<CMUZLandingObserverSystem>();
        var maps = new EntityUid[2];
        var mapIds = new MapId[2];
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < maps.Length; i++)
            {
                maps[i] = mapSystem.CreateMap(out mapIds[i]);
                SEntMan.AddComponent<MapGridComponent>(maps[i]);
                var gravity = SEntMan.EnsureComponent<GravityComponent>(maps[i]);
                gravity.Enabled = true;
                gravity.Inherent = true;
            }

            Assert.That(lifecycle.TryCombineLevels(maps, out _, out var error), Is.True, error);

            item = SEntMan.SpawnEntity(
                "MobHuman",
                new EntityCoordinates(maps[1], new Vector2(0.5f, 0.5f)));
            SEntMan.EnsureComponent<CMUZLandingProbeComponent>(item);
            SEntMan.EnsureComponent<ThrownItemComponent>(item).Landed = false;
        });

        await Server.WaitAssertion(() =>
        {
            var thrown = SEntMan.GetComponent<ThrownItemComponent>(item);
            var physics = SEntMan.GetComponent<PhysicsComponent>(item);
            observer.LandEvents = 0;

            throwing.LandComponent(item, thrown, physics, playSound: false);

            Assert.That(observer.LandEvents, Is.EqualTo(1));
        });

        await Server.WaitPost(() =>
        {
            foreach (var mapId in mapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }
        });
    }
}

public sealed class CMUZLandingObserverSystem : EntitySystem
{
    public int LandEvents;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUZLandingProbeComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<CMUZLandingProbeComponent> ent, ref LandEvent args)
    {
        LandEvents++;
    }
}

[RegisterComponent]
public sealed partial class CMUZLandingProbeComponent : Component
{
}
