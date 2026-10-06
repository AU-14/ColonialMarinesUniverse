using System;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.CMU14.Interface;
using Content.Client.Resources;
using Content.Shared.CCVar;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     Borderless ATM terminal drawn as pixel art. The machine is one base sprite; the keys, the card
///     reader, the cash slot and the status lights are sprites laid over it that change with the
///     machine's state, and the screen is drawn through the CRT shader. It boots when it opens - the
///     tube warming up, the company mark, a power-on self test - and any key hurries it along.
///     Everything is laid out on the art's own pixel grid and scaled as one, so the window shrinks to
///     fit small screens and can be resized from the bottom-right corner.
/// </summary>
/// <remarks>
///     The art comes from <c>Content.CMU/Design/ColonyEconomy/AtmPixelArt/atm_pixel_art.py</c>;
///     the grid rectangles here mirror its <c>LAYOUT</c> table.
/// </remarks>
public sealed partial class ColonyAtmWindow : BaseWindow
{
    // ─── Art (227 x 300 grid, of which the window shows the fascia) ───────────
    private const string ArtDir = "/Textures/CMU14/ColonyEconomy/";

    // The part of the art grid the window shows: the fascia, outline to outline. Every rectangle
    // here is in grid coordinates, as in the generator; Art() moves them into the view.
    private static readonly UIBox2 View = UIBox2.FromDimensions(12, 84, 203, 181);

    // Native pixels per art pixel. Layout works in native pixels, so the sizing below reads the same
    // as it did for the painted art: scale 1 is three screen pixels to an art pixel.
    private const float Px = 3f;
    private const float NativeW = 203 * Px;
    private const float NativeH = 181 * Px;
    private static readonly Vector2 NativeSize = new(NativeW, NativeH);

    // On-screen scale of the art: the default, and the limits the player can resize between.
    // The fonts are tuned for DefaultScale and scale with the art from there.
    private const float DefaultScale = 1f;
    private const float MinScale = 0.5f;
    private const float MaxScale = 1.5f;

    // Never let the ATM take more than this share of the game window.
    private const float MaxScreenFraction = 0.95f;

    // Bottom-right resize handle, in native pixels.
    private const float GripSize = 36f;

    // Size the player last resized the ATM to; null until they do. Reused the next time it opens.
    private static float? _chosenScale;

    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold    = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    // The tube face, in art pixels.
    private static readonly UIBox2 Glass = UIBox2.FromDimensions(31, 102, 107, 84);

    // Phosphor of this machine's tube. Fixed rather than following the player's CRT accent: it is a
    // piece of hardware in the world, not part of the player's interface.
    private static readonly Color Phosphor = Color.FromHex("#46ff77");

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly AtmScreenControl _screen;
    private readonly AtmSprite _glass;
    private readonly CrtScreenControl _crt;
    private readonly NativeLayout _layout;

    private Control? _trackedParent;
    private bool _clampPending;

    // Buttons accessed by the BUI. Beside the digits and 00, the keys are a real ATM's: CLEAR rubs
    // out the last digit, CANCEL abandons what you are doing, ENTER goes ahead.
    public readonly Button Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9;
    public readonly Button Btn00;
    public readonly Button BtnCancel;
    public readonly Button BtnClear;
    public readonly Button BtnEnter;
    public readonly Button BtnScrollUp;
    public readonly Button BtnScrollDown;

    /// <summary>The card reader: a click puts the player's card in, or logs them off.</summary>
    public readonly ReaderButton BtnReader;

    /// <summary>The empty reader was clicked: the player wants their ID card put in.</summary>
    public event Action? InsertCardPressed;

    /// <summary>The player's card was clicked while signed in: log off and hand it back.</summary>
    public event Action? LogOffPressed;

    /// <summary>The bills coming out of the cash slot; shown only while they are out.</summary>
    public readonly CashButton BtnCash = new();

    /// <summary>The bills were clicked on their way out: the player takes the cash in hand.</summary>
    public event Action? TakeCashPressed;

    /// <summary>Text appeared on the screen this frame, a character or a self-test line.</summary>
    public event Action? TextTyped;

    /// <summary>
    ///     The machine's first frame on screen. Until the server confirms a closed UI, every tick it
    ///     receives reopens that UI and disposes it again within one update, so the machine's sounds
    ///     start from here rather than from opening.
    /// </summary>
    public event Action? Woke;

    /// <summary>Whether the machine has had a frame on screen; see <see cref="Woke"/>.</summary>
    public bool Awake { get; private set; }

    private enum BootPhase
    {
        PowerOn,
        Logo,
        Console,
        Done,
    }

    private BootPhase _boot = BootPhase.PowerOn;

    // ─── Nav bar ───────────────────────────────────────────────────────────

    /// <summary>Height of the nav bar across the top, in UI pixels; the machine fills the rest.</summary>
    public const float NavHeight = 26f;

    /// <summary>The player's own card and PIN, once the server has said which card is theirs.</summary>
    public readonly Label NavPin = new();

    private readonly Button _popOutButton = new();
    private readonly TickBox _body;
    private OSWindow? _popOut;

