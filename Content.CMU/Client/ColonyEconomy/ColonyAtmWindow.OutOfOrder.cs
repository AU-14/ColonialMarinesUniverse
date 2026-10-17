using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.ColonyEconomy;

/// <summary>
///     The screen of a machine a siphon rig has knocked out of service.
/// </summary>
public sealed partial class ColonyAtmWindow
{
    /// <summary>
    ///     A console gone wrong: memory dumps and errors spewing up the tube faster than they can be
    ///     read, characters that will not hold still, lines slipping out of their columns - and over it
    ///     all a plain OUT OF ORDER notice, with whatever the person who broke the machine left on it.
    /// </summary>
    private sealed class FaultConsole : Control
    {
        private static readonly Color Bright = Color.FromHex("#46ff77");
        private static readonly Color Mid    = Color.FromHex("#2fd160");
        private static readonly Color Dim    = Color.FromHex("#1f9c43");
        private static readonly Color Faint  = Color.FromHex("#167a34");
        // The notice's own ground, dark enough to hide the spew behind it, and the ink of inverse video.
        private static readonly Color Ground = Color.FromHex("#030805");
        private static readonly Color Ink    = Color.FromHex("#04120a");

        // Lines kept for the spew; more than the tube shows, so a resize never runs it dry.
        private const int MaxLines = 24;
        private const string GlitchChars = "#%&@$*?!<>/\\|=+~^0123456789ABCDEF";

        // What the console throws up between its memory dumps: {0} a byte, {1} an address, {2} a try.
        private static readonly string[] Errors =
        {
            "FATAL {0:X2}: LEDGER CRC MISMATCH",
            "SEGV AT {1:X8} IN TXN_CORE",
            "TXN_CORE HALTED",
            "WATCHDOG RESET ... FAILED",
            "UPLINK LOST, RETRY {2}/3",
            "DISPENSER BUS ERROR {0:X2}",
            "AUTH TABLE CORRUPT AT {1:X8}",
            "KERNEL PANIC: NOT SYNCING",
            "STACK SMASHED, ABORTING",
            "UNHANDLED TRAP {0:X2} AT {1:X8}",
            "?? ???? ?????? ??? ??",
        };

        private readonly Font _font;
        private readonly Font _bold;
        private readonly float _widthVirtual;
        private readonly float _advanceVirtual;
        private readonly Random _random = new();
        private readonly string _banner;

        // The spew, oldest first, and where in memory the dump has walked to.
        private readonly List<string> _lines = new();
        private int _address;
        private float _spewTimer;
        private int _burst;

        // Characters garbled for the moment, a line torn sideways, the whole console dropped a few
        // pixels, the notice knocked a column out of place.
        private readonly Dictionary<(int Line, int Column), char> _glitched = new();
        private float _glitchTimer;
        private int _tearLine = -1;
        private int _tearShift;
        private float _jump;
        private int _noticeShift;
        private int _noticeGlitch = -1;

        // The banner sits in inverse video, flicking back to plain now and then.
        private float _blinkTimer;
        private bool _inverse = true;
        private float _caretTimer;
        private bool _caretOn = true;

        private string? _message;
        private List<string> _notice = new();

        /// <summary>The sapper's message, shown under the banner in place of the apology. Null for none.</summary>
        public string? Message
        {
            get => _message;
            set
            {
                if (_message == value && _notice.Count > 0)
                    return;

                _message = value;
                _notice = Wrap(value ?? Loc.GetString("cmu-atm-out-of-order-sorry"), Columns - 4);
            }
        }

        public FaultConsole(Font font, Font bold, float widthVirtual)
        {
            _font = font;
            _bold = bold;
            _widthVirtual = widthVirtual;
            _advanceVirtual = font.GetCharMetrics(new Rune('M'), 1f)?.Advance ?? 8f;
            _banner = Loc.GetString("cmu-atm-out-of-order");
            _address = _random.Next(0x1000, 0xF000) & ~0xF;
            RectClipContent = true;
            MouseFilter = MouseFilterMode.Ignore;
            Message = null;

            // Already mid-spew by the time the tube comes up.
            for (var i = 0; i < MaxLines; i++)
                _lines.Add(NextLine());
        }

        /// <summary>Characters that fit across the tube at the default size.</summary>
        private int Columns => Math.Max(8, (int) ((_widthVirtual - 16f) / _advanceVirtual));

