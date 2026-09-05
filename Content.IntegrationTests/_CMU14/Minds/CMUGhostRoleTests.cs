#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Minds;

[TestFixture]
public sealed class CMUGhostRoleTests : GameTest
{
    private const string GhostRoleProtoId = "CMUGhostRoleTestEntity";
    private const string HumanoidGhostRoleProtoId = "CMUHumanoidGhostRoleTestEntity";
    private const string RaffleGhostRoleProtoId = "CMUGhostRoleRaffleTestEntity";
    private const string RequiredGhostRoleProtoId = "CMUGhostRoleRequiredTestEntity";
    private const string EmptyRequirementsGhostRoleProtoId = "CMUGhostRoleEmptyRequirementsTestEntity";
    private const string TestMobProtoId = "CMUGhostRoleTestMob";

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
          parent: MobHumanDummy
          id: {HumanoidGhostRoleProtoId}
          name: Ghost Role Loadout Name
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
          id: {TestMobProtoId}
          components:
          - type: MobState

        - type: entity
          id: {RequiredGhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
            requirements:
            - !type:OverallPlaytimeRequirement
              time: 1000h
          - type: GhostTakeoverAvailable
          - type: MobState

        - type: entity
          id: {EmptyRequirementsGhostRoleProtoId}
          components:
          - type: MindContainer
          - type: GhostRole
            requirements: []
          - type: GhostTakeoverAvailable
          - type: MobState
        """;

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        Connected = true,
        InLobby = true,
    };

    [Test]
    public async Task LobbyPlayerCanJoinGhostRoleRaffle()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = playerMan.Sessions.Single();
        var ticker = entMan.System<GameTicker>();
        var ghostRoleSystem = entMan.System<GhostRoleSystem>();

        Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid raffleRole = default;
        await server.WaitPost(() => raffleRole = entMan.SpawnEntity(RaffleGhostRoleProtoId, mapData.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var raffleRoleId = entMan.GetComponent<GhostRoleComponent>(raffleRole).Identifier;
            ghostRoleSystem.Request(session, raffleRoleId);
        });
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachedEntity, Is.Null);
            Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));
            Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(raffleRole), Is.True);
            Assert.That(entMan.GetComponent<GhostRoleRaffleComponent>(raffleRole).CurrentMembers, Does.Contain(session));
        });
    }

    [Test]
    public async Task LobbyPlayerCanTakeGhostRole()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = playerMan.Sessions.Single();
        var ticker = entMan.System<GameTicker>();
        var ghostRoleSystem = entMan.System<GhostRoleSystem>();

        Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid ghostRole = default;
        await server.WaitPost(() => ghostRole = entMan.SpawnEntity(GhostRoleProtoId, mapData.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ghostRoleId = entMan.GetComponent<GhostRoleComponent>(ghostRole).Identifier;
            Assert.That(ghostRoleSystem.Takeover(session, ghostRoleId), Is.True);
        });
        await pair.RunTicksSync(5);

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(ghostRole));
            Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.JoinedGame));
            Assert.That(entMan.GetComponent<GhostRoleComponent>(ghostRole).Taken, Is.True);
        });
    }

    [Test]
    public async Task GhostRoleRequirementsAreNetworkedAndEnforcedServerSide()
    {
        var mapData = await Pair.CreateTestMap();
        var entMan = Server.ResolveDependency<IEntityManager>();
        var playerMan = Server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var configuration = Server.ResolveDependency<IConfigurationManager>();
        var session = playerMan.Sessions.Single();
        var ghostRoleSystem = entMan.System<GhostRoleSystem>();

        EntityUid requiredGhostRole = default;
        EntityUid emptyRequirementsGhostRole = default;
        await Server.WaitPost(() =>
        {
            configuration.SetCVar(CCVars.GameRoleTimers, true);
            requiredGhostRole = entMan.SpawnEntity(RequiredGhostRoleProtoId, mapData.GridCoords);
            emptyRequirementsGhostRole = entMan.SpawnEntity(EmptyRequirementsGhostRoleProtoId, mapData.GridCoords);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var component = entMan.GetComponent<GhostRoleComponent>(requiredGhostRole);
            var info = ghostRoleSystem.GetGhostRolesInfo(session)
                .Single(role => role.Identifier == component.Identifier);

            Assert.Multiple(() =>
            {
                Assert.That(component.Requirements, Has.Count.EqualTo(1));
                Assert.That(info.Requirements, Is.SameAs(component.Requirements));
                Assert.That(ghostRoleSystem.Takeover(session, component.Identifier), Is.False,
                    "The server must reject a ghost-role request that fails its explicit requirement override.");
            });

            var emptyComponent = entMan.GetComponent<GhostRoleComponent>(emptyRequirementsGhostRole);
            Assert.That(emptyComponent.Requirements, Is.Empty);
            Assert.That(ghostRoleSystem.Takeover(session, emptyComponent.Identifier), Is.True,
                "An explicit empty override must replace role-prototype timers rather than fall back to them.");
        });
    }

    [Test]
    public async Task LobbyHumanoidGhostRoleKeepsRoleEntityName()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = playerMan.Sessions.Single();
        var ghostRoleSystem = entMan.System<GhostRoleSystem>();
        var mindSystem = entMan.System<SharedMindSystem>();

        EntityUid ghostRole = default;
        await server.WaitPost(() => ghostRole = entMan.SpawnEntity(HumanoidGhostRoleProtoId, mapData.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var ghostRoleId = entMan.GetComponent<GhostRoleComponent>(ghostRole).Identifier;
            Assert.That(ghostRoleSystem.Takeover(session, ghostRoleId), Is.True);
        });
        await pair.RunTicksSync(5);

        string? entityName = null;
        string? mindName = null;
        await server.WaitPost(() =>
        {
            entityName = entMan.GetComponent<MetaDataComponent>(ghostRole).EntityName;
            Assert.That(mindSystem.TryGetMind(session.UserId, out _, out var mind), Is.True);
            mindName = mind!.CharacterName;
        });

        Assert.Multiple(() =>
        {
            Assert.That(entityName, Is.EqualTo("Ghost Role Loadout Name"));
            Assert.That(mindName, Is.EqualTo("Ghost Role Loadout Name"));
        });
    }

    [Test]
    public async Task SpawnedPlayerLeavesGhostRoleRaffle()
    {
        var pair = Pair;
        var server = pair.Server;
        var mapData = await pair.CreateTestMap();

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<Robust.Server.Player.IPlayerManager>();
        var session = playerMan.Sessions.Single();
        var ticker = entMan.System<GameTicker>();
        var ghostRoleSystem = entMan.System<GhostRoleSystem>();
        var mindSystem = entMan.System<SharedMindSystem>();

        Assert.That(ticker.PlayerGameStatuses[session.UserId], Is.EqualTo(PlayerGameStatus.NotReadyToPlay));

        EntityUid raffleRole = default;
        await server.WaitPost(() => raffleRole = entMan.SpawnEntity(RaffleGhostRoleProtoId, mapData.GridCoords));
        await pair.RunTicksSync(5);

        await server.WaitPost(() =>
        {
            var raffleRoleId = entMan.GetComponent<GhostRoleComponent>(raffleRole).Identifier;
            ghostRoleSystem.Request(session, raffleRoleId);
        });
        await pair.RunTicksSync(5);

        Assert.That(entMan.GetComponent<GhostRoleRaffleComponent>(raffleRole).CurrentMembers, Does.Contain(session));

        EntityUid playerBody = default;
        await server.WaitPost(() =>
        {
            playerBody = entMan.SpawnEntity(TestMobProtoId, mapData.GridCoords);
            var mind = mindSystem.CreateMind(session.UserId, "Raffle Player");
            mindSystem.TransferTo(mind, playerBody);
            mindSystem.SetUserId(mind, session.UserId);
            ticker.PlayerJoinGame(session, silent: true);
        });
        await pair.RunTicksSync(10);

        Assert.Multiple(() =>
        {
            Assert.That(session.AttachedEntity, Is.EqualTo(playerBody));
            Assert.That(entMan.HasComponent<GhostRoleRaffleComponent>(raffleRole), Is.False);
        });
    }
}
