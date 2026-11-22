using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.CMU14.Pool;
using Content.Shared.Damage;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Standing;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Pool;

[TestFixture]
public sealed class CMUPoolSimulationTest
{
    [Test]
    public void ScratchRequiresAFreeSpotForTheCueBall()
    {
        var game = new CMUPoolSimulation();
        var clearSpot = game.CueBall.Position;
        game.CueBall.Position = new Vector2(CMUPoolSimulation.Width / 2f, CMUPoolSimulation.Height / 3f);
        game.Shoot(-Vector2.UnitY, 1f);

        for (var step = 0; step < 120 && game.Rolling; step++)
            game.Update(0.05f);

        Assert.That(game.Rolling, Is.False);
        Assert.That(game.CueBall.Pocketed, Is.True);
        Assert.That(game.TryPlaceCueBall(game.Balls[1].Position), Is.False);
        Assert.That(game.CueBall.Pocketed, Is.True);

        Assert.That(game.TryPlaceCueBall(clearSpot), Is.True);
        Assert.That(game.CueBall.Pocketed, Is.False);
        Assert.That(game.CueBall.Position, Is.EqualTo(clearSpot));
    }
}

public sealed class CMUPoolInteractionTest : InteractionTest
{
    private const string Table = "CMUPoolTable";
    private const string Stick = "CMUPoolStick";
    private const string BrokenStick = "CMUPoolStickBroken";
    private const string Kit = "CMUPoolTableParts";

