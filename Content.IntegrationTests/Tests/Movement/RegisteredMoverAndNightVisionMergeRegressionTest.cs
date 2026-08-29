using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mind;
using Content.Shared._RMC14.Input;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using ClientPointLightComponent = Robust.Client.GameObjects.PointLightComponent;
using ClientSpriteComponent = Robust.Client.GameObjects.SpriteComponent;
using LightingNightVisionComponent = Content.Shared.Overlays.LightingNightVisionComponent;
using RMCNightVisionComponent = Content.Shared._RMC14.NightVision.NightVisionComponent;
using ServerPointLightComponent = Robust.Server.GameObjects.PointLightComponent;
using TransientActiveInputMoverComponent = Content.Shared.Movement.Components.ActiveInputMoverComponent;

namespace Content.IntegrationTests.Tests.Movement;

[TestFixture]
public sealed class RegisteredMoverAndNightVisionMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: NightVisionMergeNoPointGhost
  components:
  - type: Ghost
  - type: Eye
    drawFov: false
  - type: Sprite
    sprite: Mobs/Ghosts/ghost_human.rsi
    layers:
    - state: animated
  - type: LightingNightVision
    netsync: false
    enabled: false
    prioritized: false
    relayOverlay: false

- type: entity
  parent: RMCImaginaryFriend
  id: NightVisionMergeImaginaryGhost
  components:
  - type: Ghost
