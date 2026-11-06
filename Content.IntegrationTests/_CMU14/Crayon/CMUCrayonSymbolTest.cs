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

    /// <summary>
    /// Whether the player has a custom symbol picked, on the server and as their client sees it.
    /// </summary>

    // The window and the held crayon's status line, as the player sees them.

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
