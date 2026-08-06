#nullable enable
using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared.GameObjects;
using Robust.UnitTesting;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
public sealed class GhostRoleLobbyTests
{
    private const string GhostRoleProtoId = "GhostRoleLobbyTestEntity";
    private const string PlayerBodyProtoId = "GhostRoleLobbyPlayerBody";
    private const string RaffleGhostRoleProtoId = "GhostRoleLobbyRaffleTestEntity";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {GhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
          - type: GhostTakeoverAvailable
          - type: MobState

        - type: entity
          id: {RaffleGhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
            raffle:
              settings: short
          - type: GhostTakeoverAvailable
          - type: MobState

        - type: entity
          id: {PlayerBodyProtoId}
          components:
          - type: MobState
        """;

    [Test]
    public async Task LobbyPlayerCanJoinGhostRoleRaffle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            InLobby = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var player = server.PlayerMan.Sessions.Single();
        var ticker = entityManager.System<GameTicker>();
        var ghostRoles = entityManager.System<GhostRoleSystem>();

        Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid role = default;
        await server.WaitAssertion(() => role = entityManager.SpawnEntity(RaffleGhostRoleProtoId, map.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var identifier = entityManager.GetComponent<GhostRoleComponent>(role).Identifier;
            ghostRoles.Request(player, identifier);
        });
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(player.AttachedEntity, Is.Null);
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));
            Assert.That(entityManager.GetComponent<GhostRoleRaffleComponent>(role).CurrentMembers, Does.Contain(player));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LobbyPlayerCanTakeGhostRole()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            InLobby = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var player = server.PlayerMan.Sessions.Single();
        var ticker = entityManager.System<GameTicker>();
        var ghostRoles = entityManager.System<GhostRoleSystem>();

        Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid role = default;
        await server.WaitAssertion(() => role = entityManager.SpawnEntity(GhostRoleProtoId, map.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var identifier = entityManager.GetComponent<GhostRoleComponent>(role).Identifier;
            Assert.That(ghostRoles.Takeover(player, identifier), Is.True);
        });
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(role));
            Assert.That(ticker.PlayerGameStatuses[player.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(entityManager.GetComponent<GhostRoleComponent>(role).Taken, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpawnedPlayerLeavesGhostRoleRaffle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            InLobby = true,
        });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entityManager = server.EntMan;
        var player = server.PlayerMan.Sessions.Single();
        var ticker = entityManager.System<GameTicker>();
        var ghostRoles = entityManager.System<GhostRoleSystem>();
        var minds = entityManager.System<SharedMindSystem>();

        EntityUid role = default;
        await server.WaitAssertion(() => role = entityManager.SpawnEntity(RaffleGhostRoleProtoId, map.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var identifier = entityManager.GetComponent<GhostRoleComponent>(role).Identifier;
            ghostRoles.Request(player, identifier);
        });
        await pair.RunTicksSync(5);

        Assert.That(entityManager.GetComponent<GhostRoleRaffleComponent>(role).CurrentMembers, Does.Contain(player));

        EntityUid playerBody = default;
        await server.WaitAssertion(() =>
        {
            playerBody = entityManager.SpawnEntity(PlayerBodyProtoId, map.GridCoords);
            var mind = minds.CreateMind(player.UserId, "Raffle Player");
            minds.TransferTo(mind, playerBody);
            ticker.PlayerJoinGame(player, silent: true);
        });
        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(player.AttachedEntity, Is.EqualTo(playerBody));
            Assert.That(entityManager.HasComponent<GhostRoleRaffleComponent>(role), Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
