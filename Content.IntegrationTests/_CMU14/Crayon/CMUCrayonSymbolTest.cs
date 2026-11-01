using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client.CMU14.Crayon;
using Content.Client.Crayon.UI;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server.CMU14.Crayon;
using Content.Shared.Charges.Systems;
using Content.Shared.CMU14.Crayon;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Crayon;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

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
            Assert.That(Server.System<CMUCrayonStencilSystem>().TryGetStencil(SPlayer, out _), Is.False);
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
    public async Task TheColoursAreTheDefaultBoxCrayons()
    {
        await Server.WaitAssertion(() =>
        {
            var crayons = Server.System<CMUCrayonPaletteSystem>().Crayons;
            Assert.That(crayons, Is.Not.Empty);
            Assert.That(crayons.Select(c => c.Color.ToHex()).Distinct().Count(), Is.EqualTo(crayons.Count),
                "One crayon per colour, however many the box holds of it.");
            Assert.That(crayons.Select(c => c.Name), Does.Not.Contain("crayon"), "Each colour has its crayon's name.");

            var factory = Server.ResolveDependency<IComponentFactory>();
            foreach (var crayon in crayons)
            {
                Assert.That(SProtoMan.Index<EntityPrototype>(crayon.Id).TryGetComponent(out CrayonComponent? comp, factory), Is.True, crayon.Name);
                Assert.That(comp!.SelectableColor, Is.False, $"{crayon.Name}: the rainbow crayon draws any colour, it isn't one.");
            }

            foreach (var crayon in crayons)
            {
                var shades = CMUCrayonPaletteSystem.Shades(crayon.Color);
                Assert.That(shades.Select(s => s.Color.ToHex()).Distinct().Count(), Is.EqualTo(shades.Count), crayon.Name);
                Assert.That(shades.Any(s => s.Tone == 0), Is.True, crayon.Name);
            }

            Assert.That(CMUCrayonPaletteSystem.Shades(Red).Count, Is.EqualTo(2 * CMUCrayonPaletteSystem.Tones + 1),
                "Red runs from light to dark.");
        });
    }

    [Test]
    public async Task AGradientOfOneCrayonCostsOneCharge()
    {
        var crayon = ToServer(await PlaceInHands("CrayonRed"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Small,
            CMUCrayonPaletteSystem.Shade(Red, 4), CMUCrayonPaletteSystem.Shade(Red, 2), Red,
            CMUCrayonPaletteSystem.Shade(Red, -2), CMUCrayonPaletteSystem.Shade(Red, -4)));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1));
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
        });
    }

    [Test]
    public async Task AnotherRedCrayonDrawsTheBoxRed()
    {
        var crayon = ToServer(await PlaceInHands("RMCCrayonRed"));
        var before = await Select(crayon, Symbol(CMUCrayonSymbol.Small, Red, CMUCrayonPaletteSystem.Shade(Red, -3)));

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1), "Any red crayon draws the box red's tones.");
            Assert.That(Charges(crayon), Is.EqualTo(before - 1));
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
    public async Task EveryCrayonStampsTheStencil()
    {
        await Server.WaitAssertion(() =>
        {
            foreach (var proto in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract || !proto.HasComp<CrayonComponent>(Factory))
                    continue;

                Assert.That(proto.HasComp<CMUCrayonSymbolComponent>(Factory), Is.True,
                    $"{proto.ID} would draw its stock decal while a custom symbol is picked.");
            }
        });
    }

    [Test]
    public async Task PickingAStockDecalClearsTheStencil()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CMUCrayonSelectSymbolMessage(Symbol(CMUCrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

        var before = 0;
        await Server.WaitPost(() => before = Charges(ToServer(crayon)));
        await SendBui(CrayonUiKey.Key, new CrayonSelectMessage("0"), crayon);
        await AssertStencil(false);

        // Decals go on a grid, so click with grid coordinates.
        await Interact(null, MapData.GridCoords.Offset(new Vector2(1.5f, 0.5f)));

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CrayonComponent>(ToServer(crayon)).SelectedState, Is.EqualTo("0"));
            Assert.That(Stamps(), Is.Empty);
            Assert.That(Charges(ToServer(crayon)), Is.EqualTo(before - 1), "The stock decal is drawn instead.");
        });
    }

    [Test]
    public async Task TypingADecalInTheSearchClearsTheStencil()
    {
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CMUCrayonSelectSymbolMessage(Symbol(CMUCrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

        var window = GetWindow<CrayonWindow>();
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Not.Null, "The window shows the symbol the server holds.");
            Assert.That(StatusState(crayon), Is.EqualTo(CLoc("cmu-crayon-status-custom")), "The crayon says it stamps the symbol.");
        });

        // Every digit is a stock decal, so the search picks one as soon as it is typed.
        var search = GetControlFromField<LineEdit>("Search", window);
        await Client.WaitPost(() => search.SetText("0", invokeEvent: true));
        await RunTicks(15);

        await AssertStencil(false);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<CrayonComponent>(ToServer(crayon)).SelectedState, Is.EqualTo("0")));
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Null, "The window no longer shows the symbol.");
            Assert.That(StatusState(crayon), Is.EqualTo("0"), "The crayon says it draws the searched decal.");
        });
    }

    [Test]
    public async Task DeletingTheDrawnSymbolGoesBackToTheStockDecal()
    {
        var crayon = await OpenWindow("CrayonWhite");
        var window = GetWindow<CrayonWindow>();
        var symbol = Symbol(CMUCrayonSymbol.Small, White);
        const string name = "cmu delete test";
        await Client.WaitPost(() =>
        {
            var storage = (CMUCrayonSymbolStorage) typeof(CrayonWindow)
                .GetField("_cmuStorage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            storage.Save(name, symbol);
            typeof(CrayonWindow).GetMethod("CMUSelect", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(window, new object[] { name, symbol });
        });
        await RunTicks(15);
        await AssertStencil(true);

        Button? delete = null;
        await Client.WaitPost(() => delete = FindButton(window, CLoc("cmu-crayon-custom-delete")));
        Assert.That(delete, Is.Not.Null);
        Assert.That(delete!.Disabled, Is.False, "The drawn symbol is selected, so it can be deleted.");
        await ClickControl(delete);
        await RunTicks(15);

        await AssertStencil(false);
        await Client.WaitAssertion(() =>
        {
            Assert.That(WindowStencil(window), Is.Null);
            Assert.That(StatusState(crayon), Is.Not.EqualTo(CLoc("cmu-crayon-status-custom")), "The crayon draws its stock decal again.");
        });
    }

    [Test]
    public async Task TheStencilFollowsThePlayerToAnotherCrayon()
    {
        var first = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CMUCrayonSelectSymbolMessage(Symbol(CMUCrayonSymbol.Small, White)), first);

        await SwapHands();
        var second = ToServer(await PlaceInHands("CrayonWhite"));
        int firstBefore = 0, secondBefore = 0;
        await Server.WaitPost(() =>
        {
            firstBefore = Charges(ToServer(first));
            secondBefore = Charges(second);
        });

        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Stamps().Count(), Is.EqualTo(1), "A crayon never opened still stamps the picked symbol.");
            Assert.That(Charges(second), Is.EqualTo(secondBefore - 1), "The held crayon draws it.");
            Assert.That(Charges(ToServer(first)), Is.EqualTo(firstBefore));
        });
        await AssertStencil(true);
    }

    [Test]
    public async Task TheStencilOutlivesAUsedUpCrayon()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        await Select(crayon, Symbol(CMUCrayonSymbol.Small, White));
        await Server.WaitPost(() => Server.System<SharedChargesSystem>().SetCharges(crayon, 1));

        await Interact(null, TargetCoords);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(crayon), Is.True, "The last charge uses the crayon up.");
            Assert.That(Stamps().Count(), Is.EqualTo(1));
        });

        await PlaceInHands("CrayonWhite");
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() => Assert.That(Stamps().Count(), Is.EqualTo(2), "The next crayon stamps it too."));
        await AssertStencil(true);
    }

    [Test]
    public async Task DrawingElsewhereGetsTheStencilImage()
    {
        var crayon = ToServer(await PlaceInHands("CrayonWhite"));
        var symbol = Symbol(CMUCrayonSymbol.Small, White);
        await Server.WaitAssertion(() =>
        {
            var plain = new CMUCrayonDrawImageEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(crayon, ref plain);
            Assert.That(plain.Pixels, Is.Null, "Without a stencil the decal draws.");
        });

        var before = await Select(crayon, symbol);
        await Server.WaitAssertion(() =>
        {
            var image = new CMUCrayonDrawImageEvent(SPlayer);
            SEntMan.EventBus.RaiseLocalEvent(crayon, ref image);
            Assert.That(image.Cancelled, Is.False);
            Assert.That(image.Size, Is.EqualTo(symbol.Size));
            Assert.That(image.Pixels, Is.EqualTo(symbol.Pixels));
            Assert.That(image.Palette, Is.EqualTo(symbol.Palette));
            Assert.That(Server.System<SharedChargesSystem>().GetCurrentCharges(crayon), Is.EqualTo(before - 1), "It costs what a stamp costs.");
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
        var crayon = await OpenWindow("CrayonWhite");
        await SendBui(CrayonUiKey.Key, new CMUCrayonSelectSymbolMessage(Symbol(CMUCrayonSymbol.Small, White)), crayon);
        await AssertStencil(true);

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

    private async Task<NetEntity> OpenWindow(string crayon)
    {
        var held = await PlaceInHands(crayon);
        await RunTicks(10);
        await UseInHand();
        await RunTicks(10);
        return held;
    }

    /// <summary>
    /// Whether the player has a custom symbol picked, on the server and as their client sees it.
    /// </summary>
    private async Task AssertStencil(bool set)
    {
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<CMUCrayonStencilSystem>().TryGetStencil(SPlayer, out _), Is.EqualTo(set)));
        await Client.WaitAssertion(() =>
            Assert.That(Client.System<CMUCrayonStencilSystem>().TryGetStencil(CPlayer, out _), Is.EqualTo(set)));
    }

    // The window and the held crayon's status line, as the player sees them.
    private static object? WindowStencil(CrayonWindow window)
    {
        return typeof(CrayonWindow).GetField("_cmuStencil", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window);
    }

    private string? StatusState(NetEntity crayon)
    {
        var uid = CEntMan.GetEntity(crayon);
        var state = typeof(Content.Client.Crayon.CrayonSystem).GetMethod("CMUState", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (string?) state.Invoke(null, new object[] { new Entity<CrayonComponent>(uid, CEntMan.GetComponent<CrayonComponent>(uid)) });
    }

    private static Button? FindButton(Robust.Client.UserInterface.Control parent, string text)
    {
        foreach (var child in parent.Children)
        {
            if (child is Button button && button.Text == text)
                return button;

            if (FindButton(child, text) is { } found)
                return found;
        }

        return null;
    }

    private string CLoc(string id) => Client.ResolveDependency<ILocalizationManager>().GetString(id);

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
