using System.Linq;
using Content.Client.CMU14.Yautja.Lobby;
using Content.Server.CMU14.Yautja;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Players.JobWhitelist;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Access.Components;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.TacticalMap;
using Content.Shared.CMU14.Yautja;
using Content.Shared.Inventory;
using Content.Shared.NPC.Components;
using Content.Shared.Preferences;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Content.Shared.Stacks;
using Content.Shared.Storage.Components;
using Content.Shared._RMC14.TacticalMap;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaStrandedTest
{

    [Test]
    public async Task OpenStrandedSlotWithNoHuntReachesTheLobbyAndCanBeLateJoined()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            InLobby = true,
            Dirty = true,
            DummyTicker = false,
        });
        var server = pair.Server;
        var player = pair.Player!;
        var ticker = server.System<GameTicker>();
        var round = server.System<YautjaPredatorRoundSystem>();
        var stranded = server.System<YautjaStrandedSystem>();
        var jobWhitelist = server.ResolveDependency<JobWhitelistManager>();

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(YautjaPredatorRoundCVars.RandomEnabled, false);
            server.CfgMan.SetCVar(YautjaStrandedCVars.Chance, 0f);
        });

        var db = server.ResolveDependency<IServerDbManager>();
        await db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) YautjaWhitelistFlags.Yautja);
        await jobWhitelist.RefreshYautjaWhitelist(player.UserId);

        // player isn't readied, so they're still in the lobby once the round is running
        await server.WaitPost(() => ticker.StartRound());
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(round.HuntActive, Is.False);
            Assert.That(stranded.StrandedSlotOpen, Is.False, "chance is 0, nothing should have rolled");
            Assert.That(stranded.OpenStrandedSlot(), Is.True);
            Assert.That(stranded.StrandedSlotOpen, Is.True);
        });
        await pair.RunTicksSync(10);

        // no hunt, but the Join Hunt card has to show or nobody can get to the slot
        await pair.Client.WaitAssertion(() =>
        {
            var lobby = pair.Client.System<YautjaHuntLobbySystem>();
            Assert.That(lobby.HuntActive, Is.False);
            Assert.That(lobby.StrandedOpen, Is.True);
            Assert.That(lobby.CanSeeHuntCard, Is.True);
        });

        await server.WaitPost(() =>
        {
            var station = server.System<StationSystem>().GetStations().First();
            ticker.MakeJoinGame(player, station, YautjaStrandedSystem.StrandedJob);
        });
        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            Assert.That(player.AttachedEntity, Is.Not.Null);
            var mob = player.AttachedEntity!.Value;
            Assert.That(server.EntMan.HasComponent<YautjaStrandedComponent>(mob), Is.True);
            Assert.That(server.EntMan.GetComponent<TransformComponent>(mob).GridUid, Is.EqualTo(stranded.EnsureHut()),
                "late joiners start in the hut like cmss13");
            Assert.That(stranded.StrandedSlotOpen, Is.False, "the only slot got taken");
        });
        await pair.RunTicksSync(10);

        await pair.Client.WaitAssertion(() =>
            Assert.That(pair.Client.System<YautjaHuntLobbySystem>().StrandedOpen, Is.False));

        await server.WaitPost(() =>
            server.CfgMan.SetCVar(YautjaStrandedCVars.Chance, YautjaStrandedCVars.Chance.DefaultValue));

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task StrandedHutHasRackAndOneWayPadToGroundRelay()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var stranded = entMan.System<YautjaStrandedSystem>();
            var transform = entMan.System<SharedTransformSystem>();

            var hut = stranded.EnsureHut();
            Assert.That(hut, Is.Not.Null);
            Assert.That(stranded.EnsureHut(), Is.EqualTo(hut), "the hut is built once and reused");
            Assert.That(entMan.HasComponent<MapGridComponent>(hut!.Value), Is.True);

            EntityUid? pad = null;
            var hutChildren = entMan.GetComponent<TransformComponent>(hut.Value).ChildEnumerator;
            while (hutChildren.MoveNext(out var child))
            {
                if (entMan.HasComponent<YautjaStrandedTeleporterComponent>(child))
                    pad = child;
            }
            Assert.That(pad, Is.Not.Null);

            var destination = entMan.SpawnEntity("CMUYautjaGroundRelayDestination", map.GridCoords);
            entMan.GetComponent<YautjaRelayDestinationComponent>(destination).Id = "test.ground";

            var hunter = entMan.SpawnEntity("CMUMobYautjaStranded", entMan.GetComponent<TransformComponent>(pad!.Value).Coordinates);
            try
            {
                var ev = new YautjaStrandedTeleportChosenEvent(entMan.GetNetEntity(hunter), entMan.GetNetEntity(destination));
                entMan.EventBus.RaiseLocalEvent(pad.Value, ev);

                Assert.That(transform.GetMapId(hunter), Is.EqualTo(map.MapId), "the pad drops them at the chosen ground relay");
            }
            finally
            {
                entMan.DeleteEntity(hunter);
                entMan.DeleteEntity(destination);
            }
        });

        await pair.CleanReturnAsync();
    }
}
