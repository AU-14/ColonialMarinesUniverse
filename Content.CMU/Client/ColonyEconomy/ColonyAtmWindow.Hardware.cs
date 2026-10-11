using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client.Clickable;
using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     The machine around the screen: keys that light up when they do something and travel when
///     pressed, a card reader the player's own card is pushed down into like a time-clock card, a
///     cash slot that pays out and swallows bills, and two status lights that never quite sit still.
/// </summary>
public sealed partial class ColonyAtmWindow
{
    // Sprite rectangles, in art pixels.
    private static readonly UIBox2 ReaderRect = UIBox2.FromDimensions(145, 100, 51, 42);
    // Where the card is drawn: from the fascia's top band, where the hand brings it in, down to the
    // reader's slot, below which it is inside the machine.
    private static readonly UIBox2 CardRect = UIBox2.FromDimensions(145, 86, 51, 48);
    // Room for the bills fanned out toward the customer, down to the shelf.
    private static readonly UIBox2 CashRect = UIBox2.FromDimensions(42, 196, 85, 27);
    // The bills themselves, all the way out with the stack fanned (the generator's CASH_* geometry).
    private static readonly UIBox2 BillsRect = UIBox2.FromDimensions(51, 202, 67, 21);
    // The status lights sit on the fascia left of the cash slot, above the counter shelf.
    private static readonly UIBox2 PowerLedRect = UIBox2.FromDimensions(31, 196, 5, 5);
    private static readonly UIBox2 ActivityLedRect = UIBox2.FromDimensions(31, 207, 5, 5);
    // Five rows: the digits, erase / 0 / 00, then CANCEL and ENTER twice as wide.
    private const int KeyX = 146, KeyY = 145, KeyW = 15, KeyH = 13, KeyPitchX = 17, KeyPitchY = 15;
    private static readonly UIBox2 CancelRect = UIBox2.FromDimensions(146, KeyY + 4 * KeyPitchY, 24, KeyH);
    private static readonly UIBox2 EnterRect = UIBox2.FromDimensions(171, KeyY + 4 * KeyPitchY, 24, KeyH);

    // How long after the event a fresh window still plays it; a window opened later shows the result.
    private static readonly TimeSpan RecentEvent = TimeSpan.FromSeconds(1.5);

    // The reader blinks amber this long after a card goes in, as if reading it.
    private const float ReadSeconds = 0.9f;

    // The slot shows one note, and one more at each of these amounts: five for $500 and up. The
    // generator draws a set of cash states per stack height (MAX_NOTES), named dispense_1 and so on.
    private static readonly int[] MoreNotesAt = { 50, 100, 250, 500 };

    // The activity light flickers this long after anything happens.
    private const float ActivitySeconds = 0.35f;

    private AtmSprite _readerLights = default!;
    private AtmSprite _skimmer = default!;
    private ReaderCard _card = default!;
    private AtmSprite _cash = default!;
    private AtmSprite _ledPower = default!;
    private AtmSprite _ledActivity = default!;

    private ColonyAtmBuiState? _state;
    private bool _cardIn;
    private TimeSpan? _seenDispense;
    private TimeSpan? _seenDeposit;
    private float _reading;
    // Bills are out in the tray (or on their way out), waiting to be taken.
    private bool _cashOut;
    // Still coming out of the slot.
    private bool _dispensing;
    // The bills were clicked on their way out, so they are lifted away as soon as they are out.
    private bool _cashTaken;
    private float _activity;
    private int _notes = 1;