    /// <summary>The machine's screen is gone for good: closed, or the window it popped out to closed.</summary>
    public event Action? OnFinalClose;

    public ColonyAtmWindow()
    {
        IoCManager.InjectDependencies(this);
        Resizable = true;
        // The window itself must catch clicks on empty areas so the frame can be dragged
        // (Control defaults to MouseFilter.Ignore). Keypad buttons still consume their own clicks.
        MouseFilter = MouseFilterMode.Stop;

        var cache = IoCManager.Resolve<IResourceCache>();
        var bodyFont = cache.GetFont(MonoRegular, 13);
        var boldFont = cache.GetFont(MonoBold, 14);

        _layout = new NativeLayout { MouseFilter = MouseFilterMode.Ignore };
        Art(new TextureRect
        {
            Texture = cache.GetTexture(ArtDir + "atm_base.png"),
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        }, View);

        // The screen: the lit tube face with the text over it, drawn a second time through the CRT
        // shader by its sibling. The shader writes opaque pixels, so the face has to be part of what
        // it captures rather than something underneath.
        var screenContent = new Control { MouseFilter = MouseFilterMode.Ignore };
        _glass = new AtmSprite(Rsi(cache, "atm_screen"));
        _screen = new AtmScreenControl(bodyFont, boldFont, Glass.Width * Px * DefaultScale);
        screenContent.AddChild(_glass);
        screenContent.AddChild(_screen);
        _screen.Typed += () => TextTyped?.Invoke();
        Art(screenContent, Glass);

        _crt = new CrtScreenControl
        {
            Source = screenContent,
            Phosphor = Phosphor,
            // A prop terminal: the full tube, roll bar and grain included, at the shared cvars' strength.
            ArtifactPeriod = 5f,
        };
        Art(_crt, Glass);

        // History scroll arrows, drawn on the tube in its bottom-right corner, above the shader pass.
        BtnScrollUp = new ScrollButton(up: true);
        BtnScrollDown = new ScrollButton(up: false);
        Art(BtnScrollUp, UIBox2.FromDimensions(Glass.Right - 22, Glass.Bottom - 12, 9, 9));
        Art(BtnScrollDown, UIBox2.FromDimensions(Glass.Right - 12, Glass.Bottom - 12, 9, 9));

        BtnReader = new ReaderButton();
        BuildHardware(cache);

        var keys = Rsi(cache, "atm_keys");
        Btn1 = Key(keys, "1", 0, 0); Btn2 = Key(keys, "2", 1, 0); Btn3 = Key(keys, "3", 2, 0);
        Btn4 = Key(keys, "4", 0, 1); Btn5 = Key(keys, "5", 1, 1); Btn6 = Key(keys, "6", 2, 1);
        Btn7 = Key(keys, "7", 0, 2); Btn8 = Key(keys, "8", 1, 2); Btn9 = Key(keys, "9", 2, 2);
        BtnClear = Key(keys, "clear", 0, 3); Btn0 = Key(keys, "0", 1, 3); Btn00 = Key(keys, "00", 2, 3);

        var wideKeys = Rsi(cache, "atm_fkeys");
        BtnCancel = HardwareKey(wideKeys, "cancel", CancelRect);
        BtnEnter = HardwareKey(wideKeys, "enter", EnterRect);

        _layout.Add(new ResizeGrip(), NativeW - GripSize, NativeH - GripSize, GripSize, GripSize);

        // The nav bar other windows have, over the machine: what it is, the player's own PIN, and a way
        // out into a window of its own. The bar is the frame's handle; the machine fills the rest.
        _layout.VerticalExpand = true;
        _body = new TickBox { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 0 };
        _body.AddChild(BuildNav());
        _body.AddChild(_layout);
        _body.Ticked += Tick;
        AddChild(_body);
        OnClose += FinalClose;

        MinSize = WithNav(NativeSize * MinScale);
        SetSize = WithNav(NativeSize * (_chosenScale ?? DefaultScale));

        // The machine boots as it wakes: the tube warms up (in step with crt_startup.ogg), the company
        // mark comes up, then a power-on self test before the terminal itself.
        _glass.Play("power_on", () =>
        {
            // A seized machine never gets as far as its own boot: the CLF's mark comes straight up.
            if (_state?.OutOfService == true)
            {
                EnterSeized();
                return;
            }

            SetBoot(BootPhase.Logo);
            _glass.Play("boot_logo", () =>
            {
                _glass.Show("on");
                SetBoot(BootPhase.Console);
                _screen.Boot(BootLines(), () => SetBoot(BootPhase.Done));
            });
        });
    }

    private void SetBoot(BootPhase phase)
    {
        _boot = phase;
        UpdateLights();
    }

    private void EnterSeized()
    {
        _glass.Show("clf");
        _screen.EndBoot();
        SetBoot(BootPhase.Done);
    }

    /// <summary>Jumps straight to the terminal; a keypress during the boot does this.</summary>
    private void SkipBoot()
    {
        if (_boot == BootPhase.Done || _state?.OutOfService == true)
            return;

        // Replacing the tube's animation also drops the callbacks that would carry the boot on.
        _glass.Show("on");
        _screen.EndBoot();
        SetBoot(BootPhase.Done);
    }