        private string NextLine()
        {
            // Mostly a walk through memory; now and then an error, and the dump picks up somewhere else.
            if (_random.Next(5) == 0)
            {
                _address = _random.Next(0x1000, 0xF000) & ~0xF;
                var error = Errors[_random.Next(Errors.Length)];
                return string.Format(error, _random.Next(256), _random.Next(), _random.Next(1, 4));
            }

            var bytes = new byte[6];
            _random.NextBytes(bytes);
            var hex = string.Join(' ', bytes.Select(b => b.ToString("X2")));
            var text = new string(bytes.Select(b => b is > 0x20 and < 0x7F ? (char) b : '.').ToArray());
            var line = $"{_address:X4}: {hex}  {text}";
            _address = (_address + bytes.Length) & 0xFFFF;
            return line;
        }

        // Uneven on purpose: a steady patter, bursts that scroll faster than they can be read, stalls.
        private float NextDelay()
        {
            if (_burst > 0)
            {
                _burst--;
                return 0.03f;
            }

            var roll = _random.NextDouble();
            if (roll < 0.08)
            {
                _burst = _random.Next(6, 14);
                return 0.03f;
            }

            return roll < 0.18
                ? 0.9f + _random.NextSingle() * 0.8f
                : 0.12f + _random.NextSingle() * 0.2f;
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);
            var dt = args.DeltaSeconds;

            // A hitch is no reason to print a screenful at once.
            _spewTimer = MathF.Max(_spewTimer - dt, -0.5f);
            while (_spewTimer <= 0f)
            {
                _lines.Add(NextLine());
                if (_lines.Count > MaxLines)
                    _lines.RemoveAt(0);
                _spewTimer += NextDelay();
            }

            _glitchTimer -= dt;
            if (_glitchTimer <= 0f)
                Glitch();

            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _inverse = !_inverse;
                _blinkTimer = _inverse ? 0.9f + _random.NextSingle() * 0.6f : 0.08f + _random.NextSingle() * 0.25f;
            }

