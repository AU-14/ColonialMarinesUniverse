using System.Reflection;
using Content.Client.Viewport;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Ladder;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Interaction;

namespace Content.IntegrationTests.CMU14.Camera;

[TestFixture]
public sealed class CMURemotePeekViewportTest : GameTest
{
    [Test]
    public async Task LadderPeekDoesNotUseThePlayersLocalZLevelView()
    {
        var near = await Pair.CreateTestMap();
        var far = await Pair.CreateTestMap();
        var original = ServerSession!.AttachedEntity;
        EntityUid player = default, ladder = default;
        NetEntity playerNet = default;
        try
        {
            await Server.WaitPost(() =>
            {
                player = SEntMan.SpawnEntity("CMMobHuman", near.GridCoords);
                SEntMan.EnsureComponent<CMUZLevelViewerComponent>(player);
                playerNet = SEntMan.GetNetEntity(player);
                ladder = SEntMan.SpawnEntity(null, near.GridCoords);
                var exit = SEntMan.SpawnEntity(null, far.GridCoords);
#pragma warning disable RA0002 // Pair ladders on different maps for the real alt-use path.
                SEntMan.EnsureComponent<LadderComponent>(ladder).Other = exit;
                SEntMan.EnsureComponent<LadderComponent>(exit).Other = ladder;
#pragma warning restore RA0002
                Server.PlayerMan.SetAttachedEntity(ServerSession, player);
            });
            await Pair.RunTicksSync(5);
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<SharedInteractionSystem>().AltInteract(player, ladder), Is.True);
                Assert.That(SEntMan.GetComponent<EyeComponent>(player).Target, Is.Not.Null);
            });
            await Pair.RunTicksSync(5);
            await Client.WaitAssertion(() =>
            {
                var clientPlayer = CEntMan.GetEntity(playerNet);
                var eye = CEntMan.GetComponent<EyeComponent>(clientPlayer);
                Assert.That(eye.Target, Is.Not.Null, "the ladder viewpoint must replicate to the client");
                using var viewport = new ScalingViewport();
                var select = typeof(ScalingViewport).GetMethod("TryGetZLevelViewEntity", BindingFlags.Instance | BindingFlags.NonPublic)!;
                object[] args = [eye.Eye, default(EntityUid), null, null];
                var selected = (bool) select.Invoke(viewport, args)!;
                Assert.That(selected && (EntityUid) args[1] == clientPlayer, Is.False,
                    "a remote peek must never render the player's original map in place of the camera target");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, original));
        }
    }
}
