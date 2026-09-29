using System;
using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     Borderless ATM terminal. The Weyland-Yutani ATM sprite is the entire window background;
///     transparent hotspots sit over the painted keypad, and the green CRT text is drawn into
///     the painted screen. Everything is laid out in the sprite's native pixels and scaled as one,
///     so the window shrinks to fit small screens and can be resized from the bottom-right corner.
/// </summary>
public sealed class ColonyAtmWindow : BaseWindow
{
    // ─── Background art (679 x 899) ────────────────────────────────────────
    private const string BgPath        = "/Textures/CMU14/ColonyEconomy/atm_ui.png";
    private const string BgSkimmerPath = "/Textures/CMU14/ColonyEconomy/atm_ui_skimmer.png";
    private const float NativeW = 679f;
    private const float NativeH = 899f;
    private static readonly Vector2 NativeSize = new(NativeW, NativeH);

    // On-screen scale of the sprite: the default, and the limits the player can resize between.
    // The fonts are tuned for DefaultScale and scale with the art from there.
    private const float DefaultScale = 0.8f;
    private const float MinScale = 0.5f;
    private const float MaxScale = 1.5f;

    // Never let the ATM take more than this share of the game window.
    private const float MaxScreenFraction = 0.95f;

    // Bottom-right resize handle, in native sprite pixels.
    private const float GripSize = 36f;

    // Size the player last resized the ATM to; reused the next time it opens.
    private static float _preferredScale = DefaultScale;

    private const string MonoRegular = "/Fonts/RobotoMono/RobotoMono-Regular.ttf";
    private const string MonoBold    = "/Fonts/RobotoMono/RobotoMono-Bold.ttf";

    // Screen text rectangle, in native sprite pixels.
    private const float ScreenX = 77f, ScreenY = 303f, ScreenW = 337f, ScreenH = 257f;

    private readonly Texture _bg1;
    private readonly Texture _bg2;
    private readonly TextureRect _bg;
    private readonly AtmScreenControl _screen;
    private readonly NativeLayout _layout;

    private Control? _trackedParent;
    private bool _clampPending;

    // Buttons accessed by the BUI.
    public readonly Button Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9;
    public readonly Button BtnEnter;
    public readonly Button BtnOk;
    public readonly Button BtnDel;
    public readonly Button BtnScrollUp;
    public readonly Button BtnScrollDown;

    public ColonyAtmWindow()
    {
        Resizable = true;
        // The window itself must catch clicks on empty areas so the frame can be dragged
        // (Control defaults to MouseFilter.Ignore). Keypad buttons still consume their own clicks.
        MouseFilter = MouseFilterMode.Stop;

        var cache = IoCManager.Resolve<IResourceCache>();
        _bg1 = cache.GetTexture(BgPath);
        _bg2 = cache.GetTexture(BgSkimmerPath);

        var bodyFont  = cache.GetFont(MonoRegular, 11);
        var boldFont  = cache.GetFont(MonoBold, 12);

        _layout = new NativeLayout { MouseFilter = MouseFilterMode.Ignore };

        _bg = new TextureRect
        {
            Texture = _bg1,
            Stretch = TextureRect.StretchMode.Scale,
            MouseFilter = MouseFilterMode.Ignore,
        };
        _layout.Add(_bg, 0, 0, NativeW, NativeH);

        _screen = new AtmScreenControl(bodyFont, boldFont, ScreenW * DefaultScale);
        _layout.Add(_screen, ScreenX, ScreenY, ScreenW, ScreenH);

        // Keypad hotspots over the painted keys (centres measured from layout.png).
        Btn1 = Key(462, 468); Btn2 = Key(510, 468); Btn3 = Key(558, 468);
        Btn4 = Key(462, 522); Btn5 = Key(510, 522); Btn6 = Key(558, 522);
        Btn7 = Key(462, 575); Btn8 = Key(510, 575); Btn9 = Key(558, 575);
        BtnDel = Key(462, 627); Btn0 = Key(510, 627); BtnOk = Key(558, 627);
        BtnEnter = KeyRect(451, 678, 120, 55);

        // History scroll arrows, drawn on the CRT in its bottom-right corner.
        BtnScrollUp = new ScrollButton(up: true);
        BtnScrollDown = new ScrollButton(up: false);
        _layout.Add(BtnScrollUp, ScreenX + ScreenW - 62f, ScreenY + ScreenH - 32f, 26f, 26f);
        _layout.Add(BtnScrollDown, ScreenX + ScreenW - 32f, ScreenY + ScreenH - 32f, 26f, 26f);

        _layout.Add(new ResizeGrip(), NativeW - GripSize, NativeH - GripSize, GripSize, GripSize);

        AddChild(_layout);

        MinSize = NativeSize * MinScale;
        SetSize = NativeSize * _preferredScale;
    }

