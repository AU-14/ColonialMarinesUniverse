using System.Linq;
using Content.Server.Ghost; // CMU14
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
    [TestPrototypes] // CMU14
    private const string Prototypes = @"
- type: entity
  id: CMUGhostRoleTestEntity
  components:
  - type: MindContainer
  - type: MobState
  - type: MobThresholds
    thresholds:
      0: Alive
      200: Dead
";
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

    // CMU14 method
    [Test]
    public async Task GhostedBodyRegistersAsGhostRole()
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
        var ghosts = entMan.System<GhostSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid mob = default;

        await server.WaitAssertion(() =>
        {
            mob = entMan.SpawnEntity("CMUGhostRoleTestEntity", MapCoordinates.Nullspace);
            entMan.EnsureComponent<MindContainerComponent>(mob);

            var mindId = mind.CreateMind(player.UserId, "Ghosted Mob");
            mind.TransferTo(mindId, mob);
            mind.SetUserId(mindId, player.UserId);

            entMan.EnsureComponent<GhostTakeoverAvailableComponent>(mob);
            entMan.EnsureComponent<GhostRoleComponent>(mob);

            // alive player runs the ghost command: non-returnable, so the mind leaves the body
            // and the role must re-register even though the session detaches one event later
            Assert.That(ghosts.OnGhostAttempt(mindId, true, viaCommand: true), Is.True);

            var mobNet = entMan.GetNetEntity(mob);
            Assert.That(ghostRole.GetGhostRoleCount(), Is.EqualTo(1));
            Assert.That(ghostRole.GetGhostRolesInfo(null).Any(info => info.Entity == mobNet), Is.True);
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