    private static (string Text, string? Status)[] BootLines()
    {
        var ok = Loc.GetString("cmu-atm-boot-ok");
        (string, string?) Check(string id) => (Loc.GetString(id), ok);
        return new (string, string?)[]
        {
            (Loc.GetString("cmu-atm-boot-title"), null),
            (Loc.GetString("cmu-atm-boot-bios"), null),
            (string.Empty, null),
            Check("cmu-atm-boot-memory"),
            Check("cmu-atm-boot-keypad"),
            Check("cmu-atm-boot-reader"),
            Check("cmu-atm-boot-dispenser"),
            Check("cmu-atm-boot-uplink"),
            (string.Empty, null),
            (Loc.GetString("cmu-atm-boot-loading"), null),
        };
    }

    private Control BuildNav()
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        row.AddChild(new Label
        {
            Text = Loc.GetString("cmu-atm-nav-title"),
            StyleClasses = { "windowTitle" },
            Margin = new Thickness(6, 0, 0, 0),
            VAlign = Label.VAlignMode.Center,
        });

        NavPin.Text = Loc.GetString("cmu-atm-nav-pin-unknown");
        NavPin.Margin = new Thickness(14, 0, 6, 0);
        NavPin.HorizontalExpand = true;
        NavPin.ClipText = true;
        NavPin.VAlign = Label.VAlignMode.Center;
        row.AddChild(NavPin);

        _popOutButton.Text = Loc.GetString("cmu-atm-nav-pop-out");
        _popOutButton.StyleClasses.Add("OpenBoth");
        _popOutButton.Margin = new Thickness(5, 0);
        _popOutButton.VerticalAlignment = VAlignment.Center;
        _popOutButton.OnPressed += _ => PopOut();
        row.AddChild(_popOutButton);

        var close = new TextureButton
        {
            StyleClasses = { "windowCloseButton" },
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };
        close.OnPressed += _ =>
        {
            if (_popOut != null)
                _popOut.Close();
            else
                Close();
        };
        row.AddChild(close);

