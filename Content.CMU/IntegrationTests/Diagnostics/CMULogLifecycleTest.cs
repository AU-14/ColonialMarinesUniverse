using Content.IntegrationTests.Fixtures;
using Content.Server.Ghost;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Gibbing;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared.Ghost.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Shuttles.Components;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.CMU14.Diagnostics;

[TestFixture]
public sealed class CMULogLifecycleTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUTestThrowReceiver
          components:
          - type: ThrowInsertContainer
            containerId: submission
            probability: 1
            insertSound: null
          - type: ContainerContainer
            containers:
              submission: !type:Container {}
        """;

    [Test]
    public async Task ScatteringInventoryDropsRealItemsAndReleasesVirtualHands()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("RMCWeaponRifleM54C", map.GridCoords);
            var hands = Server.System<SharedHandsSystem>();
            Assert.That(hands.TryPickup(owner, item, SEntMan.GetComponent<HandsComponent>(owner).ActiveHandId!), Is.True);
            Assert.That(Server.System<SharedVirtualItemSystem>().TrySpawnVirtualItemInHand(item, owner, out var placeholder), Is.True);
            Server.System<RMCGibSystem>().ScatterInventoryItems(owner, 1f, 0f);
            Assert.That(hands.EnumerateHeld(owner), Is.Empty);
            Assert.That(SEntMan.Deleted(placeholder!.Value) || SEntMan.IsQueuedForDeletion(placeholder.Value), Is.True);
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(item).LinearVelocity.LengthSquared(), Is.GreaterThan(0));
            SEntMan.DeleteEntity(owner);
            SEntMan.DeleteEntity(item);
        });
    }

    [Test]
    public async Task ThrowCollisionAfterContainerShutdownLeavesItemOutside()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var owner = SEntMan.SpawnEntity("CMUTestThrowReceiver", map.GridCoords);
            var item = SEntMan.SpawnEntity(null, map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            var container = containers.GetContainer(owner, "submission");
            var hit = new ThrowHitByEvent(item, owner, new ThrownItemComponent());
            SEntMan.EventBus.RaiseLocalEvent(owner, ref hit);
            Assert.That(container.Contains(item), Is.True, "Normal throw insertion must still work.");
            Assert.That(containers.Remove(item, container), Is.True);
            containers.ShutdownContainer(container);
            SEntMan.EventBus.RaiseLocalEvent(owner, ref hit);
            Assert.That(containers.IsEntityInContainer(item), Is.False);
            Assert.That(SEntMan.EntityExists(item), Is.True);
            SEntMan.DeleteEntity(owner);
            SEntMan.DeleteEntity(item);
        });
    }

    [Test]
    public async Task GhostWarpToOwnChildUsesWorldPositionWithoutParentCycle()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
                var child = SEntMan.SpawnEntity(null, new EntityCoordinates(ghost, 2, 3));
                var transforms = Server.System<SharedTransformSystem>();
                var destination = transforms.GetMapCoordinates(child);
                Server.PlayerMan.SetAttachedEntity(ServerSession, ghost);
                Server.System<GhostSystem>().GhostWarpRequest(ServerSession, SEntMan.GetNetEntity(child));
                Assert.That(transforms.GetMapCoordinates(ghost), Is.EqualTo(destination));
                Assert.That(SEntMan.GetComponent<TransformComponent>(ghost).ParentUid, Is.Not.EqualTo(child));
                Server.PlayerMan.SetAttachedEntity(ServerSession, previous);
                SEntMan.DeleteEntity(ghost);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }

    [Test]
    public async Task ParasiteChoiceAfterRoleRemovalKeepsGhostAttached()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var parasite = SEntMan.SpawnEntity("CMXenoParasite", map.GridCoords);
                var ghost = SEntMan.SpawnEntity("MobObserver", map.GridCoords);
                Server.PlayerMan.SetAttachedEntity(ServerSession, ghost);
                Server.System<GhostSystem>().SetTimeOfDeath((ghost, SEntMan.GetComponent<GhostComponent>(ghost)),
                    SGameTiming.RealTime - TimeSpan.FromMinutes(4));
                SEntMan.RemoveComponent<GhostRoleComponent>(parasite);
                SEntMan.EventBus.RaiseLocalEvent(parasite, new XenoParasiteGhostBuiMsg
                {
                    Actor = ghost,
                    UiKey = XenoParasiteGhostUI.Key,
                });
                Assert.That(ServerSession.AttachedEntity, Is.EqualTo(ghost));
                Server.PlayerMan.SetAttachedEntity(ServerSession, previous);
                SEntMan.DeleteEntity(parasite);
                SEntMan.DeleteEntity(ghost);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }

    [Test]
    public async Task RemovingFlightStopsOwnedTravelLoop()
    {
        var map = await Pair.CreateTestMap();
        EntityUid stream = default;
        await Server.WaitAssertion(() =>
        {
            var ship = SEntMan.SpawnEntity(null, map.GridCoords);
            var flight = SEntMan.AddComponent<FTLComponent>(ship);
            stream = Server.System<SharedAudioSystem>().PlayPvs(new SoundPathSpecifier(
                "/Audio/Effects/Shuttle/hyperspace_progress.ogg", AudioParams.Default.WithLoop(true)), ship)!.Value.Entity;
            flight.TravelStream = stream;
            SEntMan.RemoveComponent<FTLComponent>(ship);
            Assert.That(SEntMan.IsQueuedForDeletion(stream) || SEntMan.Deleted(stream), Is.True,
                "Removing an interrupted flight must release its infinite travel loop before another flight starts.");
            SEntMan.DeleteEntity(ship);
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(stream), Is.True));
    }
}