    private void BuildHardware(IResourceCache cache)
    {
        var reader = Rsi(cache, "atm_reader");
        _readerLights = new AtmSprite(reader);
        _skimmer = new AtmSprite(reader);
        _card = new ReaderCard();
        var mouth = new AtmSprite(reader);
        // The card stands in front of the recess with the slot's shadow over where it goes in; a
        // siphon rig's damage goes over the slot itself. The reader as a whole is a button on top.
        Art(_readerLights, ReaderRect);
        Art(_card, CardRect);
        Art(mouth, ReaderRect);
        Art(_skimmer, ReaderRect);
        mouth.Show("bay_glass");

        Art(BtnReader, ReaderRect);
        BtnReader.OnPressed += _ =>
        {
            SkipBoot();
            if (BtnReader.Click == ReaderMode.Insert)
                InsertCardPressed?.Invoke();
            else if (BtnReader.Click == ReaderMode.LogOff)
                LogOffPressed?.Invoke();
        };

        _cash = new AtmSprite(Rsi(cache, "atm_cash"));
        Art(_cash, CashRect);

        Art(BtnCash, BillsRect);
        BtnCash.Visible = false;
        BtnCash.OnPressed += _ =>
        {
            BtnCash.Visible = false;
            TakeCashPressed?.Invoke();
            if (_dispensing)
                _cashTaken = true;
            else
                LiftCash();
        };

        var leds = Rsi(cache, "atm_leds");
        _ledPower = new AtmSprite(leds);
        _ledActivity = new AtmSprite(leds);
        Art(_ledPower, PowerLedRect);
        Art(_ledActivity, ActivityLedRect);
        _ledPower.Show("off");
        _ledActivity.Show("off");
        _readerLights.Show("lights_idle");
    }

    private Button Key(RSI rsi, string id, int col, int row)
        => HardwareKey(rsi, id, UIBox2.FromDimensions(KeyX + col * KeyPitchX, KeyY + row * KeyPitchY, KeyW, KeyH));

    private Button HardwareKey(RSI rsi, string id, UIBox2 rect)
    {
        var key = new KeyButton(rsi, id + "_");
        Art(key, rect);
        // Any key hurries the machine past its boot, as a real one would on a keypress.
        key.OnPressed += _ => SkipBoot();
        return key;
    }

    // ─── Keys ──────────────────────────────────────────────────────────────

    /// <summary>
    ///     Backlights the keys that do something on this screen and leaves the rest dark, mirroring
    ///     ColonyAtmSystem's handlers. Dark keys still press; the server simply ignores them.
    /// </summary>
    private void UpdateKeys(ColonyAtmBuiState s)
    {
        if (s.OutOfService)
        {
            foreach (var key in new[] { Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9, Btn00, BtnClear, BtnCancel, BtnEnter })
            {
                ((KeyButton) key).Lit = false;
                ((KeyButton) key).Ready = false;
            }
            return;
        }

        var buffer = s.KeypadBuffer.Length;
        var digits = new[] { Btn0, Btn1, Btn2, Btn3, Btn4, Btn5, Btn6, Btn7, Btn8, Btn9 };
        for (var digit = 0; digit < digits.Length; digit++)
            ((KeyButton) digits[digit]).Lit = DigitLive(s.Screen, digit, buffer);
        // 00 types two zeros, so it needs room for both.
        ((KeyButton) Btn00).Lit = DigitLive(s.Screen, 0, buffer + 1) && IsInputScreen(s.Screen);

        ((KeyButton) BtnClear).Lit = buffer > 0;
        ((KeyButton) BtnCancel).Lit = s.Screen != AtmScreen.Welcome;
        var enter = (KeyButton) BtnEnter;
        enter.Lit = s.Screen is not (AtmScreen.Welcome or AtmScreen.MainMenu);
        // ENTER pulses when it is the one thing the machine is waiting for.
        enter.Ready = s.Screen is AtmScreen.WithdrawConfirm or AtmScreen.TransferConfirm
            or AtmScreen.RemoteDepositConfirm or AtmScreen.Result or AtmScreen.PinLocked;
    }

    private static bool DigitLive(AtmScreen screen, int digit, int buffer)
        => screen switch
        {
            AtmScreen.Welcome => digit == 1,
            AtmScreen.MainMenu => digit is >= 1 and <= 6,
            AtmScreen.PinEntry => buffer < ColonyAtmComponent.PinLength,
            _ => IsInputScreen(screen) && buffer < ColonyAtmComponent.MaxAmountDigits,
        };

    // ─── Card reader, cash slot, lights ────────────────────────────────────