    // ─── Layout helpers ────────────────────────────────────────────────────

    private Button Key(float cx, float cy) => KeyRect(cx - 22f, cy - 25f, 44f, 50f);

    private Button KeyRect(float x, float y, float w, float h)
    {
        var b = new KeyButton();
        _layout.Add(b, x, y, w, h);
        return b;
    }

    // ─── Sizing ────────────────────────────────────────────────────────────

    /// <summary>Largest scale at which the whole ATM still fits inside the game window.</summary>
    private float MaxFittingScale()
    {
        if (Parent is not { } parent || parent.Size.X <= 0 || parent.Size.Y <= 0)
            return MaxScale;

        var fit = MathF.Min(parent.Size.X / NativeW, parent.Size.Y / NativeH) * MaxScreenFraction;
        return MathF.Min(fit, MaxScale);
    }

    /// <summary>Resizes the window to <paramref name="scale"/>, clamped to the limits and to the screen.</summary>
    private void ApplyScale(float scale)
    {
        // On a screen too small for even MinScale, fitting the screen wins so the keypad stays reachable.
        var max = MaxFittingScale();
        scale = MathF.Min(MathF.Max(scale, MinScale), max);
        MinSize = NativeSize * MathF.Min(MinScale, max);

        var size = NativeSize * scale;
        if (!SetSize.EqualsApprox(size, 0.5))
            SetSize = size;

        _clampPending = true;
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _trackedParent = Parent;
        if (_trackedParent != null)
            _trackedParent.OnResized += OnParentResized;

        ApplyScale(_preferredScale);
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
    private void OnParentResized() => ApplyScale(_preferredScale);

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        var before = SetSize;
        base.MouseMove(args);

        // A resize drag moves the corner freely; snap it back to the sprite's aspect ratio,
        // following whichever edge the player moved further.
        if (SetSize.EqualsApprox(before))
            return;

        var current = before.X / NativeW;
        var byWidth = SetSize.X / NativeW;
        var byHeight = SetSize.Y / NativeH;
        _preferredScale = MathF.Abs(byWidth - current) >= MathF.Abs(byHeight - current) ? byWidth : byHeight;
        ApplyScale(_preferredScale);
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
            LayoutContainer.SetPosition(this, clamped);
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
        _bg.Texture = s.Tampered ? _bg2 : _bg1;

        var header  = s.AccountNumber > 0 ? $"ACCT #{s.AccountNumber}" : "COLONY ATM";
        var balance = s.Screen >= AtmScreen.MainMenu && s.AccountNumber > 0 ? $"BAL ${s.Balance}" : string.Empty;
        var input   = IsInputScreen(s.Screen);

        _screen.SetState(header, balance, BuildBody(s), BuildBuffer(s), input);

        var history = s.Screen == AtmScreen.History;
        BtnScrollUp.Visible = BtnScrollDown.Visible = history && s.HistoryTotal > s.History.Length;
        BtnScrollUp.Disabled = s.HistoryOffset <= 0;
        BtnScrollDown.Disabled = s.HistoryOffset + s.History.Length >= s.HistoryTotal;
    }

