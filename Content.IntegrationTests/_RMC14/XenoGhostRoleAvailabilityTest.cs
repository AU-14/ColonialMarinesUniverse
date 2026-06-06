using System.Linq;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoGhostRoleAvailabilityTest
{
    [Test]
    public async Task LarvaDoesNotRegisterAsGhostRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ghostRole = entMan.System<GhostRoleSystem>();

        EntityUid larva = default;

        await server.WaitAssertion(() =>
        {
            larva = entMan.SpawnEntity("CMXenoLarva", MapCoordinates.Nullspace);

            var larvaNet = entMan.GetNetEntity(larva);
            Assert.That(ghostRole.GetGhostRoleCount(), Is.EqualTo(0));
            Assert.That(ghostRole.GetGhostRolesInfo(null).Any(info => info.Entity == larvaNet), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ControlledEntityDoesNotRegisterAsGhostRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var entMan = server.EntMan;
        var ghostRole = entMan.System<GhostRoleSystem>();
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid controlled = default;

        await server.WaitAssertion(() =>
        {
            controlled = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(controlled);

            var mindId = mind.CreateMind(player.UserId, "Controlled Xeno");
            mind.TransferTo(mindId, controlled);
            mind.SetUserId(mindId, player.UserId);

            entMan.EnsureComponent<GhostTakeoverAvailableComponent>(controlled);
            var role = entMan.EnsureComponent<GhostRoleComponent>(controlled);

            ghostRole.RegisterGhostRole(new Entity<GhostRoleComponent>(controlled, role));

            var controlledNet = entMan.GetNetEntity(controlled);
            Assert.That(ghostRole.GetGhostRoleCount(), Is.EqualTo(0));
            Assert.That(ghostRole.GetGhostRolesInfo(null).Any(info => info.Entity == controlledNet), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
