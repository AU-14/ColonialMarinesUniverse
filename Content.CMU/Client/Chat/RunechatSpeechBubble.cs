using System.Globalization;
using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Content.Shared._CMU14.Chat;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Ghost;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Chat.UI;

public sealed partial class RunechatSpeechBubble : SpeechBubble
{
    private const string SayStyle = "runechatSay";
    private const string WhisperStyle = "runechatWhisper";
    private const string RadioStyle = "runechatRadio";
    private const string EmoteStyle = "runechatEmote";
    private const string LoocStyle = "runechatLooc";

    private const int LongestText = 80;
    private const int ContinueTextLength = LongestText - 5;
    private const float SplitChunkSeconds = 4f;
    private const float SplitFinalSeconds = 6f;
    private const float BaselineRunechatScale = 1.15f;
    private const float DefaultRunechatScale = 2.5f;
    private const float MinimumRunechatScale = 0.5f;
    private const float MaximumRunechatScale = 2f;
    private const float CmssLangchatWidth = 96f;
    private const float CmssSplitLangchatWidth = CmssLangchatWidth * 2f;

    private static readonly Color DefaultColor = Color.White;
    private static readonly Color XenoColor = Color.FromHex("#b491c8");
    private static readonly Color ObserverColor = Color.FromHex("#c51fb7");
    private static readonly Color LoocColor = Color.FromHex("#48d1cc");
    private static readonly Color PainColor = Color.FromHex("#c83232");
    private static readonly Color RadioColor = Color.FromHex("#73d48f");

    [Dependency] private IEntityManager _entityManager = default!;

    public RunechatSpeechBubble(SpeechType type, ChatMessage message, EntityUid senderEntity)
        : base(
            message,
            senderEntity,
            GetStyleClass(type, message),
            GetTextColor(type, message, senderEntity),
            GetLifetime(GetPages(GetText(message, GetStyleClass(type, message)))))
    {
        RectClipContent = false;
    }

    protected override Control BuildBubble(ChatMessage message, string speechStyleClass, Color? fontColor = null)
    {
        var text = GetText(message, speechStyleClass);
        var pages = GetPages(text);
        var style = GetVisualStyle(message, speechStyleClass, text);

        return new RunechatTextControl(pages, fontColor ?? DefaultColor, style);
    }

