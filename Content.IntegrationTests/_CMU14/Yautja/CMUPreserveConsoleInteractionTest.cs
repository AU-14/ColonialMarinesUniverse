using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dialog;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using System.Numerics;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class CMUPreserveConsoleInteractionTest : GameTest
{
    [Test]
    public async Task MappedEscapeConsoleOpensDialogAndShuttersForItsUser()
    {
        var map = await Pair.CreateTestMap();
        var previous = ServerSession!.AttachedEntity;
        try
        {
            await Server.WaitAssertion(() =>
            {
                var hunter = SEntMan.SpawnEntity("CMUMobYautja", map.GridCoords);
                var console = SEntMan.SpawnEntity("CMUYautjaHuntingGroundEscapeConsole", map.GridCoords);
                var shutter = SEntMan.SpawnEntity("CMUYautjaHuntingGroundPreserveShutter", map.GridCoords.Offset(new Vector2(2, 0)));
                Server.PlayerMan.SetAttachedEntity(ServerSession, hunter);
                SEntMan.EventBus.RaiseLocalEvent(console, new InteractHandEvent(hunter, console));
                Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(console, DialogUiKey.Key, hunter), Is.True,
                    "the real mapped console must expose its dialog to the connected player");
                SEntMan.EventBus.RaiseLocalEvent(console, new DialogOptionBuiMsg(0) { Actor = hunter, UiKey = DialogUiKey.Key });
                Assert.That(SEntMan.GetComponent<DoorComponent>(shutter).State, Is.Not.EqualTo(DoorState.Closed));
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, previous));
        }
    }
}