    private void UpdateHardware(ColonyAtmBuiState s)
    {
        var previous = _state;
        _state = s;

        if (s.CardInserted && !_cardIn)
        {
            _cardIn = true;
            var recent = Recent(s.CardInsertedAt);
            _card.Insert(s.CardPrototype, animate: recent);
            if (recent)
                _reading = ReadSeconds;
        }
        else if (!s.CardInserted && _cardIn)
        {
            _cardIn = false;
            _card.Eject();
        }

        // Timestamps already seen when the window opened are history, not events.
        if (TakeEvent(s.CashDispensedAt, ref _seenDispense, previous == null))
        {
            _cashTaken = false;
            _cashOut = true;
            _dispensing = true;
            _notes = Notes(s.CashAmount);
            BtnCash.Visible = true;
            _cash.Play($"dispense_{_notes}", () =>
            {
                _dispensing = false;
                // Clicked on the way out, or taken from the tray by someone else meanwhile.
                if (_cashTaken || _state?.CashWaiting == 0)
                {
                    LiftCash();
                    return;
                }

                _cash.Show($"dispensed_{_notes}");
            });
        }
        else if (s.CashWaiting > 0 && !_cashOut)
        {
            // Opened on a tray someone left cash in: the bills are already out, waiting.
            _cashOut = true;
            _notes = Notes(s.CashWaiting);
            BtnCash.Visible = true;
            _cash.Show($"dispensed_{_notes}");
        }

        // Left too long, the bills are drawn back into the machine.
        if (TakeEvent(s.CashDepositedAt, ref _seenDeposit, previous == null))
        {
            _cashOut = _dispensing = false;
            BtnCash.Visible = false;
            _notes = Notes(s.CashAmount);
            _cash.Play($"deposit_{_notes}", () => _cash.Show(null));
        }
        else if (s.CashWaiting == 0 && _cashOut && !_dispensing)
        {
            // Taken from the tray, by the player here or by someone at the machine.
            LiftCash();
        }

        // A siphoned machine: broken down and taped off while it is out, pry marks once it is back.
        _skimmer.Show(s.OutOfService ? "broken" : s.Tampered ? "tampered" : null);

        if (previous != null && (previous.Screen != s.Screen || previous.KeypadBuffer != s.KeypadBuffer || previous.StatusMessage != s.StatusMessage))
            _activity = ActivitySeconds;

        UpdateLights();
    }

    /// <summary>The bills are lifted out of the tray.</summary>
    private void LiftCash()
    {
        if (!_cashOut)
            return;

        _cashOut = false;
        BtnCash.Visible = false;
        _cash.Play($"take_{_notes}", () => _cash.Show(null));
    }

    private static int Notes(int amount)
    {
        var notes = 1;
        foreach (var at in MoreNotesAt)
        {
            if (amount >= at)
                notes++;
        }

        return notes;
    }

    /// <summary>True once per new timestamp, and only while it is still recent.</summary>
    private bool TakeEvent(TimeSpan? at, ref TimeSpan? seen, bool opening)
    {
        if (at == null || at == seen)
            return false;

        seen = at;
        return !opening || Recent(at);
    }

    private bool Recent(TimeSpan? at)
        => at is { } t && _timing.CurTime - t < RecentEvent;

    private void UpdateHardware(float dt)
    {
        var lightsDue = false;

        if (_reading > 0f)
        {
            _reading -= dt;
            lightsDue |= _reading <= 0f;
        }

        if (_activity > 0f)
        {
            _activity -= dt;
            lightsDue |= _activity <= 0f;
        }

        if (lightsDue)
            UpdateLights();
    }

    /// <summary>
    ///     Dark until the tube warms; busy through the boot; then idling - a heartbeat on the power
    ///     light and the odd blip of traffic on the activity light - until something happens.
    /// </summary>
    private void UpdateLights()
    {
        var booting = _boot is BootPhase.Logo or BootPhase.Console;
        _ledPower.Show(_boot == BootPhase.PowerOn ? "off" : booting ? "green" : "green_pulse");

        if (_state is not { } s)
        {
            _ledActivity.Show(booting ? "amber_busy" : "off");
            return;
        }

        if (s.OutOfService)
        {
            _ledPower.Show(_boot == BootPhase.PowerOn ? "off" : "red");
            _ledActivity.Show(_boot == BootPhase.PowerOn ? "off" : "red_blink");
            _readerLights.Show("lights_locked");
            BtnReader.Click = ReaderMode.None;
            return;
        }

        var fault = s.Screen == AtmScreen.PinLocked || ColonyAtmBui.IsErrorMessage(s.StatusMessage);
        _ledActivity.Show(_boot == BootPhase.PowerOn ? "off"
            : booting ? "amber_busy"
            : fault ? "red_blink"
            : _activity > 0f ? "amber_blink"
            : "amber_idle");

        var signedIn = s.CardInserted && s.Screen > AtmScreen.PinLocked && _reading <= 0f;
        _readerLights.Show(!s.CardInserted ? "lights_ready"
            : s.Screen == AtmScreen.PinLocked ? "lights_locked"
            : _reading > 0f ? "lights_reading"
            : signedIn ? "lights_ok"
            : "lights_wait");

        // An empty reader takes the player's card; their card, once the lights are green, logs them off.
        BtnReader.Click = !s.CardInserted ? ReaderMode.Insert : signedIn ? ReaderMode.LogOff : ReaderMode.None;
    }

