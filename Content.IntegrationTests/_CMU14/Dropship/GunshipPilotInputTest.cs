using Content.Client.CMU14.Dropship.TacticalLand;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Input;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class GunshipPilotInputTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman";

    [TestCase(MobState.Critical)]
    [TestCase(MobState.Dead)]
    public async Task IncapacitatedPilotLosesControlAndDriftsUntilRecovery(MobState state)
    {
        await SpawnTarget("Chair", coords: PlayerCoords);
        var seat = STarget!.Value;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(MapData.Grid);
            SEntMan.AddComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, seat), Is.True);
            SEntMan.AddComponent<DropshipTacticalHoverComponent>(MapData.Grid);
        });
        await RunTicks(10);
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Down);
        await RunTicks(5);
        await Server.WaitAssertion(() => Server.System<MobStateSystem>().ChangeMobState(SPlayer, state));
        await RunTicks(5);
        var initialDirection = GunshipControlInput.None;
        await Server.WaitAssertion(() =>
        {
            var controls = SEntMan.GetComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(controls.HeldInputs, Is.EqualTo(GunshipControlInput.None));
            Assert.That(controls.UncontrolledInputs, Is.Not.EqualTo(GunshipControlInput.None));
            initialDirection = controls.UncontrolledInputs;
        });
        await SetKey(CMUKeyFunctions.CMUGunshipRight, BoundKeyState.Down);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var controls = SEntMan.GetComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(controls.HeldInputs, Is.EqualTo(GunshipControlInput.None));
            controls.NextUncontrolledDirection = TimeSpan.Zero;
        });
        await RunTicks(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<GunshipPilotSeatComponent>(seat).UncontrolledInputs,
                Is.Not.EqualTo(initialDirection));
            Server.System<MobStateSystem>().ChangeMobState(SPlayer, MobState.Alive);
        });
        await RunTicks(5);
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Up);
        await SetKey(CMUKeyFunctions.CMUGunshipRight, BoundKeyState.Up);
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Down);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var controls = SEntMan.GetComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(controls.UncontrolledInputs, Is.EqualTo(GunshipControlInput.None));
            Assert.That(controls.HeldInputs, Is.EqualTo(GunshipControlInput.Forward));
        });
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Up);
    }

    [Test]
    public async Task SeatedPilotCanSendMovementAndThrustWithoutVisor()
    {
        await SpawnTarget("Chair", coords: PlayerCoords);
        var seat = STarget!.Value;
        await Server.WaitAssertion(() =>
        {
            SEntMan.EnsureComponent<DropshipComponent>(MapData.Grid);
            SEntMan.AddComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(Server.System<SharedBuckleSystem>().TryBuckle(SPlayer, SPlayer, seat), Is.True);
            SEntMan.AddComponent<DropshipTacticalHoverComponent>(MapData.Grid);
        });

        await RunTicks(10);
        await Client.WaitAssertion(() =>
            Assert.That(CEntMan.HasComponent<GunshipPilotHudComponent>(CPlayer), Is.False));

        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Down);
        await SetKey(CMUKeyFunctions.CMUGunshipRotateLeft, BoundKeyState.Down);
        await RunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<GunshipPilotSeatComponent>(seat).HeldInputs,
                Is.EqualTo(GunshipControlInput.Forward | GunshipControlInput.RotateLeft)));

        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Up);
        await SetKey(CMUKeyFunctions.CMUGunshipRotateLeft, BoundKeyState.Up);
        await Client.WaitAssertion(() =>
            Assert.That(Client.System<GunshipPilotInputSystem>().TryAdjustThrustFromMouseWheel(-1), Is.True));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var controls = SEntMan.GetComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(controls.HeldInputs, Is.EqualTo(GunshipControlInput.None));
            Assert.That(controls.ThrustPercent, Is.EqualTo(95f));
            SEntMan.RemoveComponent<DropshipTacticalHoverComponent>(MapData.Grid);
        });

        // The server must still reject flight commands outside tactical hover.
        await RunTicks(5);
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Down);
        await Client.WaitAssertion(() =>
            Client.System<GunshipPilotInputSystem>().TryAdjustThrustFromMouseWheel(-1));
        await RunTicks(5);
        await Server.WaitAssertion(() =>
        {
            var controls = SEntMan.GetComponent<GunshipPilotSeatComponent>(seat);
            Assert.That(controls.HeldInputs, Is.EqualTo(GunshipControlInput.None));
            Assert.That(controls.ThrustPercent, Is.EqualTo(95f));
        });
        await SetKey(CMUKeyFunctions.CMUGunshipForward, BoundKeyState.Up);
    }
}