    private static string GetStyleClass(SpeechType type, ChatMessage message)
    {
        if (type == SpeechType.Emote &&
            message.UseEmoteSpeechBubble &&
            (message.Channel == ChatChannel.Local || message.Channel == ChatChannel.Whisper))
        {
            return message.Channel == ChatChannel.Whisper
                ? WhisperStyle
                : SayStyle;
        }

        return type switch
        {
            SpeechType.Emote => EmoteStyle,
            SpeechType.Say => SayStyle,
            SpeechType.Whisper => WhisperStyle,
            SpeechType.Radio => RadioStyle,
            SpeechType.Looc => LoocStyle,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    private static Color GetTextColor(SpeechType type, ChatMessage message, EntityUid senderEntity)
    {
        if (message.MessageColorOverride is { } color)
            return color;

        if (CMURunechatStyles.IsInterrupting(message.SpeechStyleClass))
            return PainColor;

        if (type == SpeechType.Looc)
            return LoocColor;

        if (type == SpeechType.Radio)
            return message.Display?.AccentColor ?? RadioColor;

        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (entityManager.HasComponent<XenoComponent>(senderEntity))
            return XenoColor;

        if (entityManager.HasComponent<GhostComponent>(senderEntity))
            return ObserverColor;

        var squads = entityManager.System<SquadSystem>();
        return squads.TryGetSquadMemberColor(senderEntity, out var squadColor)
            ? squadColor
            : DefaultColor;
    }

    private static RunechatVisualStyle GetVisualStyle(ChatMessage message, string speechStyleClass, string text)
    {
        if (message.SpeechStyleClass == CMURunechatStyles.Scream)
            return RunechatVisualStyle.Scream;

        if (message.SpeechStyleClass == CMURunechatStyles.Pain)
            return RunechatVisualStyle.Pain;

        if (speechStyleClass == EmoteStyle)
        {
            return IsYellEmote(text)
                ? RunechatVisualStyle.EmoteYell
                : RunechatVisualStyle.Emote;
        }

        if (message.SpeechStyleClass == "megaphoneSpeech")
            return RunechatVisualStyle.Announce;

        if (message.SpeechStyleClass == "commanderSpeech" ||
            speechStyleClass == SayStyle && IsBoldSpeech(message))
            return RunechatVisualStyle.Bolded;

        if (speechStyleClass == RadioStyle)
            return RunechatVisualStyle.Radio;

        if (speechStyleClass == WhisperStyle)
            return RunechatVisualStyle.Whisper;

        return RunechatVisualStyle.Normal;
    }

    private static bool IsYellEmote(string text)
    {
        return text.Contains("scream", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("pain", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("medic", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("corpsman", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBoldSpeech(ChatMessage message)
    {
        var bubbleContent = SharedChatSystem.GetStringInsideTag(message, "BubbleContent");
        return bubbleContent.Contains("[bold]", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetLifetime(IReadOnlyList<string> pages)
    {
        if (pages.Count <= 1)
        {
            var length = pages.Count == 0 ? 0 : pages[0].Length;
            return TimeSpan.FromSeconds(length / (float) LongestText * SplitChunkSeconds + 2f);
        }

        return TimeSpan.FromSeconds((pages.Count - 1) * SplitChunkSeconds + SplitFinalSeconds);
    }

    private static string GetText(ChatMessage message, string speechStyleClass)
    {
        var text = speechStyleClass switch
        {
            EmoteStyle => message.Message,
            SayStyle => GetBubbleContent(message),
            WhisperStyle => FormatWhisperText(message),
            RadioStyle => FormatRadioText(message),
            LoocStyle => $"LOOC: {message.Message}",
            _ => message.WrappedMessage,
        };

        if (string.IsNullOrWhiteSpace(text))
            text = message.Message;

        text = FormattedMessage.RemoveMarkupPermissive(text);
        return NormalizeWhitespace(text);
    }

    private static string GetBubbleContent(ChatMessage message)
    {
        return SharedChatSystem.GetStringInsideTag(message, "BubbleContent");
    }

    private static string FormatWhisperText(ChatMessage message)
    {
        return GetBubbleContent(message);
    }

    private static string FormatRadioText(ChatMessage message)
    {
        var label = GetRadioLabel(message);
        return string.IsNullOrWhiteSpace(label)
            ? message.Message
            : $"[{label}] {message.Message}";
    }

    private static string? GetRadioLabel(ChatMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Display?.ChannelLabel))
            return null;

        var label = FormattedMessage.RemoveMarkupPermissive(message.Display.ChannelLabel);
        label = NormalizeWhitespace(label)
            .Trim('[', ']')
            .Trim();

        return string.IsNullOrWhiteSpace(label)
            ? null
            : label.ToUpperInvariant();
    }

    private static List<string> GetPages(string text)
    {
        var pages = new List<string>();

        if (text.Length <= LongestText)
        {
            pages.Add(text);
            return pages;
        }

        var remaining = text;
        while (remaining.Length > LongestText)
        {
            var split = GetSplitIndex(remaining);
            pages.Add(remaining[..split].TrimEnd() + "...");
            remaining = "..." + remaining[split..].TrimStart();
        }

        pages.Add(remaining);
        return pages;
    }

    private static int GetSplitIndex(string text)
    {
        var max = Math.Min(ContinueTextLength, text.Length);
        var split = text.LastIndexOf(' ', max - 1, max);

        return split >= LongestText / 2
            ? split
            : max;
    }

    private static string NormalizeWhitespace(string text)
    {
        var builder = new StringBuilder(text.Length);
        var previousWasWhitespace = false;

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                if (!previousWasWhitespace)
                    builder.Append(' ');

                previousWasWhitespace = true;
                continue;
            }

            builder.Append(character);
            previousWasWhitespace = false;
        }

        return builder.ToString().Trim();
    }

    private static int ScaleFontSize(int fontSize, float scale)
    {
        return (int) MathF.Round(fontSize * scale);
    }

    private readonly record struct RunechatVisualStyle(
        int FontSize,
        bool UseBold,
        bool PrefixEmoteIcon,
        float MaxWidth,
        float LineHeightOffset = 0f,
        bool UsePanicShake = false,
        bool UseItalic = false)
    {
        public static readonly RunechatVisualStyle Normal = new(7, false, false, CmssLangchatWidth);
        public static readonly RunechatVisualStyle Whisper = new(6, false, false, CmssLangchatWidth, -1f, UseItalic: true);
        public static readonly RunechatVisualStyle Radio = new(7, false, false, CmssSplitLangchatWidth);
        public static readonly RunechatVisualStyle Emote = new(6, false, true, CmssLangchatWidth, -1f);
        public static readonly RunechatVisualStyle EmoteYell = new(9, true, true, CmssLangchatWidth);
        public static readonly RunechatVisualStyle Bolded = new(8, true, false, CmssLangchatWidth);
        public static readonly RunechatVisualStyle Announce = new(12, true, false, CmssSplitLangchatWidth);
        public static readonly RunechatVisualStyle Pain = new(10, true, false, CmssLangchatWidth);
        public static readonly RunechatVisualStyle Scream = new(10, true, false, CmssLangchatWidth, UsePanicShake: true);

        public int GetScaledFontSize(float scale)
        {
            return ScaleFontSize(FontSize, scale);
        }

        public float GetScaledMaxWidth(float scale)
        {
            return MaxWidth * scale;
        }

        public float GetScaledLineHeightOffset(float scale)
        {
            return LineHeightOffset * scale;
        }
    }

    private sealed partial class RunechatTextControl : Control
    {
        private const string SmallFontsFamily = "Small Fonts";
        private const string SmallFonts120Family = "Small Fonts (120)";
        private const string FallbackFontPath = "/Fonts/Cozette/CozetteVector.ttf";
        private const string FallbackItalicFontPath = "/Fonts/RobotoMono/RobotoMono-Italic.ttf";
        private const float CmuMaxAlpha = 1f;
        private const float SyntheticBoldOffset = 1f;
        private const float TextShadowAlpha = 0.18f;
        private const float TextShadowOffset = 1f;
        private const float EmoteIconBaseSize = 9f;
        private const float DefaultEmoteIconPixelSize = 1.4f;
        private const float PanicShakeDuration = 0.85f;
        private const float PanicShakeFrequency = 18f;
        private const float PanicShakeSize = 6f;
        private const float EmoteIconVisibleLeft = 3f;
        private const float EmoteIconVisibleRight = 8f;
        private const float EmoteIconVisibleTop = 3f;
        private const float EmoteIconVisibleBottom = 8f;
        private const float EmoteIconAlpha = 200f / 255f;

        private static readonly Color EmoteIconBlue = Color.FromHex("#3399ff");
        private static readonly CultureInfo EnUsCulture = CultureInfo.GetCultureInfo("en-US");
        private static bool SmallFontsLoadFailed;

        private static readonly string[] EmoteIcon =
        {
            ".........",
            ".........",
            ".........",
            "...B#B#B.",
            "...##B##.",
            "...BBBBB.",
            "...##B##.",
            "...B#B#B.",
            ".........",
        };

        [Dependency] private IConfigurationManager _configManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private ISystemFontManager _systemFontManager = default!;

        private readonly IReadOnlyList<string> _pages;
        private readonly Color _color;
        private readonly RunechatVisualStyle _style;
        private readonly float _scale;
        private readonly Font _font;

        private readonly List<RunechatPageLayout> _layouts = new();
        private Vector2 _cachedSize;
        private bool _layoutDirty = true;
        private int _currentPage;
        private float _pageTime;
        private float _animationTime;

        public RunechatTextControl(IReadOnlyList<string> pages, Color color, RunechatVisualStyle style)
        {
            IoCManager.InjectDependencies(this);

            MouseFilter = MouseFilterMode.Ignore;
            _pages = pages;
            _color = color;
            _style = style;
            _scale = DefaultRunechatScale *
                     Math.Clamp(_configManager.GetCVar(CCVars.ChatRunechatBubbleScale), MinimumRunechatScale, MaximumRunechatScale);
            _font = LoadRunechatFont(style.GetScaledFontSize(_scale), style.UseItalic);
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            _animationTime += args.DeltaSeconds;

            if (_pages.Count <= 1 || _currentPage >= _pages.Count - 1)
                return;

            _pageTime += args.DeltaSeconds;
            while (_pageTime >= SplitChunkSeconds && _currentPage < _pages.Count - 1)
            {
                _pageTime -= SplitChunkSeconds;
                _currentPage++;
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            EnsureLayout();
            return _cachedSize;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            if (_pages.Count == 0)
                return;

            EnsureLayout();

            var layout = _layouts[Math.Min(_currentPage, _layouts.Count - 1)];
            var textOpacity = _configManager.GetCVar(CCVars.SpeechBubbleTextOpacity) * CmuMaxAlpha;
            var outlineColor = Color.Black.WithAlpha(textOpacity);
            var textColor = _color.WithAlpha(_color.A * textOpacity);
            var lineHeight = GetLineHeight();
            var y = (PixelSize.Y - layout.Height) / 2f;
            var shakeOffset = GetPanicShakeOffset();

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                var visibleBounds = GetVisibleBounds(line);
                var iconWidth = _style.PrefixEmoteIcon && i == 0 ? GetVisibleIconWidth() : 0f;
                var iconGap = iconWidth > 0f ? GetIconGap() : 0f;
                var contentWidth = iconWidth + iconGap + visibleBounds.Width;
                var x = (PixelSize.X - contentWidth) / 2f + iconWidth + iconGap - visibleBounds.Left + shakeOffset;
                var position = new Vector2(x, y);

                if (_style.PrefixEmoteIcon && i == 0)
                {
                    var iconY = position.Y +
                                visibleBounds.Top +
                                visibleBounds.Height / 2f -
                                GetVisibleIconHeight() / 2f -
                                GetVisibleIconTop();

                    DrawEmoteIcon(
                        handle,
                        new Vector2(position.X - iconGap - iconWidth - GetVisibleIconLeft(), iconY),
                        outlineColor,
                        textColor);
                }

                DrawOutlinedString(handle, position, line, outlineColor, textColor);
                y += lineHeight;
            }
        }

        protected override void UIScaleChanged()
        {
            _layoutDirty = true;
            base.UIScaleChanged();
        }

        private void EnsureLayout()
        {
            if (!_layoutDirty)
                return;

            _layouts.Clear();

            var width = 0f;
            var height = 0f;
            foreach (var page in _pages)
            {
                var layout = LayoutPage(page);
                _layouts.Add(layout);
                width = MathF.Max(width, layout.Width);
                height = MathF.Max(height, layout.Height);
            }

            var horizontalPadding = (GetPadding() + GetHorizontalSafetyPadding()) * UIScale;
            var verticalPadding = GetPadding() * UIScale;
            _cachedSize = new Vector2(
                (width + horizontalPadding * 2f) / UIScale,
                (height + verticalPadding * 2f) / UIScale);
            _layoutDirty = false;
        }

        private RunechatPageLayout LayoutPage(string text)
        {
            var lines = new List<string>();
            var lineWidths = new List<float>();

            WrapText(text, lines, lineWidths);

            if (lines.Count == 0)
                AddLine(string.Empty, lines, lineWidths);

            var width = 0f;
            foreach (var lineWidth in lineWidths)
            {
                width = MathF.Max(width, lineWidth);
            }

            var height = GetLineHeight() * lines.Count;
            return new RunechatPageLayout(lines, lineWidths, width, height);
        }

        private void WrapText(string text, List<string> lines, List<float> lineWidths)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var line = string.Empty;

            foreach (var word in words)
            {
                if (line.Length == 0)
                {
                    AppendWordToEmptyLine(word, lines, lineWidths, ref line);
                    continue;
                }

                var candidate = $"{line} {word}";
                if (MeasureLineWidth(candidate) <= GetMaxWidth())
                {
                    line = candidate;
                    continue;
                }

                AddLine(line, lines, lineWidths);
                line = string.Empty;
                AppendWordToEmptyLine(word, lines, lineWidths, ref line);
            }

            if (line.Length > 0)
                AddLine(line, lines, lineWidths);
        }

        private void AppendWordToEmptyLine(string word, List<string> lines, List<float> lineWidths, ref string line)
        {
            if (MeasureLineWidth(word) <= GetMaxWidth())
            {
                line = word;
                return;
            }

            var builder = new StringBuilder();
            foreach (var rune in word.EnumerateRunes())
            {
                var candidate = builder.ToString() + rune;
                if (builder.Length > 0 && MeasureLineWidth(candidate) > GetMaxWidth())
                {
                    AddLine(builder.ToString(), lines, lineWidths);
                    builder.Clear();
                }

                builder.Append(rune);
            }

            line = builder.ToString();
        }

        private void AddLine(string line, List<string> lines, List<float> lineWidths)
        {
            lines.Add(line);
            lineWidths.Add(MeasureLineWidth(line));
        }

        private float GetMaxWidth()
        {
            return _style.GetScaledMaxWidth(_scale) * UIScale;
        }

        private float MeasureLineWidth(string text)
        {
            var width = 0f;

            foreach (var rune in text.EnumerateRunes())
            {
                var metrics = _font.GetCharMetrics(rune, UIScale);
                if (metrics == null)
                    continue;

                width += metrics.Value.Advance;
            }

            if (_style.UseBold && width > 0f)
                width += GetSyntheticBoldOffset();

            return width;
        }

        private (float Left, float Width, float Top, float Height) GetVisibleBounds(string text)
        {
            var cursor = 0f;
            var left = 0f;
            var right = 0f;
            var top = 0f;
            var bottom = 0f;
            var foundGlyph = false;
            var ascent = _font.GetAscent(UIScale);

            foreach (var rune in text.EnumerateRunes())
            {
                var metrics = _font.GetCharMetrics(rune, UIScale);
                if (metrics == null)
                    continue;

                var glyphLeft = cursor + metrics.Value.BearingX;
                var glyphRight = glyphLeft + metrics.Value.Width;
                var glyphTop = ascent - metrics.Value.BearingY;
                var glyphBottom = glyphTop + metrics.Value.Height;

                if (!foundGlyph)
                {
                    left = glyphLeft;
                    right = glyphRight;
                    top = glyphTop;
                    bottom = glyphBottom;
                    foundGlyph = true;
                }
                else
                {
                    left = MathF.Min(left, glyphLeft);
                    right = MathF.Max(right, glyphRight);
                    top = MathF.Min(top, glyphTop);
                    bottom = MathF.Max(bottom, glyphBottom);
                }

                cursor += metrics.Value.Advance;
            }

            if (_style.UseBold && foundGlyph)
                right += GetSyntheticBoldOffset();

            return foundGlyph
                ? (left, right - left, top, bottom - top)
                : (0f, 0f, 0f, 0f);
        }

        private float GetLineHeight()
        {
            return MathF.Max(1f, _font.GetLineHeight(UIScale) + _style.GetScaledLineHeightOffset(_scale) * UIScale);
        }

        private float GetPadding()
        {
            return _scale * 4f;
        }

        private float GetHorizontalSafetyPadding()
        {
            return _scale * 24f;
        }

        private float GetIconPixelSize()
        {
            return DefaultEmoteIconPixelSize * _scale / BaselineRunechatScale;
        }

        private float GetPanicShakeOffset()
        {
            if (!_style.UsePanicShake || _animationTime >= PanicShakeDuration)
                return 0f;

            var amount = PanicShakeSize * _scale / BaselineRunechatScale * UIScale;
            return MathF.Sin(_animationTime * MathF.PI * PanicShakeFrequency) * amount;
        }

        private float GetIconGap()
        {
            return _scale * 2f * UIScale;
        }

        private float GetSyntheticBoldOffset()
        {
            return SyntheticBoldOffset * UIScale;
        }

        private float GetVisibleIconLeft()
        {
            return EmoteIconVisibleLeft * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconWidth()
        {
            return (EmoteIconVisibleRight - EmoteIconVisibleLeft) * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconTop()
        {
            return EmoteIconVisibleTop * GetIconPixelSize() * UIScale;
        }

        private float GetVisibleIconHeight()
        {
            return (EmoteIconVisibleBottom - EmoteIconVisibleTop) * GetIconPixelSize() * UIScale;
        }

        private void DrawOutlinedString(
            DrawingHandleScreen handle,
            Vector2 position,
            string text,
            Color outlineColor,
            Color textColor)
        {
            var shadowOffset = TextShadowOffset * _scale * UIScale;
            var shadowColor = Color.Black.WithAlpha(textColor.A * TextShadowAlpha);
            var outlineOffset = _scale * UIScale;
            DrawStringPass(handle, position + new Vector2(shadowOffset, shadowOffset), text, shadowColor);

            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    DrawStringPass(handle, position + new Vector2(x * outlineOffset, y * outlineOffset), text, outlineColor);
                }
            }

            DrawStringPass(handle, position, text, textColor);
        }

        private void DrawStringPass(DrawingHandleScreen handle, Vector2 position, string text, Color color)
        {
            handle.DrawString(_font, position, text, UIScale, color);

            if (_style.UseBold)
                handle.DrawString(_font, position + new Vector2(GetSyntheticBoldOffset(), 0f), text, UIScale, color);
        }

        private void DrawEmoteIcon(
            DrawingHandleScreen handle,
            Vector2 position,
            Color outlineColor,
            Color textColor)
        {
            var scale = MathF.Max(1f, GetIconPixelSize() * UIScale);
            var iconOrigin = position;

            var iconAlpha = textColor.A * EmoteIconAlpha;

            for (var y = 0; y < EmoteIcon.Length; y++)
            {
                for (var x = 0; x < EmoteIcon[y].Length; x++)
                {
                    var color = EmoteIcon[y][x] switch
                    {
                        '#' => Color.Black.WithAlpha(iconAlpha),
                        'B' => EmoteIconBlue.WithAlpha(iconAlpha),
                        _ => (Color?) null,
                    };

                    if (color is { } iconColor)
                        DrawIconPixel(handle, iconOrigin, x, y, scale, iconColor);
                }
            }
        }

        private static void DrawIconPixel(
            DrawingHandleScreen handle,
            Vector2 iconOrigin,
            int x,
            int y,
            float scale,
            Color color)
        {
            var position = iconOrigin + new Vector2(x * scale, y * scale);
            var size = new Vector2(scale, scale);
            handle.DrawRect(UIBox2.FromDimensions(position, size), color);
        }

        private Font LoadRunechatFont(int size, bool italic)
        {
            size = Math.Max(1, size);

            if (!SmallFontsLoadFailed && TryGetSmallFontsFace(italic) is { } face)
            {
                try
                {
                    return face.Load(size);
                }
                catch
                {
                    SmallFontsLoadFailed = true;
                }
            }

            return _resourceCache.GetFont(italic ? FallbackItalicFontPath : FallbackFontPath, size);
        }

        private ISystemFontFace? TryGetSmallFontsFace(bool italic)
        {
            if (!_systemFontManager.IsSupported)
                return null;

            ISystemFontFace? regularFallback = null;

            foreach (var face in _systemFontManager.SystemFontFaces)
            {
                if (!IsSmallFontsFace(face))
                    continue;

                if (italic && face.Slant != FontSlant.Normal)
                    return face;

                if (!italic && face.Weight == FontWeight.Regular && face.Slant == FontSlant.Normal)
                    return face;

                if (face.Slant == FontSlant.Normal)
                    regularFallback ??= face;
            }

            return italic
                ? null
                : regularFallback;
        }

        private static bool IsSmallFontsFace(ISystemFontFace face)
        {
            return IsSmallFontsName(face.FamilyName) ||
                   IsSmallFontsName(face.FullName) ||
                   IsSmallFontsName(face.GetLocalizedFamilyName(CultureInfo.InvariantCulture)) ||
                   IsSmallFontsName(face.GetLocalizedFullName(CultureInfo.InvariantCulture)) ||
                   IsSmallFontsName(face.GetLocalizedFamilyName(EnUsCulture)) ||
                   IsSmallFontsName(face.GetLocalizedFullName(EnUsCulture));
        }

        private static bool IsSmallFontsName(string name)
        {
            return name.Equals(SmallFontsFamily, StringComparison.OrdinalIgnoreCase) ||
                   name.Equals(SmallFonts120Family, StringComparison.OrdinalIgnoreCase);
        }

        private sealed record RunechatPageLayout(
            IReadOnlyList<string> Lines,
            IReadOnlyList<float> LineWidths,
            float Width,
            float Height);
    }
}
