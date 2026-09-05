using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Tag;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Follower;

[TestFixture]
[TestOf(typeof(FollowerSystem))]
public sealed class FollowerMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        Dirty = true,
    };

    [Test]
    public async Task InvalidEntitiesParentsAndTransfersAreSafeWhileRandomSelectionRetainsFilters()
    {
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid mapEntity = default;
        EntityUid follower = default;

        await Server.WaitPost(() =>
        {
            var mapSystem = Server.System<SharedMapSystem>();
            var followSystem = Server.System<FollowerSystem>();
            var tagSystem = Server.System<TagSystem>();
            mapSystem.CreateMap(out var mapId);
            mapEntity = mapSystem.GetMap(mapId);

            var coordinates = new MapCoordinates(0, 0, mapId);
            follower = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
            var first = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
            var second = SEntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
            Server.PlayerMan.SetAttachedEntity(session, follower);

            followSystem.StartFollowingEntity(follower, first);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FollowerComponent>(follower).Following, Is.EqualTo(first));
                Assert.That(SEntMan.GetComponent<FollowedComponent>(first).Following, Does.Contain(follower));
                Assert.That(followSystem.GetRandomGhostFollowed(), Is.EqualTo(first));
                Assert.That(followSystem.GetMostGhostFollowed(), Is.EqualTo(first));
            });

            followSystem.TransferFollowers(first, second);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FollowerComponent>(follower).Following, Is.EqualTo(second));
                Assert.That(SEntMan.HasComponent<FollowedComponent>(first), Is.False);
                Assert.That(SEntMan.GetComponent<FollowedComponent>(second).Following,
                    Is.EquivalentTo(new[] { follower }));
                Assert.That(followSystem.GetRandomGhostFollowed(), Is.EqualTo(second));
            });

            tagSystem.AddTag(second, "NotGhostnadoWarpable");
            Assert.Multiple(() =>
            {
                Assert.That(followSystem.GetRandomGhostFollowed(), Is.Null,
                    "the fork tag remains an explicit random-warp exclusion");
                Assert.That(followSystem.GetMostGhostFollowed(), Is.Null);
            });

            followSystem.StopFollowingEntity(follower, second);
            Assert.That(SEntMan.HasComponent<FollowerComponent>(follower), Is.False);

            var invalidParent = SEntMan.SpawnEntity(null, coordinates);
            SEntMan.DeleteEntity(invalidParent);
            var secondTransform = SEntMan.GetComponent<TransformComponent>(second);
            var originalParent = secondTransform.ParentUid;
            var parentField = typeof(TransformComponent)
                .GetField("_parent", BindingFlags.Instance | BindingFlags.NonPublic)!;
            parentField.SetValue(secondTransform, invalidParent);
            try
            {
                Assert.DoesNotThrow(() => followSystem.StartFollowingEntity(follower, second));
                Assert.That(SEntMan.HasComponent<FollowerComponent>(follower), Is.False,
                    "a stale parent chain must be rejected before follow/container mutation");
            }
            finally
            {
                parentField.SetValue(secondTransform, originalParent);
            }

            SEntMan.DeleteEntity(second);
            Assert.DoesNotThrow(() => followSystem.StartFollowingEntity(follower, second));
            Assert.DoesNotThrow(() => followSystem.StartFollowingEntity(EntityUid.Invalid, first));
            Assert.DoesNotThrow(() => followSystem.StopFollowingEntity(EntityUid.Invalid, first));

            Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            SEntMan.DeleteEntity(follower);
            Assert.DoesNotThrow(() => followSystem.StartFollowingEntity(follower, first));
            Assert.DoesNotThrow(() => followSystem.StopFollowingEntity(follower, first));
            Assert.DoesNotThrow(() => followSystem.TransferFollowers(first, follower));
        });

        await Server.WaitPost(() =>
        {
            Server.PlayerMan.SetAttachedEntity(session, originalAttached);
            SEntMan.DeleteEntity(mapEntity);
        });
    }
}
