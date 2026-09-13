using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client._CMU14.UserInterface.ColorPicker;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets
{
    /// <summary>
    ///     The CRT terminal theme, split out of <see cref="StyleNano"/> so that upstream merges
    ///     touch one file and this theme touches another. Everything here is fork-local: the
    ///     palette, the style classes, the styleboxes and the rules that bind them, plus the
    ///     base-mode fallbacks that only exist because the theme can be switched off
    ///     (<see cref="StyleClassNanoSliderValue"/>, <see cref="StyleClassButtonToggleRed"/>).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The rules built here are spliced into <see cref="StyleNano"/>'s rule list at the exact
    ///     position they used to occupy. Order is not cosmetic: <see cref="Stylesheet"/> records an
    ///     insertion index per rule and uses it to break specificity ties, so moving the block
    ///     would silently change which rule wins for any pair of equal-specificity selectors.
    ///     </para>
    ///     <para>
    ///     Fonts are rebuilt from the resource cache rather than passed in from the constructor.
    ///     The cache hands back the same font object for the same stack and size, so this costs
    ///     nothing and keeps the two files from sharing locals.
    ///     </para>
    /// </remarks>
    public sealed partial class StyleNano
    {
        public const string StyleClassCrtWindow = "CrtWindow";
        public const string StyleClassCrtWindowHeader = "CrtWindowHeader";
        public const string StyleClassCrtWindowTitle = "CrtWindowTitle";
        public const string StyleClassCrtPanel = "CrtPanel";

        /// <summary>
        ///     A plain, borderless flat fill in <see cref="CrtPanelBackground"/> - no corner ticks,
        ///     no padding, none of CrtStyleBox's other trimmings. For backing a control that has no
        ///     background of its own (so it doesn't show black through its gaps) and needs to stay
        ///     invisible against a CrtPanel behind it. A style class rather than a hand-set
        ///     StyleBoxFlat: reading CrtPanelBackground once and storing the result is a snapshot
        ///     that never updates when the stylesheet later rebuilds, whereas a class-based rule
        ///     re-resolves every time. That distinction is why ServerInfoBacking, built the
        ///     snapshot way, briefly carried the CRT-enabled colour on a base-mode client - the
        ///     value it read had not yet been corrected from the class default when it ran.
        /// </summary>
        public const string StyleClassCrtPanelFill = "CrtPanelFill";
        public const string StyleClassCrtPanelTicked = "CrtPanelTicked";

        /// <summary>
        ///     A whole screen: the tube's own surface, with no border and no corner ticks. For the
        ///     outermost container of a CRT-themed region, where <see cref="StyleClassCrtPanel"/>
        ///     would draw a frame around everything inside it - which reads as a sidebar box rather
        ///     than as a screen. Unlike CrtPanel it has no content margins: an inset child leaves the
        ///     panel's own fill visible as a ring around it, which reads as a border even though
        ///     nothing is stroked - that ring was the "border" on this panel, not a border at all.
        ///     Children are expected to carry their own padding.
        /// </summary>
        public const string StyleClassCrtScreenPanel = "CrtScreenPanel";
        public const string StyleClassCrtInsetPanel = "CrtInsetPanel";
        public const string StyleClassCrtQuietPanel = "CrtQuietPanel";
        public const string StyleClassCrtHeaderPanel = "CrtHeaderPanel";
        /// <summary>
        ///     The ground a <see cref="StyleClassCrtCommandCell"/> row sits on. Filled with
        ///     <c>Void</c>, so the 1px separations between cells show through as gaps rather than as
        ///     borders - the cells are told apart by the dark line between two fills, and nothing is
        ///     stroked.
        /// </summary>
        public const string StyleClassCrtCommandBand = "CrtCommandBand";

        /// <summary>
        ///     One command in a band. Replaces a row of individually outlined buttons: fill says
        ///     pressable, the gap says separate, and four frames come off the panel.
        /// </summary>
        public const string StyleClassCrtCommandCell = "CrtCommandCell";

        /// <summary>
        ///     A cell in a command band that is not itself a <see cref="ContainerButton"/> - the
        ///     collapse arrow. Resting fill only; it has no states to show.
        /// </summary>
        public const string StyleClassCrtCommandCellPanel = "CrtCommandCellPanel";

        public const string StyleClassCrtButton = "CrtButton";
        public const string StyleClassCrtAttentionButton = "CrtAttentionButton";
        public const string StyleClassCrtButtonLabel = "CrtButtonLabel";
        public const string StyleClassCrtNativeButtonLabel = "CrtNativeButtonLabel";
        public const string StyleClassCrtText = "CrtText";
        public const string StyleClassCrtDimText = "CrtDimText";
        public const string StyleClassCrtHeading = "CrtHeading";

        /// <summary>
        ///     A heading that shares its row with something else, so it has to give ground. The
        ///     sidebar's server name is the case: operator-configured text of no bounded length next
        ///     to a round-state readout, where the name is the half that clips.
        /// </summary>
        public const string StyleClassCrtHeadingSmall = "CrtHeadingSmall";

        /// <summary>
        ///     Right-hand end of a heading row - a status readout beside a title, dim and small so it
        ///     is legible without competing with the heading it sits next to.
        /// </summary>
        public const string StyleClassCrtHeadingStatus = "CrtHeadingStatus";

        public const string StyleClassCrtHeadingBig = "CrtHeadingBig";
        public const string StyleClassCrtHeadingBigWarning = "CrtHeadingBigWarning";

        /// <summary>
        ///     The lobby clock face. Its own size step rather than a bigger heading: a heading shares
        ///     a panel with body text and has to stay in proportion to it, whereas this is read from
        ///     across the screen and has nothing beside it to keep in scale with.
        /// </summary>
        public const string StyleClassCrtClock = "CrtClock";

        public const string StyleClassCrtClockWarning = "CrtClockWarning";
        public const string StyleClassCrtClockDanger = "CrtClockDanger";

        /// <summary>
        ///     The ready toggle, which has to say which of two states it is in rather than merely
        ///     look pressable. Deliberately not a <see cref="StyleClassCrtButton"/> variant - both
        ///     rules would match the same control at the same specificity, which has no defined
        ///     winner, so <c>CrtLobbyTheme</c> withholds the button class from anything wearing this.
        /// </summary>
        public const string StyleClassCrtReadyToggle = "CrtReadyToggle";

        /// <summary>
        ///     The ready toggle while it is on. A second class rather than a Pressed rule on the
        ///     first: the toggle carries exactly one of the two at a time, so only one rule can ever
        ///     match it. The pseudo-class version of this rendered both states identically - the
        ///     bare class rule and the Pressed rule matched the same control at the same
        ///     specificity, and that has no defined winner.
        /// </summary>
        public const string StyleClassCrtReadyToggleOn = "CrtReadyToggleOn";
        public const string StyleClassCrtHeadingBigDanger = "CrtHeadingBigDanger";
        public const string StyleClassCrtRichText = "CrtRichText";
        public const string StyleClassCrtServerInfoText = "CrtServerInfoText";
        public const string StyleClassCrtTableCell = "CrtTableCell";
        public const string StyleClassCrtUnderlineRow = "CrtUnderlineRow";
        public const string StyleClassCrtCharacterSummary = "CrtCharacterSummary";

        /// <summary>
        ///     A block heading - SERVER INFO, CHARACTER. Its own class because off-theme it wants to
        ///     be a heading and under CRT it wants to stay the small caps strip it already is: these
        ///     were sharing <see cref="StyleClassCrtFieldLabel"/> with the field names inside the
        ///     block, which off-theme left a section title the same size as the labels beneath it.
        /// </summary>
        public const string StyleClassCrtSectionTitle = "CrtSectionTitle";

        /// <summary>
        ///     The hairline that runs from a section title to the right edge of its row. A rule
        ///     rather than a fill because it divides nothing - it is the oldest terminal device
        ///     there is, and it is what stops a heading reading as another line of body text.
        ///     Give it <c>HorizontalExpand</c> and <c>MinHeight="1"</c>; it has no content to
        ///     size itself from.
        /// </summary>
        public const string StyleClassCrtSectionRule = "CrtSectionRule";
        public const string StyleClassCrtDivider = "CrtDivider";
        public const string StyleClassCrtChatPanel = "CrtChatPanel";

        /// <summary>
        ///     A chat channel tab. Its own class rather than <see cref="StyleClassCrtButton"/>: a tab
        ///     is a selector, not an action, and the generic pressed-button fill made the active tab
        ///     the brightest thing in the panel. Selection is a one-step lift on the surface ladder
        ///     instead - Surface1 resting, Surface3 selected.
        /// </summary>
        public const string StyleClassCrtChatTab = "CrtChatTab";

        /// <summary>
        ///     The active channel tab. A real style class alongside the pressed pseudo-class, not
        ///     instead of it. <see cref="Robust.Client.UserInterface.Controls.BaseButton.DrawMode"/>
        ///     reports Pressed both for a button with <c>Pressed</c> set and for one with a click
        ///     merely held on it, so the pseudo-class alone cannot tell a selected tab from a tab
        ///     being clicked. This class carries the selection; the pseudo-class carries the input.
        /// </summary>
        public const string StyleClassCrtChatTabSelected = "CrtChatTabSelected";
        public const string StyleClassCrtChatInput = "CrtChatInput";

        /// <summary>
        ///     AHelp/MentorHelp's own chat input band - a filled row with a rule on the top and
        ///     bottom edges, rather than an unstyled wrapper with no visible background or border at
        ///     all. Not <see cref="StyleClassCrtChatInput"/>: that one is borderless by design (the
        ///     lobby chat's seam rule already marks the boundary above it), and these windows have no
        ///     such rule to lean on.
        /// </summary>
        public const string StyleClassCrtBwoinkInput = "CrtBwoinkInput";

        /// <summary>
        ///     A chat message body. Its own class rather than <see cref="StyleClassCrtRichText"/>,
        ///     which is shared with the guidebook and the lobby's text blocks: chat has a readable-font
        ///     accessibility option (<see cref="CCVars.CMUChatReadableFont"/>) and those must not
        ///     follow it. <see cref="RichTextLabel"/> takes its font only from the stylesheet, so this
        ///     class is the only way to reach a message body's typeface at all.
        /// </summary>
        public const string StyleClassCrtChatText = "CrtChatText";

        /// <summary>Announcement body. Two points up from <see cref="StyleClassCrtChatText"/>.</summary>
        public const string StyleClassCrtChatAnnouncementText = "CrtChatAnnouncementText";

        /// <summary>
        ///     The channel chip at the left of the input row ("OOC", "SAY", a radio name). Its own
        ///     class rather than <see cref="StyleClassCrtButton"/>: a CRT button is sized to be
        ///     pressed, and at that size a three-letter chip became a slab filling most of the bar's
        ///     height, which is what made it read as a box inside a box. The gallery draws it as a
        ///     snug label - Surface2, accent text, 10x3 padding - so this matches those values.
        /// </summary>
        public const string StyleClassCrtChatChannelChip = "CrtChatChannelChip";
        public const string StyleClassCrtChatScrollBar = "CrtChatScrollBar";
        public const string StyleClassCrtChatPopup = "CrtChatPopup";
        public const string StyleClassCrtCheckBox = "CrtCheckBox";
        public const string StyleClassCrtFooterRow = "CrtFooterRow";
        public const string StyleClassCrtSectionHeader = "CrtSectionHeader";
        public const string StyleClassCrtSliderValue = "CrtSliderValue";
        public const string StyleClassCrtOptionRow = "CrtOptionRow";

        /// <summary>
        ///     Base/NanoUI variant of <see cref="StyleClassCrtSliderValue"/> - see the stylebox
        ///     comment where it is built for why the two cannot share one class.
        /// </summary>
        public const string StyleClassNanoSliderValue = "NanoSliderValue";

        /// <summary>
        ///     Text inside a round-info table cell (<see cref="StyleClassCrtTableCell"/>). Its own
        ///     class rather than reusing <see cref="StyleClassCrtText"/> so the base-mode font can be
        ///     sized down for this one dense table without shrinking every other CrtText label in the
        ///     base UI.
        /// </summary>
        public const string StyleClassCrtTableCellText = "CrtTableCellText";

        /// <summary>
        ///     Banded variant of <see cref="StyleClassCrtTableCell"/>. Applied to alternating rows so
        ///     a borderless table stays trackable across its width.
        /// </summary>
        public const string StyleClassCrtTableCellAlt = "CrtTableCellAlt";

        /// <summary>
        ///     The lead pair of cells in a round-info table, on the lightest surface in the ladder.
        /// </summary>
        public const string StyleClassCrtTableCellLead = "CrtTableCellLead";

        /// <summary>
        ///     The name half of a round-info cell. Separate from
        ///     <see cref="StyleClassCrtFieldValue"/> so the two can differ in size - sharing one class
        ///     is what previously forced label and value to the same size and left dimness as the only
        ///     way to tell them apart.
        /// </summary>
        public const string StyleClassCrtFieldLabel = "CrtFieldLabel";

        public const string StyleClassCrtFieldValue = "CrtFieldValue";

        /// <summary>
        ///     A value shown inline in a heading row rather than in a cell of its own. Smaller than
        ///     <see cref="StyleClassCrtFieldValue"/>: a cell value is the only thing in its box and can
        ///     carry size, whereas these sit in a row beside a section heading and compete with it.
        /// </summary>
        public const string StyleClassCrtStatValue = "CrtStatValue";

        /// <summary>Value in a <see cref="StyleClassCrtTableCellLead"/> cell - one size up.</summary>
        public const string StyleClassCrtFieldValueLead = "CrtFieldValueLead";

        /// <summary>
        ///     Turns a toggled <c>Button</c> solid red on the base theme. The CRT theme already
        ///     themes every button consistently through <see cref="StyleClassCrtButton"/>, so this
        ///     is applied only when that theme is off - see call sites.
        /// </summary>
        public const string StyleClassButtonToggleRed = "ButtonToggleRed";

        /// <summary>
        ///     The CRT font stack, with Noto fallbacks for glyphs the OSD font lacks.
        /// </summary>
        public static readonly string[] UavOsdFontStack =
        {
            "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf",
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
        };

        /// <summary>
        ///     For controls that need a CRT font outside the stylesheet, e.g. via FontOverride to
        ///     beat a stylesheet rule of equal specificity.
        /// </summary>
        public static Font GetCrtFont(IResourceCache resCache, int size)
        {
            return resCache.GetFont(UavOsdFontStack, size);
        }

        /// <summary>
        ///     Chat's face under the CRT theme, and the one place the OSD font is deliberately not
        ///     used.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///     UAV-OSD is an all-caps face, which suits labels, headings and readouts - short, fixed
        ///     strings. Chat is prose written by players: in caps it loses the case out of names, reads
        ///     as shouting, and gives up the ascenders and descenders that make a wall of text
        ///     scannable.
        ///     </para>
        ///     <para>
        ///     RobotoMono rather than NotoSans, because monospace keeps the terminal character the
        ///     theme is built on - this should read as a different terminal face, not as an escape from
        ///     the theme. The readable-chat option already exists for that, and this is not it. The
        ///     Noto entries are the same glyph fallbacks <see cref="UavOsdFontStack"/> carries.
        ///     </para>
        /// </remarks>
        public static readonly string[] CrtChatFontStack =
        {
            "/Fonts/RobotoMono/RobotoMono-Regular.ttf",
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
        };

        public static Font GetCrtChatFont(IResourceCache resCache, int size)
        {
            return resCache.GetFont(CrtChatFontStack, size);
        }

        public const string StyleClassCrtLineEdit = "CrtLineEdit";
        public const string StyleClassCrtNativeLineEdit = "CrtNativeLineEdit";
        public const string StyleClassCrtSlider = "CrtSlider";
        public const string StyleClassCrtProgressBar = "CrtProgressBar";
        public const string StyleClassCrtTabContainer = "CrtTabContainer";
        public const string StyleClassCrtStripeBack = "CrtStripeBack";
        public const string StyleClassCrtIconButton = "CrtIconButton";
        public const string StyleClassCrtItemList = "CrtItemList";
        public const string StyleClassCrtScrollBar = "CrtScrollBar";

        private static CrtPalette _crtPalette = CrtPalette.Green;
        private static bool _crtUiEnabled = true;
        private static bool _chatReadableFont;
        private static int _chatFontStep;
        private static readonly Color DefaultCrtBackground = Color.FromHex("#07090B");
        private static readonly Color DefaultCrtPanelBackground = Color.FromHex("#25252A");
        private static readonly Color DefaultCrtPanelBackgroundAlt = Color.FromHex("#202023");
        private static Color DefaultCrtInsetBackground => PanelDark;
        private static readonly Color DefaultCrtHeaderBackground = Color.FromHex("#2F3035");
        private static readonly Color DefaultCrtButtonBackground = Color.FromHex("#464966");
        private static readonly Color DefaultCrtButtonHoverBackground = Color.FromHex("#565A78");
        private static readonly Color DefaultCrtButtonPressedBackground = Color.FromHex("#383B52");
        private static readonly Color DefaultCrtButtonDisabledBackground = Color.FromHex("#252734");
        private static readonly Color DefaultCrtSliderForeground = Color.FromHex("#5B5E77");
        private static readonly Color DefaultCrtItemBackground = Color.FromHex("#202028");
        private static readonly Color DefaultCrtItemSelectedBackground = Color.FromHex("#373744");
        private static readonly Color DefaultCrtItemDisabledBackground = Color.FromHex("#202024");
        private static readonly Color DefaultCrtDim = Color.FromHex("#9A9A9A");
        private static readonly Color DefaultCrtDisabled = Color.FromHex("#5A5A5A");

        public static bool CrtUiEnabled => _crtUiEnabled;

        public static Color CrtBackground => _crtUiEnabled ? _crtPalette.Background : DefaultCrtBackground;
        public static Color CrtPanelBackground => _crtUiEnabled ? _crtPalette.PanelBackground : DefaultCrtPanelBackground;
        public static Color CrtPanelBackgroundAlt => _crtUiEnabled ? _crtPalette.PanelBackgroundAlt : DefaultCrtPanelBackgroundAlt;
        public static Color CrtInsetBackground => _crtUiEnabled ? _crtPalette.InsetBackground : DefaultCrtInsetBackground;
        public static Color CrtHeaderBackground => _crtUiEnabled ? _crtPalette.HeaderBackground : DefaultCrtHeaderBackground;
        public static Color CrtButtonBackground => _crtUiEnabled ? _crtPalette.ButtonBackground : DefaultCrtButtonBackground;
        public static Color CrtButtonHoverBackground => _crtUiEnabled ? _crtPalette.ButtonHoverBackground : DefaultCrtButtonHoverBackground;
        public static Color CrtButtonPressedBackground => _crtUiEnabled ? _crtPalette.ButtonPressedBackground : DefaultCrtButtonPressedBackground;
        public static Color CrtButtonDisabledBackground => _crtUiEnabled ? _crtPalette.ButtonDisabledBackground : DefaultCrtButtonDisabledBackground;
        public static Color CrtSliderForeground => _crtUiEnabled ? _crtPalette.SliderForeground : DefaultCrtSliderForeground;
        public static Color CrtProgressForeground => _crtUiEnabled ? _crtPalette.ProgressForeground : DefaultCrtSliderForeground;
        public static Color CrtItemBackground => _crtUiEnabled ? _crtPalette.ItemBackground : DefaultCrtItemBackground;
        public static Color CrtItemSelectedBackground => _crtUiEnabled ? _crtPalette.ItemSelectedBackground : DefaultCrtItemSelectedBackground;
        public static Color CrtItemDisabledBackground => _crtUiEnabled ? _crtPalette.ItemDisabledBackground : DefaultCrtItemDisabledBackground;
        public static Color CrtGreen => _crtUiEnabled ? _crtPalette.Accent : NanoGold;
        public static Color CrtGreenDim => _crtUiEnabled ? _crtPalette.AccentDim : DefaultCrtDim;
        public static Color CrtGreenSoft => _crtUiEnabled ? _crtPalette.AccentSoft : Color.White;
        public static Color CrtGreenDisabled => _crtUiEnabled ? _crtPalette.AccentDisabled : DefaultCrtDisabled;
        /// <summary>
        ///     Semantic colours for the CRT theme. The palette derives all eighteen of its colours
        ///     from a single hue, so on its own it cannot say "warning" or "danger" - a fill can only
        ///     ever be the theme colour, brighter. These borrow the Orange and Red presets' own
        ///     accents rather than a hand-picked hex, so they already sit at the luminance an accent
        ///     needs against a dark phosphor background and stay legible under any preset.
        ///     Known limit: under the Red preset, danger and accent coincide and the signal flattens.
        /// </summary>
        public static Color CrtWarning => _crtUiEnabled ? CrtPalette.Orange.Accent : ConcerningOrangeFore;
        public static Color CrtDanger => _crtUiEnabled ? CrtPalette.Red.Accent : DangerousRedFore;

        public static void SetCrtPalette(string palette)
        {
            _crtPalette = palette switch
            {
                CCVars.CrtUiColorGreen => CrtPalette.Green,
                CCVars.CrtUiColorBlue => CrtPalette.Blue,
                CCVars.CrtUiColorOrange => CrtPalette.Orange,
                CCVars.CrtUiColorRed => CrtPalette.Red,
                CCVars.CrtUiColorPurple => CrtPalette.Purple,
                _ => Color.TryFromHex(palette, out var color)
                    ? CrtPalette.FromAccent(color)
                    : CrtPalette.Green,
            };
        }

        public static void SetCrtUiEnabled(bool enabled)
        {
            _crtUiEnabled = enabled;
        }

        /// <summary>
        ///     Chat is drawn in a plain proportional face rather than the terminal one. Accessibility
        ///     option; see <see cref="CCVars.CMUChatReadableFont"/> for why it exists.
        /// </summary>
        public static bool ChatReadableFont => _chatReadableFont;

        public static void SetChatReadableFont(bool enabled)
        {
            _chatReadableFont = enabled;
        }

        /// <summary>
        ///     Points added to chat's normal size by <see cref="CCVars.CMUChatBigFont"/>. 0, 1 or 2.
        /// </summary>
        public static int ChatFontStep => _chatFontStep;

        public static void SetChatFontStep(int step)
        {
            _chatFontStep = Math.Clamp(step, 0, ChatBigFontMaxStep);
        }

        /// <summary>
        ///     The <see cref="CCVars.CMUChatBigFont"/> cvar's string as a point step. An unrecognised
        ///     value reads as off rather than throwing, so a stale client_config costs only the default.
        /// </summary>
        public static int ParseChatFontStep(string setting)
        {
            return setting switch
            {
                CCVars.CMUChatBigFontOne => 1,
                CCVars.CMUChatBigFontTwo => 2,
                _ => 0,
            };
        }

        /// <summary>
        ///     Base point size chat uses in terminal mode. Must match <c>crtChatFont</c>.
        /// </summary>
        /// <remarks>
        ///     Larger than the 8 the OSD face used here. That face is all-caps, so every glyph filled
        ///     the full cap height; a face with lowercase spends roughly half its point size on the
        ///     x-height, so the same number reads smaller and 8 came out under what it replaced.
        /// </remarks>
        public const int ChatCrtFontSizeBase = 11;

        /// <summary>
        ///     Base point size chat uses in readable mode. Larger than the terminal size on purpose:
        ///     what suits a dense all-caps face is small for a proportional one.
        /// </summary>
        public const int ChatReadableFontSizeBase = 12;

        /// <summary>Largest step the big-font option offers, applied to whichever face is in use.</summary>
        public const int ChatBigFontMaxStep = 2;

        /// <summary>Terminal-mode chat size as it stands right now, big-font option included.</summary>
        public static int ChatCrtFontSize => ChatCrtFontSizeBase + _chatFontStep;

        /// <summary>Readable-mode chat size as it stands right now, big-font option included.</summary>
        public static int ChatReadableFontSize => ChatReadableFontSizeBase + _chatFontStep;

        /// <summary>
        ///     The font chat should use right now. For the controls that set a FontOverride directly
        ///     rather than taking one from a rule - anything whose stylesheet rule would only tie on
        ///     specificity.
        /// </summary>
        public static Font GetChatFont(IResourceCache resCache)
        {
            return _chatReadableFont
                ? resCache.NotoStack(size: ChatReadableFontSize)
                : GetCrtChatFont(resCache, ChatCrtFontSize);
        }

        private sealed class CrtPalette
        {
            public static readonly CrtPalette Green = new(
                "#000906",
                "#02130B",
                "#032314",
                "#000E08",
                "#003B1C",
                "#001D0E",
                "#003B1C",
                "#075E2D",
                "#041109",
                "#002412",
                "#0A4B28",
                "#00130A",
                "#0A3B20",
                "#020805",
                "#46FF8E",
                "#0D7E43",
                "#B0FFC8",
                "#12351F");

            public static readonly CrtPalette Blue = new(
                "#00070D",
                "#061221",
                "#0A1D32",
                "#020C15",
                "#073251",
                "#041A2A",
                "#073A5C",
                "#0E5D8E",
                "#05111A",
                "#061F30",
                "#0B4567",
                "#061728",
                "#0C3551",
                "#02070B",
                "#58CCFF",
                "#126A91",
                "#B9ECFF",
                "#123042");

            public static readonly CrtPalette Orange = new(
                "#0B0500",
                "#160B02",
                "#281404",
                "#130800",
                "#4A2605",
                "#241000",
                "#54300A",
                "#895018",
                "#140A02",
                "#2D1402",
                "#70420E",
                "#1A0B00",
                "#4B2A08",
                "#090400",
                "#FFB454",
                "#9B5A12",
                "#FFD8A6",
                "#3C2410");

            public static readonly CrtPalette Red = new(
                "#0B0000",
                "#170303",
                "#2A0607",
                "#120101",
                "#4A070A",
                "#230203",
                "#560B0F",
                "#8E1820",
                "#140303",
                "#2C0508",
                "#6B1017",
                "#1A0203",
                "#4C0B10",
                "#080101",
                "#FF4E5E",
                "#9A1723",
                "#FFC3CA",
                "#3A1115");

            public static readonly CrtPalette Purple = new(
                "#07000D",
                "#12041F",
                "#210832",
                "#0C0214",
                "#310750",
                "#190326",
                "#3A0B5E",
                "#5F1790",
                "#100318",
                "#200730",
                "#4B0F6D",
                "#150320",
                "#350B4F",
                "#050109",
                "#C45BFF",
                "#6F1D99",
                "#E8C5FF",
                "#2E143F");

            public readonly Color Background;
            public readonly Color PanelBackground;
            public readonly Color PanelBackgroundAlt;
            public readonly Color InsetBackground;
            public readonly Color HeaderBackground;
            public readonly Color ButtonBackground;
            public readonly Color ButtonHoverBackground;
            public readonly Color ButtonPressedBackground;
            public readonly Color ButtonDisabledBackground;
            public readonly Color SliderForeground;
            public readonly Color ProgressForeground;
            public readonly Color ItemBackground;
            public readonly Color ItemSelectedBackground;
            public readonly Color ItemDisabledBackground;
            public readonly Color Accent;
            public readonly Color AccentDim;
            public readonly Color AccentSoft;
            public readonly Color AccentDisabled;

            public static CrtPalette FromAccent(Color accent)
            {
                var hsv = Color.ToHsv(accent);
                var hue = hsv.X;
                var saturation = Clamp(hsv.Y, 0.05f, 1f);
                var value = Clamp(hsv.Z, 0.55f, 1f);
                var backgroundSaturation = Clamp(saturation * 0.85f, 0.02f, 0.85f);

                Color Hsv(float sat, float val)
                {
                    return Color.FromHsv(new Vector4(
                        hue,
                        Clamp(sat, 0f, 1f),
                        Clamp(val, 0f, 1f),
                        1f));
                }

                return new CrtPalette(
                    Hsv(backgroundSaturation, 0.04f),
                    Hsv(backgroundSaturation, 0.075f),
                    Hsv(backgroundSaturation, 0.135f),
                    Hsv(backgroundSaturation, 0.055f),
                    Hsv(saturation, 0.23f),
                    Hsv(saturation, 0.115f),
                    Hsv(saturation, 0.23f),
                    Hsv(saturation, 0.37f),
                    Hsv(backgroundSaturation, 0.07f),
                    Hsv(saturation, 0.14f),
                    Hsv(saturation, 0.30f),
                    Hsv(saturation, 0.08f),
                    Hsv(saturation, 0.23f),
                    Hsv(backgroundSaturation, 0.035f),
                    Hsv(saturation, value),
                    Hsv(saturation, value * 0.50f),
                    Hsv(saturation * 0.30f, 1f),
                    Hsv(saturation * 0.60f, 0.21f));
            }

            private CrtPalette(
                string background,
                string panelBackground,
                string panelBackgroundAlt,
                string insetBackground,
                string headerBackground,
                string buttonBackground,
                string buttonHoverBackground,
                string buttonPressedBackground,
                string buttonDisabledBackground,
                string sliderForeground,
                string progressForeground,
                string itemBackground,
                string itemSelectedBackground,
                string itemDisabledBackground,
                string accent,
                string accentDim,
                string accentSoft,
                string accentDisabled)
            {
                Background = Color.FromHex(background);
                PanelBackground = Color.FromHex(panelBackground);
                PanelBackgroundAlt = Color.FromHex(panelBackgroundAlt);
                InsetBackground = Color.FromHex(insetBackground);
                HeaderBackground = Color.FromHex(headerBackground);
                ButtonBackground = Color.FromHex(buttonBackground);
                ButtonHoverBackground = Color.FromHex(buttonHoverBackground);
                ButtonPressedBackground = Color.FromHex(buttonPressedBackground);
                ButtonDisabledBackground = Color.FromHex(buttonDisabledBackground);
                SliderForeground = Color.FromHex(sliderForeground);
                ProgressForeground = Color.FromHex(progressForeground);
                ItemBackground = Color.FromHex(itemBackground);
                ItemSelectedBackground = Color.FromHex(itemSelectedBackground);
                ItemDisabledBackground = Color.FromHex(itemDisabledBackground);
                Accent = Color.FromHex(accent);
                AccentDim = Color.FromHex(accentDim);
                AccentSoft = Color.FromHex(accentSoft);
                AccentDisabled = Color.FromHex(accentDisabled);
            }

            private CrtPalette(
                Color background,
                Color panelBackground,
                Color panelBackgroundAlt,
                Color insetBackground,
                Color headerBackground,
                Color buttonBackground,
                Color buttonHoverBackground,
                Color buttonPressedBackground,
                Color buttonDisabledBackground,
                Color sliderForeground,
                Color progressForeground,
                Color itemBackground,
                Color itemSelectedBackground,
                Color itemDisabledBackground,
                Color accent,
                Color accentDim,
                Color accentSoft,
                Color accentDisabled)
            {
                Background = background;
                PanelBackground = panelBackground;
                PanelBackgroundAlt = panelBackgroundAlt;
                InsetBackground = insetBackground;
                HeaderBackground = headerBackground;
                ButtonBackground = buttonBackground;
                ButtonHoverBackground = buttonHoverBackground;
                ButtonPressedBackground = buttonPressedBackground;
                ButtonDisabledBackground = buttonDisabledBackground;
                SliderForeground = sliderForeground;
                ProgressForeground = progressForeground;
                ItemBackground = itemBackground;
                ItemSelectedBackground = itemSelectedBackground;
                ItemDisabledBackground = itemDisabledBackground;
                Accent = accent;
                AccentDim = accentDim;
                AccentSoft = accentSoft;
                AccentDisabled = accentDisabled;
            }

            private static float Clamp(float value, float min, float max)
            {
                return Math.Min(Math.Max(value, min), max);
            }
        }

        /// <summary>
        ///     The handful of CRT styleboxes that base-theme rules also reach for, handed back
        ///     from <see cref="BuildCrtRules"/> so those rules can stay where they are.
        /// </summary>
        private readonly record struct CrtShared(
            StyleBox WindowPanel,
            StyleBox WindowHeader,
            StyleBox InsetPanel,
            Color TextColor);

        /// <summary>
        ///     Builds every CRT-theme stylebox and the rules that use them.
        /// </summary>
        private static StyleRule[] BuildCrtRules(IResourceCache resCache, out CrtShared shared)
        {
            var notoSans10 = resCache.NotoStack(size: 10);
            var notoSans12 = resCache.NotoStack(size: 12);
            var notoSansBold12 = resCache.NotoStack(variation: "Bold", size: 12);
            var notoSansBold16 = resCache.NotoStack(variation: "Bold", size: 16);
            var notoSansBold18 = resCache.NotoStack(variation: "Bold", size: 18);
            var notoSansBold28 = resCache.NotoStack(variation: "Bold", size: 28);
            var notoSansBold11 = resCache.NotoStack(variation: "Bold", size: 11);
            var uavOsdStack = UavOsdFontStack;
            var uavOsd13 = GetCrtFont(resCache, 8);
            var uavOsd14 = GetCrtFont(resCache, 8);
            var uavOsdBold14 = GetCrtFont(resCache, 8);
            var uavOsdBold16 = GetCrtFont(resCache, 10);
            var uavOsdBold18 = GetCrtFont(resCache, 12);
            // NOTE: the uavOsd* names above are misleading - uavOsd13/uavOsd14/uavOsdBold14 are all
            // actually size 8. This one backs the lobby's intro lines. It shares a row with the
            // SERVER INFO heading, so it has to stay small enough that the welcome line does not
            // wrap - raise it only if you also give that row more width.
            var uavOsdServerInfo = GetCrtFont(resCache, 8);
            var useCrtUi = CrtUiEnabled;
            var crtTextFont = useCrtUi ? uavOsdBold14 : notoSans12;
            // The round-info table (GOVFOR SHIP, PLANET, ...) reported too big in base mode at the
            // shared 12px CrtText size, then too thin once dropped to plain 10px regular - a
            // dense, all-caps-heading table wants weight, not just a smaller size. Bold 11px is the
            // middle ground. CRT keeps its own shared size untouched either way.
            var crtTableCellFont = useCrtUi ? crtTextFont : notoSansBold11;

            // The round-info field pair. The OSD font's nominal sizes run small - the existing
            // uavOsd* locals are all size 8 despite their names - so these are picked by eye against
            // the rest of the panel rather than scaled off crtTextFont.
            var crtFieldLabelFont = useCrtUi ? GetCrtFont(resCache, 8) : notoSansBold11;

            // Same as the field label under CRT, deliberately - the terminal look leans on the caps
            // strip rather than on size, and it is already right. Off-theme it becomes a real
            // heading.
            var crtSectionTitleFont = useCrtUi ? GetCrtFont(resCache, 8) : notoSansBold16;
            var crtFieldValueFont = useCrtUi ? GetCrtFont(resCache, 11) : notoSans12;
            var crtFieldValueLeadFont = useCrtUi ? GetCrtFont(resCache, 10) : notoSansBold16;

            // Muted, not dim. CrtGreenDim is the theme's "switched off" tone and is too dark to read
            // at label size; this sits between it and the body colour.
            var crtFieldLabelColor = useCrtUi
                ? Color.InterpolateBetween(CrtGreenDim, CrtGreenSoft, 0.45f)
                : Color.FromHex("#B8B8B8");
            var crtFieldValueColor = useCrtUi ? CrtGreenSoft : Color.White;
            var crtStatValueFont = useCrtUi ? GetCrtFont(resCache, 9) : notoSansBold11;
            var crtDimFont = useCrtUi ? uavOsd13 : notoSans10;
            var crtHeadingFont = useCrtUi ? uavOsdBold16 : notoSansBold12;
            // Half a step down from crtHeadingFont's 10, for a heading that shares its row. Written as
            // a size rather than reusing uavOsdBold14, which is really 8 - the same size as body text,
            // where a heading stops reading as one. See StyleClassCrtHeadingSmall.
            var crtHeadingSmallFont = useCrtUi ? GetCrtFont(resCache, 9) : notoSansBold11;
            var crtHeadingBigFont = useCrtUi ? uavOsdBold18 : notoSansBold18;

            // The lobby clock face. Roughly twice the big heading, because it is the one thing on
            // the lobby meant to be read without looking at it - the number you catch out of the
            // corner of an eye while reading the server info. The OSD sizes run small (see the note
            // above: uavOsdBold18 is really size 12), so this is picked by eye, not by ratio.
            var crtClockFont = useCrtUi ? GetCrtFont(resCache, 22) : notoSansBold28;
            var crtRichTextFont = useCrtUi ? uavOsd14 : notoSans12;

            // Chat's own font, kept separate from crtRichTextFont on purpose. That one is shared with
            // the guidebook, the lobby's server info and every other rich-text block, and the
            // readable-chat option must not drag those with it - it exists to make *conversation*
            // legible, not to opt out of the theme. See CCVars.CMUChatReadableFont.
            var crtChatFont = useCrtUi && !ChatReadableFont
                ? GetCrtChatFont(resCache, ChatCrtFontSize)
                : resCache.NotoStack(size: ChatReadableFontSize);

            // The OSD face has tight vertical metrics and needs the extra leading; NotoSans does not,
            // and at 1.25 it looks gappy rather than airy.
            var crtChatLineHeight = useCrtUi && !ChatReadableFont ? 1.25f : 1.0f;
            var crtChatAnnouncementFont = useCrtUi && !ChatReadableFont
                ? GetCrtChatFont(resCache, ChatCrtFontSize + 2)
                : resCache.NotoStack(size: ChatReadableFontSize + 2);
            var crtServerInfoFont = useCrtUi ? uavOsdServerInfo : notoSans12;
            // Sized to leave room for the job line underneath without crowding the sprite beside it.
            var crtCharacterSummaryFont = useCrtUi ? GetCrtFont(resCache, 9) : notoSans12;
            var crtButtonLabelFont = useCrtUi ? uavOsdBold14 : notoSans12;
            var crtLineEditFont = useCrtUi ? uavOsd14 : notoSans12;
            var crtNativeLineEditFont = notoSans12;
            var crtTextColor = useCrtUi ? CrtGreenSoft : Color.White;
            var crtDimTextColor = useCrtUi ? CrtGreenDim : Color.FromHex("#B8B8B8");
            var crtHeadingColor = useCrtUi ? CrtGreen : NanoGold;
            var crtSelectionColor = (useCrtUi ? CrtGreen : NanoGold).WithAlpha(useCrtUi ? 0.33f : 0.25f);

            var crtWindowPanel = new CrtStyleBox
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.72f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
            };

            var crtWindowHeader = new CrtStyleBox
            {
                BackgroundColor = CrtHeaderBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.85f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4
            };

            var crtPanel = new CrtStyleBox
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.28f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                CornerLength = 10,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            };

            // See StyleClassCrtPanelFill: a plain fill in the same colour crtPanel uses, with none
            // of its border/corner/texture trimmings, for backing a control that must stay invisible
            // against a CrtPanel behind it.
            // Corner ticks, no border. The sidebar needs an outer edge that says "instrument" without
            // enclosing anything - a stroked rect here is the outermost of the nested boxes, and
            // everything inside it then has to justify not being one too. Four L-marks read as a
            // device and leave all four sides open.
            var crtScreenPanel = new CrtStyleBox
            {
                BackgroundColor = CrtTerminalPalette.Surface0,
                BorderThickness = new Thickness(0),
                DrawCornerTicks = true,
                CornerColor = CrtTerminalPalette.Line,
                CornerLength = 11,
                ContentMarginLeftOverride = 0,
                ContentMarginRightOverride = 0,
                ContentMarginTopOverride = 0,
                ContentMarginBottomOverride = 0
            };

            var crtPanelFill = new StyleBoxFlat
            {
                BackgroundColor = CrtPanelBackground,
            };

            // The command band. Void behind the cells so the 1px separations read as gaps cut in one
            // plate; the cells themselves are borderless fills stepping up on hover. Padding is
            // vertical only - the outer cells run to the band's edges, so the band has no rim.
            var crtCommandBand = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Void,
                BorderThickness = new Thickness(0),
            };

            var crtCommandCell = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
                BorderThickness = new Thickness(0),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            };

            var crtCommandCellHover = new StyleBoxFlat(crtCommandCell)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
            };

            var crtCommandCellPressed = new StyleBoxFlat(crtCommandCell)
            {
                BackgroundColor = CrtTerminalPalette.Surface4,
            };

            var crtCommandCellDisabled = new StyleBoxFlat(crtCommandCell)
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
            };

            // crtPanel with the corner brackets left on. The vote popup builds its panel this way and
            // it is what makes the popup read as a piece of equipment rather than a flat card; the
            // lobby action panel sits directly above a vote popup, so the two have to agree.
            var crtPanelTicked = new CrtStyleBox
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.28f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = true,
                CornerLength = 10,
                // Tighter than crtPanel's 10/10/8/8. This panel is nothing but a heading and a
                // button grid, both of which already carry their own separations, so the panel's
                // padding was compounding rather than framing.
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6
            };

            var crtInsetPanel = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.22f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                CornerLength = 8,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6
            };

            var crtQuietPanel = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.28f),
                BorderThickness = new Thickness(0),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4
            };

            // Background for a StripeBack. Borderless on purpose: a StripeBack only ever sits inside
            // a CrtPanel, and a bordered rectangle inside a bordered panel reads as a box in a box.
            // The band is delimited instead by the edge lines StripeBack draws itself, which span the
            // full width and so read as two rules rather than a frame.
            var crtStripeBack = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderThickness = new Thickness(0),
                DrawCornerTicks = false,
            };

            // One cell of the lobby round-info table. Deliberately very tight: the right-hand lobby
            // panel is height-constrained, and every pixel of padding here is multiplied by six.
            // Borderless. Every cell used to carry a 1px border, which is what made the round-info
            // table read as a grid of boxes inside the panel that already had a border of its own.
            // The separation now comes from fill alone: cells sit on the screen plane and the grid's
            // 2px gutter lets that plane show through as the rule between them. Padding is up from
            // 6/3 because a bordered cell borrows its edge for visual separation and a flat one has
            // to earn the same separation with air.
            var crtTableCell = new CrtStyleBox
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                BorderThickness = new Thickness(0),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 14,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 7,
                ContentMarginBottomOverride = 7
            };

            // The banded row. Alternating Surface1/Surface2 is what replaces the removed borders as
            // the thing that keeps a long two-column table trackable across a row.
            var crtTableCellAlt = new CrtStyleBox(crtTableCell)
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
            };

            // The lead pair - planet and gamemode. Surface3 is the lightest tone in the ladder, the
            // same one the header strip uses, so these two read as the top of the panel's hierarchy
            // without needing a heading, a rule or a frame to say so.
            var crtTableCellLead = new CrtStyleBox(crtTableCell)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
                ContentMarginTopOverride = 9,
                ContentMarginBottomOverride = 9
            };

            // The lobby's chat surface: keeps chat's own dark backing for legibility, but takes the
            // CrtPanel's border so it reads as a section of the same panel. Defined here rather than
            // assigned to the control so it tracks the CRT palette when the stylesheet rebuilds.
            // Surface1, the same tone as a table cell, and no border. The chat used to keep its own
            // near-black backing and a green outline on the theory that it must stay legible - but
            // legibility comes from the text colour against its ground, and Surface1 is still dark.
            // The outline was the last framed rectangle on the screen.
            // Surface0 and NO content margins. This is the "one ground" change: this panel used to be
            // Surface1 with 10px of padding, which made it a lighter wrapper around all three bands -
            // and the 9px of it left showing on either side of the input bar is what read as a border
            // around that bar, though nothing was ever stroked. As the darkest surface with zero
            // padding it stops being a frame and becomes the ground the bands sit on.
            //
            // The padding it used to supply now lives on the log's own container, so the bands can go
            // full bleed while their *contents* stay inset. See ChatBox.xaml's ChatPanes margin.
            var crtChatPanel = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface0,
                BorderThickness = new Thickness(0),
            };

            // The lobby's chat input row. Top rule only - the chat panel already supplies the left,
            // right and bottom borders, so a full box here would double them up.
            // Surface1, one step off the Surface0 ground - not Surface3. With the chat's ground now
            // the darkest surface, one step is enough to mark the live line, and Surface3 on top of
            // Surface0 is a jump wide enough that the bar reads as a separate plate laid on the
            // panel. The channel is a prompt rather than a chip now (see crtChatChannelChip), so the
            // bar no longer has to out-contrast a box sitting on it.
            // The ChatBox carries no outer margin, so these are insets from the true panel edge.
            // Left is 8 because the channel chip's own box adds 6 more before its label: 8 + 6 puts
            // the chip's text on the same x as ChatMessageRow's message prefixes. Right stays 14 -
            // the filter button on that end has no inner inset.
            var crtChatInput = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                BorderThickness = new Thickness(0),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 14,
                ContentMarginTopOverride = 10,
                ContentMarginBottomOverride = 10,
            };

            // AHelp/MentorHelp's input band. Surface1 for the same reason the lobby chat's input bar
            // is Surface1 - one step off the window's Surface0 ground is enough to mark the live
            // line without the jump to Surface3 reading as a separate plate. Unlike the lobby chat,
            // there is no seam rule above this to lean on, so the border carries both edges itself.
            var crtBwoinkInput = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(0, 1, 0, 1),
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8,
            };

            // A prompt, not a chip: no fill. This was the last nested box on the row - a three-letter
            // label in a box, in a bar, in a panel - and the label already says which channel in
            // words, so the box was carrying no information the text did not. Accent text on the
            // input band reads as a prompt, which is what it is.
            //
            // Padding stays small but non-zero, and identical in both states. A hover that changed
            // the content margins would resize the control and shove the input field sideways every
            // time the pointer crossed it.
            var crtChatChannelChip = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderThickness = new Thickness(0),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            };

            // Hover still needs to say "this is clickable", and with no resting fill to brighten,
            // a fill is the only thing left. One step off the input band, which is Surface1.
            var crtChatChannelChipHover = new StyleBoxFlat(crtChatChannelChip)
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
            };

            // A checkbox is a toggle, not a button - but CheckBox derives from ContainerButton, so
            // it used to inherit the full CrtButton box and every option row rendered as a wide
            // filled bar. The tick texture already carries the state; all this needs to do is keep
            // the row clickable and give a little padding.
            // The bottom rule is what separates back-to-back toggles: a run of them reads as a list
            // of rows instead of one undifferentiated block, which matters most where several are
            // checked at once and their fills would otherwise merge.
            var crtCheckBox = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = CrtGreenDim.WithAlpha(0.35f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            };

            // Hover still has to be legible with no border to light up, so it's a faint wash.
            var crtCheckBoxHover = new StyleBoxFlat(crtCheckBox)
            {
                BackgroundColor = CrtGreen.WithAlpha(0.10f),
            };

            // Checked is NOT a filled row. A CheckBox's pressed pseudo-class is its checked state, so
            // tinting it painted every enabled option as a full-width green bar - a list with several
            // on read as a block of solid colour with the text fighting it. The tick already says
            // "on"; that is what the control is for.
            var crtCheckBoxPressed = new StyleBoxFlat(crtCheckBox);

            // Bottom rule across a whole option row, label included. Checkboxes already carry one via
            // crtCheckBox; without this the sliders and dropdowns were the only rows in a section
            // with no divider, so the rule appeared to stop partway along the list.
            var crtOptionRow = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
                BorderColor = CrtGreenDim.WithAlpha(0.35f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 0,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 3,
            };

            // Boxes the value readout so it begins exactly where the track ends, carrying the same
            // frame the slider draws around its empty portion. No left edge: the slider's own right
            // border serves as the divider, and a second line there would read as a double rule.
            var crtSliderValue = new StyleBoxFlat
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                BorderThickness = new Thickness(0, 2, 2, 2),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            };

            // Base/NanoUI variant. The box above is deliberately borderless on the left and flush
            // against the slider so it reads as a continuation of the frame the CRT slider draws
            // around its own track - that reasoning only holds because the CRT slider has such a
            // frame to continue. NanoUI's slider draws no matching edge, so the same box read as a
            // fragment with a missing left side and no gap from the track. A full border plus real
            // separation (added as a margin at the call site) fixes both without touching the CRT box.
            var nanoSliderValue = new StyleBoxFlat
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            };

            // Band behind a section heading in a settings list. Rules top and bottom only - side
            // borders would close it into a box, and the whole point of these headings is to divide
            // a long list without adding another rectangle to it.
            var crtSectionHeader = new StyleBoxFlat
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim,
                BorderThickness = new Thickness(0, 1, 0, 1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3,
            };

            // Footer strip under a tabbed window. A single top rule rather than a full box: it sits
            // inside the window frame and the tab panel already, and a third rectangle around three
            // buttons is what made the options footer look so heavy.
            var crtFooterRow = new StyleBoxFlat
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim,
                BorderThickness = new Thickness(0, 1, 0, 0),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            };

            // Visible fills and bright text distinguish available tabs from disabled controls.
            var crtChatTab = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 5,
                ContentMarginBottomOverride = 5,
            };

            var crtChatTabHover = new StyleBoxFlat(crtChatTab)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
                BorderColor = CrtTerminalPalette.Text,
            };

            var crtChatTabSelected = new StyleBoxFlat(crtChatTab)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
                BorderColor = CrtTerminalPalette.Accent,
                BorderThickness = new Thickness(1, 1, 1, 2),
                ContentMarginBottomOverride = 4,
            };

            // The channel-selector popup that sits on top of the chat input row. Deliberately much
            // tighter than CrtInsetPanel (8/8/6/6): this is a strip spanning the input bar, not a
            // window, and the CRT button chrome inside it already carries 3px + a border of its own.
            var crtChatPopup = new StyleBoxFlat
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 2,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2,
            };

            // The chat log's scrollbar. Separate from the general CrtScrollBar because that one is
            // 16px wide (its grabber carries 8px content margins all round), which is far too heavy
            // for a gutter running down the side of the message list.
            //
            // The track is what gives the gutter a visible channel - CrtScrollBar sets only a
            // grabber, so it reads as a nub floating over the content. This bar is owned by
            // ChatLogPanel rather than by the ScrollContainer, so it is always visible and the
            // track is always drawn.
            // Hairlines top and bottom, not a filled channel - a border, not a background, is what
            // keeps this from re-becoming a second surface running down the log. BorderThickness on
            // a StyleBoxFlat draws full width by default, which is exactly what a 10px-wide lane
            // wants; there is no meaningful "inset" to speak of at this size.
            var crtChatScrollTrack = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface0,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(0, 1, 0, 1),
            };

            // A shape, not a fill. Design docs/cmu/crt-chat-scrollbar.html has the full set this was
            // picked from - "shipped 2026-08-27" was a flat coloured rectangle dimmed low enough to
            // solve "too loud" by going unnoticed instead. A bar with end-caps wider than itself
            // reads as a handle with stops from its geometry alone, which survives being dim in a way
            // a plain rectangle cannot - see ChatScrollThumbStyleBox for why.
            //
            // Content margins: left+right set the control's own width (10, up from the 4px
            // stopgap - the bracket shape needs room to read as a bracket, and being airy rather
            // than solid is what keeps 10px from reading as "loud" the way a solid 8px fill did).
            // Top+bottom set the *minimum draggable thumb length* on a long log, not a visual
            // margin - 8, enough that the two 2px caps never touch even at minimum size.
            var crtChatScrollGrabber = new ChatScrollThumbStyleBox
            {
                BarColor = CrtTerminalPalette.Line,
                CapColor = CrtTerminalPalette.TextDim,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            };

            var crtChatScrollGrabberHover = new ChatScrollThumbStyleBox(crtChatScrollGrabber)
            {
                BarColor = CrtTerminalPalette.TextDim,
                CapColor = CrtTerminalPalette.Text,
            };

            var crtChatScrollGrabberPressed = new ChatScrollThumbStyleBox(crtChatScrollGrabber)
            {
                BarColor = CrtTerminalPalette.Accent,
                CapColor = CrtTerminalPalette.Accent,
            };

            // Solid divider rule. Must come from the stylesheet, not a control's own StyleBoxFlat:
            // the stylesheet is rebuilt whenever the CRT palette changes, whereas a colour baked
            // into a control at construction stays whatever the palette was at that moment.
            var crtDivider = new StyleBoxFlat
            {
                BackgroundColor = CrtGreenDim,
                ContentMarginTopOverride = 2,
            };

            // See StyleClassCrtSectionRule. Line rather than CrtGreenDim, and no content margin: this
            // runs beside a heading at 1px, where crtDivider's 2px reads as a bar.
            var crtSectionRule = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Line,
            };

            // Bottom rule under the lobby's SERVER INFO row. The rich-text markup has no underline
            // tag (only bold/italic/color/head/bullet/font/cmdlink), so the line is drawn as a
            // border on the row instead.
            var crtUnderlineRow = new CrtStyleBox
            {
                BackgroundColor = Color.Transparent,
                BorderColor = CrtGreenDim.WithAlpha(0.7f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 0,
                ContentMarginRightOverride = 0,
                ContentMarginTopOverride = 0,
                ContentMarginBottomOverride = 2
            };

            var crtHeaderPanel = new CrtStyleBox
            {
                BackgroundColor = CrtHeaderBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.24f),
                DrawCornerTicks = false,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            // Borderless, like every other surface in the theme. A bordered button was fine while
            // the panels around it were bordered too, but once the tables went to fill-only
            // separation the outlines left on the chrome became the loudest thing on the screen. The
            // states are carried entirely by fill now - which they already were, the border was
            // never the thing distinguishing hover from pressed.
            // On the surface ladder, not the old CrtPalette button family. Those two are different
            // greens, so a button sitting on a ladder surface read as a foreign box laid on top -
            // which is what made a plain filled chip look like it had a border round it. States are
            // the ladder's own steps, so a button is simply a surface one level up from its ground.
            // Top and bottom are 6/4, not 5/5, and the asymmetry is on purpose. The uavOsd face is
            // all-caps and its line box reserves descender room that a label like AHELP or CUSTOMIZE
            // never uses, so centring the *box* leaves the visible ink sitting about 3px high in
            // every button. Measured at padTop=4/padBottom=7 across AHELP, CALL VOTE, CUSTOMIZE and
            // IGNORE ALLEGIANCE before this; 1px out after, with the button height unchanged.
            // Re-measure if the button font size ever changes - this compensates for one metric.
            // Bordered off-theme, borderless under CRT. The CRT ladder separates a button from its
            // panel by fill alone, which is the whole point of widening it - but the neutral ladder
            // has nothing like that much room between steps, so off-theme a button needs an edge to
            // be a button at all. Every control on the lobby was an unmarked rectangle without this.
            var crtButton = new CrtStyleBox
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = useCrtUi ? new Thickness(0) : new Thickness(1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 4
            };

            var crtButtonHover = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
            };

            var crtButtonPressed = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface4,};

            var crtButtonDisabled = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
            };

            var crtAttentionButton = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface3,
            };

            var crtAttentionButtonHover = new CrtStyleBox(crtAttentionButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface4,};

            var crtAttentionButtonPressed = new CrtStyleBox(crtAttentionButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface4,};

            // The ready toggle. Not-ready is the theme's inert ground with a dim edge - switched
            // off, and it should look it. The two states have to be told apart at a glance, so they
            // differ on fill, on edge colour, and on the mark in the label, rather than by one step
            // of the surface ladder - which is what left the old pressed state all but invisible
            // (Surface2 to Surface4 and nothing else).
            //
            // The edge is a single rule down the leading side, not a frame: a box inside a box is
            // the one thing this theme has refused everywhere else.
            // Not ready: the theme's inert ground with a dim edge. Switched off, and it looks it.
            var crtReadyToggle = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtTerminalPalette.Surface1,
                BorderColor = CrtTerminalPalette.TextDim,
                BorderThickness = new Thickness(3, 0, 0, 0),
            };

            var crtReadyToggleHover = new CrtStyleBox(crtReadyToggle)
            {
                BackgroundColor = CrtTerminalPalette.Surface2,
            };

            // Ready: inverted. Not one step up the surface ladder - a lit panel with dark text on
            // it, which is the largest difference this palette can make between two states of one
            // control. The first attempt moved Surface2 to Surface4 and was genuinely hard to read
            // at a glance; a step on the ladder says "hovered", it does not say "you are ready".
            var crtReadyToggleOn = new CrtStyleBox(crtReadyToggle)
            {
                BackgroundColor = Color.InterpolateBetween(
                    CrtTerminalPalette.Surface4, CrtTerminalPalette.Accent, 0.7f),
                BorderColor = CrtTerminalPalette.TextBright,
            };

            var crtReadyToggleOnHover = new CrtStyleBox(crtReadyToggleOn)
            {
                BackgroundColor = CrtTerminalPalette.Accent,
            };

            var crtLineEdit = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.2f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var crtNativeLineEdit = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.55f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 3
            };

            var crtTabActive = new CrtStyleBox
            {
                BackgroundColor = CrtHeaderBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.24f),
                BorderThickness = new Thickness(1, 1, 1, 0),
                CornerLength = 8,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var crtTabInactive = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreenDim.WithAlpha(0.2f),
                BorderThickness = new Thickness(1, 1, 1, 0),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            // 2px, not 1: this frame is only really seen along the unfilled part of the track, and a
            // hairline there left the slider looking like a floating bar rather than a bounded
            // control. It also has to hold its own against the value box butted up beside it.
            var crtSliderBackground = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.16f),
                BorderThickness = new Thickness(2),
                DrawCornerTicks = false,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            };

            // Transparent, and that is deliberate. Slider adds its panels in the order background,
            // fill, foreground, grabber, and anchors the foreground to the full width - so an opaque
            // colour here paints straight over the filled portion and every slider renders as one
            // flat bar with no readable level. The background supplies the empty part of the track.
            var crtSliderForeground = new StyleBoxFlat
            {
                BackgroundColor = Color.Transparent,
            };
            crtSliderForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

            var crtSliderFill = new StyleBoxFlat
            {
                BackgroundColor = CrtGreenDim,
            };
            crtSliderFill.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

            // ColorableSlider asks for these two by name when a channel's track has to read as a
            // true colour ramp rather than the CRT palette - tinting those green would defeat the
            // point of a colour picker. Without them the lookup returns null and the track is blank.
            var crtSliderFillWhite = new StyleBoxFlat
            {
                BackgroundColor = Color.White,
            };
            crtSliderFillWhite.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

            var crtSliderBackgroundWhite = new StyleBoxFlat
            {
                BackgroundColor = Color.White,
            };
            crtSliderBackgroundWhite.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

            var crtSliderGrabber = new StyleBoxFlat
            {
                BackgroundColor = CrtGreen,
                BorderColor = Color.White,
                BorderThickness = new Thickness(1),
            };
            crtSliderGrabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

            var crtProgressBackground = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.15f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
            };
            crtProgressBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 10);

            var crtProgressForeground = new CrtStyleBox
            {
                BackgroundColor = CrtProgressForeground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.2f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
            };
            crtProgressForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 10);

            var crtItemListBackground = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.14f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3
            };

            var crtItemBackground = new StyleBoxFlat
            {
                BackgroundColor = CrtItemBackground.WithAlpha(0.42f),
                BorderColor = CrtGreenDisabled.WithAlpha(0.45f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3
            };

            var crtItemSelectedBackground = new StyleBoxFlat
            {
                BackgroundColor = CrtItemSelectedBackground.WithAlpha(0.72f),
                BorderColor = CrtGreen.WithAlpha(0.48f),
                BorderThickness = new Thickness(1, 0, 1, 1),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3
            };

            var crtItemDisabledBackground = new StyleBoxFlat
            {
                BackgroundColor = CrtItemDisabledBackground.WithAlpha(0.64f),
                BorderColor = CrtGreenDisabled.WithAlpha(0.25f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3
            };

            var crtScrollGrabber = new StyleBoxFlat
            {
                BackgroundColor = CrtGreenDim.WithAlpha(0.78f),
                BorderColor = CrtGreen.WithAlpha(0.42f),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            };

            var crtScrollGrabberHover = new StyleBoxFlat(crtScrollGrabber)
            {
                BackgroundColor = CrtGreen.WithAlpha(0.5f),
                BorderColor = CrtGreenSoft.WithAlpha(0.58f)
            };

            var crtScrollGrabberPressed = new StyleBoxFlat(crtScrollGrabber)
            {
                BackgroundColor = CrtGreen.WithAlpha(0.72f),
                BorderColor = CrtGreenSoft.WithAlpha(0.8f)
            };

            shared = new CrtShared(crtWindowPanel, crtWindowHeader, crtInsetPanel, crtTextColor);

            return new StyleRule[]
            {
                // CRT lobby/preferences theme.
                Element<PanelContainer>().Class(StyleClassCrtPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtPanelFill)
                    .Prop(PanelContainer.StylePropertyPanel, crtPanelFill)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtScreenPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtScreenPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtPanelTicked)
                    .Prop(PanelContainer.StylePropertyPanel, crtPanelTicked)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtInsetPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtInsetPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtQuietPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtQuietPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtHeaderPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtHeaderPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtTableCell)
                    .Prop(PanelContainer.StylePropertyPanel, crtTableCell)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtTableCellAlt)
                    .Prop(PanelContainer.StylePropertyPanel, crtTableCellAlt)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtTableCellLead)
                    .Prop(PanelContainer.StylePropertyPanel, crtTableCellLead)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtUnderlineRow)
                    .Prop(PanelContainer.StylePropertyPanel, crtUnderlineRow)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtDivider)
                    .Prop(PanelContainer.StylePropertyPanel, crtDivider)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // Every rule below carries a pseudo-class, and that is load-bearing rather than
                // decorative. A tab button also gets StyleClassChatChannelSelectorButton, whose
                // stock rule is Element<Button> + one class; Robust scores an element type by how
                // far it sits below Control, so Button (3) outranks ContainerButton (2) and a
                // plain Element<ContainerButton>().Class(CrtChatTab) rule lost to it outright, at
                // every value it was ever given. Class selectors are compared before types, so the
                // pseudo takes this side to two classes and the stock rule can no longer win.
                //
                // Pinning ModulateSelf is part of the same fix: the generic button rules set it
                // per pseudo-class, and left alone they multiply the flat fills below by NanoUI's
                // slate. Every CrtButton rule already does this - these were the odd ones out.
                Element<ContainerButton>().Class(StyleClassCrtChatTab)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTab)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatTab)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTab)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatTab)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTabHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // An *inactive* tab renders pressed too, for as long as a click is held on it -
                // DrawMode counts an in-progress press, not just the Pressed property. Without this
                // rule that state fell through to the generic button rules and the tab flashed
                // NanoUI slate on mouse-down. Held reads the same as hovered, which is the state it
                // is already in.
                Element<ContainerButton>().Class(StyleClassCrtChatTab)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTabHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // The active tab, which carries both classes - so these tie with the resting rules
                // above on specificity and win only by sitting after them. Keep them in this order:
                // it is what stops the pressed rule just above from claiming the active tab.
                //
                // Both call sites set Pressed, so the pressed variant is the one that normally
                // fires and the active tab keeps its fill under the pointer - DrawMode checks
                // Pressed before hover. The class is the second, independent signal: it is what
                // distinguishes a genuinely selected tab from one that merely has a click held on
                // it, since DrawMode reports both as pressed.
                Element<ContainerButton>().Class(StyleClassCrtChatTabSelected)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTabSelected)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatTabSelected)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTabSelected)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatTabSelected)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatTabSelected)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // The channel chip. Pseudo variants for the same reason the tab rules have them:
                // this control also carries chatSelectorOptionButton, whose stock rule is on the
                // deeper Button type and outranks a one-class rule here.
                Element<ContainerButton>().Class(StyleClassCrtChatChannelChip)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatChannelChip)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatChannelChip)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatChannelChip)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtChatChannelChip)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatChannelChipHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // Open (the popup is showing) reads the same as hover - it is the same "you are
                // touching this" state, and the popup itself is the real feedback.
                Element<ContainerButton>().Class(StyleClassCrtChatChannelChip)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtChatChannelChipHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // Accent, not the channel's own colour. The gallery carries channel identity in the
                // log prefix only; the chip already says which channel in words, so colouring it
                // per-channel just put an off-palette hue on the busiest row of the panel.
                Child().Parent(Element<ContainerButton>().Class(StyleClassCrtChatChannelChip))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFontColor, useCrtUi ? CrtTerminalPalette.Accent : Color.White),

                // The chat input itself. There was no CRT rule for this class at all, so the typed
                // text and the placeholder stayed NanoUI's proportional grey (#D6DCE0) while every
                // surface around them had gone terminal - the single biggest reason the input row
                // did not read as a terminal. StyleBoxEmpty matches the stock rule: the bar behind
                // it already supplies the fill and padding.
                Element<LineEdit>().Class(StyleClassChatLineEdit)
                    .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty())
                    .Prop("font", crtChatFont)
                    .Prop("font-color", useCrtUi ? CrtTerminalPalette.Text : Color.FromHex("#D6DCE0"))
                    .Prop(LineEdit.StylePropertyCursorColor, useCrtUi ? CrtTerminalPalette.Accent : Color.White)
                    .Prop(LineEdit.StylePropertySelectionColor, crtSelectionColor),

                Element<LineEdit>().Class(StyleClassChatLineEdit)
                    .Pseudo(LineEdit.StylePseudoClassPlaceholder)
                    .Prop("font-color", useCrtUi ? CrtTerminalPalette.TextDim : Color.FromHex("#7F8792")),

                // Chat chrome takes the same font as the message bodies. RichTextLabel can only be
                // given a font through the stylesheet, so the bodies are pinned to crtRichTextFont
                // (uavOsd 8) and everything beside them has to match or the panel reads as chrome
                // laid over a log rather than as one terminal. This was a real mismatch: the channel
                // chip was 12 against text at 8, half again as large.
                // Message bodies. RichTextLabel has no FontOverride, so this rule is the only route
                // to their typeface - which is also why the readable-font option has to be a
                // stylesheet concern rather than something set on the control.
                Element<RichTextLabel>().Class(StyleClassCrtChatText)
                    .Prop("font", crtChatFont)
                    .Prop(nameof(RichTextLabel.LineHeightScale), crtChatLineHeight),

                Element<RichTextLabel>().Class(StyleClassCrtChatAnnouncementText)
                    .Prop("font", crtChatAnnouncementFont)
                    .Prop(nameof(RichTextLabel.LineHeightScale), crtChatLineHeight),

                Child().Parent(Element<ContainerButton>().Class(StyleClassCrtChatTab))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFont, crtChatFont)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                Child().Parent(Element<ContainerButton>().Class(StyleClassCrtChatChannelChip))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFont, crtChatFont),

                Element<PanelContainer>().Class(StyleClassCrtChatPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtChatPanel)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtChatInput)
                    .Prop(PanelContainer.StylePropertyPanel, crtChatInput)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtBwoinkInput)
                    .Prop(PanelContainer.StylePropertyPanel, crtBwoinkInput)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtChatPopup)
                    .Prop(PanelContainer.StylePropertyPanel, crtChatPopup)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtFooterRow)
                    .Prop(PanelContainer.StylePropertyPanel, crtFooterRow)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtSectionHeader)
                    .Prop(PanelContainer.StylePropertyPanel, crtSectionHeader)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtSliderValue)
                    .Prop(PanelContainer.StylePropertyPanel, crtSliderValue)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassNanoSliderValue)
                    .Prop(PanelContainer.StylePropertyPanel, nanoSliderValue)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtOptionRow)
                    .Prop(PanelContainer.StylePropertyPanel, crtOptionRow)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // The clickable variant used by CmuOptionSection. A ContainerButton takes its box
                // from a different property than a PanelContainer, so it needs its own rule.
                Element<ContainerButton>().Class(StyleClassCrtSectionHeader)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtSectionHeader)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCheckBox)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCheckBox)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCheckBox)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCheckBoxHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCheckBox)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCheckBoxPressed)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtCommandBand)
                    .Prop(PanelContainer.StylePropertyPanel, crtCommandBand)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<PanelContainer>().Class(StyleClassCrtCommandCellPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtCommandCell)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCommandCell)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCommandCell)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCommandCell)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCommandCell)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCommandCell)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCommandCellHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCommandCell)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCommandCellPressed)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtCommandCell)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtCommandCellDisabled)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // No label rule of its own: CrtLobbyTheme still hands these labels
                // StyleClassCrtButtonLabel, and a second rule here would tie with it at equal
                // specificity, which has no defined winner.

                Element<ContainerButton>().Class(StyleClassCrtButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButton)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButton)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonPressed)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonDisabled)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtAttentionButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtAttentionButton)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtAttentionButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtAttentionButton)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtAttentionButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtAttentionButtonHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtAttentionButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtAttentionButtonPressed)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtAttentionButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonDisabled)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // Off. No bare class rule and no Pressed rule: the toggle wears exactly one of the
                // two ready classes at a time, so for any given state exactly one of these matches
                // and there is no tie to resolve.
                Element<ContainerButton>().Class(StyleClassCrtReadyToggle)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggle)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtReadyToggle)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggleHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtReadyToggle)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggleHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtReadyToggle)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonDisabled)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // On.
                Element<ContainerButton>().Class(StyleClassCrtReadyToggleOn)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggleOn)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtReadyToggleOn)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggleOnHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                // Pressed, not hover: a ToggleMode button that is on holds the Pressed pseudo-class
                // for as long as it is on, so this - not the Normal rule above - is what a ready
                // player actually looks at. Pointing it at the hover box left the resting state at
                // full accent and gave hovering nothing left to say.
                Element<ContainerButton>().Class(StyleClassCrtReadyToggleOn)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtReadyToggleOn)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<ContainerButton>().Class(StyleClassCrtReadyToggleOn)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(ContainerButton.StylePropertyStyleBox, crtButtonDisabled)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<Label>().Class(StyleClassCrtText)
                    .Prop(Label.StylePropertyFont, crtTextFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor),

                Element<Label>().Class(StyleClassCrtTableCellText)
                    .Prop(Label.StylePropertyFont, crtTableCellFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor),

                // The field name. Legibility here was previously traded away for hierarchy: the
                // label was the same size as its own value and separated from it only by being dim,
                // which is the one axis that costs readability directly. The hierarchy now comes
                // from size, so the label can sit at a colour that is muted rather than murky.
                Element<Label>().Class(StyleClassCrtFieldLabel)
                    .Prop(Label.StylePropertyFont, crtFieldLabelFont)
                    .Prop(Label.StylePropertyFontColor, crtFieldLabelColor),

                Element<Label>().Class(StyleClassCrtSectionTitle)
                    .Prop(Label.StylePropertyFont, crtSectionTitleFont)
                    .Prop(Label.StylePropertyFontColor, crtFieldLabelColor),

                Element<PanelContainer>().Class(StyleClassCrtSectionRule)
                    .Prop(PanelContainer.StylePropertyPanel, crtSectionRule)
                    .Prop(Control.StylePropertyModulateSelf, Color.White),

                Element<Label>().Class(StyleClassCrtFieldValue)
                    .Prop(Label.StylePropertyFont, crtFieldValueFont)
                    .Prop(Label.StylePropertyFontColor, crtFieldValueColor),

                // Planet and gamemode. Same colour as any other value, one step larger, because
                // being first in the reading order is carried by the Surface3 fill behind it and
                // this only has to keep up with that.
                Element<Label>().Class(StyleClassCrtStatValue)
                    .Prop(Label.StylePropertyFont, crtStatValueFont)
                    .Prop(Label.StylePropertyFontColor, crtFieldValueColor),

                Element<Label>().Class(StyleClassCrtFieldValueLead)
                    .Prop(Label.StylePropertyFont, crtFieldValueLeadFont)
                    .Prop(Label.StylePropertyFontColor, crtFieldValueColor),

                Element<Label>().Class(StyleClassCrtDimText)
                    .Prop(Label.StylePropertyFont, crtDimFont)
                    .Prop(Label.StylePropertyFontColor, crtDimTextColor),

                Element<Label>().Class(StyleClassCrtHeading)
                    .Prop(Label.StylePropertyFont, crtHeadingFont)
                    .Prop(Label.StylePropertyFontColor, crtHeadingColor),

                Element<Label>().Class(StyleClassCrtHeadingSmall)
                    .Prop(Label.StylePropertyFont, crtHeadingSmallFont)
                    .Prop(Label.StylePropertyFontColor, crtHeadingColor),

                Element<Label>().Class(StyleClassCrtHeadingStatus)
                    .Prop(Label.StylePropertyFont, crtDimFont)
                    .Prop(Label.StylePropertyFontColor, crtDimTextColor),

                Element<Label>().Class(StyleClassCrtHeadingBig)
                    .Prop(Label.StylePropertyFont, crtHeadingBigFont)
                    .Prop(Label.StylePropertyFontColor, crtHeadingColor),

                Element<Label>().Class(StyleClassCrtHeadingBigWarning)
                    .Prop(Label.StylePropertyFont, crtHeadingBigFont)
                    .Prop(Label.StylePropertyFontColor, CrtWarning),

                Element<Label>().Class(StyleClassCrtHeadingBigDanger)
                    .Prop(Label.StylePropertyFont, crtHeadingBigFont)
                    .Prop(Label.StylePropertyFontColor, CrtDanger),

                // The clock face. Three colours, one font - the urgency swap must not also change
                // the size, or the panel resizes under the countdown as it runs down.
                Element<Label>().Class(StyleClassCrtClock)
                    .Prop(Label.StylePropertyFont, crtClockFont)
                    .Prop(Label.StylePropertyFontColor, CrtTerminalPalette.TextBright),

                Element<Label>().Class(StyleClassCrtClockWarning)
                    .Prop(Label.StylePropertyFont, crtClockFont)
                    .Prop(Label.StylePropertyFontColor, CrtWarning),

                Element<Label>().Class(StyleClassCrtClockDanger)
                    .Prop(Label.StylePropertyFont, crtClockFont)
                    .Prop(Label.StylePropertyFontColor, CrtDanger),

                Element<Label>().Class(StyleClassCrtButtonLabel)
                    .Prop(Label.StylePropertyFont, crtButtonLabelFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                // AlignMode.Center was missing here, so every CRT button label sat left in a button
                // wider than its text - visible on AHELP, CALL VOTE, OPTIONS, CUSTOMIZE, anything
                // whose box is set by a MinWidth or a shared column rather than by the text. The
                // CrtNativeButtonLabel rule immediately below has always centred; this one just
                // never got the same line.
                Child().Parent(Element<Button>().Class(StyleClassCrtButton))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFont, crtButtonLabelFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                Element<Label>().Class(StyleClassCrtNativeButtonLabel)
                    .Prop(Label.StylePropertyFont, notoSans12)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                Element<RichTextLabel>().Class(StyleClassCrtRichText)
                    .Prop("font", crtRichTextFont)
                    // The uavOsd font has very tight vertical metrics; at the default scale of 1.0
                    // multi-line blocks (lobby server info, guidebook text) render with no visible
                    // gap between lines. Tune this if CRT body text looks cramped or too airy.
                    .Prop(nameof(RichTextLabel.LineHeightScale), 1.25f),

                // The lobby's character name/age lines, sized up so they carry the block.
                Element<RichTextLabel>().Class(StyleClassCrtCharacterSummary)
                    .Prop("font", crtCharacterSummaryFont)
                    .Prop(nameof(RichTextLabel.LineHeightScale), 1.2f),

                // Lobby server-info block. Larger than normal CRT body text and slightly airier,
                // since it is the first thing a player reads and the panel has room to spare.
                Element<RichTextLabel>().Class(StyleClassCrtServerInfoText)
                    .Prop("font", crtServerInfoFont)
                    .Prop(nameof(RichTextLabel.LineHeightScale), 1.35f),

                Element<ItemList>().Class(StyleClassCrtItemList)
                    .Prop(ItemList.StylePropertyBackground, crtItemListBackground)
                    .Prop(ItemList.StylePropertyItemBackground, crtItemBackground)
                    .Prop(ItemList.StylePropertySelectedItemBackground, crtItemSelectedBackground)
                    .Prop(ItemList.StylePropertyDisabledItemBackground, crtItemDisabledBackground)
                    .Prop("font", crtRichTextFont)
                    .Prop("font-color", crtTextColor),

                Element<VScrollBar>().Class(StyleClassCrtScrollBar)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabber),

                Element<VScrollBar>().Class(StyleClassCrtScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassHover)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabberHover),

                Element<VScrollBar>().Class(StyleClassCrtScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassGrabbed)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabberPressed),

                Element<HScrollBar>().Class(StyleClassCrtScrollBar)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabber),

                Element<HScrollBar>().Class(StyleClassCrtScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassHover)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabberHover),

                Element<HScrollBar>().Class(StyleClassCrtScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassGrabbed)
                    .Prop(ScrollBar.StylePropertyGrabber, crtScrollGrabberPressed),

                // Chat log gutter. The track has to be repeated on every pseudo-class: style
                // properties are resolved per rule, so a rule that only sets the grabber leaves the
                // track unset and the channel vanishes the moment the grabber is hovered.
                Element<VScrollBar>().Class(StyleClassCrtChatScrollBar)
                    .Prop(ScrollBar.StylePropertyTrack, crtChatScrollTrack)
                    .Prop(ScrollBar.StylePropertyGrabber, crtChatScrollGrabber),

                Element<VScrollBar>().Class(StyleClassCrtChatScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassHover)
                    .Prop(ScrollBar.StylePropertyTrack, crtChatScrollTrack)
                    .Prop(ScrollBar.StylePropertyGrabber, crtChatScrollGrabberHover),

                Element<VScrollBar>().Class(StyleClassCrtChatScrollBar)
                    .Pseudo(ScrollBar.StylePseudoClassGrabbed)
                    .Prop(ScrollBar.StylePropertyTrack, crtChatScrollTrack)
                    .Prop(ScrollBar.StylePropertyGrabber, crtChatScrollGrabberPressed),

                Element<LineEdit>().Class(StyleClassCrtLineEdit)
                    .Prop(LineEdit.StylePropertyStyleBox, crtLineEdit)
                    .Prop("font", crtLineEditFont)
                    .Prop("font-color", crtTextColor)
                    .Prop(LineEdit.StylePropertyCursorColor, crtHeadingColor)
                    .Prop(LineEdit.StylePropertySelectionColor, crtSelectionColor),

                Element<LineEdit>().Class(StyleClassCrtNativeLineEdit)
                    .Prop(LineEdit.StylePropertyStyleBox, crtNativeLineEdit)
                    .Prop("font", crtNativeLineEditFont)
                    .Prop("font-color", crtTextColor)
                    .Prop(LineEdit.StylePropertyCursorColor, crtHeadingColor)
                    .Prop(LineEdit.StylePropertySelectionColor, crtSelectionColor),

                // The colour picker's gradient fields. No class: every one of them wants the frame,
                // and without it a gradient sits on the background with no edge at all.
                Element<ColorFieldControl>()
                    .Prop(ColorFieldControl.StylePropertyBorderColor, CrtGreenDim),

                Element<Slider>().Class(StyleClassCrtSlider)
                    .Prop(Slider.StylePropertyBackground, crtSliderBackground)
                    .Prop(Slider.StylePropertyForeground, crtSliderForeground)
                    .Prop(Slider.StylePropertyFill, crtSliderFill)
                    .Prop(Slider.StylePropertyGrabber, crtSliderGrabber)
                    .Prop(ColorableSlider.StylePropertyFillWhite, crtSliderFillWhite)
                    .Prop(ColorableSlider.StylePropertyBackgroundWhite, crtSliderBackgroundWhite),

                Element<ProgressBar>().Class(StyleClassCrtProgressBar)
                    .Prop(ProgressBar.StylePropertyBackground, crtProgressBackground)
                    .Prop(ProgressBar.StylePropertyForeground, crtProgressForeground),

                Element<TabContainer>().Class(StyleClassCrtTabContainer)
                    .Prop(TabContainer.StylePropertyPanelStyleBox, crtInsetPanel)
                    .Prop(TabContainer.StylePropertyTabStyleBox, crtTabActive)
                    .Prop(TabContainer.StylePropertyTabStyleBoxInactive, crtTabInactive)
                    .Prop(TabContainer.stylePropertyTabFontColor, crtHeadingColor)
                    .Prop(TabContainer.StylePropertyTabFontColorInactive, crtDimTextColor),

                Element<StripeBack>().Class(StyleClassCrtStripeBack)
                    .Prop(StripeBack.StylePropertyBackground, crtStripeBack)
                    // The edges are the whole point of the control - they band the strip off from
                    // what surrounds it without boxing it in. They were previously hidden because
                    // StripeBack's own light grey drew a stray white rule through the theme; now
                    // that the colour is styleable they carry the separation the border used to.
                    .Prop(StripeBack.StylePropertyEdgeColor, CrtGreenDim.WithAlpha(0.55f)),

                Element<TextureButton>().Class(StyleClassCrtIconButton)
                    .Prop(Control.StylePropertyModulateSelf, crtTextColor),
            };
        }
    }
}
