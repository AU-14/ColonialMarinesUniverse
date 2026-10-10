using System.Numerics;
using System.Reflection;
using Content.Client.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ColonyEconomy;

/// <summary>
///     Opens the ATM window on a range of screen resolutions and UI scales and checks that
///     it always fits, keeps the art's aspect ratio, and leaves the whole keypad reachable.
/// </summary>
[TestFixture]
public sealed class ColonyAtmWindowSizeTest
{
    // The window shows the art's fascia, 203 x 181 art pixels, under a nav bar of fixed height.
    private const float AspectRatio = 203f / 181f;

    // Physical resolution and UI scale; the window works in the resulting virtual pixels.
    private static readonly (int Width, int Height, float UiScale)[] Screens =
    {
        (3840, 2160, 2f),
        (2560, 1440, 1f),
        (1920, 1080, 1f),
        (1920, 1080, 1.25f),
        (1920, 1080, 1.5f),
        (1600, 900, 1f),
        (1366, 768, 1f),
        (1366, 768, 1.25f),
        (1280, 720, 1f),
        (1280, 720, 1.5f),
        (1280, 1024, 1f),
        (1024, 768, 1f),
        (1024, 600, 1f),
        (800, 600, 1f),
    };

    // Edge-inclusive containment with a little slack for float layout rounding.
    private static bool Inside(UIBox2 outer, UIBox2 inner)
    {
        const float slack = 0.5f;
        return inner.Left >= outer.Left - slack && inner.Top >= outer.Top - slack &&
               inner.Right <= outer.Right + slack && inner.Bottom <= outer.Bottom + slack;
    }

    // Content cannot feed mouse movement through the UI manager, so drags call the handler directly.
    private static readonly MethodInfo MouseMoveMethod =
        typeof(Control).GetMethod("MouseMove", BindingFlags.Instance | BindingFlags.NonPublic)!;

    // The size the player last dragged an ATM to is remembered for the session, so a test that
    // depends on the opening size forgets it first rather than inheriting another test's.
    private static readonly FieldInfo ChosenScale =
        typeof(ColonyAtmWindow).GetField("_chosenScale", BindingFlags.Static | BindingFlags.NonPublic)!;

    private static void ForgetChosenSize()
        => ChosenScale.SetValue(null, null);

    private static Control[] Keypad(ColonyAtmWindow window) =>
        new Control[] { window.Btn1, window.Btn9, window.BtnClear, window.Btn0, window.Btn00, window.BtnCancel, window.BtnEnter };

    private static ScreenCoordinates Pixels(Control control, Vector2 position) =>
        new(position * control.UIScale, control.Window?.Id ?? default);

    private static GUIBoundKeyEventArgs Click(Control control, Vector2 position, BoundKeyState state) =>
        new(EngineKeyFunctions.UIClick, state, Pixels(control, position), default,
            position - control.GlobalPosition, (position - control.GlobalPosition) * control.UIScale);

    private static void MoveMouse(Control control, Vector2 position) =>
        MouseMoveMethod.Invoke(control, new object[]
        {
            new GUIMouseMoveEventArgs(Vector2.Zero, control, position, Pixels(control, position),
                position - control.GlobalPosition, (position - control.GlobalPosition) * control.UIScale),
        });