    // ─── Sprite player ─────────────────────────────────────────────────────

    /// <summary>
    ///     Draws one state of an RSI over its rectangle: looping it, or playing it once and handing
    ///     over to whatever comes next. Frame timings come from the RSI's own delays.
    /// </summary>
    private sealed class AtmSprite : Control
    {
        private readonly RSI _rsi;
        private Texture[] _frames = Array.Empty<Texture>();
        private float[] _delays = Array.Empty<float>();
        private int _frame;
        private float _elapsed;
        private Action? _then;

        public string? Current { get; private set; }

        public AtmSprite(RSI rsi)
        {
            _rsi = rsi;
            MouseFilter = MouseFilterMode.Ignore;
        }

        /// <summary>Loops <paramref name="state"/>, or hides the sprite for null. No-op if already showing it.</summary>
        public void Show(string? state)
        {
            if (state == Current && _then == null)
                return;

            Set(state, null);
        }

        /// <summary>Plays <paramref name="state"/> once, then runs <paramref name="then"/>.</summary>
        public void Play(string state, Action then)
            => Set(state, then);

        private void Set(string? state, Action? then)
        {
            Current = state;
            _then = then;
            _frame = 0;
            _elapsed = 0f;

            if (state != null && _rsi.TryGetState(state, out var rsiState))
            {
                _frames = rsiState.GetFrames(RsiDirection.South);
                _delays = rsiState.GetDelays();
            }
            else
            {
                _frames = Array.Empty<Texture>();
                _delays = Array.Empty<float>();
            }
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            if (_frames.Length == 0 || (_frames.Length == 1 && _then == null))
                return;

            _elapsed += args.DeltaSeconds;
            while (_elapsed >= _delays[_frame])
            {
                _elapsed -= _delays[_frame];
                if (_frame + 1 < _frames.Length)
                {
                    _frame++;
                    continue;
                }

                if (_then is { } then)
                {
                    _then = null;
                    then();
                    return;
                }

                _frame = 0;
            }
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (_frames.Length > 0)
                handle.DrawTextureRect(_frames[_frame], PixelSizeBox);
        }
    }

    // ─── The card reader ──────────────────────────────────────────────────

    /// <summary>What a click on the reader does right now.</summary>
    public enum ReaderMode
    {
        None,
        Insert,
        LogOff,
    }

    /// <summary>
    ///     The reader as one big button: with no card in it, a click puts the player's card in; with
    ///     their card in and the lights green, a click logs them off and hands it back. A light outline
    ///     round the recess shows when a click will do something.
    /// </summary>
    public sealed class ReaderButton : Button
    {
        // The card recess and its slot, relative to ReaderRect, in art pixels.
        private static readonly UIBox2 Recess = UIBox2.FromDimensions(16, 11, 18, 27);
        private static readonly Color Hover = new(1f, 0.95f, 0.8f, 0.35f);
        private static readonly Color HoverSignedIn = Color.FromHex("#7dffa0").WithAlpha(0.45f);

        /// <summary>What a click on the reader does right now.</summary>
        public ReaderMode Click { get; set; }

        public ReaderButton()
        {
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            ModulateSelfOverride = Color.White;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (Click == ReaderMode.None || DrawMode is not (DrawModeEnum.Hover or DrawModeEnum.Pressed))
                return;

            var artPixel = PixelSize.X / ReaderRect.Width;
            var box = UIBox2.FromDimensions(Recess.TopLeft * artPixel, Recess.Size * artPixel);
            var colour = Click == ReaderMode.LogOff ? HoverSignedIn : Hover;
            for (var i = 0; i < 2; i++)
            {
                var inset = i * artPixel;
                handle.DrawRect(new UIBox2(box.Left - inset, box.Top - inset, box.Right + inset, box.Bottom + inset),
                    colour.WithAlpha(colour.A / (i + 1)), filled: false);
            }
        }
    }