        var bar = new PanelContainer { StyleClasses = { "windowHeader" }, MinHeight = NavHeight, SetHeight = NavHeight };
        bar.AddChild(row);
        return bar;
    }

    /// <summary>Shows the player's own card and its PIN, or that they have no card of their own.</summary>
    public void ShowOwnCard(int account, int pin)
    {
        NavPin.Text = pin > 0
            ? Loc.GetString("cmu-atm-nav-pin", ("account", account), ("pin", pin))
            : Loc.GetString("cmu-atm-nav-no-card");
    }

    /// <summary>
    ///     Moves the machine into a window of its own, as the tactical map can. The in-game window
    ///     closes without ending the session, which ends when that window does instead.
    /// </summary>
    private void PopOut()
    {
        if (_popOut != null)
            return;

        OnClose -= FinalClose;
        _popOut = new OSWindow
        {
            Title = Loc.GetString("cmu-atm-nav-title"),
            SetWidth = PixelWidth,
            SetHeight = PixelHeight,
        };

        _body.Orphan();
        Close();
        _popOut.Closed += FinalClose;
        _popOutButton.Visible = false;

        var backdrop = new PanelContainer { PanelOverride = new StyleBoxFlat(Color.FromHex("#25252A")) };
        backdrop.AddChild(_body);
        _popOut.AddChild(backdrop);
        _popOut.Show();
    }

    private void FinalClose()
        => OnFinalClose?.Invoke();

    /// <summary>Closes the popped-out window along with the session, without reporting it back.</summary>
    public void DisposePopOut()
    {
        if (_popOut is not { } window)
            return;

        _popOut = null;
        window.Closed -= FinalClose;
        window.Close();
    }

    // ─── Layout helpers ────────────────────────────────────────────────────

    private static RSI Rsi(IResourceCache cache, string name)
        => cache.GetResource<RSIResource>($"{ArtDir}{name}.rsi").RSI;

    /// <summary>Places <paramref name="control"/> over a rectangle given in art-grid pixels.</summary>
    private void Art(Control control, UIBox2 grid)
        => _layout.Add(control, (grid.Left - View.Left) * Px, (grid.Top - View.Top) * Px, grid.Width * Px, grid.Height * Px);

    // ─── Sizing ────────────────────────────────────────────────────────────

    /// <summary>The window's size for the machine drawn at <paramref name="art"/>: the nav bar on top.</summary>
    private static Vector2 WithNav(Vector2 art)
        => art + new Vector2(0, NavHeight);

    /// <summary>Largest scale at which the whole ATM still fits inside the game window.</summary>
    private float MaxFittingScale()
    {
        if (Parent is not { } parent || parent.Size.X <= 0 || parent.Size.Y <= 0)
            return MaxScale;

        var fit = MathF.Min(parent.Size.X / NativeW, (parent.Size.Y - NavHeight) / NativeH) * MaxScreenFraction;
        return MathF.Min(fit, MaxScale);
    }

    /// <summary>
    ///     The size the ATM opens at. A size the player picked is kept exactly; otherwise the default
    ///     is rounded down to a whole number of screen pixels per art pixel, so the pixel art stays
    ///     crisp instead of some pixels drawing a column wider than their neighbours.
    /// </summary>
    private float PreferredScale()
    {
        if (_chosenScale is { } chosen)
            return chosen;

        var perArtPixel = Px * UIScale;
        var whole = MathF.Floor(DefaultScale * perArtPixel + 0.001f);
        var crisp = whole >= 1f ? MathF.Max(whole / perArtPixel, MinScale) : DefaultScale;

        // Too big for this screen: fill what fits, crisp or not - but never grow past the crisp size.
        return MathF.Min(crisp, MaxFittingScale());
    }

    /// <summary>
    ///     Resizes the window to <paramref name="scale"/>, clamped to the limits and to the screen, and
    ///     keeps it on screen afterwards if it changed size or <paramref name="clamp"/> asks.
    /// </summary>
    private void ApplyScale(float scale, bool clamp = true)
    {
        // On a screen too small for even MinScale, fitting the screen wins so the keypad stays reachable.
        var max = MaxFittingScale();
        scale = MathF.Min(MathF.Max(scale, MinScale), max);
        MinSize = WithNav(NativeSize * MathF.Min(MinScale, max));

        var size = WithNav(NativeSize * scale);
        if (!SetSize.EqualsApprox(size, 0.5))
        {
            SetSize = size;
            clamp = true;
        }

        _clampPending |= clamp;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _trackedParent = Parent;
        if (_trackedParent != null)
            _trackedParent.OnResized += OnParentResized;

        ApplyScale(PreferredScale());
        // Re-measure now so OpenCentered centres the window at its fitted size.
        Measure(Vector2Helpers.Infinity);
    }

    protected override void ExitedTree()
    {
        if (_trackedParent != null)
            _trackedParent.OnResized -= OnParentResized;
        _trackedParent = null;

        base.ExitedTree();
    }

    // Game window resized or UI scale changed: shrink to fit, or grow back towards the preferred size.
    private void OnParentResized()
        => ApplyScale(PreferredScale());

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        var before = SetSize;
        base.MouseMove(args);

        // A resize drag moves the corner freely; snap it back to the art's aspect ratio,
        // following whichever edge the player moved further.
        if (SetSize.EqualsApprox(before))
            return;

        var current = before.X / NativeW;
        var byWidth = SetSize.X / NativeW;
        var byHeight = (SetSize.Y - NavHeight) / NativeH;
        _chosenScale = MathF.Abs(byWidth - current) >= MathF.Abs(byHeight - current) ? byWidth : byHeight;
        ApplyScale(_chosenScale.Value);
    }

    // The machine's own clockwork. It runs off the window's contents, not the window, so it keeps
    // going once the machine has popped out into a window of its own.
    private void Tick(FrameEventArgs args)
    {
        if (!Awake)
        {
            Awake = true;
            Woke?.Invoke();
        }

        // The full tube pass is optional for readability; the screen keeps its own scanlines without it.
        _crt.Visible = _cfg.GetCVar(CCVars.CMUCrtMenuEffect);
        _screen.Scanlines = !_crt.Drawing;

        UpdateHardware(args.DeltaSeconds);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        // Keep the window fully on screen after it opens at a remembered position or the screen shrinks.
        // Frame updates run before layout, so wait until both sizes are real before clamping.
        if (!_clampPending || Parent is not { } parent || !IsArrangeValid || !parent.IsArrangeValid)
            return;

        _clampPending = false;
        var max = Vector2.Max(parent.Size - Size, Vector2.Zero);
        var clamped = Vector2.Clamp(Position, Vector2.Zero, max);
        if (!clamped.EqualsApprox(Position))
        {
            // Margins, not SetPosition: SetPosition moves by the difference from Position, which can
            // lag the margins in the frame the engine has moved the window itself, so the two moves
            // would add up. Set outright, the window lands where it is put.
            LayoutContainer.SetMarginLeft(this, clamped.X);
            LayoutContainer.SetMarginTop(this, clamped.Y);
            LayoutContainer.SetMarginRight(this, clamped.X + Size.X);
            LayoutContainer.SetMarginBottom(this, clamped.Y + Size.Y);
        }
    }

    // The bottom-right corner resizes; the rest of the frame drags. Buttons consume their own clicks first.
    protected override DragMode GetDragModeFor(Vector2 relativeMousePos)
    {
        var grip = GripSize * _layout.Scale;
        if (relativeMousePos.X >= Size.X - grip && relativeMousePos.Y >= Size.Y - grip)
            return DragMode.Bottom | DragMode.Right;

        return DragMode.Move;
    }

    // ─── State ─────────────────────────────────────────────────────────────

    public void UpdateDisplay(ColonyAtmBuiState s)
    {
        if (s.OutOfService)
        {
            ShowSeized(s);
            return;
        }

        var header  = s.AccountNumber > 0 ? $"ACCT #{s.AccountNumber}" : "COLONY ATM";
        var balance = s.Screen >= AtmScreen.MainMenu && s.AccountNumber > 0 ? $"BAL ${s.Balance}" : string.Empty;
        var input   = IsInputScreen(s.Screen);

        _screen.SetState(header, balance, BuildBody(s), BuildBuffer(s), input);

        var history = s.Screen == AtmScreen.History;
        BtnScrollUp.Visible = BtnScrollDown.Visible = history && s.HistoryTotal > s.History.Length;
        BtnScrollUp.Disabled = s.HistoryOffset <= 0;
        BtnScrollDown.Disabled = s.HistoryOffset + s.History.Length >= s.HistoryTotal;

        // A skimmer clamped over the reader interferes with the tube, too.
        _crt.ArtifactAmount = s.Tampered ? 0.6f : 0f;

        UpdateKeys(s);
        UpdateHardware(s);
    }

    /// <summary>The CLF's mark on the tube, the seizure notice under it, and a dead keypad.</summary>
    private void ShowSeized(ColonyAtmBuiState s)
    {
        // The roundel fills the top half of the tube (art rows 107-145 of 102-186). Six blank lines
        // put the notice just under it, centred, with all three lines clear of the tube's bottom edge.
        // No newline after the last line: the cursor waits at its end, as on every other screen.
        var lines = new[] { "cmu-atm-seized-1", "cmu-atm-seized-2", "cmu-atm-seized-3" }
            .Select(id => _screen.Centre(Loc.GetString(id)));
        var notice = new string('\n', 6) + string.Join('\n', lines);
        _screen.SetState(string.Empty, string.Empty, notice, string.Empty, false);

        BtnScrollUp.Visible = BtnScrollDown.Visible = false;
        _crt.ArtifactAmount = 0.6f;
        UpdateKeys(s);
        UpdateHardware(s);
        if (_boot == BootPhase.Done)
            _glass.Show("clf");
    }

    // The keys are labelled OK and X, so the screen names them that way.
    private static string BuildBody(ColonyAtmBuiState s)
    {
        return s.Screen switch
        {
            AtmScreen.Welcome => "COLONY FINANCIAL TERMINAL\nv2.7  UN TREASURY\n\n1) REMOTE DEPOSIT\n\nInsert ID card for account access.",
            AtmScreen.PinEntry => Combine("ENTER PIN:", s.StatusMessage),
            AtmScreen.PinLocked => "** CARD LOCKED **\nToo many incorrect attempts. Please try again later.",
            AtmScreen.MainMenu => $"Welcome, {s.OwnerName}.\n\n1) WITHDRAW\n2) DEPOSIT\n3) TRANSFER\n4) REMOTE DEPOSIT\n5) HISTORY\n6) EXIT",
            AtmScreen.Withdraw => Combine("WITHDRAW\nEnter amount:", s.StatusMessage),
            AtmScreen.WithdrawConfirm => $"{s.StatusMessage}\n\n{Loc.GetString("cmu-atm-hint-confirm")}",
            AtmScreen.Deposit => Combine("DEPOSIT\nEnter amount:", s.StatusMessage),
            AtmScreen.RemoteDeposit => Combine("REMOTE DEPOSIT\nRecipient account #:", s.StatusMessage),
            AtmScreen.RemoteDepositAmount => s.StatusMessage,
            AtmScreen.RemoteDepositConfirm => $"{s.StatusMessage}\n\n{Loc.GetString("cmu-atm-hint-confirm")}",
            AtmScreen.Transfer => Combine("TRANSFER\nRecipient account #:", s.StatusMessage),
            AtmScreen.TransferAmount => s.StatusMessage,
            AtmScreen.TransferConfirm => $"{s.StatusMessage}\n\n{Loc.GetString("cmu-atm-hint-confirm")}",
            AtmScreen.Result => $"{s.StatusMessage}\n\n{Loc.GetString("cmu-atm-hint-continue")}",
            AtmScreen.History => BuildHistory(s),
            _ => string.Empty,
        };
    }

    // One page, newest first, one line each, e.g. "01:42 -$100 WITHDRAWAL".
    private static string BuildHistory(ColonyAtmBuiState s)
    {
        var sb = new StringBuilder("ACCOUNT HISTORY");
        if (s.History.Length == 0)
            sb.Append("\nNo transactions yet.");
        else if (s.HistoryTotal > s.History.Length)
            sb.Append($" {s.HistoryOffset + 1}-{s.HistoryOffset + s.History.Length}/{s.HistoryTotal}");

        foreach (var entry in s.History)
        {
            var time = $"{(int) entry.Time.TotalHours:00}:{entry.Time.Minutes:00}";
            var line = entry.Kind switch
            {
                AtmHistoryKind.Withdrawal => $"-${entry.Amount} WITHDRAWAL",
                AtmHistoryKind.Deposit => $"+${entry.Amount} DEPOSIT",
                AtmHistoryKind.CashDeposit => $"+${entry.Amount} CASH DEPOSIT",
                AtmHistoryKind.TransferOut => $"-${entry.Amount} TO #{entry.OtherAccount}",
                AtmHistoryKind.TransferIn => $"+${entry.Amount} FROM #{entry.OtherAccount}",
                _ => $"{entry.Amount}",
            };
            sb.Append('\n').Append(time).Append(' ').Append(line);
        }

        sb.Append("\nENTER = back");
        return sb.ToString();
    }

    private static string BuildBuffer(ColonyAtmBuiState s)
    {
        return s.Screen switch
        {
            AtmScreen.PinEntry => s.KeypadBuffer,
            AtmScreen.Withdraw or AtmScreen.Deposit
                or AtmScreen.RemoteDepositAmount or AtmScreen.TransferAmount
                    => s.KeypadBuffer.Length > 0 ? $"${s.KeypadBuffer}" : string.Empty,
            AtmScreen.RemoteDeposit or AtmScreen.Transfer
                    => s.KeypadBuffer.Length > 0 ? $"#{s.KeypadBuffer}" : string.Empty,
            _ => string.Empty,
        };
    }

    private static bool IsInputScreen(AtmScreen screen) =>
        screen is AtmScreen.PinEntry or AtmScreen.Withdraw or AtmScreen.Deposit
            or AtmScreen.RemoteDeposit or AtmScreen.RemoteDepositAmount
            or AtmScreen.Transfer or AtmScreen.TransferAmount;

    private static string Combine(string prompt, string status) =>
        string.IsNullOrEmpty(status) ? prompt : $"{prompt}\n{status}";

    // ─── Native-pixel layout ───────────────────────────────────────────────

    /// <summary>
    ///     Places each child at a fixed rectangle in native pixels and scales them all uniformly to
    ///     whatever size the window currently has (letterboxed if the aspect is off).
    /// </summary>
    /// <summary>A box that reports every frame, so the machine keeps time wherever it is shown.</summary>
    private sealed class TickBox : BoxContainer
    {
        public event Action<FrameEventArgs>? Ticked;

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            Ticked?.Invoke(args);
        }
    }

    private sealed class NativeLayout : Control
    {
        private readonly Dictionary<Control, UIBox2> _rects = new();

        /// <summary>Current on-screen pixels per native pixel.</summary>
        public float Scale { get; private set; } = DefaultScale;

        public void Add(Control child, float x, float y, float w, float h)
        {
            AddChild(child);
            _rects[child] = UIBox2.FromDimensions(x, y, w, h);
        }

        private float ScaleFor(Vector2 size)
        {
            var scale = MathF.Min(size.X / NativeW, size.Y / NativeH);
            return float.IsFinite(scale) && scale > 0 ? scale : Scale;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var scale = ScaleFor(availableSize);
            foreach (var child in Children)
            {
                if (_rects.TryGetValue(child, out var rect))
                    child.Measure(rect.Size * scale);
            }

            // The window decides the size; this control just fills it.
            return Vector2.Zero;
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            Scale = ScaleFor(finalSize);
            var offset = (finalSize - NativeSize * Scale) / 2;
            foreach (var child in Children)
            {
                if (_rects.TryGetValue(child, out var rect))
                    child.Arrange(UIBox2.FromDimensions(offset + rect.TopLeft * Scale, rect.Size * Scale));
            }

            return finalSize;
        }
    }

    /// <summary>Faint diagonal ridges marking the bottom-right resize corner.</summary>
    private sealed class ResizeGrip : Control
    {
        private static readonly Color Ridge = new(1f, 1f, 1f, 0.22f);

        public ResizeGrip()
        {
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var size = PixelSize;
            for (var i = 1; i <= 3; i++)
            {
                var inset = size.X * i / 4f;
                handle.DrawLine(new Vector2(inset, size.Y), new Vector2(size.X, inset), Ridge);
            }
        }
    }

    // A green arrow drawn on the CRT; dimmed when there is nothing further to scroll to.
    private sealed class ScrollButton : Button
    {
        private static readonly Color Green = Color.FromHex("#46ff77");
        private static readonly Color Dim = Color.FromHex("#1f9c43").WithAlpha(0.5f);
        private static readonly Color Hover = new(0.27f, 1f, 0.42f, 0.16f);

        private readonly bool _up;

        public ScrollButton(bool up)
        {
            _up = up;
            Visible = false;
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            // The button stylesheet tints by state, which would darken the green; the colours are drawn below.
            ModulateSelfOverride = Color.White;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            var size = PixelSize;
            if (!Disabled && DrawMode is DrawModeEnum.Hover or DrawModeEnum.Pressed)
                handle.DrawRect(PixelSizeBox, Hover);

            var pad = size.X * 0.22f;
            var (top, bottom) = (pad, size.Y - pad);
            var tip = new Vector2(size.X / 2f, _up ? top : bottom);
            var left = new Vector2(pad, _up ? bottom : top);
            var right = new Vector2(size.X - pad, _up ? bottom : top);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { tip, left, right }, Disabled ? Dim : Green);
            handle.DrawRect(new UIBox2(Vector2.Zero, size), Disabled ? Dim : Green, filled: false);
        }
    }

    // ─── CRT screen text (caret, typing, fallback scanlines) ───────────────

    private sealed class AtmScreenControl : Control
    {
        private static readonly Color Green   = Color.FromHex("#46ff77");
        private static readonly Color Balance = Color.FromHex("#2fd160");
        private static readonly Color Rule    = Color.FromHex("#1f9c43");
        // Light bleeding round each glyph, as phosphor does; the CRT pass has no bloom of its own.
        private static readonly Color Bloom   = Green.WithAlpha(0.13f);
        private static readonly Color Scan    = new(0f, 0f, 0f, 0.22f);

        private const float CharInterval = 0.022f;
        private const float CaretBlink   = 0.5f;

        private readonly Font _font;
        private readonly Font _bold;
        private readonly float _widthVirtual;
        private readonly float _advanceVirtual;

        private string _header = string.Empty;
        private string _balance = string.Empty;
        private string _body = string.Empty;   // already word-wrapped
        private string _buffer = string.Empty;
        private bool _input;

        private int _shown;
        private float _charTimer;
        private float _caretTimer;
        private bool _caretOn = true;

        // Blank until the boot hands over to the console, then the self test, then the terminal.
        private bool _blank = true;
        private (string Text, string? Status)[]? _bootLines;
        private float _bootTime;
        private Action? _bootDone;

        // Self-test pacing: a line every BootLineInterval, its status a beat after it, and a pause
        // on the last line before the terminal takes over.
        private const float BootLineInterval = 0.17f;
        private const float BootStatusDelay = 0.12f;
        private const float BootLinger = 0.45f;

        /// <summary>Raised at most once a frame, when characters or a self-test line appear.</summary>
        public event Action? Typed;

        // Self-test lines shown so far, each of which ticks once.
        private int _bootTicks;

        /// <summary>
        ///     Draw the screen's own scanlines. Only while the CRT pass is not running: with it, they
        ///     would beat against the shader's and moiré.
        /// </summary>
        public bool Scanlines { get; set; } = true;

        public AtmScreenControl(Font font, Font bold, float widthVirtual)
        {
            _font = font;
            _bold = bold;
            _widthVirtual = widthVirtual;
            _advanceVirtual = font.GetCharMetrics(new System.Text.Rune('M'), 1f)?.Advance ?? 8f;
            RectClipContent = true;
            MouseFilter = MouseFilterMode.Ignore;
        }

        /// <summary>Runs the power-on self test, then calls <paramref name="done"/> and shows the terminal.</summary>
        public void Boot((string Text, string? Status)[] lines, Action done)
        {
            _blank = false;
            _bootLines = lines;
            _bootTime = 0f;
            _bootTicks = 0;
            _bootDone = done;
        }

        /// <summary>Cuts the boot short and shows the terminal, typing in from the start.</summary>
        public void EndBoot()
        {
            _blank = false;
            _bootLines = null;
            _bootDone = null;
            _shown = 0;
            _charTimer = 0f;
        }

        public void SetState(string header, string balance, string body, string buffer, bool input)
        {
            _header = header;
            _balance = balance;
            _buffer = buffer;
            _input = input;

            var wrapped = Wrap(body);
            if (wrapped != _body)
            {
                _body = wrapped;
                _shown = 0;
                _charTimer = 0f;
            }
        }

        /// <summary>Characters that fit across the tube at the default size; text wraps to this.</summary>
        public int Columns => Math.Max(8, (int) ((_widthVirtual - 16f) / _advanceVirtual));

        /// <summary>
        ///     <paramref name="line"/> padded to sit in the middle of the tube. The padding is
        ///     non-breaking: the word wrap would eat ordinary leading spaces.
        /// </summary>
        public string Centre(string line)
            => new string('\u00a0', Math.Max(0, (Columns - line.Length) / 2)) + line;

        private string Wrap(string text)
        {
            var maxCols = Columns;
            var sb = new StringBuilder();
            var paragraphs = text.Split('\n');
            for (var p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0)
                    sb.Append('\n');

                var col = 0;
                foreach (var word in paragraphs[p].Split(' '))
                {
                    if (col > 0 && col + 1 + word.Length > maxCols)
                    {
                        sb.Append('\n');
                        col = 0;
                    }
                    else if (col > 0)
                    {
                        sb.Append(' ');
                        col++;
                    }

                    if (word.Length > maxCols)
                    {
                        foreach (var ch in word)
                        {
                            if (col >= maxCols)
                            {
                                sb.Append('\n');
                                col = 0;
                            }
                            sb.Append(ch);
                            col++;
                        }
                    }
                    else
                    {
                        sb.Append(word);
                        col += word.Length;
                    }
                }
            }
            return sb.ToString();
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var dt = args.DeltaSeconds;

            _caretTimer += dt;
            if (_caretTimer >= CaretBlink)
            {
                _caretTimer -= CaretBlink;
                _caretOn = !_caretOn;
            }

            if (_blank)
                return;

            if (_bootLines is { } lines)
            {
                _bootTime += dt;
                var shownLines = Math.Min(lines.Length, (int) (_bootTime / BootLineInterval) + 1);
                if (shownLines > _bootTicks)
                {
                    _bootTicks = shownLines;
                    Typed?.Invoke();
                }

                if (_bootTime >= lines.Length * BootLineInterval + BootStatusDelay + BootLinger)
                {
                    var done = _bootDone;
                    EndBoot();
                    done?.Invoke();
                }
                return;
            }

            if (_shown < _body.Length)
            {
                _charTimer += dt;
                var printed = false;
                while (_charTimer >= CharInterval && _shown < _body.Length)
                {
                    _charTimer -= CharInterval;
                    printed |= !char.IsWhiteSpace(_body[_shown]);
                    _shown++;
                }

                // One tick a frame however many characters landed in it, so a slow frame never stacks them.
                if (printed)
                    Typed?.Invoke();
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (_bootLines != null)
                DrawBoot(handle);
            else if (!_blank)
                DrawText(handle);

            if (!Scanlines)
                return;

            var box = PixelSizeBox;
            var scale = UIScale * MathF.Max(0.5f, Size.X / _widthVirtual);
            var step = Math.Max(2f, 3f * scale);
            for (var sy = 0f; sy < box.Bottom; sy += step)
                handle.DrawRect(new UIBox2(0f, sy, box.Right, sy + scale), Scan);
        }

        private void DrawText(DrawingHandleScreen handle)
        {
            // Text is tuned for the default window size and grows or shrinks with the art. The ratio is
            // stepped (rounded down) so a resize drag doesn't build a new font atlas every frame or overflow.
            var ratio = MathF.Floor(Size.X / _widthVirtual * 20f) / 20f;
            var scale = UIScale * MathF.Max(0.5f, ratio);
            var inset = 8f * scale;
            var x = inset;
            var y = inset;

            // Header line + right-aligned balance.
            if (_header.Length > 0)
            {
                Glow(handle, _bold, new Vector2(x, y), _header, scale, Green);
                if (_balance.Length > 0)
                {
                    var bw = TextWidth(_bold, _balance.Length, scale);
                    handle.DrawString(_bold, new Vector2(PixelSize.X - inset - bw, y), _balance, scale, Balance);
                }
                y += _bold.GetLineHeight(scale);
                handle.DrawRect(new UIBox2(x, y, PixelSize.X - inset, y + Math.Max(1f, scale)), Rule);
                y += 5f * scale;
            }

            // Body (typed out) plus the live input echo.
            var clamped = Math.Clamp(_shown, 0, _body.Length);
            var draw = _body[..clamped];
            if (_input && clamped >= _body.Length)
                draw += "\n> " + _buffer;

            Glow(handle, _font, new Vector2(x, y), draw, scale, Green);

            // Block caret drawn at the end of the visible text.
            if (_caretOn)
            {
                var advance = _font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? (int) (_advanceVirtual * scale);
                var lineHeight = _font.GetLineHeight(scale);
                var lastNl = draw.LastIndexOf('\n');
                var lastLine = lastNl < 0 ? draw : draw[(lastNl + 1)..];
                var lineCount = 1;
                foreach (var ch in draw)
                {
                    if (ch == '\n')
                        lineCount++;
                }
                var caretX = x + lastLine.Length * advance;
                var caretY = y + (lineCount - 1) * lineHeight;
                handle.DrawRect(new UIBox2(caretX, caretY + 2f * scale, caretX + advance, caretY + lineHeight), Green);
            }
        }

        /// <summary>The self test: lines arriving one by one, each check's status a beat behind it.</summary>
        private void DrawBoot(DrawingHandleScreen handle)
        {
            var lines = _bootLines!;
            var ratio = MathF.Floor(Size.X / _widthVirtual * 20f) / 20f;
            var scale = UIScale * MathF.Max(0.5f, ratio);
            var inset = 8f * scale;
            var lineHeight = _font.GetLineHeight(scale);
            var y = inset;
            var lastShown = -1;

            for (var i = 0; i < lines.Length; i++)
            {
                var since = _bootTime - i * BootLineInterval;
                if (since < 0f)
                    break;

                lastShown = i;
                var (text, status) = lines[i];
                // Checks are dotted out so their statuses line up in a column at the right.
                var line = status == null ? text : text.PadRight(Columns - 4, '.') + " ";
                Glow(handle, _font, new Vector2(inset, y), line, scale, i < 2 ? Green : Balance);
                if (status != null && since >= BootStatusDelay)
                {
                    var x = inset + TextWidth(_font, line.Length, scale);
                    Glow(handle, _bold, new Vector2(x, y), status, scale, Green);
                }

                y += lineHeight;
            }

            // A caret waiting after the last line, as the self test works.
            if (_caretOn && lastShown >= 0)
            {
                var advance = _font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? 8;
                handle.DrawRect(UIBox2.FromDimensions(inset, y + 2f * scale, advance, lineHeight - 2f * scale), Green);
            }
        }

        private static void Glow(DrawingHandleScreen handle, Font font, Vector2 at, string text, float scale, Color color)
        {
            var d = MathF.Max(1f, scale);
            handle.DrawString(font, at + new Vector2(-d, 0), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(d, 0), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(0, -d), text, scale, Bloom);
            handle.DrawString(font, at + new Vector2(0, d), text, scale, Bloom);
            handle.DrawString(font, at, text, scale, color);
        }

        private static float TextWidth(Font font, int chars, float scale)
        {
            var advance = font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? 7;
            return chars * advance;
        }
    }
}