    [Test]
    public async Task AtmWindowFitsEveryScreen()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        foreach (var (width, height, uiScale) in Screens)
        {
            var screenSize = new Vector2(width, height) / uiScale;
            LayoutContainer screen = default!;
            ColonyAtmWindow window = default!;

            await client.WaitPost(() =>
            {
                // Stand-in for the game window at this resolution.
                screen = new LayoutContainer { SetSize = screenSize };
                uiMan.WindowRoot.AddChild(screen);
                LayoutContainer.SetPosition(screen, Vector2.Zero);

                window = new ColonyAtmWindow();
                screen.AddChild(window);
                LayoutContainer.SetPosition(window, Vector2.Zero);
            });

            await client.WaitRunTicks(10);

            await client.WaitAssertion(() =>
            {
                var name = $"{width}x{height} @ {uiScale}x";
                var screenBox = UIBox2.FromDimensions(Vector2.Zero, screenSize);
                var windowBox = UIBox2.FromDimensions(window.Position, window.Size);
                TestContext.Out.WriteLine($"{name}: screen {screenSize.X:0}x{screenSize.Y:0}, ATM {window.Size.X:0}x{window.Size.Y:0}");

                Assert.Multiple(() =>
                {
                    Assert.That(window.Size.X, Is.LessThanOrEqualTo(screenSize.X + 0.5f), $"{name}: window wider than screen");
                    Assert.That(window.Size.Y, Is.LessThanOrEqualTo(screenSize.Y + 0.5f), $"{name}: window taller than screen");
                    Assert.That(window.Size.X / (window.Size.Y - ColonyAtmWindow.NavHeight), Is.EqualTo(AspectRatio).Within(0.01f), $"{name}: art is stretched");
                    Assert.That(Inside(screenBox, windowBox), $"{name}: window {windowBox} is off screen {screenBox}");

                    // Every key, ENTER included, sits inside the window and on screen.
                    foreach (var key in Keypad(window))
                    {
                        var keyBox = UIBox2.FromDimensions(key.GlobalPosition - screen.GlobalPosition, key.Size);
                        Assert.That(key.Size.X, Is.GreaterThan(0), $"{name}: key has no size");
                        Assert.That(Inside(windowBox, keyBox), $"{name}: key {keyBox} outside window {windowBox}");
                    }
                });

                screen.Orphan();
            });
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtmWindowOpensCentered()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        ColonyAtmWindow window = default!;
        await client.WaitPost(() =>
        {
            window = new ColonyAtmWindow();
            window.OpenCentered();
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var root = uiMan.WindowRoot.Size;
            var center = window.Position + window.Size / 2;
            Assert.That(center.X, Is.EqualTo(root.X / 2).Within(2f), $"Window at {window.Position} is not centred on {root}");
            Assert.That(center.Y, Is.EqualTo(root.Y / 2).Within(2f), $"Window at {window.Position} is not centred on {root}");
            window.Close();
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>A window reopened at a remembered spot that now hangs off the screen is pulled back on.</summary>
    [Test]
    public async Task AtmWindowOpenedOffScreenIsPulledBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        LayoutContainer screen = default!;
        ColonyAtmWindow window = default!;
        var start = new Vector2(1800, 1000);

        await client.WaitPost(() =>
        {
            screen = new LayoutContainer { SetSize = new Vector2(1920, 1080) };
            uiMan.WindowRoot.AddChild(screen);
            LayoutContainer.SetPosition(screen, Vector2.Zero);
        });
        // The game window is already laid out when an ATM opens on it.
        await client.WaitRunTicks(5);

        await client.WaitPost(() =>
        {
            window = new ColonyAtmWindow();
            screen.AddChild(window);
            LayoutContainer.SetPosition(window, start);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var screenBox = UIBox2.FromDimensions(Vector2.Zero, screen.Size);
            var windowBox = UIBox2.FromDimensions(window.Position, window.Size);
            Assert.That(Inside(screenBox, windowBox), $"Window {windowBox} left hanging off screen {screenBox}");
            screen.Orphan();
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AtmWindowFollowsScreenResize()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        LayoutContainer screen = default!;
        ColonyAtmWindow window = default!;
        var bigSize = Vector2.Zero;

        await client.WaitPost(() =>
        {
            ForgetChosenSize();
            screen = new LayoutContainer { SetSize = new Vector2(1920, 1080) };
            uiMan.WindowRoot.AddChild(screen);
            LayoutContainer.SetPosition(screen, Vector2.Zero);

            window = new ColonyAtmWindow();
            screen.AddChild(window);
            // Parked in the bottom-right corner, where a shrinking screen would push it off.
            LayoutContainer.SetPosition(window, new Vector2(1300, 300));
        });
        await client.WaitRunTicks(10);

        // A smaller screen the ATM still fits: it keeps its size but is pulled back on screen.
        await client.WaitPost(() =>
        {
            bigSize = window.Size;
            screen.SetSize = new Vector2(1024, 600);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var screenBox = UIBox2.FromDimensions(Vector2.Zero, screen.SetSize);
            Assert.Multiple(() =>
            {
                Assert.That(window.Size.X, Is.EqualTo(bigSize.X).Within(0.5f), "Window shrank on a screen it fits");
                Assert.That(Inside(screenBox, UIBox2.FromDimensions(window.Position, window.Size)),
                    $"Window at {window.Position} left off the {screen.SetSize} screen");
            });

            // A screen too short for it: it shrinks to fit, and stays on screen.
            screen.SetSize = new Vector2(700, 400);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            var screenBox = UIBox2.FromDimensions(Vector2.Zero, screen.SetSize);
            Assert.Multiple(() =>
            {
                Assert.That(window.Size.Y, Is.LessThan(bigSize.Y), "Window did not shrink with the screen");
                Assert.That(Inside(screenBox, UIBox2.FromDimensions(window.Position, window.Size)),
                    $"Window at {window.Position} left off the {screen.SetSize} screen");
            });

            screen.SetSize = new Vector2(1920, 1080);
        });
        await client.WaitRunTicks(10);

        await client.WaitAssertion(() =>
        {
            Assert.That(window.Size.X, Is.EqualTo(bigSize.X).Within(0.5f), "Window did not grow back to its normal size");
            screen.Orphan();
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Dragging the bottom-right corner scales the whole ATM without stretching the art, stops at
    ///     the smallest usable size, and the next ATM opens at the size the player picked.
    /// </summary>
    [Test]
    public async Task CornerDragResizesWithoutStretching()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        LayoutContainer screen = default!;
        ColonyAtmWindow window = default!;
        var start = Vector2.Zero;
        var corner = Vector2.Zero;
        var pull = 0f;

        await client.WaitPost(ForgetChosenSize);

        async Task<ColonyAtmWindow> Open()
        {
            ColonyAtmWindow opened = default!;
            await client.WaitPost(() =>
            {
                opened = new ColonyAtmWindow();
                screen.AddChild(opened);
                LayoutContainer.SetPosition(opened, new Vector2(100, 100));
            });
            await client.WaitRunTicks(10);
            return opened;
        }

        async Task DragTo(Vector2 position)
        {
            await client.WaitPost(() => MoveMouse(window, position));
            await client.WaitRunTicks(5);
        }

        await client.WaitPost(() =>
        {
            screen = new LayoutContainer { SetSize = new Vector2(1920, 1080) };
            uiMan.WindowRoot.AddChild(screen);
            LayoutContainer.SetPosition(screen, Vector2.Zero);
        });
        await client.WaitRunTicks(5);
        window = await Open();

        await client.WaitPost(() =>
        {
            start = window.Size;
            pull = MathF.Min(150f, (start.X - window.MinSize.X) / 2f);
            // Grab just inside the bottom-right corner, on the resize grip.
            corner = window.GlobalPosition + window.Size - new Vector2(4, 4);
        });
        await client.DoGuiEvent(window, Click(window, corner, BoundKeyState.Down));

        // Pull the corner in, mostly sideways: the height follows the width.
        await DragTo(corner - new Vector2(pull, pull / 7f));
        await client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(pull, Is.GreaterThan(20f), "No room to drag the corner in");
                Assert.That(window.Size.X, Is.EqualTo(start.X - pull).Within(1f), "The window did not follow the corner");
                Assert.That(window.Size.X / (window.Size.Y - ColonyAtmWindow.NavHeight), Is.EqualTo(AspectRatio).Within(0.01f), "Resizing stretched the art");
                // The engine's layout leaves float dust (~1e-5 px) on the position; anything visible is a move.
                Assert.That(window.Position.EqualsApprox(new Vector2(100, 100), 0.01),
                    $"A corner drag moved the window to {window.Position}");
            });
        });

        // Pull it far past the top-left: it stops at the minimum with every key still inside.
        await DragTo(corner - new Vector2(2000, 2000));
        await client.WaitAssertion(() =>
        {
            var windowBox = UIBox2.FromDimensions(window.GlobalPosition, window.Size);
            Assert.Multiple(() =>
            {
                Assert.That(window.Size.X, Is.EqualTo(window.MinSize.X).Within(1f), "The window shrank past its minimum");
                Assert.That(window.Size.X, Is.LessThan(start.X));
                Assert.That(window.Size.X / (window.Size.Y - ColonyAtmWindow.NavHeight), Is.EqualTo(AspectRatio).Within(0.01f), "Resizing stretched the art");
                foreach (var key in Keypad(window))
                    Assert.That(Inside(windowBox, UIBox2.FromDimensions(key.GlobalPosition, key.Size)), "A key fell outside the small window");
            });
        });
        await client.DoGuiEvent(window, Click(window, corner, BoundKeyState.Up));

        // The next ATM opens at the size the player left it at.
        var small = window.Size;
        await client.WaitPost(() => window.Close());
        window = await Open();
        await client.WaitAssertion(() =>
            Assert.That(window.Size.X, Is.EqualTo(small.X).Within(1f), "The chosen size was forgotten"));

        // Dragging back out restores the original size.
        await client.WaitPost(() => corner = window.GlobalPosition + window.Size - new Vector2(4, 4));
        await client.DoGuiEvent(window, Click(window, corner, BoundKeyState.Down));
        await DragTo(corner + start - small);
        await client.DoGuiEvent(window, Click(window, corner + start - small, BoundKeyState.Up));
        await client.WaitAssertion(() =>
        {
            Assert.That(window.Size.X, Is.EqualTo(start.X).Within(1f), "The window did not grow back");
            screen.Orphan();
        });

        await pair.CleanReturnAsync();
    }
}
