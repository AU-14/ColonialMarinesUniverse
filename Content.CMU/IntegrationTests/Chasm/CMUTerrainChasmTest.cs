using System.Numerics;
using Content.Client.CMU14.Chasm;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.Chasm;
using Content.Shared.CMU14.Chasm;
using Content.Shared.Movement.Components;
using Robust.Client.Input;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.CMU14.Chasm;

public sealed class CMUTerrainChasmTest : MovementTest
{
    protected override string PlayerPrototype => "CMMobHuman";

    [Test]
    public async Task WalkingPastRimAndFollowingPromptsReturnsPlayerToLedge()
    {
        await SpawnTarget("FloorChasmEntity");
        await Server.WaitPost(() => Transform.SetCoordinates(SPlayer,
            ToServer(TargetCoords).Offset(new Vector2(-0.35f, 0))));
        await RunTicks(5);
        Assert.That(HasComp<CMUChasmLedgeGripComponent>(Player), Is.False);
        Assert.That(HasComp<ChasmFallingComponent>(Player), Is.False);

        await Move(DirectionFlag.East, 0.25f);
        await Client.WaitPost(() => UiMan.ReleaseKeyboardFocus());
        for (var i = 0; i < CMUChasmLedgeGripComponent.RequiredPresses; i++)
        {
            await Client.WaitPost(() =>
            {
                var grip = CEntMan.GetComponent<CMUChasmLedgeGripComponent>(CPlayer);
                Assert.That(grip.Presses, Is.EqualTo(i));
                var key = CMUChasmLedgeInputSystem.GetKey(grip.Key);
                InputManager.KeyDown(new KeyEventArgs(key, false, false, false, false, false, 0));
                InputManager.KeyUp(new KeyEventArgs(key, false, false, false, false, false, 0));
            });
            await RunTicks(6);
        }

        Assert.That(Delta(), Is.GreaterThan(0.5f));
        Assert.That(HasComp<CMUChasmLedgeGripComponent>(Player), Is.False);
        Assert.That(SEntMan.GetComponent<InputMoverComponent>(SPlayer).CanMove, Is.True);
    }
}
