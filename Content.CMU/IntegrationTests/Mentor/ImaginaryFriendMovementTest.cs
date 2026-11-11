using System.Linq;
using System.Numerics;
using Content.Server._RMC14.Mentor.ImaginaryFriend;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Physics.Controllers;
using Content.Shared._RMC14.Mentor.ImaginaryFriend;
using Content.Shared.Movement.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Mentor;

[TestFixture]
public sealed class ImaginaryFriendMovementTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task MovementKeepsMentorInFriendAndCanReturn(bool visiting)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var minds = entities.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();
        EntityUid body = default;
        EntityUid friend = default;
        Vector2 start = default;

        await server.WaitAssertion(() =>
        {
            body = entities.SpawnEntity("CMMobHuman", map.GridCoords);
            var ghost = entities.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var mind = minds.CreateMind(player.UserId, "Mentor");
            minds.TransferTo(mind, body);
            minds.SetUserId(mind, player.UserId);
            if (visiting)
                minds.Visit(mind, ghost);
            else
                minds.TransferTo(mind, ghost);

            entities.System<ImaginaryFriendSystem>().BecomeImaginaryFriend(body, ghost);
            friend = player.AttachedEntity!.Value;
            Assert.That(entities.HasComponent<ImaginaryFriendComponent>(friend), Is.True);
            start = entities.System<SharedTransformSystem>().GetWorldPosition(friend);
            var input = entities.GetComponent<InputMoverComponent>(friend);
            entities.System<MoverController>().SetVelocityDirection((friend, input), Direction.East, 0, true);
            Assert.That(player.AttachedEntity, Is.EqualTo(friend), "movement must not ghost the mentor");
        });

        await pair.RunTicksSync(15);
        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(friend));
            var position = entities.System<SharedTransformSystem>().GetWorldPosition(friend);
            Assert.That(position.X, Is.GreaterThan(start.X), "the friend must actually move");
            var stop = new ImaginaryFriendStopBeingFriendsActionEvent();
            entities.EventBus.RaiseLocalEvent(friend, stop);
            Assert.That(player.AttachedEntity, Is.EqualTo(body));
        });
        await pair.CleanReturnAsync();
    }
}