    // ─── The cash slot ────────────────────────────────────────────────────

    /// <summary>
    ///     The bills while they are out of the slot: a click takes them in hand. The same light outline
    ///     as the reader's shows round them.
    /// </summary>
    public sealed class CashButton : Button
    {
        private static readonly Color Hover = new(1f, 0.95f, 0.8f, 0.35f);

        public CashButton()
        {
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            ModulateSelfOverride = Color.White;
            DefaultCursorShape = CursorShape.Hand;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);
            if (DrawMode is not (DrawModeEnum.Hover or DrawModeEnum.Pressed))
                return;

            var artPixel = PixelSize.X / BillsRect.Width;
            for (var i = 0; i < 2; i++)
            {
                var inset = i * artPixel;
                handle.DrawRect(new UIBox2(inset, inset, PixelSize.X - inset, PixelSize.Y - inset),
                    Hover.WithAlpha(Hover.A / (i + 1)), filled: false);
            }
        }
    }

    /// <summary>
    ///     The inserted card itself, at its own size and drawn from its own sprite, so every kind of
    ///     card shows as the player knows it. It goes in like a time-clock card pushed by hand - brought
    ///     down to the slot, a beat to line it up, pushed home a pixel past and settling - and stands
    ///     with its top showing. Ejecting, the machine pops it up and it is lifted away. The slot is
    ///     upright, so a card wider than it is tall goes in on its side.
    /// </summary>
    /// <remarks>
    ///     Drawn unscaled and on whole art pixels: the sprite is placed by its 32x32 frame, not by the
    ///     card's own pixels, which vary from badge to landscape tag. The card's shape comes from the
    ///     click map the client keeps of every sprite; reading the texture back instead would pull the
    ///     whole sprite atlas off the GPU. The design script mirrors these keyframes
    ///     (<c>INSERT_KEYS</c>, <c>EJECT_KEYS</c>) for its review GIF.
    /// </remarks>
    private sealed partial class ReaderCard : Control
    {
        // Whether each kind of card is wider than it is tall. Sprites never change shape in a session.
        private static readonly Dictionary<string, bool> Landscape = new();

        // (seconds, frame top in art pixels from the top of CardRect). The slot is CardRect's bottom edge.
        private static readonly (float Time, float Top)[] InsertKeys =
            { (0f, 0f), (0.30f, 18f), (0.40f, 18f), (0.70f, 30f), (0.78f, 29f) };
        private static readonly (float Time, float Top)[] EjectKeys =
            { (0f, 29f), (0.12f, 21f), (0.32f, 21f), (0.60f, 0f) };

        // The card frame's left edge relative to CardRect: the 32-wide frame centred on the recess,
        // which is as wide as the card inside it. Wider card sprites stand proud of the recess.
        private const float FrameLeft = 9f;
        private const float FrameSize = 32f;

        [Dependency] private IEntityManager _entities = default!;
        [Dependency] private IClickMapManager _clickMaps = default!;

        private Texture? _icon;
        private bool _sideways;
        private (float Time, float Top)[]? _keys;
        private float _time;
        private bool _ejecting;

        public ReaderCard()
        {
            IoCManager.InjectDependencies(this);
            MouseFilter = MouseFilterMode.Ignore;
        }

        public void Insert(string? prototype, bool animate)
        {
            _icon = null;
            _sideways = false;
            if (prototype != null)
            {
                var icon = _entities.System<SpriteSystem>().GetPrototypeIcon(prototype);
                _icon = icon.Default;
                _sideways = IsLandscape(prototype, icon);
            }

            _keys = InsertKeys;
            _time = animate ? 0f : InsertKeys[^1].Time;
            _ejecting = false;
        }

        private bool IsLandscape(string prototype, IRsiStateLike icon)
        {
            if (Landscape.TryGetValue(prototype, out var cached))
                return cached;

            // Without a click map (no RSI, or nothing loaded) the card simply goes in as drawn.
            var landscape = false;
            if (icon is RSI.State state)
            {
                int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
                for (var y = 0; y < state.Size.Y; y++)
                {
                    for (var x = 0; x < state.Size.X; x++)
                    {
                        if (!_clickMaps.IsOccluding(state.RSI, state.StateId, RsiDirection.South, 0, new Vector2i(x, y)))
                            continue;

                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                        top = Math.Min(top, y);
                        bottom = Math.Max(bottom, y);
                    }
                }

                // The click map pads every pixel by the same margin on all sides, so the comparison holds.
                landscape = right >= 0 && right - left > bottom - top;
            }

            return Landscape[prototype] = landscape;
        }

        public void Eject()
        {
            if (_icon == null)
                return;

            _keys = EjectKeys;
            _time = 0f;
            _ejecting = true;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            if (_keys == null || _time >= _keys[^1].Time)
                return;

            _time += args.DeltaSeconds;
            if (_ejecting && _time >= _keys[^1].Time)
                _icon = null;
        }

        private float Top()
        {
            var keys = _keys ?? InsertKeys;
            for (var i = 1; i < keys.Length; i++)
            {
                var (t0, v0) = keys[i - 1];
                var (t1, v1) = keys[i];
                if (_time > t1)
                    continue;

                var t = t1 > t0 ? Math.Clamp((_time - t0) / (t1 - t0), 0f, 1f) : 1f;
                return MathF.Round(v0 + (v1 - v0) * t * t * (3f - 2f * t));
            }

            return keys[^1].Top;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (_icon == null)
                return;

            var artPixel = PixelSize.X / CardRect.Width;
            var dest = UIBox2.FromDimensions(new Vector2(FrameLeft, Top()) * artPixel, new Vector2(FrameSize) * artPixel);

            // Below the slot the card is inside the machine; nothing shows there.
            var visible = new UIBox2(Vector2.Max(dest.TopLeft, Vector2.Zero), Vector2.Min(dest.BottomRight, PixelSize));
            if (visible.Width <= 0f || visible.Height <= 0f)
                return;

            // On its side, the card is turned a quarter clockwise about its frame's centre, so its left
            // end - where the photo usually is - stands above the slot. What shows is the part of the
            // sprite that the turn carries onto the visible box.
            var region = visible;
            var transform = handle.GetTransform();
            if (_sideways)
            {
                var c = dest.Center;
                region = new UIBox2(c.X - c.Y + visible.Top, c.X + c.Y - visible.Right,
                    c.X - c.Y + visible.Bottom, c.X + c.Y - visible.Left);
                handle.SetTransform(Matrix3x2.Multiply(new Matrix3x2(0, 1, -1, 0, c.X + c.Y, c.Y - c.X), transform));
            }

            var texel = (Vector2) _icon.Size / dest.Size;
            var source = UIBox2.FromDimensions((region.TopLeft - dest.TopLeft) * texel, region.Size * texel);
            handle.DrawTextureRectRegion(_icon, region, source);
            handle.SetTransform(transform);
        }
    }

    // ─── Physical key ──────────────────────────────────────────────────────

    /// <summary>
    ///     A key drawn from its sprites: lit or dark, raised, hovered or pressed down. A quick click
    ///     still shows the key travel - it stays down a moment after the mouse lets go.
    /// </summary>
    private sealed class KeyButton : Button
    {
        private const float ReleaseSeconds = 0.09f;

        private readonly AtmSprite _sprite;
        private readonly string _prefix;
        private float _release;
        private bool _wasDown;

        /// <summary>Backlit: the key does something on the current screen.</summary>
        public bool Lit { get; set; } = true;

        /// <summary>Pulses while lit, when the machine is waiting on this key. ENTER only.</summary>
        public bool Ready { get; set; }

        public KeyButton(RSI rsi, string prefix)
        {
            _prefix = prefix;
            _sprite = new AtmSprite(rsi);
            AddChild(_sprite);
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            // The button stylesheet tints by state, which would muddy the sprites.
            ModulateSelfOverride = Color.White;
            _sprite.Show(_prefix + "on");
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            var down = DrawMode == DrawModeEnum.Pressed;
            if (!down && _wasDown)
                _release = ReleaseSeconds;
            _wasDown = down;
            _release -= args.DeltaSeconds;

            var state = down || _release > 0f ? "pressed"
                : !Lit ? "off"
                : DrawMode == DrawModeEnum.Hover ? "hover"
                : Ready ? "ready"
                : "on";
            _sprite.Show(_prefix + state);
        }
    }
}
