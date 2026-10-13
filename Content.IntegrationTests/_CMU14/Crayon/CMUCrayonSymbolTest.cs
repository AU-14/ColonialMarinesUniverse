using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Crayon;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Crayon;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Crayon;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Crayon;

[TestFixture]
public sealed class CMUCrayonSymbolTest : InteractionTest
{
    private static readonly Color White = Color.White;
    private static readonly Color Red = Color.Red;

    [Test]
    public async Task InvalidSymbolIsRejected()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Server.WaitAssertion(() =>
        {
            var symbols = Server.System<CMUCrayonSymbolSystem>();
            var bad = new CMUCrayonSymbol { Size = 33, Palette = { White }, Pixels = new byte[33 * 33] };
            Assert.That(symbols.TrySelect(crayon, SPlayer, bad), Is.False);
            Assert.That(SEntMan.HasComponent<CMUCrayonSymbolComponent>(crayon), Is.False);
        });
    }

    [Test]
    public async Task StampsSingleColourSymbolOnTheFloor()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Small, White));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var stamp = Stamps().Single();
            Assert.That(stamp.Symbol.Size, Is.EqualTo(CMUCrayonSymbol.Small));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
        });
    }

    [Test]
    public async Task MissingColourBlocksTheStamp()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Small, White, Red));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps(), Is.Empty);
            Assert.That(Charges(crayon), Is.EqualTo(before));
        });
    }

    [Test]
    public async Task NearbyCrayonSuppliesTheOtherColour()
    {
        var red = ToServer(await Spawn("CrayonRed", PlayerCoords));
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Small, White, Red));
        var redBefore = 0;
        await Server.WaitPost(() => redBefore = Charges(red));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
            Assert.That(Charges(red), Is.EqualTo(redBefore - 1));
        });
    }

    [Test]
    public async Task RainbowCrayonDrawsAllColoursAlone()
    {
        var crayon = ToServer(await PlaceInHands("CrayonRainbow"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Large, White, Red, Color.Blue));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Single().Symbol.Size, Is.EqualTo(CMUCrayonSymbol.Large));
            Assert.That(Charges(crayon), Is.EqualTo(before - 3), "One charge per colour.");
        });
    }

    [Test]
    public async Task PickingAStockSymbolClearsTheCustomOne()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Select(crayon, Symbol(CMUCrayonSymbol.Small, White));

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(crayon, new CrayonSelectMessage("0")));
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUCrayonSymbolComponent>(crayon).Symbol, Is.Null);
            Assert.That(Stamps(), Is.Empty);
        });
    }

    [Test]
    public async Task SpaceCleanerWashesTheStampOff()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Select(crayon, Symbol(CMUCrayonSymbol.Small, White));
        await Interact(null, TargetCoords);

        await Server.WaitPost(() =>
        {
            var tile = SEntMan.System<SharedMapSystem>()
                .GetTileRef(MapData.Grid, MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f)));
            var cleaner = SProtoMan.Index<ReagentPrototype>("SpaceCleaner");
            new CMUCleanCrayonStampsReaction().TileReact(tile, cleaner, FixedPoint2.New(10), SEntMan, null);
        });
        await RunTicks(2);

        await Server.WaitAssertion(() => Assert.That(Stamps(), Is.Empty));
    }

    [Test]
    public async Task SelectingFromTheWindowThenClickingWithAnotherItemKeepsPredictionIntact()
    {
        var crayon = await PlaceInHands("CrayonWhite");
        await RunTicks(10);
        await UseInHand();
        await RunTicks(10);
        await SendBui(CrayonUiKey.Key, new CMUCrayonSelectSymbolMessage(Symbol(CMUCrayonSymbol.Small, White)), crayon);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CMUCrayonSymbolComponent>(ToServer(crayon)).Symbol, Is.Not.Null));

        await SwapHands();
        await PlaceInHands("CrayonBox");
        await PressKey(EngineKeyFunctions.Use, 3, TargetCoords, NetEntity.Invalid);
        await RunTicks(30);
    }

    private static CMUCrayonSymbol Symbol(int size, params Color[] colors)
    {
        var symbol = new CMUCrayonSymbol { Size = size, Pixels = new byte[size * size] };
        symbol.Palette.AddRange(colors);
        for (var i = 0; i < colors.Length; i++)
        {
            symbol.Pixels[i] = (byte) (i + 1);
        }

        return symbol;
    }

    private async Task<int> Select(EntityUid crayon, CMUCrayonSymbol symbol)
    {
        var charges = 0;
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<CMUCrayonSymbolSystem>().TrySelect(crayon, SPlayer, symbol), Is.True);
            charges = Charges(crayon);
        });
        return charges;
    }

    private int Charges(EntityUid crayon) => Server.System<SharedChargesSystem>().GetCurrentCharges(crayon);

    private IEnumerable<CMUCrayonStampComponent> Stamps() => SEntMan.EntityQuery<CMUCrayonStampComponent>();
}