    private static string BuildBody(ColonyAtmBuiState s)
    {
        return s.Screen switch
        {
            AtmScreen.Welcome => "COLONY FINANCIAL TERMINAL\nv2.7  UN TREASURY\n\n1) REMOTE DEPOSIT\n\nInsert ID card for account access.",
            AtmScreen.PinEntry => Combine("ENTER PIN:", s.StatusMessage),
            AtmScreen.PinLocked => "** CARD LOCKED **\nToo many incorrect attempts. Please try again later.",
            AtmScreen.MainMenu => $"Welcome, {s.OwnerName}.\n\n1) WITHDRAW\n2) DEPOSIT\n3) TRANSFER\n4) REMOTE DEPOSIT\n5) HISTORY\n6) EXIT",
            AtmScreen.Withdraw => Combine("WITHDRAW\nEnter amount:", s.StatusMessage),
            AtmScreen.WithdrawConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Deposit => Combine("DEPOSIT\nEnter amount:", s.StatusMessage),
            AtmScreen.RemoteDeposit => Combine("REMOTE DEPOSIT\nRecipient account #:", s.StatusMessage),
            AtmScreen.RemoteDepositAmount => s.StatusMessage,
            AtmScreen.RemoteDepositConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Transfer => Combine("TRANSFER\nRecipient account #:", s.StatusMessage),
            AtmScreen.TransferAmount => s.StatusMessage,
            AtmScreen.TransferConfirm => $"{s.StatusMessage}\n\nENTER = confirm   DEL = cancel",
            AtmScreen.Result => $"{s.StatusMessage}\n\nENTER to continue.",
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
    ///     Places each child at a fixed rectangle in native sprite pixels and scales them all
    ///     uniformly to whatever size the window currently has (letterboxed if the aspect is off).
    /// </summary>
    private sealed class NativeLayout : Control
    {
        private readonly Dictionary<Control, UIBox2> _rects = new();

        /// <summary>Current on-screen pixels per native sprite pixel.</summary>
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

    // ─── Transparent keypad hotspot ────────────────────────────────────────

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

    private sealed class KeyButton : Button
    {
        private static readonly Color Hover = new(0.27f, 1f, 0.42f, 0.16f);
        private static readonly Color Press = new(0.27f, 1f, 0.42f, 0.34f);

        public KeyButton()
        {
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (DrawMode == DrawModeEnum.Pressed)
                handle.DrawRect(PixelSizeBox, Press);
            else if (DrawMode == DrawModeEnum.Hover)
                handle.DrawRect(PixelSizeBox, Hover);
        }
    }

    // ─── CRT screen renderer (text + caret + scanlines) ────────────────────

    private sealed class AtmScreenControl : Control
    {
        private static readonly Color Green = Color.FromHex("#46ff77");
        private static readonly Color Dim   = Color.FromHex("#1f9c43");
        private static readonly Color Scan  = new(0f, 0f, 0f, 0.16f);
        private static readonly Color Tint  = new(0.12f, 1f, 0.42f, 0.05f);

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

        public AtmScreenControl(Font font, Font bold, float widthVirtual)
        {
            _font = font;
            _bold = bold;
            _widthVirtual = widthVirtual;
            _advanceVirtual = font.GetCharMetrics(new System.Text.Rune('M'), 1f)?.Advance ?? 7f;
            RectClipContent = true;
            MouseFilter = MouseFilterMode.Ignore;
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

        private string Wrap(string text)
        {
            var maxCols = Math.Max(8, (int) ((_widthVirtual - 12f) / _advanceVirtual));
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

            if (_shown < _body.Length)
            {
                _charTimer += dt;
                while (_charTimer >= CharInterval && _shown < _body.Length)
                {
                    _charTimer -= CharInterval;
                    _shown++;
                }
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            // Text is tuned for the default window size and grows or shrinks with the art. The ratio is
            // stepped (rounded down) so a resize drag doesn't build a new font atlas every frame or overflow.
            var ratio = MathF.Floor(Size.X / _widthVirtual * 20f) / 20f;
            var scale = UIScale * MathF.Max(0.5f, ratio);
            var inset = 6f * scale;
            var x = inset;
            var y = inset;

            // Header line + right-aligned balance.
            if (_header.Length > 0)
            {
                handle.DrawString(_bold, new Vector2(x, y), _header, scale, Green);
                if (_balance.Length > 0)
                {
                    var bw = TextWidth(_bold, _balance.Length, scale);
                    handle.DrawString(_bold, new Vector2(PixelSize.X - inset - bw, y), _balance, scale, Dim);
                }
                y += _bold.GetLineHeight(scale);
                handle.DrawRect(new UIBox2(x, y, PixelSize.X - inset, y + Math.Max(1f, scale)), Dim);
                y += 5f * scale;
            }

            // Body (typed out) plus the live input echo.
            var clamped = Math.Clamp(_shown, 0, _body.Length);
            var draw = _body[..clamped];
            if (_input && clamped >= _body.Length)
                draw += "\n> " + _buffer;

            handle.DrawString(_font, new Vector2(x, y), draw, scale, Green);

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

            // Phosphor tint + scanlines over the whole screen.
            var box = PixelSizeBox;
            handle.DrawRect(box, Tint);
            var step = Math.Max(2f, 3f * scale);
            for (var sy = 0f; sy < box.Bottom; sy += step)
                handle.DrawRect(new UIBox2(0f, sy, box.Right, sy + scale), Scan);
        }

        private static float TextWidth(Font font, int chars, float scale)
        {
            var advance = font.GetCharMetrics(new System.Text.Rune('M'), scale)?.Advance ?? 7;
            return chars * advance;
        }
    }
}