    public override async Task DoSetup()
    {
        await base.DoSetup();
        await Server.WaitPost(() => HandSys.AddHand(SPlayer, "hand_left", HandLocation.Left));
        await RunTicks(1);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TableBecomesAvailableWhenTheFirstPlayerLosesAccess(bool walkAway)
    {
        var table = ToServer(await SpawnTarget(Table));
        var firstStick = ToServer(await PlaceInHands(Stick));

        var second = EntityUid.Invalid;
        var activation = Server.System<ActivatableUISystem>();
        var ui = Server.System<SharedUserInterfaceSystem>();
        await Server.WaitAssertion(() =>
        {
            var position = SEntMan.GetCoordinates(PlayerCoords);
            second = SEntMan.SpawnEntity(PlayerPrototype, position);
            HandSys.AddHand(second, "hand_left", HandLocation.Left);
            var stick = SEntMan.SpawnEntity(Stick, position);
            Assert.That(HandSys.TryPickup(second, stick, animate: false), Is.True);

            Assert.That(activation.InteractUI(SPlayer, table), Is.True);
            Assert.That(ui.IsUiOpen(table, CMUPoolUiKey.FreePlay, SPlayer), Is.True);

            activation.InteractUI(second, table);
            Assert.That(ui.IsUiOpen(table, CMUPoolUiKey.FreePlay, SPlayer), Is.True);
            Assert.That(ui.IsUiOpen(table, CMUPoolUiKey.FreePlay, second), Is.False);

            if (walkAway)
                Transform.SetCoordinates(SPlayer, position.Offset(new Vector2(-12, 0)));
            else
                Assert.That(HandSys.TryDrop(SPlayer, firstStick, checkActionBlocker: false), Is.True);
        });

        await RunTicks(10);
        await Server.WaitAssertion(() =>
        {
            Assert.That(ui.IsUiOpen(table, CMUPoolUiKey.FreePlay, SPlayer), Is.False);
            Assert.That(activation.InteractUI(second, table), Is.True);
            Assert.That(ui.IsUiOpen(table, CMUPoolUiKey.FreePlay, second), Is.True);
        });
    }

    [Test]
    public async Task HeldStickLocksTheActiveHandUntilDropped()
    {
        var cue = await Spawn(Stick, PlayerCoords);
        var serverCue = ToServer(cue);
        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.GetActiveHand(SPlayer), Is.EqualTo("hand_right"));
            Assert.That(HandSys.TryPickup(SPlayer, serverCue, "hand_left", animate: false), Is.True);
            Assert.That(HandSys.GetActiveItem(SPlayer), Is.EqualTo(serverCue));
        });

        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var hands = Client.System<SharedHandsSystem>();
            hands.SwapHands(CPlayer);
            Assert.That(hands.GetActiveItem(CPlayer), Is.EqualTo(ToClient(cue)));
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.TrySetActiveHand(SPlayer, "hand_right"), Is.False);
            HandSys.SwapHands(SPlayer, reverse: true);
            Assert.That(SMelee.TryGetWeapon(SPlayer, out var weapon, out _), Is.True);
            Assert.That(weapon, Is.EqualTo(serverCue));

            Assert.That(HandSys.TryDrop(SPlayer, serverCue, checkActionBlocker: false), Is.True);
            Assert.That(HandSys.TrySetActiveHand(SPlayer, "hand_right"), Is.True);
        });
    }

    [Test]
    public async Task AHitBreaksTheStickAndTheBrokenStickCannotOpenTheTable()
    {
        var table = ToServer(await SpawnTarget(Table));
        var original = await PlaceInHands(Stick);
        var stick = ToServer(original);

        await Server.WaitPost(() =>
            SEntMan.EventBus.RaiseLocalEvent(stick, new MeleeHitEvent([], SPlayer, stick, new DamageSpecifier(), null)));
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(HandSys.GetActiveItem((SPlayer, Hands)), Is.EqualTo(stick));
            SEntMan.EventBus.RaiseLocalEvent(stick, new MeleeHitEvent([table], SPlayer, stick, new DamageSpecifier(), null));
        });
        await RunTicks(5);

        AssertDeleted(original);
        await Server.WaitAssertion(() =>
        {
            var held = HandSys.GetActiveItem((SPlayer, Hands));
            Assert.That(held, Is.Not.Null);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(held!.Value).EntityPrototype?.ID, Is.EqualTo(BrokenStick));

            Assert.That(Server.System<ActivatableUISystem>().InteractUI(SPlayer, table), Is.False);
            Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(table, CMUPoolUiKey.FreePlay, SPlayer), Is.False);
        });
    }

    [Test]
    public async Task BlockedOuterTileRefundsTheOriginalKit()
    {
        var center = SEntMan.GetCoordinates(TargetCoords);
        var left = SEntMan.GetNetCoordinates(center.Offset(new Vector2(-1, 0)));
        var right = SEntMan.GetNetCoordinates(center.Offset(new Vector2(1, 0)));
        await SetTile(Plating, left, MapData.Grid);
        await SetTile(Plating, right, MapData.Grid);
        await RunTicks(5);

        var kit = ToServer(await PlaceInHands(Kit));
        await StartConstruction(Table);
        await Interact(awaitDoAfters: false);
        Assert.That(ActiveDoAfters.Count(), Is.EqualTo(1));

        await Spawn("WallSolid", right);
        await AwaitDoAfters();
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityQuery<CMUPoolTableComponent>().Any(), Is.False);
            var kits = SEntMan.EntityQuery<MetaDataComponent>()
                .Where(meta => meta.EntityPrototype?.ID == Kit)
                .Select(meta => meta.Owner);
            Assert.That(kits, Is.EqualTo(new[] { kit }));
            Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(kit), Is.False);
            Assert.That(SEntMan.GetComponent<TransformComponent>(kit).MapID, Is.EqualTo(MapId));
        });
    }

    [TestCase(1, true)]
    [TestCase(2, false)]
    public async Task AnUnprotectedTargetCanFallOrStayStandingDependingOnTheRoll(int seed, bool falls)
    {
        var target = ToServer(await Spawn("CMMobHuman"));
        var stick = ToServer(await PlaceInHands(Stick));

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.False);
            Server.ResolveDependency<IRobustRandom>().SetSeed(seed);
            SEntMan.EventBus.RaiseLocalEvent(stick, new MeleeHitEvent([target], SPlayer, stick, new DamageSpecifier(), null));
        });

        await RunTicks(5);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<StandingStateSystem>().IsDown(target), Is.EqualTo(falls)));
    }
}