";

    private static readonly EntProtoId[] LightingNightVisionPrototypes =
    [
        "ChangelingNightVisionDummy",
        "MobObserver",
        "MobBat",
    ];

    private static readonly EntProtoId[] RmcNightVisionPrototypes =
    [
        "CMUMobCarpInvasive",
        "CMUMobApe",
        "AU14AbominationGrunt",
        "CMUZombieSummoner",
        "CMXenoBurrower",
        "RMCSynthAddComponents",
    ];

    private static readonly EntProtoId[] RmcActiveMoverPrototypes =
    [
        "MobObserver",
        "CMMobHuman",
        "CMXenoBurrower",
    ];

    [Test]
    public async Task RegisteredNamesAndPrototypeFamiliesRemainDisjoint()
    {
        await Pair.Server.WaitAssertion(() =>
        {
            var factory = Pair.Server.ResolveDependency<IComponentFactory>();
            var prototypes = Pair.Server.ResolveDependency<IPrototypeManager>();

            Assert.Multiple(() =>
            {
                Assert.That(factory.TryGetRegistration("LightingNightVision", out var lightingRegistration), Is.True);
                Assert.That(lightingRegistration!.Type, Is.EqualTo(typeof(LightingNightVisionComponent)));
                Assert.That(factory.TryGetRegistration("NightVision", out var rmcRegistration), Is.True);
                Assert.That(rmcRegistration!.Type, Is.EqualTo(typeof(RMCNightVisionComponent)));
                Assert.That(lightingRegistration.Idx, Is.Not.EqualTo(rmcRegistration.Idx));

                Assert.That(factory.TryGetRegistration("ActiveInputMover", out var transientRegistration), Is.True);
                Assert.That(transientRegistration!.Type, Is.EqualTo(typeof(TransientActiveInputMoverComponent)));
                Assert.That(factory.TryGetRegistration("RMCActiveInputMover", out var rmcMoverRegistration), Is.True);
                Assert.That(rmcMoverRegistration!.Type, Is.EqualTo(typeof(RMCActiveInputMoverComponent)));
                Assert.That(transientRegistration.Idx, Is.Not.EqualTo(rmcMoverRegistration.Idx));
            });

            foreach (var id in LightingNightVisionPrototypes)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryGetComponent<LightingNightVisionComponent>(out _, factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<RMCNightVisionComponent>(out _, factory), Is.False, id.Id);
                });
            }

            foreach (var id in RmcNightVisionPrototypes)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryGetComponent<RMCNightVisionComponent>(out _, factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<LightingNightVisionComponent>(out _, factory), Is.False, id.Id);
                });
            }

            foreach (var id in RmcActiveMoverPrototypes)
            {
                var prototype = prototypes.Index<EntityPrototype>(id);
                Assert.Multiple(() =>
                {
                    Assert.That(prototype.TryGetComponent<RMCActiveInputMoverComponent>(out _, factory), Is.True, id.Id);
                    Assert.That(prototype.TryGetComponent<TransientActiveInputMoverComponent>(out _, factory), Is.False, id.Id);
                });
            }

            var observer = prototypes.Index<EntityPrototype>("MobObserver");
            Assert.That(observer.TryGetComponent<LightingNightVisionComponent>(out var observerNightVision, factory), Is.True);
            Assert.That(observer.TryGetComponent<ServerPointLightComponent>(out var observerLight, factory), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(observerNightVision!.Enabled, Is.False);
                Assert.That(observerNightVision.Prioritized, Is.False);
                Assert.That(observerNightVision.RelayOverlay, Is.False);
                Assert.That(observerNightVision.NetSyncEnabled, Is.False);
                Assert.That(observerLight!.Enabled, Is.False);
                Assert.That(observerLight.Radius, Is.EqualTo(6));
                Assert.That(observerLight.CastShadows, Is.False);
                Assert.That(observerLight.NetSyncEnabled, Is.False);
            });
        });
    }

    [Test]
    public async Task GhostLightingCyclesPreservePersonalAndNoPointPaths()
    {
        var map = await Pair.CreateTestMap();
        var player = Pair.Server.PlayerMan.Sessions.Single();
        EntityUid mindId = default;
        NetEntity observerNet = default;

        await Pair.Server.WaitAssertion(() =>
        {
            var observer = Pair.Server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
            var mind = Pair.Server.System<MindSystem>();
            mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, observer);
            mind.SetUserId(mindId, player.UserId);
            observerNet = Pair.Server.EntMan.GetNetEntity(observer);
        });
        await Pair.RunTicksSync(5);

        var clientObserver = EntityUid.Invalid;
        await Pair.Client.WaitAssertion(() =>
        {
            clientObserver = Pair.Client.EntMan.GetEntity(observerNet);
            AssertLightingState(clientObserver, drawLight: true, pointLight: false, nightVision: false);
        });

        await ToggleLighting(clientObserver);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertLightingState(clientObserver, drawLight: true, pointLight: true, nightVision: false));

        await ToggleLighting(clientObserver);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertLightingState(clientObserver, drawLight: true, pointLight: false, nightVision: true));

        await ToggleLighting(clientObserver);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertLightingState(clientObserver, drawLight: false, pointLight: false, nightVision: false));

        await ToggleLighting(clientObserver);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertLightingState(clientObserver, drawLight: true, pointLight: false, nightVision: false));

        NetEntity noPointNet = default;
        await Pair.Server.WaitAssertion(() =>
        {
            var noPoint = Pair.Server.EntMan.SpawnEntity("NightVisionMergeNoPointGhost", map.GridCoords);
            Pair.Server.System<MindSystem>().TransferTo(mindId, noPoint);
            noPointNet = Pair.Server.EntMan.GetNetEntity(noPoint);
        });
        await Pair.RunTicksSync(5);

        var clientNoPoint = EntityUid.Invalid;
        await Pair.Client.WaitAssertion(() =>
        {
            clientNoPoint = Pair.Client.EntMan.GetEntity(noPointNet);
            AssertNoPointLightingState(clientNoPoint, drawLight: true, nightVision: false);
        });

        await ToggleLighting(clientNoPoint);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertNoPointLightingState(clientNoPoint, drawLight: true, nightVision: true));

        await ToggleLighting(clientNoPoint);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertNoPointLightingState(clientNoPoint, drawLight: false, nightVision: false));

        await ToggleLighting(clientNoPoint);
        await Pair.RunTicksSync(2);
        await Pair.Client.WaitAssertion(() =>
            AssertNoPointLightingState(clientNoPoint, drawLight: true, nightVision: false));
    }

    [Test]
    public async Task GlobalGhostVisibilityNeverHidesImaginaryFriend()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ghostSystem = Pair.Client.System<Content.Client.Ghost.GhostSystem>();
            ghostSystem.ToggleGhostVisibility(true);
            ghostSystem.ToggleGhostVisibility(false);
            var ordinaryGhost = Pair.Client.EntMan.Spawn("MobObserver");
            var imaginaryFriend = Pair.Client.EntMan.Spawn("NightVisionMergeImaginaryGhost");

            var ordinarySprite = Pair.Client.EntMan.GetComponent<ClientSpriteComponent>(ordinaryGhost);
            var imaginarySprite = Pair.Client.EntMan.GetComponent<ClientSpriteComponent>(imaginaryFriend);
            Assert.Multiple(() =>
            {
                Assert.That(ordinarySprite.Visible, Is.False);
                Assert.That(imaginarySprite.Visible, Is.True,
                    "the startup hook keeps an imaginary friend visible while ordinary ghosts are hidden");
            });

            ghostSystem.ToggleGhostVisibility(true);
            Assert.Multiple(() =>
            {
                Assert.That(ordinarySprite.Visible, Is.True);
                Assert.That(imaginarySprite.Visible, Is.True);
            });

            ghostSystem.ToggleGhostVisibility(false);
            Assert.Multiple(() =>
            {
                Assert.That(ordinarySprite.Visible, Is.False);
                Assert.That(imaginarySprite.Visible, Is.True,
                    "global ghost visibility must never suppress imaginary friends");
            });

            ghostSystem.ToggleGhostVisibility(true);
        });
    }

    [Test]
    public async Task DeclarativeRmcMoverSurvivesDetachAndReattachesInputMover()
    {
        var map = await Pair.CreateTestMap();
        EntityUid observer = default;

        await Pair.Server.WaitAssertion(() =>
        {
            observer = Pair.Server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
        });
        await Pair.RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.False);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.False);
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerAttachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.True,
                    "InputMover startup owns the separate upstream transient marker");
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerDetachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
        });
        await Pair.RunTicksSync(1);

        await Pair.Server.WaitAssertion(() =>
        {
            var entities = Pair.Server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.False);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True,
                    "detach removes only runtime mover state, never the declarative RMC marker");
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.False);
            });

            entities.EventBus.RaiseLocalEvent(observer, new PlayerAttachedEvent(observer, Pair.Server.PlayerMan.Sessions.Single()));
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<InputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<RMCActiveInputMoverComponent>(observer), Is.True);
                Assert.That(entities.HasComponent<TransientActiveInputMoverComponent>(observer), Is.True);
            });
        });
    }

    private async Task ToggleLighting(EntityUid entity)
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var ev = new Content.Shared.Ghost.Components.ToggleLightingActionEvent
            {
                Performer = entity,
            };
            Pair.Client.EntMan.EventBus.RaiseLocalEvent(entity, ev);
            Assert.That(ev.Handled, Is.True);
        });
    }

    private void AssertLightingState(EntityUid entity, bool drawLight, bool pointLight, bool nightVision)
    {
        var eye = Pair.Client.EntMan.GetComponent<EyeComponent>(entity);
        var light = Pair.Client.EntMan.GetComponent<ClientPointLightComponent>(entity);
        var vision = Pair.Client.EntMan.GetComponent<LightingNightVisionComponent>(entity);
        Assert.Multiple(() =>
        {
            Assert.That(eye.DrawLight, Is.EqualTo(drawLight));
            Assert.That(light.Enabled, Is.EqualTo(pointLight));
            Assert.That(vision.Enabled, Is.EqualTo(nightVision));
        });
    }

    private void AssertNoPointLightingState(EntityUid entity, bool drawLight, bool nightVision)
    {
        var eye = Pair.Client.EntMan.GetComponent<EyeComponent>(entity);
        var vision = Pair.Client.EntMan.GetComponent<LightingNightVisionComponent>(entity);
        Assert.Multiple(() =>
        {
            Assert.That(Pair.Client.EntMan.HasComponent<ClientPointLightComponent>(entity), Is.False);
            Assert.That(eye.DrawLight, Is.EqualTo(drawLight));
            Assert.That(vision.Enabled, Is.EqualTo(nightVision));
        });
    }
}