            _caretTimer += dt;
            if (_caretTimer >= 0.5f)
            {
                _caretTimer -= 0.5f;
                _caretOn = !_caretOn;
            }
        }

        /// <summary>Picks what is wrong with the screen for the next moment.</summary>
        private void Glitch()
        {
            // Mostly quick flickers, now and then a longer hold.
            _glitchTimer = _random.NextDouble() < 0.15 ? 0.4f : 0.04f + _random.NextSingle() * 0.12f;

            _glitched.Clear();
            for (var i = _random.Next(3, 11); i > 0; i--)
            {
                var line = _random.Next(_lines.Count);
                if (_lines[line].Length > 0)
                    _glitched[(line, _random.Next(_lines[line].Length))] = RandomGlyph();
            }

            _tearLine = _random.NextDouble() < 0.3 ? _random.Next(_lines.Count) : -1;
            _tearShift = _random.Next(1, 5) * (_random.Next(2) == 0 ? -1 : 1);
            // The vertical hold slipping: everything drops a few pixels for a beat.
            _jump = _random.NextDouble() < 0.06 ? _random.Next(2, 7) : 0f;
            _noticeShift = _random.NextDouble() < 0.05 ? (_random.Next(2) == 0 ? -1 : 1) : 0;
            _noticeGlitch = _random.NextDouble() < 0.2 ? _random.Next(1000) : -1;
        }

        private char RandomGlyph() => GlitchChars[_random.Next(GlitchChars.Length)];

        /// <summary>The spew line with whatever is garbled in it right now.</summary>
        private string Garble(int line)
        {
            var text = _lines[line];
            char[]? chars = null;
            foreach (var ((l, column), glyph) in _glitched)
            {
                if (l != line || column >= text.Length)
                    continue;

                chars ??= text.ToCharArray();
                chars[column] = glyph;
            }

            return chars == null ? text : new string(chars);
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            // Sized like the terminal's own text, so the two read as one machine.
            var ratio = MathF.Floor(Size.X / _widthVirtual * 20f) / 20f;
            var scale = UIScale * MathF.Max(0.5f, ratio);
            var inset = 8f * scale;
            var lineHeight = _font.GetLineHeight(scale);
            var advance = _font.GetCharMetrics(new Rune('M'), scale)?.Advance ?? _advanceVirtual * scale;
            var size = PixelSize;

            // The spew: newest at the bottom, as far up the tube as it fits.
            var rows = Math.Min(_lines.Count, (int) ((size.Y - inset) / lineHeight));
            var y = size.Y - inset - rows * lineHeight + _jump * scale;
            for (var i = _lines.Count - rows; i < _lines.Count; i++, y += lineHeight)
            {
                var x = inset + (i == _tearLine ? _tearShift * advance : 0f);
                var newest = i == _lines.Count - 1;
                handle.DrawString(_font, new Vector2(x, y), Garble(i), scale, newest ? Mid : Faint);

                if (newest && _caretOn)
                {
                    var caret = x + _lines[i].Length * advance;
                    handle.DrawRect(new UIBox2(caret, y + 2f * scale, caret + advance, y + lineHeight), Mid);
                }
            }

            DrawNotice(handle, scale, advance, lineHeight);
        }

        /// <summary>
        ///     The notice across the middle of the tube: a double-ruled box, OUT OF ORDER in inverse video
        ///     that will not quite hold steady, and under it the apology, or the sapper's message instead.
        /// </summary>
        private void DrawNotice(DrawingHandleScreen handle, float scale, float advance, float lineHeight)
        {
            var size = PixelSize;
            var rule = MathF.Max(1f, MathF.Round(scale));
            var pad = MathF.Round(5f * scale);
            var boldHeight = _bold.GetLineHeight(scale);
            var boldAdvance = _bold.GetCharMetrics(new Rune('M'), scale)?.Advance ?? advance;
            var shift = _noticeShift * advance;

            var width = MathF.Round(size.X - 2f * (8f * scale + advance));
            var height = MathF.Round(4f * rule + pad + boldHeight + pad + _notice.Count * lineHeight + pad + 4f * rule);
            var box = UIBox2.FromDimensions(MathF.Round((size.X - width) / 2f + shift), MathF.Round((size.Y - height) / 2f), width, height);

            handle.DrawRect(box, Ground);
            handle.DrawRect(box, Bright, filled: false);
            handle.DrawRect(Shrink(box, 2f * rule), Dim, filled: false);

            var inner = Shrink(box, 4f * rule);
            var top = inner.Top + pad;
            var bannerAt = new Vector2(MathF.Round((size.X - _banner.Length * boldAdvance) / 2f + shift), top);
            if (_inverse)
            {
                handle.DrawRect(new UIBox2(inner.Left + pad, top, inner.Right - pad, top + boldHeight), Bright);
                handle.DrawString(_bold, bannerAt, _banner, scale, Ink);
            }
            else
            {
                AtmScreenControl.Glow(handle, _bold, bannerAt, _banner, scale, Bright);
            }

            // The message garbles too, a character at a time, never long enough to stop it being read.
            var y = top + boldHeight + pad;
            var glitch = _noticeGlitch;
            foreach (var line in _notice)
            {
                var text = line;
                if (glitch >= 0 && line.Length > 0)
                {
                    var chars = line.ToCharArray();
                    var at = glitch % line.Length;
                    if (!char.IsWhiteSpace(chars[at]))
                        chars[at] = GlitchChars[glitch % GlitchChars.Length];
                    text = new string(chars);
                    glitch = -1;
                }

                var x = MathF.Round((size.X - line.Length * advance) / 2f + shift);
                AtmScreenControl.Glow(handle, _font, new Vector2(x, y), text, scale, Bright);
                y += lineHeight;
            }
        }

        private static UIBox2 Shrink(UIBox2 box, float by) =>
            new(box.Left + by, box.Top + by, box.Right - by, box.Bottom - by);

        /// <summary>Word-wraps <paramref name="text"/> to <paramref name="columns"/>, splitting words too long for a line.</summary>
        private static List<string> Wrap(string text, int columns)
        {
            var lines = new List<string>();
            var line = new StringBuilder();
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var rest = word;
                while (rest.Length > 0)
                {
                    var room = line.Length == 0 ? columns : columns - line.Length - 1;
                    if (rest.Length <= room)
                    {
                        if (line.Length > 0)
                            line.Append(' ');
                        line.Append(rest);
                        rest = string.Empty;
                    }
                    else if (line.Length > 0)
                    {
                        lines.Add(line.ToString());
                        line.Clear();
                    }
                    else
                    {
                        lines.Add(rest[..columns]);
                        rest = rest[columns..];
                    }
                }
            }

            if (line.Length > 0)
                lines.Add(line.ToString());
            return lines;
        }
    }
}
