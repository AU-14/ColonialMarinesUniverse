using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._RMC14.Roles.FindParasite;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class FindParasiteTest
{
    [Test]
    public async Task OpenWindowUpdatesWhenParasiteBecomesAvailable()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
        });

        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        var mind = entMan.System<MindSystem>();
        var player = server.PlayerMan.Sessions.Single();

        EntityUid ghost = default;
        await server.WaitAssertion(() =>
        {
            ghost = entMan.SpawnEntity(GameTicker.ObserverPrototypeName, map.GridCoords);
            var mindId = mind.CreateMind(player.UserId, "Observer");
            mind.TransferTo(mindId, ghost);
            mind.SetUserId(mindId, player.UserId);

            var ui = entMan.System<SharedUserInterfaceSystem>();
            ui.OpenUi(ghost, XenoFindParasiteUI.Key, ghost);
            Assert.That(ui.IsUiOpen(ghost, XenoFindParasiteUI.Key, ghost), Is.True);
        });

        EntityUid parasite = default;
        await server.WaitAssertion(() =>
            parasite = entMan.SpawnEntity("CMXenoParasite", map.GridCoords.Offset(new Vector2(1, 0))));
        await pair.RunSeconds(2);

        await server.WaitAssertion(() =>
        {
            var ui = entMan.System<SharedUserInterfaceSystem>();
            Assert.That(
                ui.TryGetUiState<FindParasiteUIState>(ghost, XenoFindParasiteUI.Key, out var state),
                Is.True);
            Assert.That(
                state!.ActiveParasiteSpawners.Any(entry => entry.Spawner == entMan.GetNetEntity(parasite)),
                Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
