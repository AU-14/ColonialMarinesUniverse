using System.Linq;
using System.Numerics;
using Content.Client._RMC14;
using Content.Client.ContextMenu.UI;
using Content.Client.Examine;
using Content.Client.PDA;
using Content.Client.Resources;
using Content.Client.Silicons.Laws.SiliconLawEditUi;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Controls.FancyTree;
using Content.Client.Verbs.UI;
using Content.Shared.CCVar;
using Content.Shared.Verbs;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets
{
    public static class ResCacheExtension
    {
        public static Font NotoStack(this IResourceCache resCache, string variation = "Regular", int size = 10, bool display = false)
        {
            var ds = display ? "Display" : "";
            var sv = variation.StartsWith("Bold", StringComparison.Ordinal) ? "Bold" : "Regular";
            return resCache.GetFont
            (
                // Ew, but ok
                new[]
                {
                    $"/Fonts/NotoSans{ds}/NotoSans{ds}-{variation}.ttf",
                    $"/Fonts/NotoSans/NotoSansSymbols-{sv}.ttf",
                    "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
                },
                size
            );

        }

    }
    // STLYE SHEETS WERE A MISTAKE. KILL ALL OF THIS WITH FIRE
    public sealed class StyleNano : StyleBase
    {
        public const string StyleClassBorderedWindowPanel = "BorderedWindowPanel";
        public const string StyleClassInventorySlotBackground = "InventorySlotBackground";
        public const string StyleClassHandSlotHighlight = "HandSlotHighlight";
        public const string StyleClassChatPanel = "ChatPanel";
        public const string StyleClassChatSubPanel = "ChatSubPanel";
        public const string StyleClassTransparentBorderedWindowPanel = "TransparentBorderedWindowPanel";
        public const string StyleClassHotbarPanel = "HotbarPanel";
        public const string StyleClassTooltipPanel = "tooltipBox";
        public const string StyleClassTooltipAlertTitle = "tooltipAlertTitle";
        public const string StyleClassTooltipAlertDescription = "tooltipAlertDesc";
        public const string StyleClassTooltipAlertCooldown = "tooltipAlertCooldown";
        public const string StyleClassTooltipActionTitle = "tooltipActionTitle";
        public const string StyleClassTooltipActionDescription = "tooltipActionDesc";
        public const string StyleClassTooltipActionCooldown = "tooltipActionCooldown";
        public const string StyleClassTooltipActionDynamicMessage = "tooltipActionDynamicMessage";
        public const string StyleClassTooltipActionRequirements = "tooltipActionCooldown";
        public const string StyleClassTooltipActionCharges = "tooltipActionCharges";
        public const string StyleClassHotbarSlotNumber = "hotbarSlotNumber";
        public const string StyleClassActionSearchBox = "actionSearchBox";
        public const string StyleClassActionMenuItemRevoked = "actionMenuItemRevoked";
        public const string StyleClassChatLineEdit = "chatLineEdit";
        public const string StyleClassChatChannelSelectorButton = "chatSelectorOptionButton";
        public const string StyleClassChatFilterOptionButton = "chatFilterOptionButton";
        public const string StyleClassStorageButton = "storageButton";
        public const string StyleClassInset = "Inset";

        public const string StyleClassConsoleHeading = "ConsoleHeading";
        public const string StyleClassConsoleSubHeading = "ConsoleSubHeading";
        public const string StyleClassConsoleText = "ConsoleText";

        public const string StyleClassSliderRed = "Red";
        public const string StyleClassSliderGreen = "Green";
        public const string StyleClassSliderBlue = "Blue";
        public const string StyleClassSliderWhite = "White";

        public const string StyleClassLabelHeadingBigger = "LabelHeadingBigger";
        public const string StyleClassLabelKeyText = "LabelKeyText";
        public const string StyleClassLabelSecondaryColor = "LabelSecondaryColor";
        public const string StyleClassLabelBig = "LabelBig";
        public const string StyleClassLabelSmall = "LabelSmall";
        public const string StyleClassCharacterName = "CharacterName";
        public const string StyleClassCharacterNameInput = "CharacterNameInput";
        public const string StyleClassButtonBig = "ButtonBig";
        public const string StyleClassCrtWindow = "CrtWindow";
        public const string StyleClassCrtWindowHeader = "CrtWindowHeader";
        public const string StyleClassCrtWindowTitle = "CrtWindowTitle";
        public const string StyleClassCrtPanel = "CrtPanel";
        public const string StyleClassCrtInsetPanel = "CrtInsetPanel";
        public const string StyleClassCrtQuietPanel = "CrtQuietPanel";
        public const string StyleClassCrtHeaderPanel = "CrtHeaderPanel";
        public const string StyleClassCrtButton = "CrtButton";
        public const string StyleClassCrtAttentionButton = "CrtAttentionButton";
        public const string StyleClassCrtButtonLabel = "CrtButtonLabel";
        public const string StyleClassCrtNativeButtonLabel = "CrtNativeButtonLabel";
        public const string StyleClassCrtText = "CrtText";
        public const string StyleClassCrtDimText = "CrtDimText";
        public const string StyleClassCrtHeading = "CrtHeading";
        public const string StyleClassCrtHeadingBig = "CrtHeadingBig";
        public const string StyleClassCrtRichText = "CrtRichText";
        public const string StyleClassCrtLineEdit = "CrtLineEdit";
        public const string StyleClassCrtNativeLineEdit = "CrtNativeLineEdit";
        public const string StyleClassCrtSlider = "CrtSlider";
        public const string StyleClassCrtProgressBar = "CrtProgressBar";
        public const string StyleClassCrtTabContainer = "CrtTabContainer";
        public const string StyleClassCrtStripeBack = "CrtStripeBack";
        public const string StyleClassCrtIconButton = "CrtIconButton";
        public const string StyleClassCrtItemList = "CrtItemList";
        public const string StyleClassCrtScrollBar = "CrtScrollBar";

        public const string StyleClassButtonHelp = "HelpButton";

        public const string StyleClassPopupMessageSmall = "PopupMessageSmall";
        public const string StyleClassPopupMessageSmallCaution = "PopupMessageSmallCaution";
        public const string StyleClassPopupMessageMedium = "PopupMessageMedium";
        public const string StyleClassPopupMessageMediumCaution = "PopupMessageMediumCaution";
        public const string StyleClassPopupMessageLarge = "PopupMessageLarge";
        public const string StyleClassPopupMessageLargeCaution = "PopupMessageLargeCaution";

        public static readonly Color PanelDark = Color.FromHex("#1E1E22");

        public static readonly Color NanoGold = Color.FromHex("#A88B5E");
        private static CrtPalette _crtPalette = CrtPalette.Green;
        private static bool _crtUiEnabled = true;
        private static readonly Color DefaultCrtBackground = Color.FromHex("#07090B");
        private static readonly Color DefaultCrtPanelBackground = Color.FromHex("#25252A");
        private static readonly Color DefaultCrtPanelBackgroundAlt = Color.FromHex("#202023");
        private static readonly Color DefaultCrtInsetBackground = PanelDark;
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
        public static readonly Color GoodGreenFore = Color.FromHex("#31843E");
        public static readonly Color ConcerningOrangeFore = Color.FromHex("#A5762F");
        public static readonly Color DangerousRedFore = Color.FromHex("#BB3232");
        public static readonly Color DisabledFore = Color.FromHex("#5A5A5A");

        public static readonly Color ButtonColorDefault = Color.FromHex("#464966");
        public static readonly Color ButtonColorDefaultRed = Color.FromHex("#D43B3B");
        public static readonly Color ButtonColorHovered = Color.FromHex("#575b7f");
        public static readonly Color ButtonColorHoveredRed = Color.FromHex("#DF6B6B");
        public static readonly Color ButtonColorPressed = Color.FromHex("#3e6c45");
        public static readonly Color ButtonColorDisabled = Color.FromHex("#30313c");

        public static readonly Color ButtonColorCautionDefault = Color.FromHex("#ab3232");
        public static readonly Color ButtonColorCautionHovered = Color.FromHex("#cf2f2f");
        public static readonly Color ButtonColorCautionPressed = Color.FromHex("#3e6c45");
        public static readonly Color ButtonColorCautionDisabled = Color.FromHex("#602a2a");

        public static readonly Color ButtonColorGoodDefault = Color.FromHex("#3E6C45");
        public static readonly Color ButtonColorGoodHovered = Color.FromHex("#31843E");
        public static readonly Color ButtonColorGoodDisabled = Color.FromHex("#164420");

        //NavMap
        public static readonly Color PointRed = Color.FromHex("#B02E26");
        public static readonly Color PointGreen = Color.FromHex("#38b026");
        public static readonly Color PointMagenta = Color.FromHex("#FF00FF");

        // Context menu button colors
        public static readonly Color ButtonColorContext = Color.FromHex("#1119");
        public static readonly Color ButtonColorContextHover = Color.DarkSlateGray;
        public static readonly Color ButtonColorContextPressed = Color.LightSlateGray;
        public static readonly Color ButtonColorContextDisabled = Color.Black;

        // Examine button colors
        public static readonly Color ExamineButtonColorContext = Color.Transparent;
        public static readonly Color ExamineButtonColorContextHover = Color.DarkSlateGray;
        public static readonly Color ExamineButtonColorContextPressed = Color.LightSlateGray;
        public static readonly Color ExamineButtonColorContextDisabled = Color.FromHex("#5A5A5A");

        // Fancy Tree elements
        public static readonly Color FancyTreeEvenRowColor = Color.FromHex("#25252A");
        public static readonly Color FancyTreeOddRowColor = FancyTreeEvenRowColor * new Color(0.8f, 0.8f, 0.8f);
        public static readonly Color FancyTreeSelectedRowColor = new Color(55, 55, 68);

        //Used by the APC and SMES menus
        public const string StyleClassPowerStateNone = "PowerStateNone";
        public const string StyleClassPowerStateLow = "PowerStateLow";
        public const string StyleClassPowerStateGood = "PowerStateGood";

        public const string StyleClassItemStatus = "ItemStatus";
        public const string StyleClassItemStatusNotHeld = "ItemStatusNotHeld";
        public static readonly Color ItemStatusNotHeldColor = Color.Gray;

        //Background
        public const string StyleClassBackgroundBaseDark = "PanelBackgroundBaseDark";

        //Buttons
        public const string StyleClassCrossButtonRed = "CrossButtonRed";
        public const string StyleClassButtonColorRed = "ButtonColorRed";
        public const string StyleClassButtonColorGreen = "ButtonColorGreen";

        public static readonly Color ChatBackgroundColor = Color.FromHex("#07090B");

        //Bwoink
        public const string StyleClassPinButtonPinned = "pinButtonPinned";
        public const string StyleClassPinButtonUnpinned = "pinButtonUnpinned";

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


        public override Stylesheet Stylesheet { get; }

        public static void SetCrtPalette(string palette)
        {
            _crtPalette = palette switch
            {
                CCVars.CrtUiColorGreen => CrtPalette.Green,
                CCVars.CrtUiColorBlue => CrtPalette.Blue,
                CCVars.CrtUiColorOrange => CrtPalette.Orange,
                CCVars.CrtUiColorRed => CrtPalette.Red,
                CCVars.CrtUiColorPurple => CrtPalette.Purple,
                _ => Color.TryFromHex(palette) is { } color
                    ? CrtPalette.FromAccent(color)
                    : CrtPalette.Green,
            };
        }

        public static void SetCrtUiEnabled(bool enabled)
        {
            _crtUiEnabled = enabled;
        }

        public StyleNano(IResourceCache resCache) : base(resCache)
        {
            var notoSans8 = resCache.NotoStack(size: 8);
            var notoSans10 = resCache.NotoStack(size: 10);
            var notoSansItalic10 = resCache.NotoStack(variation: "Italic", size: 10);
            var notoSans12 = resCache.NotoStack(size: 12);
            var notoSansItalic12 = resCache.NotoStack(variation: "Italic", size: 12);
            var notoSansBold12 = resCache.NotoStack(variation: "Bold", size: 12);
            var notoSansBoldItalic12 = resCache.NotoStack(variation: "BoldItalic", size: 12);
            var notoSansBoldItalic14 = resCache.NotoStack(variation: "BoldItalic", size: 14);
            var notoSansBoldItalic16 = resCache.NotoStack(variation: "BoldItalic", size: 16);
            var notoSansDisplayBold14 = resCache.NotoStack(variation: "Bold", display: true, size: 14);
            var notoSansDisplayBold16 = resCache.NotoStack(variation: "Bold", display: true, size: 16);
            var notoSans15 = resCache.NotoStack(variation: "Regular", size: 15);
            var notoSans16 = resCache.NotoStack(variation: "Regular", size: 16);
            var notoSansBold16 = resCache.NotoStack(variation: "Bold", size: 16);
            var notoSansBold18 = resCache.NotoStack(variation: "Bold", size: 18);
            var notoSansBold20 = resCache.NotoStack(variation: "Bold", size: 20);
            var notoSansMono = resCache.GetFont("/EngineFonts/NotoSans/NotoSansMono-Regular.ttf", size: 12);
            var uavOsdStack = new[]
            {
                "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf",
                "/Fonts/NotoSans/NotoSans-Regular.ttf",
                "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
                "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
            };
            var robotoMonoBoldStack = new[]
            {
                "/Fonts/RobotoMono/RobotoMono-Bold.ttf",
                "/Fonts/NotoSans/NotoSans-Bold.ttf",
                "/Fonts/NotoSans/NotoSansSymbols-Bold.ttf",
                "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf"
            };
            var uavOsd13 = resCache.GetFont
            (
                uavOsdStack,
                size: 8
            );
            var uavOsd14 = resCache.GetFont
            (
                uavOsdStack,
                size: 8
            );
            var uavOsdBold14 = resCache.GetFont
            (
                uavOsdStack,
                size: 8
            );
            var uavOsdBold16 = resCache.GetFont
            (
                uavOsdStack,
                size: 10
            );
            var uavOsdBold18 = resCache.GetFont
            (
                uavOsdStack,
                size: 12
            );
            var robotoMonoBold11 = resCache.GetFont(robotoMonoBoldStack, size: 11);
            var robotoMonoBold12 = resCache.GetFont(robotoMonoBoldStack, size: 12);
            var robotoMonoBold14 = resCache.GetFont(robotoMonoBoldStack, size: 14);
            var useCrtUi = CrtUiEnabled;
            var crtTextFont = useCrtUi ? uavOsdBold14 : notoSans12;
            var crtDimFont = useCrtUi ? uavOsd13 : notoSans10;
            var crtHeadingFont = useCrtUi ? uavOsdBold16 : notoSansBold12;
            var crtHeadingBigFont = useCrtUi ? uavOsdBold18 : notoSansBold18;
            var crtRichTextFont = useCrtUi ? uavOsd14 : notoSans12;
            var crtButtonLabelFont = useCrtUi ? uavOsdBold14 : notoSans12;
            var crtLineEditFont = useCrtUi ? uavOsd14 : notoSans12;
            var crtNativeLineEditFont = notoSans12;
            var characterNameFont = useCrtUi ? robotoMonoBold12 : notoSans12;
            var crtTextColor = useCrtUi ? CrtGreenSoft : Color.White;
            var crtDimTextColor = useCrtUi ? CrtGreenDim : Color.FromHex("#B8B8B8");
            var crtHeadingColor = useCrtUi ? CrtGreen : NanoGold;
            var crtSelectionColor = (useCrtUi ? CrtGreen : NanoGold).WithAlpha(useCrtUi ? 0.33f : 0.25f);

            var windowHeaderTex = resCache.GetTexture("/Textures/Interface/Nano/window_header.png");
            var windowHeader = new StyleBoxTexture
            {
                Texture = windowHeaderTex,
                PatchMarginBottom = 3,
                ExpandMarginBottom = 3,
                ContentMarginBottomOverride = 0
            };
            var windowHeaderAlertTex = resCache.GetTexture("/Textures/Interface/Nano/window_header_alert.png");
            var windowHeaderAlert = new StyleBoxTexture
            {
                Texture = windowHeaderAlertTex,
                PatchMarginBottom = 3,
                ExpandMarginBottom = 3,
                ContentMarginBottomOverride = 0
            };
            var windowBackgroundTex = resCache.GetTexture("/Textures/Interface/Nano/window_background.png");
            var windowBackground = new StyleBoxTexture
            {
                Texture = windowBackgroundTex,
            };
            windowBackground.SetPatchMargin(StyleBox.Margin.Horizontal | StyleBox.Margin.Bottom, 2);
            windowBackground.SetExpandMargin(StyleBox.Margin.Horizontal | StyleBox.Margin.Bottom, 2);

            var borderedWindowBackgroundTex = resCache.GetTexture("/Textures/Interface/Nano/window_background_bordered.png");
            var borderedWindowBackground = new StyleBoxTexture
            {
                Texture = borderedWindowBackgroundTex,
            };
            borderedWindowBackground.SetPatchMargin(StyleBox.Margin.All, 2);

            var contextMenuBackground = new StyleBoxTexture
            {
                Texture = borderedWindowBackgroundTex,
            };
            contextMenuBackground.SetPatchMargin(StyleBox.Margin.All, ContextMenuElement.ElementMargin);

            var invSlotBgTex = resCache.GetTexture("/Textures/Interface/Inventory/inv_slot_background.png");
            var invSlotBg = new StyleBoxTexture
            {
                Texture = invSlotBgTex,
            };
            invSlotBg.SetPatchMargin(StyleBox.Margin.All, 2);
            invSlotBg.SetContentMarginOverride(StyleBox.Margin.All, 0);

            var handSlotHighlightTex = resCache.GetTexture("/Textures/Interface/Inventory/hand_slot_highlight.png");
            var handSlotHighlight = new StyleBoxTexture
            {
                Texture = handSlotHighlightTex,
            };
            handSlotHighlight.SetPatchMargin(StyleBox.Margin.All, 2);

            var borderedTransparentWindowBackgroundTex = resCache.GetTexture("/Textures/Interface/Nano/transparent_window_background_bordered.png");
            var borderedTransparentWindowBackground = new StyleBoxTexture
            {
                Texture = borderedTransparentWindowBackgroundTex,
            };
            borderedTransparentWindowBackground.SetPatchMargin(StyleBox.Margin.All, 2);

            var hotbarBackground = new StyleBoxTexture
            {
                Texture = borderedWindowBackgroundTex,
            };
            hotbarBackground.SetPatchMargin(StyleBox.Margin.All, 2);
            hotbarBackground.SetExpandMargin(StyleBox.Margin.All, 4);

            var buttonStorage = new StyleBoxTexture(BaseButton);
            buttonStorage.SetPatchMargin(StyleBox.Margin.All, 10);
            buttonStorage.SetPadding(StyleBox.Margin.All, 0);
            buttonStorage.SetContentMarginOverride(StyleBox.Margin.Vertical, 0);
            buttonStorage.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);

            var buttonContext = new StyleBoxTexture { Texture = Texture.White };

            var buttonRectTex = resCache.GetTexture("/Textures/Interface/Nano/light_panel_background_bordered.png");
            var buttonRect = new StyleBoxTexture(BaseButton)
            {
                Texture = buttonRectTex
            };
            buttonRect.SetPatchMargin(StyleBox.Margin.All, 2);
            buttonRect.SetPadding(StyleBox.Margin.All, 2);
            buttonRect.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            buttonRect.SetContentMarginOverride(StyleBox.Margin.Horizontal, 2);

            var buttonRectHover = new StyleBoxTexture(buttonRect)
            {
                Modulate = ButtonColorHovered
            };

            var buttonRectPressed = new StyleBoxTexture(buttonRect)
            {
                Modulate = ButtonColorPressed
            };

            var buttonRectDisabled = new StyleBoxTexture(buttonRect)
            {
                Modulate = ButtonColorDisabled
            };

            var buttonRectActionMenuItemTex = resCache.GetTexture("/Textures/Interface/Nano/black_panel_light_thin_border.png");
            var buttonRectActionMenuRevokedItemTex = resCache.GetTexture("/Textures/Interface/Nano/black_panel_red_thin_border.png");
            var buttonRectActionMenuItem = new StyleBoxTexture(BaseButton)
            {
                Texture = buttonRectActionMenuItemTex
            };
            buttonRectActionMenuItem.SetPatchMargin(StyleBox.Margin.All, 2);
            buttonRectActionMenuItem.SetPadding(StyleBox.Margin.All, 2);
            buttonRectActionMenuItem.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            buttonRectActionMenuItem.SetContentMarginOverride(StyleBox.Margin.Horizontal, 2);
            var buttonRectActionMenuItemRevoked = new StyleBoxTexture(buttonRectActionMenuItem)
            {
                Texture = buttonRectActionMenuRevokedItemTex
            };
            var buttonRectActionMenuItemHover = new StyleBoxTexture(buttonRectActionMenuItem)
            {
                Modulate = ButtonColorHovered
            };
            var buttonRectActionMenuItemPressed = new StyleBoxTexture(buttonRectActionMenuItem)
            {
                Modulate = ButtonColorPressed
            };

            var buttonTex = resCache.GetTexture("/Textures/Interface/Nano/button.svg.96dpi.png");
            var topButtonBase = new StyleBoxTexture
            {
                Texture = buttonTex,
            };
            topButtonBase.SetPatchMargin(StyleBox.Margin.All, 10);
            topButtonBase.SetPadding(StyleBox.Margin.All, 0);
            topButtonBase.SetContentMarginOverride(StyleBox.Margin.All, 0);

            var topButtonOpenRight = new StyleBoxTexture(topButtonBase)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(0, 0), new Vector2(14, 24))),
            };
            topButtonOpenRight.SetPatchMargin(StyleBox.Margin.Right, 0);

            var topButtonOpenLeft = new StyleBoxTexture(topButtonBase)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(14, 24))),
            };
            topButtonOpenLeft.SetPatchMargin(StyleBox.Margin.Left, 0);

            var topButtonSquare = new StyleBoxTexture(topButtonBase)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(3, 24))),
            };
            topButtonSquare.SetPatchMargin(StyleBox.Margin.Horizontal, 0);

            var chatChannelButtonTex = resCache.GetTexture("/Textures/Interface/Nano/rounded_button.svg.96dpi.png");
            var chatChannelButton = new StyleBoxTexture
            {
                Texture = chatChannelButtonTex,
            };
            chatChannelButton.SetPatchMargin(StyleBox.Margin.All, 5);
            chatChannelButton.SetPadding(StyleBox.Margin.All, 2);

            var chatFilterButtonTex = resCache.GetTexture("/Textures/Interface/Nano/rounded_button_bordered.svg.96dpi.png");
            var chatFilterButton = new StyleBoxTexture
            {
                Texture = chatFilterButtonTex,
            };
            chatFilterButton.SetPatchMargin(StyleBox.Margin.All, 5);
            chatFilterButton.SetPadding(StyleBox.Margin.All, 2);

            var outputPanelScrollDownButtonTex = resCache.GetTexture("/Textures/Interface/Nano/rounded_button_half_bordered.svg.96dpi.png");
            var outputPanelScrollDownButton = new StyleBoxTexture
            {
                Texture = outputPanelScrollDownButtonTex,
            };
            outputPanelScrollDownButton.SetPatchMargin(StyleBox.Margin.All, 5);
            outputPanelScrollDownButton.SetPadding(StyleBox.Margin.All, 2);
            outputPanelScrollDownButton.SetPadding(StyleBox.Margin.Top, 0);
            outputPanelScrollDownButton.SetPadding(StyleBox.Margin.Bottom, 0);

            var smallButtonTex = resCache.GetTexture("/Textures/Interface/Nano/button_small.svg.96dpi.png");
            var smallButtonBase = new StyleBoxTexture
            {
                Texture = smallButtonTex,
            };

            var textureInvertedTriangle = resCache.GetTexture("/Textures/Interface/Nano/inverted_triangle.svg.png");

            var lineEditTex = resCache.GetTexture("/Textures/Interface/Nano/lineedit.png");
            var lineEdit = new StyleBoxTexture
            {
                Texture = lineEditTex,
            };
            lineEdit.SetPatchMargin(StyleBox.Margin.All, 3);
            lineEdit.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

            var crtWindowPanel = new CrtStyleBox
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.72f),
                ScanlineColor = CrtGreen.WithAlpha(0.01f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                DrawPixelation = false,
                MaxScanlines = 1
            };

            var crtWindowHeader = new CrtStyleBox
            {
                BackgroundColor = CrtHeaderBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.85f),
                ScanlineColor = CrtGreen.WithAlpha(0.012f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                DrawCornerTicks = false,
                DrawPixelation = false,
                MaxScanlines = 1,
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4
            };

            var crtPanel = new CrtStyleBox
            {
                BackgroundColor = CrtPanelBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.28f),
                ScanlineColor = CrtGreen.WithAlpha(0.016f),
                GridColor = CrtGreen.WithAlpha(0.01f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.045f),
                PixelationColor = CrtGreen.WithAlpha(0.035f),
                PixelationShadowColor = CrtBackground.WithAlpha(0.16f),
                BorderThickness = new Thickness(1),
                DrawGrid = false,
                DrawPixelation = true,
                CornerLength = 10,
                PixelationBlockSize = 3,
                PixelationSpacing = 150,
                PixelationChance = 12,
                PixelationClusterSize = 2,
                PixelationSeed = 61,
                NoiseSpacing = 10,
                NoiseChance = 9,
                NoiseSeed = 11,
                ContentMarginLeftOverride = 10,
                ContentMarginRightOverride = 10,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            };

            var crtInsetPanel = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.22f),
                ScanlineColor = CrtGreen.WithAlpha(0.014f),
                GridColor = CrtGreen.WithAlpha(0.008f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.04f),
                PixelationColor = CrtGreen.WithAlpha(0.03f),
                PixelationShadowColor = CrtPanelBackground.WithAlpha(0.14f),
                BorderThickness = new Thickness(1),
                DrawGrid = false,
                DrawPixelation = true,
                CornerLength = 8,
                PixelationBlockSize = 2,
                PixelationSpacing = 140,
                PixelationChance = 14,
                PixelationClusterSize = 1,
                PixelationSeed = 67,
                NoiseSpacing = 11,
                NoiseChance = 10,
                NoiseSeed = 17,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 6,
                ContentMarginBottomOverride = 6
            };

            var crtQuietPanel = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.28f),
                ScanlineColor = CrtGreen.WithAlpha(0.008f),
                BorderThickness = new Thickness(0),
                DrawCornerTicks = false,
                DrawPixelation = false,
                MaxScanlines = 1,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4
            };

            var crtHeaderPanel = new CrtStyleBox
            {
                BackgroundColor = CrtHeaderBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.24f),
                ScanlineColor = CrtBackground.WithAlpha(0.06f),
                DrawNoise = false,
                DrawPixelation = true,
                PixelationColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationShadowColor = CrtBackground.WithAlpha(0.12f),
                PixelationBlockSize = 2,
                PixelationSpacing = 120,
                PixelationChance = 14,
                PixelationClusterSize = 1,
                PixelationSeed = 71,
                MaxScanlines = 1,
                DrawCornerTicks = false,
                BorderThickness = new Thickness(0, 0, 0, 1),
                ContentMarginLeftOverride = 6,
                ContentMarginRightOverride = 6,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var crtButton = new CrtStyleBox
            {
                BackgroundColor = CrtButtonBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.24f),
                ScanlineColor = CrtGreen.WithAlpha(0.012f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationColor = CrtGreen.WithAlpha(0.02f),
                PixelationShadowColor = CrtBackground.WithAlpha(0.1f),
                BorderThickness = new Thickness(1),
                DrawPixelation = true,
                CornerLength = 6,
                PixelationBlockSize = 2,
                PixelationSpacing = 118,
                PixelationChance = 18,
                PixelationClusterSize = 1,
                PixelationSeed = 73,
                NoiseSpacing = 12,
                NoiseChance = 12,
                NoiseSeed = 23,
                MaxScanlines = 1,
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 3,
                ContentMarginBottomOverride = 3
            };

            var crtButtonHover = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtButtonHoverBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.34f),
                ScanlineColor = CrtGreen.WithAlpha(0.02f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.055f),
                PixelationColor = CrtGreenSoft.WithAlpha(0.03f)
            };

            var crtButtonPressed = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtButtonPressedBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.42f)
            };

            var crtButtonDisabled = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtButtonDisabledBackground,
                BorderColor = CrtGreenDisabled,
                CornerColor = CrtGreenDisabled.WithAlpha(0.24f),
                ScanlineColor = CrtGreenDisabled.WithAlpha(0.012f),
                NoiseColor = CrtGreenDisabled.WithAlpha(0.035f)
            };

            var crtAttentionButton = new CrtStyleBox(crtButton)
            {
                BackgroundColor = CrtButtonHoverBackground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.38f),
                ScanlineColor = CrtGreen.WithAlpha(0.02f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.055f),
                PixelationColor = CrtGreenSoft.WithAlpha(0.03f)
            };

            var crtAttentionButtonHover = new CrtStyleBox(crtAttentionButton)
            {
                BackgroundColor = CrtButtonPressedBackground,
                CornerColor = CrtGreenSoft.WithAlpha(0.46f)
            };

            var crtAttentionButtonPressed = new CrtStyleBox(crtAttentionButton)
            {
                BackgroundColor = CrtButtonPressedBackground,
                BorderColor = CrtGreenSoft,
                CornerColor = CrtGreenSoft.WithAlpha(0.52f)
            };

            var crtLineEdit = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.2f),
                ScanlineColor = CrtGreen.WithAlpha(0.012f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationColor = CrtGreen.WithAlpha(0.018f),
                PixelationShadowColor = CrtPanelBackground.WithAlpha(0.11f),
                BorderThickness = new Thickness(1),
                DrawPixelation = true,
                DrawCornerTicks = false,
                PixelationBlockSize = 2,
                PixelationSpacing = 116,
                PixelationChance = 18,
                PixelationClusterSize = 1,
                PixelationSeed = 79,
                NoiseSpacing = 12,
                NoiseChance = 12,
                NoiseSeed = 31,
                MaxScanlines = 1,
                ContentMarginLeftOverride = 5,
                ContentMarginRightOverride = 5,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var crtNativeLineEdit = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim.WithAlpha(0.55f),
                ScanlineColor = CrtGreen.WithAlpha(0.008f),
                BorderThickness = new Thickness(0, 0, 0, 1),
                DrawCornerTicks = false,
                DrawPixelation = false,
                MaxScanlines = 1,
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
                ScanlineColor = CrtGreen.WithAlpha(0.016f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.04f),
                PixelationColor = CrtGreenSoft.WithAlpha(0.025f),
                PixelationShadowColor = CrtBackground.WithAlpha(0.1f),
                BorderThickness = new Thickness(1, 1, 1, 0),
                DrawPixelation = true,
                CornerLength = 8,
                PixelationBlockSize = 2,
                PixelationSpacing = 112,
                PixelationChance = 18,
                PixelationClusterSize = 1,
                PixelationSeed = 83,
                NoiseSpacing = 12,
                NoiseChance = 12,
                NoiseSeed = 37,
                MaxScanlines = 1,
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
                ScanlineColor = CrtGreen.WithAlpha(0.01f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.03f),
                BorderThickness = new Thickness(1, 1, 1, 0),
                DrawCornerTicks = false,
                NoiseSpacing = 13,
                NoiseChance = 13,
                NoiseSeed = 41,
                MaxScanlines = 1,
                ContentMarginLeftOverride = 8,
                ContentMarginRightOverride = 8,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var crtSliderBackground = new CrtStyleBox
            {
                BackgroundColor = CrtBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.16f),
                ScanlineColor = CrtGreen.WithAlpha(0.01f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                NoiseSpacing = 12,
                NoiseChance = 13,
                NoiseSeed = 43,
                MaxScanlines = 1,
                ContentMarginTopOverride = 8,
                ContentMarginBottomOverride = 8
            };

            var crtSliderForeground = new StyleBoxFlat
            {
                BackgroundColor = CrtSliderForeground,
            };
            crtSliderForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

            var crtSliderFill = new StyleBoxFlat
            {
                BackgroundColor = CrtGreenDim,
            };
            crtSliderFill.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);

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
                ScanlineColor = CrtGreen.WithAlpha(0.012f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationColor = CrtGreen.WithAlpha(0.02f),
                PixelationShadowColor = CrtPanelBackground.WithAlpha(0.12f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                DrawPixelation = true,
                PixelationBlockSize = 2,
                PixelationSpacing = 116,
                PixelationChance = 18,
                PixelationClusterSize = 1,
                PixelationSeed = 107,
                NoiseSpacing = 12,
                NoiseChance = 12,
                NoiseSeed = 109,
                MaxScanlines = 1,
            };
            crtProgressBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 10);

            var crtProgressForeground = new CrtStyleBox
            {
                BackgroundColor = CrtProgressForeground,
                BorderColor = CrtGreen,
                CornerColor = CrtGreenSoft.WithAlpha(0.2f),
                ScanlineColor = CrtBackground.WithAlpha(0.04f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationColor = CrtGreenSoft.WithAlpha(0.025f),
                PixelationShadowColor = CrtBackground.WithAlpha(0.1f),
                BorderThickness = new Thickness(1),
                DrawCornerTicks = false,
                DrawPixelation = true,
                PixelationBlockSize = 2,
                PixelationSpacing = 108,
                PixelationChance = 18,
                PixelationClusterSize = 1,
                PixelationSeed = 113,
                NoiseSpacing = 12,
                NoiseChance = 12,
                NoiseSeed = 127,
                MaxScanlines = 1,
            };
            crtProgressForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 10);

            var crtItemListBackground = new CrtStyleBox
            {
                BackgroundColor = CrtInsetBackground,
                BorderColor = CrtGreenDim,
                CornerColor = CrtGreen.WithAlpha(0.14f),
                ScanlineColor = CrtGreen.WithAlpha(0.012f),
                GridColor = CrtGreen.WithAlpha(0.008f),
                NoiseColor = CrtGreenSoft.WithAlpha(0.035f),
                PixelationColor = CrtGreen.WithAlpha(0.02f),
                PixelationShadowColor = CrtPanelBackground.WithAlpha(0.12f),
                BorderThickness = new Thickness(1),
                DrawGrid = false,
                DrawPixelation = true,
                DrawCornerTicks = false,
                PixelationBlockSize = 2,
                PixelationSpacing = 150,
                PixelationChance = 16,
                PixelationClusterSize = 1,
                PixelationSeed = 97,
                NoiseSpacing = 11,
                NoiseChance = 11,
                NoiseSeed = 101,
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

            var chatBg = new StyleBoxFlat
            {
                BackgroundColor = ChatBackgroundColor,
                BorderColor = Color.FromHex("#263039"),
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 2,
                ContentMarginRightOverride = 2,
                ContentMarginTopOverride = 2,
                ContentMarginBottomOverride = 2
            };

            var chatSubBg = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#101317"),
                BorderColor = Color.FromHex("#2f3941"),
                BorderThickness = new Thickness(1),
            };
            chatSubBg.SetContentMarginOverride(StyleBox.Margin.All, 2);

            var actionSearchBoxTex = resCache.GetTexture("/Textures/Interface/Nano/black_panel_dark_thin_border.png");
            var actionSearchBox = new StyleBoxTexture
            {
                Texture = actionSearchBoxTex,
            };
            actionSearchBox.SetPatchMargin(StyleBox.Margin.All, 3);
            actionSearchBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

            var tabContainerPanelTex = resCache.GetTexture("/Textures/Interface/Nano/tabcontainer_panel.png");
            var tabContainerPanel = new StyleBoxTexture
            {
                Texture = tabContainerPanelTex,
            };
            tabContainerPanel.SetPatchMargin(StyleBox.Margin.All, 2);

            var tabContainerBoxActive = new StyleBoxFlat { BackgroundColor = new Color(64, 64, 64) };
            tabContainerBoxActive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);
            var tabContainerBoxInactive = new StyleBoxFlat { BackgroundColor = new Color(32, 32, 32) };
            tabContainerBoxInactive.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);

            var progressBarBackground = new StyleBoxFlat
            {
                BackgroundColor = new Color(0.25f, 0.25f, 0.25f)
            };
            progressBarBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);

            var progressBarForeground = new StyleBoxFlat
            {
                BackgroundColor = new Color(0.25f, 0.50f, 0.25f)
            };
            progressBarForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 14.5f);

            // Monotone (unfilled)
            var monotoneButton = new StyleBoxTexture
            {
                Texture = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_button.svg.96dpi.png"),
            };
            monotoneButton.SetPatchMargin(StyleBox.Margin.All, 11);
            monotoneButton.SetPadding(StyleBox.Margin.All, 1);
            monotoneButton.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            monotoneButton.SetContentMarginOverride(StyleBox.Margin.Horizontal, 14);

            var monotoneButtonOpenLeft = new StyleBoxTexture(monotoneButton)
            {
                Texture = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_button_open_left.svg.96dpi.png"),
            };

            var monotoneButtonOpenRight = new StyleBoxTexture(monotoneButton)
            {
                Texture = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_button_open_right.svg.96dpi.png"),
            };

            var monotoneButtonOpenBoth = new StyleBoxTexture(monotoneButton)
            {
                Texture = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_button_open_both.svg.96dpi.png"),
            };

            // Monotone (filled)
            var monotoneFilledButton = new StyleBoxTexture(monotoneButton)
            {
                Texture = buttonTex,
            };

            var monotoneFilledButtonOpenLeft = new StyleBoxTexture(monotoneButton)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(14, 24))),
            };
            monotoneFilledButtonOpenLeft.SetPatchMargin(StyleBox.Margin.Left, 0);

            var monotoneFilledButtonOpenRight = new StyleBoxTexture(monotoneButton)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(0, 0), new Vector2(14, 24))),
            };
            monotoneFilledButtonOpenRight.SetPatchMargin(StyleBox.Margin.Right, 0);

            var monotoneFilledButtonOpenBoth = new StyleBoxTexture(monotoneButton)
            {
                Texture = new AtlasTexture(buttonTex, UIBox2.FromDimensions(new Vector2(10, 0), new Vector2(3, 24))),
            };
            monotoneFilledButtonOpenBoth.SetPatchMargin(StyleBox.Margin.Horizontal, 0);

            // CheckBox
            var checkBoxTextureChecked = resCache.GetTexture("/Textures/Interface/Nano/checkbox_checked.svg.96dpi.png");
            var checkBoxTextureUnchecked = resCache.GetTexture("/Textures/Interface/Nano/checkbox_unchecked.svg.96dpi.png");
            var monotoneCheckBoxTextureChecked = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_checkbox_checked.svg.96dpi.png");
            var monotoneCheckBoxTextureUnchecked = resCache.GetTexture("/Textures/Interface/Nano/Monotone/monotone_checkbox_unchecked.svg.96dpi.png");

            // Tooltip box
            var tooltipTexture = resCache.GetTexture("/Textures/Interface/Nano/tooltip.png");
            var tooltipBox = new StyleBoxTexture
            {
                Texture = tooltipTexture,
            };
            tooltipBox.SetPatchMargin(StyleBox.Margin.All, 2);
            tooltipBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

            // Whisper box
            var whisperTexture = resCache.GetTexture("/Textures/Interface/Nano/whisper.png");
            var whisperBox = new StyleBoxTexture
            {
                Texture = whisperTexture,
            };
            whisperBox.SetPatchMargin(StyleBox.Margin.All, 2);
            whisperBox.SetContentMarginOverride(StyleBox.Margin.Horizontal, 7);

            // Placeholder
            var placeholderTexture = resCache.GetTexture("/Textures/Interface/Nano/placeholder.png");
            var placeholder = new StyleBoxTexture { Texture = placeholderTexture };
            placeholder.SetPatchMargin(StyleBox.Margin.All, 19);
            placeholder.SetExpandMargin(StyleBox.Margin.All, -5);
            placeholder.Mode = StyleBoxTexture.StretchMode.Tile;

            var itemListBackgroundSelected = new StyleBoxFlat { BackgroundColor = new Color(75, 75, 86) };
            itemListBackgroundSelected.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            itemListBackgroundSelected.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
            var itemListItemBackgroundDisabled = new StyleBoxFlat { BackgroundColor = new Color(10, 10, 12) };
            itemListItemBackgroundDisabled.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            itemListItemBackgroundDisabled.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
            var itemListItemBackground = new StyleBoxFlat { BackgroundColor = new Color(55, 55, 68) };
            itemListItemBackground.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            itemListItemBackground.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);
            var itemListItemBackgroundTransparent = new StyleBoxFlat { BackgroundColor = Color.Transparent };
            itemListItemBackgroundTransparent.SetContentMarginOverride(StyleBox.Margin.Vertical, 2);
            itemListItemBackgroundTransparent.SetContentMarginOverride(StyleBox.Margin.Horizontal, 4);

            var squareTex = resCache.GetTexture("/Textures/Interface/Nano/square.png");
            var listContainerButton = new StyleBoxTexture
            {
                Texture = squareTex,
                ContentMarginLeftOverride = 10
            };

            // NanoHeading
            var nanoHeadingTex = resCache.GetTexture("/Textures/Interface/Nano/nanoheading.svg.96dpi.png");
            var nanoHeadingBox = new StyleBoxTexture
            {
                Texture = nanoHeadingTex,
                PatchMarginRight = 10,
                PatchMarginTop = 10,
                ContentMarginTopOverride = 2,
                ContentMarginLeftOverride = 10,
                PaddingTop = 4
            };

            nanoHeadingBox.SetPatchMargin(StyleBox.Margin.Left | StyleBox.Margin.Bottom, 2);

            // Stripe background
            var stripeBackTex = resCache.GetTexture("/Textures/Interface/Nano/stripeback.svg.96dpi.png");
            var stripeBack = new StyleBoxTexture
            {
                Texture = stripeBackTex,
                Mode = StyleBoxTexture.StretchMode.Tile
            };

            // Slider
            var sliderOutlineTex = resCache.GetTexture("/Textures/Interface/Nano/slider_outline.svg.96dpi.png");
            var sliderFillTex = resCache.GetTexture("/Textures/Interface/Nano/slider_fill.svg.96dpi.png");
            var sliderGrabTex = resCache.GetTexture("/Textures/Interface/Nano/slider_grabber.svg.96dpi.png");

            var sliderFillBox = new StyleBoxTexture
            {
                Texture = sliderFillTex,
                Modulate = Color.FromHex("#3E6C45")
            };

            var sliderBackBox = new StyleBoxTexture
            {
                Texture = sliderFillTex,
                Modulate = PanelDark,
            };

            var sliderForeBox = new StyleBoxTexture
            {
                Texture = sliderOutlineTex,
                Modulate = Color.FromHex("#494949")
            };

            var sliderGrabBox = new StyleBoxTexture
            {
                Texture = sliderGrabTex,
            };

            sliderFillBox.SetPatchMargin(StyleBox.Margin.All, 12);
            sliderBackBox.SetPatchMargin(StyleBox.Margin.All, 12);
            sliderForeBox.SetPatchMargin(StyleBox.Margin.All, 12);
            sliderGrabBox.SetPatchMargin(StyleBox.Margin.All, 12);

            var sliderFillGreen = new StyleBoxTexture(sliderFillBox) { Modulate = Color.LimeGreen };
            var sliderFillRed = new StyleBoxTexture(sliderFillBox) { Modulate = Color.Red };
            var sliderFillBlue = new StyleBoxTexture(sliderFillBox) { Modulate = Color.Blue };
            var sliderFillWhite = new StyleBoxTexture(sliderFillBox) { Modulate = Color.White };

            var boxFont13 = resCache.GetFont("/Fonts/Boxfont-round/Boxfont Round.ttf", 13);

            var insetBack = new StyleBoxTexture
            {
                Texture = buttonTex,
                Modulate = Color.FromHex("#202023"),
            };
            insetBack.SetPatchMargin(StyleBox.Margin.All, 10);

            // Default paper background:
            var paperBackground = new StyleBoxTexture
            {
                Texture = resCache.GetTexture("/Textures/Interface/Paper/paper_background_default.svg.96dpi.png"),
                Modulate = Color.FromHex("#eaedde"), // A light cream
            };
            paperBackground.SetPatchMargin(StyleBox.Margin.All, 16.0f);

            var contextMenuExpansionTexture = resCache.GetTexture("/Textures/Interface/VerbIcons/group.svg.192dpi.png");
            var verbMenuConfirmationTexture = resCache.GetTexture("/Textures/Interface/VerbIcons/group.svg.192dpi.png");

            // south-facing arrow:
            var directionIconArrowTex = resCache.GetTexture("/Textures/Interface/VerbIcons/drop.svg.192dpi.png");
            var directionIconQuestionTex = resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png");
            var directionIconHereTex = resCache.GetTexture("/Textures/Interface/VerbIcons/dot.svg.192dpi.png");

            Stylesheet = new Stylesheet(BaseRules.Concat(new[]
            {
                Element().Class("monospace")
                    .Prop("font", notoSansMono),
                // Window title.
                new StyleRule(
                    new SelectorElement(typeof(Label), new[] {DefaultWindow.StyleClassWindowTitle}, null, null),
                    new[]
                    {
                        new StyleProperty(Label.StylePropertyFontColor, NanoGold),
                        new StyleProperty(Label.StylePropertyFont, notoSansDisplayBold14),
                    }),
                // Alert (white) window title.
                new StyleRule(
                    new SelectorElement(typeof(Label), new[] {"windowTitleAlert"}, null, null),
                    new[]
                    {
                        new StyleProperty(Label.StylePropertyFontColor, Color.White),
                        new StyleProperty(Label.StylePropertyFont, notoSansDisplayBold14),
                    }),
                // Window background.
                new StyleRule(
                    new SelectorElement(null, new[] {DefaultWindow.StyleClassWindowPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, windowBackground),
                    }),
                // bordered window background
                new StyleRule(
                    new SelectorElement(null, new[] {StyleClassBorderedWindowPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, borderedWindowBackground),
                    }),
                new StyleRule(
                    new SelectorElement(null, new[] {StyleClassTransparentBorderedWindowPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, borderedTransparentWindowBackground),
                    }),
                // inventory slot background
                new StyleRule(
                    new SelectorElement(null, new[] {StyleClassInventorySlotBackground}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, invSlotBg),
                    }),
                // hand slot highlight
                new StyleRule(
                    new SelectorElement(null, new[] {StyleClassHandSlotHighlight}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, handSlotHighlight),
                    }),
                // Hotbar background
                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {StyleClassHotbarPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, hotbarBackground),
                    }),
                // Window header.
                new StyleRule(
                    new SelectorElement(typeof(PanelContainer), new[] {DefaultWindow.StyleClassWindowHeader}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, windowHeader),
                    }),
                // Alert (red) window header.
                new StyleRule(
                    new SelectorElement(typeof(PanelContainer), new[] {"windowHeaderAlert"}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, windowHeaderAlert),
                    }),

                Child().Parent(Element<DefaultWindow>().Class(StyleClassCrtWindow))
                    .Child(Element<PanelContainer>().Class(DefaultWindow.StyleClassWindowPanel))
                    .Prop(PanelContainer.StylePropertyPanel, crtWindowPanel),

                Element<PanelContainer>().Class(StyleClassCrtWindowHeader)
                    .Prop(PanelContainer.StylePropertyPanel, crtWindowHeader),

                Element<Label>().Class(StyleClassCrtWindowTitle)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyFont, notoSansDisplayBold14),

                // Shapes for the buttons.
                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, BaseButton),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Class(ButtonOpenRight)
                    .Prop(ContainerButton.StylePropertyStyleBox, BaseButtonOpenRight),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Class(ButtonOpenLeft)
                    .Prop(ContainerButton.StylePropertyStyleBox, BaseButtonOpenLeft),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Class(ButtonOpenBoth)
                    .Prop(ContainerButton.StylePropertyStyleBox, BaseButtonOpenBoth),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Class(ButtonSquare)
                    .Prop(ContainerButton.StylePropertyStyleBox, BaseButtonSquare),

                new StyleRule(new SelectorElement(typeof(Label), new[] { Button.StyleClassButton }, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyAlignMode, Label.AlignMode.Center),
                }),

                // Colors for the buttons.
                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),

                // Colors for the caution buttons.
                Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(ButtonCaution)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDefault),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(ButtonCaution)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionHovered),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(ButtonCaution)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionPressed),

                Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(ButtonCaution)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDisabled),

                // Colors for confirm buttons confirm states.
                Element<ConfirmButton>()
                    .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDefault),

                Element<ConfirmButton>()
                    .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionHovered),

                Element<ConfirmButton>()
                    .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionPressed),

                Element<ConfirmButton>()
                    .Pseudo(ConfirmButton.ConfirmPrefix + ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDisabled),

                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button), null, null, new[] {ContainerButton.StylePseudoClassDisabled}),
                    new SelectorElement(typeof(Label), null, null, null)),
                    new[]
                    {
                        new StyleProperty("font-color", Color.FromHex("#E5E5E581")),
                    }),

                // ItemStatus for hands
                Element()
                    .Class(StyleClassItemStatusNotHeld)
                    .Prop("font", notoSansItalic10)
                    .Prop("font-color", ItemStatusNotHeldColor)
                    .Prop(nameof(Control.Margin), new Thickness(4, 0, 0, 2)),

                Element()
                    .Class(StyleClassItemStatus)
                    .Prop(nameof(RichTextLabel.LineHeightScale), 0.7f)
                    .Prop(nameof(Control.Margin), new Thickness(4, 0, 0, 2)),

                // Context Menu window
                Element<PanelContainer>().Class(ContextMenuPopup.StyleClassContextMenuPopup)
                    .Prop(PanelContainer.StylePropertyPanel, contextMenuBackground),

                // Context menu buttons
                Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

                Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorContext),

                Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorContextHover),

                Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorContextPressed),

                Element<ContextMenuElement>().Class(ContextMenuElement.StyleClassContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorContextDisabled),

                // Context Menu Labels
                Element<RichTextLabel>().Class(InteractionVerb.DefaultTextStyleClass)
                    .Prop(Label.StylePropertyFont, notoSansBoldItalic12),

                Element<RichTextLabel>().Class(ActivationVerb.DefaultTextStyleClass)
                    .Prop(Label.StylePropertyFont, notoSansBold12),

                Element<RichTextLabel>().Class(AlternativeVerb.DefaultTextStyleClass)
                    .Prop(Label.StylePropertyFont, notoSansItalic12),

                Element<RichTextLabel>().Class(Verb.DefaultTextStyleClass)
                    .Prop(Label.StylePropertyFont, notoSans12),

                Element<TextureRect>().Class(ContextMenuElement.StyleClassContextMenuExpansionTexture)
                    .Prop(TextureRect.StylePropertyTexture, contextMenuExpansionTexture),

                Element<TextureRect>().Class(VerbMenuElement.StyleClassVerbMenuConfirmationTexture)
                    .Prop(TextureRect.StylePropertyTexture, verbMenuConfirmationTexture),

                // Context menu confirm buttons
                Element<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

                Element<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDefault),

                Element<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionHovered),

                Element<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionPressed),

                Element<ContextMenuElement>().Class(ConfirmationMenuElement.StyleClassConfirmationContextMenuButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorCautionDisabled),

                // Examine buttons
                Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, buttonContext),

                Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContext),

                Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextHover),

                Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextPressed),

                Element<ExamineButton>().Class(ExamineButton.StyleClassExamineButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ExamineButtonColorContextDisabled),

                // Direction / arrow icon
                Element<DirectionIcon>().Class(DirectionIcon.StyleClassDirectionIconArrow)
                    .Prop(TextureRect.StylePropertyTexture, directionIconArrowTex),

                Element<DirectionIcon>().Class(DirectionIcon.StyleClassDirectionIconUnknown)
                    .Prop(TextureRect.StylePropertyTexture, directionIconQuestionTex),

                Element<DirectionIcon>().Class(DirectionIcon.StyleClassDirectionIconHere)
                    .Prop(TextureRect.StylePropertyTexture, directionIconHereTex),

                // Thin buttons (No padding nor vertical margin)
                Element<ContainerButton>().Class(StyleClassStorageButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, buttonStorage),

                Element<ContainerButton>().Class(StyleClassStorageButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDefault),

                Element<ContainerButton>().Class(StyleClassStorageButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorHovered),

                Element<ContainerButton>().Class(StyleClassStorageButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorPressed),

                Element<ContainerButton>().Class(StyleClassStorageButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDisabled),
// ListContainer
                Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton)
                    .Prop(ContainerButton.StylePropertyStyleBox, listContainerButton),

                Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton)
                    .Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, new Color(55, 55, 68)),

                Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, new Color(75, 75, 86)),

                Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton)
                    .Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, new Color(75, 75, 86)),

                Element<ContainerButton>().Class(ListContainer.StyleClassListContainerButton)
                    .Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, new Color(10, 10, 12)),

                // Main menu: Make those buttons bigger.
                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button), null, "mainMenu", null),
                    new SelectorElement(typeof(Label), null, null, null)),
                    new[]
                    {
                        new StyleProperty("font", notoSansBold16),
                    }),

                // Main menu: also make those buttons slightly more separated.
                new StyleRule(new SelectorElement(typeof(BoxContainer), null, "mainMenuVBox", null),
                    new[]
                    {
                        new StyleProperty(BoxContainer.StylePropertySeparation, 2),
                    }),

                // Fancy LineEdit
                new StyleRule(new SelectorElement(typeof(LineEdit), null, null, null),
                    new[]
                    {
                        new StyleProperty(LineEdit.StylePropertyStyleBox, lineEdit),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(LineEdit), new[] {LineEdit.StyleClassLineEditNotEditable}, null, null),
                    new[]
                    {
                        new StyleProperty("font-color", new Color(192, 192, 192)),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(LineEdit), null, null, new[] {LineEdit.StylePseudoClassPlaceholder}),
                    new[]
                    {
                        new StyleProperty("font-color", Color.Gray),
                    }),

                Element<TextEdit>().Pseudo(TextEdit.StylePseudoClassPlaceholder)
                    .Prop("font-color", Color.Gray),

                // chat subpanels (chat lineedit backing, popup backings)
                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {StyleClassChatPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, chatBg),
                    }),

                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {StyleClassChatSubPanel}, null, null),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, chatSubBg),
                    }),

                // Chat lineedit - we don't actually draw a stylebox around the lineedit itself, we put it around the
                // input + other buttons, so we must clear the default stylebox
                new StyleRule(new SelectorElement(typeof(LineEdit), new[] {StyleClassChatLineEdit}, null, null),
                    new[]
                    {
                        new StyleProperty(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),
                        new StyleProperty("font-color", Color.FromHex("#D6DCE0")),
                    }),

                // Action searchbox lineedit
                new StyleRule(new SelectorElement(typeof(LineEdit), new[] {StyleClassActionSearchBox}, null, null),
                    new[]
                    {
                        new StyleProperty(LineEdit.StylePropertyStyleBox, actionSearchBox),
                    }),

                // TabContainer
                new StyleRule(new SelectorElement(typeof(TabContainer), null, null, null),
                    new[]
                    {
                        new StyleProperty(TabContainer.StylePropertyPanelStyleBox, tabContainerPanel),
                        new StyleProperty(TabContainer.StylePropertyTabStyleBox, tabContainerBoxActive),
                        new StyleProperty(TabContainer.StylePropertyTabStyleBoxInactive, tabContainerBoxInactive),
                    }),

                // ProgressBar
                new StyleRule(new SelectorElement(typeof(ProgressBar), null, null, null),
                    new[]
                    {
                        new StyleProperty(ProgressBar.StylePropertyBackground, progressBarBackground),
                        new StyleProperty(ProgressBar.StylePropertyForeground, progressBarForeground)
                    }),

                // CheckBox
                new StyleRule(new SelectorElement(typeof(TextureRect), new [] { CheckBox.StyleClassCheckBox }, null, null), new[]
                {
                    new StyleProperty(TextureRect.StylePropertyTexture, checkBoxTextureUnchecked),
                }),

                new StyleRule(new SelectorElement(typeof(TextureRect), new [] { CheckBox.StyleClassCheckBox, CheckBox.StyleClassCheckBoxChecked }, null, null), new[]
                {
                    new StyleProperty(TextureRect.StylePropertyTexture, checkBoxTextureChecked),
                }),

                new StyleRule(new SelectorElement(typeof(BoxContainer), new [] { CheckBox.StyleClassCheckBox }, null, null), new[]
                {
                    new StyleProperty(BoxContainer.StylePropertySeparation, 10),
                }),

                // MonotoneCheckBox
                new StyleRule(new SelectorElement(typeof(TextureRect), new [] { MonotoneCheckBox.StyleClassMonotoneCheckBox }, null, null), new[]
                {
                    new StyleProperty(TextureRect.StylePropertyTexture, monotoneCheckBoxTextureUnchecked),
                }),

                new StyleRule(new SelectorElement(typeof(TextureRect), new [] { MonotoneCheckBox.StyleClassMonotoneCheckBox, CheckBox.StyleClassCheckBoxChecked }, null, null), new[]
                {
                    new StyleProperty(TextureRect.StylePropertyTexture, monotoneCheckBoxTextureChecked),
                }),

                // Tooltip
                new StyleRule(new SelectorElement(typeof(Tooltip), null, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                }),

                new StyleRule(new SelectorElement(typeof(PanelContainer), new [] { StyleClassTooltipPanel }, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                }),

                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "sayBox"}, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                }),

                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "whisperBox"}, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, whisperBox)
                }),

                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "whisperBox"}, null, null),
                    new SelectorElement(typeof(RichTextLabel), new[] {"bubbleContent"}, null, null)),
                    new[]
                {
                    new StyleProperty("font", notoSansItalic12),
                }),

                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "emoteBox"}, null, null),
                    new SelectorElement(typeof(RichTextLabel), null, null, null)),
                    new[]
                {
                    new StyleProperty("font", notoSansItalic12),
                }),

                // RMC14
                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] { "speechBox", "commanderSpeech" }, null, null),
                    new SelectorElement(typeof(RichTextLabel), new[] { "bubbleContent" }, null, null)),
                    new[]
                {
                    new StyleProperty("font", notoSansBold16),
                }),

                // RMC14
                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "commanderSpeech"}, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                }),

                // RMC14
                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(PanelContainer), new[] { "speechBox", "megaphoneSpeech" }, null, null),
                    new SelectorElement(typeof(RichTextLabel), new[] { "bubbleContent" }, null, null)),
                    new[]
                {
                    new StyleProperty("font", resCache.NotoStack(variation: "Bold", size: 20)),
                }),

                // RMC14
                new StyleRule(new SelectorElement(typeof(PanelContainer), new[] {"speechBox", "megaphoneSpeech"}, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                }),

                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassLabelKeyText}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, notoSansBold12),
                    new StyleProperty( Control.StylePropertyModulateSelf, NanoGold)
                }),

                // alert tooltip
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipAlertTitle}, null, null), new[]
                {
                    new StyleProperty("font", notoSansBold18)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipAlertDescription}, null, null), new[]
                {
                    new StyleProperty("font", notoSans16)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipAlertCooldown}, null, null), new[]
                {
                    new StyleProperty("font", notoSans16)
                }),

                // action tooltip
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionTitle}, null, null), new[]
                {
                    new StyleProperty("font", notoSansBold16)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionDescription}, null, null), new[]
                {
                    new StyleProperty("font", notoSans15)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionCooldown}, null, null), new[]
                {
                    new StyleProperty("font", notoSans15)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionDynamicMessage}, null, null), new[]
                {
                    new StyleProperty("font", notoSans15)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionRequirements}, null, null), new[]
                {
                    new StyleProperty("font", notoSans15)
                }),
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassTooltipActionCharges}, null, null), new[]
                {
                    new StyleProperty("font", notoSans15)
                }),

                // small number for the entity counter in the entity menu
                new StyleRule(new SelectorElement(typeof(Label), new[] {ContextMenuElement.StyleClassEntityMenuIconLabel}, null, null), new[]
                {
                    new StyleProperty("font", notoSans10),
                    new StyleProperty(Label.StylePropertyAlignMode, Label.AlignMode.Right),
                }),

                // hotbar slot
                new StyleRule(new SelectorElement(typeof(RichTextLabel), new[] {StyleClassHotbarSlotNumber}, null, null), new[]
                {
                    new StyleProperty("font", notoSansDisplayBold16)
                }),

                // Entity tooltip
                new StyleRule(
                    new SelectorElement(typeof(PanelContainer), new[] {ExamineSystem.StyleClassEntityTooltip}, null,
                        null), new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, tooltipBox)
                    }),

                // ItemList
                new StyleRule(new SelectorElement(typeof(ItemList), null, null, null), new[]
                {
                    new StyleProperty(ItemList.StylePropertyBackground,
                        new StyleBoxFlat {BackgroundColor = new Color(32, 32, 40)}),
                    new StyleProperty(ItemList.StylePropertyItemBackground,
                        itemListItemBackground),
                    new StyleProperty(ItemList.StylePropertyDisabledItemBackground,
                        itemListItemBackgroundDisabled),
                    new StyleProperty(ItemList.StylePropertySelectedItemBackground,
                        itemListBackgroundSelected)
                }),

                new StyleRule(new SelectorElement(typeof(ItemList), new[] {"transparentItemList"}, null, null), new[]
                {
                    new StyleProperty(ItemList.StylePropertyBackground,
                        new StyleBoxFlat {BackgroundColor = Color.Transparent}),
                    new StyleProperty(ItemList.StylePropertyItemBackground,
                        itemListItemBackgroundTransparent),
                    new StyleProperty(ItemList.StylePropertyDisabledItemBackground,
                        itemListItemBackgroundDisabled),
                    new StyleProperty(ItemList.StylePropertySelectedItemBackground,
                        itemListBackgroundSelected)
                }),

                 new StyleRule(new SelectorElement(typeof(ItemList), new[] {"transparentBackgroundItemList"}, null, null), new[]
                {
                    new StyleProperty(ItemList.StylePropertyBackground,
                        new StyleBoxFlat {BackgroundColor = Color.Transparent}),
                    new StyleProperty(ItemList.StylePropertyItemBackground,
                        itemListItemBackground),
                    new StyleProperty(ItemList.StylePropertyDisabledItemBackground,
                        itemListItemBackgroundDisabled),
                    new StyleProperty(ItemList.StylePropertySelectedItemBackground,
                        itemListBackgroundSelected)
                }),

                // Tree
                new StyleRule(new SelectorElement(typeof(Tree), null, null, null), new[]
                {
                    new StyleProperty(Tree.StylePropertyBackground,
                        new StyleBoxFlat {BackgroundColor = new Color(32, 32, 40)}),
                    new StyleProperty(Tree.StylePropertyItemBoxSelected, new StyleBoxFlat
                    {
                        BackgroundColor = new Color(55, 55, 68),
                        ContentMarginLeftOverride = 4
                    })
                }),

                // Placeholder
                new StyleRule(new SelectorElement(typeof(Placeholder), null, null, null), new[]
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, placeholder),
                }),

                new StyleRule(
                    new SelectorElement(typeof(Label), new[] {Placeholder.StyleClassPlaceholderText}, null, null), new[]
                    {
                        new StyleProperty(Label.StylePropertyFont, notoSans16),
                        new StyleProperty(Label.StylePropertyFontColor, new Color(103, 103, 103, 128)),
                    }),

                // Big Label
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassLabelHeading}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, notoSansBold16),
                    new StyleProperty(Label.StylePropertyFontColor, NanoGold),
                }),

                // Bigger Label
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassLabelHeadingBigger}, null, null),
                    new[]
                    {
                        new StyleProperty(Label.StylePropertyFont, notoSansBold20),
                        new StyleProperty(Label.StylePropertyFontColor, NanoGold),
                    }),

                // Small Label
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassLabelSubText}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, notoSans10),
                    new StyleProperty(Label.StylePropertyFontColor, Color.DarkGray),
                }),

                // Label Key
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassLabelKeyText}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, notoSansBold12),
                    new StyleProperty(Label.StylePropertyFontColor, NanoGold)
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassLabelSecondaryColor}, null, null),
                    new[]
                    {
                        new StyleProperty(Label.StylePropertyFont, notoSans12),
                        new StyleProperty(Label.StylePropertyFontColor, Color.DarkGray),
                    }),

                // Console text
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassConsoleText}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, robotoMonoBold11)
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassConsoleSubHeading}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, robotoMonoBold12)
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassConsoleHeading}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFont, robotoMonoBold14)
                }),

                // Big Button
                new StyleRule(new SelectorChild(
                    new SelectorElement(typeof(Button), new[] {StyleClassButtonBig}, null, null),
                    new SelectorElement(typeof(Label), null, null, null)),
                    new[]
                    {
                        new StyleProperty("font", notoSans16)
                    }),

                //APC and SMES power state label colors
                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassPowerStateNone}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFontColor, new Color(0.8f, 0.0f, 0.0f))
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassPowerStateLow}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFontColor, new Color(0.9f, 0.36f, 0.0f))
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] {StyleClassPowerStateGood}, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyFontColor, new Color(0.024f, 0.8f, 0.0f))
                }),

                // Those top menu buttons.
                // these use slight variations on the various BaseButton styles so that the content within them appears centered,
                // which is NOT the case for the default BaseButton styles (OpenLeft/OpenRight adds extra padding on one of the sides
                // which makes the TopButton icons appear off-center, which we don't want).
                new StyleRule(
                    new SelectorElement(typeof(MenuButton), new[] {ButtonSquare}, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, topButtonSquare),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), new[] {ButtonOpenLeft}, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, topButtonOpenLeft),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), new[] {ButtonOpenRight}, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, topButtonOpenRight),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), null, null, new[] {Button.StylePseudoClassNormal}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorDefault),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), new[] {MenuButton.StyleClassRedTopButton}, null, new[] {Button.StylePseudoClassNormal}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorDefaultRed),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), null, null, new[] {Button.StylePseudoClassNormal}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorDefault),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), null, null, new[] {Button.StylePseudoClassPressed}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorPressed),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), null, null, new[] {Button.StylePseudoClassHover}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorHovered),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MenuButton), new[] {MenuButton.StyleClassRedTopButton}, null, new[] {Button.StylePseudoClassHover}),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyModulateSelf, ButtonColorHoveredRed),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(Label), new[] {MenuButton.StyleClassLabelTopButton}, null, null),
                    new[]
                    {
                        new StyleProperty(Label.StylePropertyFont, notoSansDisplayBold14),
                    }),

                // MonotoneButton (unfilled)
                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), null, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneButton),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenLeft }, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneButtonOpenLeft),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenRight }, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneButtonOpenRight),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenBoth }, null, null),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneButtonOpenBoth),
                    }),

                // MonotoneButton (filled)
                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), null, null, new[] { Button.StylePseudoClassPressed }),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneFilledButton),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenLeft }, null, new[] { Button.StylePseudoClassPressed }),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneFilledButtonOpenLeft),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenRight }, null, new[] { Button.StylePseudoClassPressed }),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneFilledButtonOpenRight),
                    }),

                new StyleRule(
                    new SelectorElement(typeof(MonotoneButton), new[] { ButtonOpenBoth }, null, new[] { Button.StylePseudoClassPressed }),
                    new[]
                    {
                        new StyleProperty(Button.StylePropertyStyleBox, monotoneFilledButtonOpenBoth),
                    }),

                // NanoHeading

                new StyleRule(
                    new SelectorChild(
                        SelectorElement.Type(typeof(NanoHeading)),
                        SelectorElement.Type(typeof(PanelContainer))),
                    new[]
                    {
                        new StyleProperty(PanelContainer.StylePropertyPanel, nanoHeadingBox),
                    }),

                // StripeBack
                new StyleRule(
                    SelectorElement.Type(typeof(StripeBack)),
                    new[]
                    {
                        new StyleProperty(StripeBack.StylePropertyBackground, stripeBack),
                    }),

                // StyleClassItemStatus
                new StyleRule(SelectorElement.Class(StyleClassItemStatus), new[]
                {
                    new StyleProperty("font", notoSans10),
                }),

                Element()
                    .Class(StyleClassItemStatusNotHeld)
                    .Prop("font", notoSansItalic10)
                    .Prop("font-color", ItemStatusNotHeldColor),

                Element<RichTextLabel>()
                    .Class(StyleClassItemStatus)
                    .Prop(nameof(RichTextLabel.LineHeightScale), 0.7f)
                    .Prop(nameof(Control.Margin), new Thickness(0, 0, 0, -6)),

                // Slider
                new StyleRule(SelectorElement.Type(typeof(Slider)), new []
                {
                    new StyleProperty(Slider.StylePropertyBackground, sliderBackBox),
                    new StyleProperty(Slider.StylePropertyForeground, sliderForeBox),
                    new StyleProperty(Slider.StylePropertyGrabber, sliderGrabBox),
                    new StyleProperty(Slider.StylePropertyFill, sliderFillBox),
                }),

                new StyleRule(SelectorElement.Type(typeof(ColorableSlider)), new []
                {
                    new StyleProperty(ColorableSlider.StylePropertyFillWhite, sliderFillWhite),
                    new StyleProperty(ColorableSlider.StylePropertyBackgroundWhite, sliderFillWhite),
                }),

                new StyleRule(new SelectorElement(typeof(Slider), new []{StyleClassSliderRed}, null, null), new []
                {
                    new StyleProperty(Slider.StylePropertyFill, sliderFillRed),
                }),

                new StyleRule(new SelectorElement(typeof(Slider), new []{StyleClassSliderGreen}, null, null), new []
                {
                    new StyleProperty(Slider.StylePropertyFill, sliderFillGreen),
                }),

                new StyleRule(new SelectorElement(typeof(Slider), new []{StyleClassSliderBlue}, null, null), new []
                {
                    new StyleProperty(Slider.StylePropertyFill, sliderFillBlue),
                }),

                new StyleRule(new SelectorElement(typeof(Slider), new []{StyleClassSliderWhite}, null, null), new []
                {
                    new StyleProperty(Slider.StylePropertyFill, sliderFillWhite),
                }),

                // chat channel option selector
                new StyleRule(new SelectorElement(typeof(Button), new[] {StyleClassChatChannelSelectorButton}, null, null), new[]
                {
                    new StyleProperty(Button.StylePropertyStyleBox, chatChannelButton),
                }),

                // chat filter button
                new StyleRule(new SelectorElement(typeof(ContainerButton), new[] {StyleClassChatFilterOptionButton}, null, null), new[]
                {
                    new StyleProperty(ContainerButton.StylePropertyStyleBox, chatFilterButton),
                }),
                new StyleRule(new SelectorElement(typeof(ContainerButton), new[] {StyleClassChatFilterOptionButton}, null, new[] {ContainerButton.StylePseudoClassNormal}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorDefault),
                }),
                new StyleRule(new SelectorElement(typeof(ContainerButton), new[] {StyleClassChatFilterOptionButton}, null, new[] {ContainerButton.StylePseudoClassHover}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorHovered),
                }),
                new StyleRule(new SelectorElement(typeof(ContainerButton), new[] {StyleClassChatFilterOptionButton}, null, new[] {ContainerButton.StylePseudoClassPressed}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorPressed),
                }),
                new StyleRule(new SelectorElement(typeof(ContainerButton), new[] {StyleClassChatFilterOptionButton}, null, new[] {ContainerButton.StylePseudoClassDisabled}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorDisabled),
                }),

                // output panel scroll button
                Element<Button>()
                    .Class(OutputPanel.StyleClassOutputPanelScrollDownButton)
                    .Prop(Button.StylePropertyStyleBox, outputPanelScrollDownButton),

                // OptionButton
                new StyleRule(new SelectorElement(typeof(OptionButton), null, null, null), new[]
                {
                    new StyleProperty(ContainerButton.StylePropertyStyleBox, BaseButton),
                }),
                new StyleRule(new SelectorElement(typeof(OptionButton), null, null, new[] {ContainerButton.StylePseudoClassNormal}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorDefault),
                }),
                new StyleRule(new SelectorElement(typeof(OptionButton), null, null, new[] {ContainerButton.StylePseudoClassHover}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorHovered),
                }),
                new StyleRule(new SelectorElement(typeof(OptionButton), null, null, new[] {ContainerButton.StylePseudoClassPressed}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorPressed),
                }),
                new StyleRule(new SelectorElement(typeof(OptionButton), null, null, new[] {ContainerButton.StylePseudoClassDisabled}), new[]
                {
                    new StyleProperty(Control.StylePropertyModulateSelf, ButtonColorDisabled),
                }),

                new StyleRule(new SelectorElement(typeof(TextureRect), new[] {OptionButton.StyleClassOptionTriangle}, null, null), new[]
                {
                    new StyleProperty(TextureRect.StylePropertyTexture, textureInvertedTriangle),
                    //new StyleProperty(Control.StylePropertyModulateSelf, Color.FromHex("#FFFFFF")),
                }),

                new StyleRule(new SelectorElement(typeof(Label), new[] { OptionButton.StyleClassOptionButton }, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyAlignMode, Label.AlignMode.Center),
                }),

                Element<PanelContainer>().Class(OptionButton.StyleClassOptionsBackground)
                    .Prop(PanelContainer.StylePropertyPanel, crtInsetPanel),

                new StyleRule(new SelectorElement(typeof(PanelContainer), new []{ ClassHighDivider}, null, null), new []
                {
                    new StyleProperty(PanelContainer.StylePropertyPanel, new StyleBoxFlat { BackgroundColor = NanoGold, ContentMarginBottomOverride = 2, ContentMarginLeftOverride = 2}),
                }),

                Element<TextureButton>()
                    .Class(StyleClassButtonHelp)
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),

                // Labels ---
                Element<Label>().Class(StyleClassLabelBig)
                    .Prop(Label.StylePropertyFont, notoSans16),

                Element<Label>().Class(StyleClassLabelSmall)
                 .Prop(Label.StylePropertyFont, notoSans10),
                // ---

                // Different Background shapes ---
                Element<PanelContainer>().Class(ClassAngleRect)
                    .Prop(PanelContainer.StylePropertyPanel, BaseAngleRect)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#25252A")),

                Element<PanelContainer>().Class("BackgroundOpenRight")
                    .Prop(PanelContainer.StylePropertyPanel, BaseButtonOpenRight)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#25252A")),

                Element<PanelContainer>().Class("BackgroundOpenLeft")
                    .Prop(PanelContainer.StylePropertyPanel, BaseButtonOpenLeft)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#25252A")),
                // ---

                // Dividers
                Element<PanelContainer>().Class(ClassLowDivider)
                    .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#444"),
                        ContentMarginLeftOverride = 2,
                        ContentMarginBottomOverride = 2
                    }),

                // Window Headers
                Element<Label>().Class("FancyWindowTitle")
                    .Prop("font", boxFont13)
                    .Prop("font-color", NanoGold),

                Element<PanelContainer>().Class("WindowHeadingBackground")
                    .Prop("panel", new StyleBoxTexture(BaseButtonOpenLeft) { Padding = default })
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#1F1F23")),

                Element<PanelContainer>().Class("WindowHeadingBackgroundLight")
                    .Prop("panel", new StyleBoxTexture(BaseButtonOpenLeft) { Padding = default }),

                // Window Header Help Button
                Element<TextureButton>().Class(FancyWindow.StyleClassWindowHelpButton)
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Nano/help.png"))
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#4B596A")),

                Element<TextureButton>().Class(FancyWindow.StyleClassWindowHelpButton).Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#7F3636")),

                Element<TextureButton>().Class(FancyWindow.StyleClassWindowHelpButton).Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#753131")),

                //The lengths you have to go through to change a background color smh
                Element<PanelContainer>().Class("PanelBackgroundBaseDark")
                    .Prop("panel", new StyleBoxTexture(BaseButtonOpenBoth) { Padding = default })
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#1F1F23")),

                Element<PanelContainer>().Class("PanelBackgroundLight")
                    .Prop("panel", new StyleBoxTexture(BaseButtonOpenBoth) { Padding = default })
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#2F2F3B")),

                // Window Footer
                Element<TextureRect>().Class("NTLogoDark")
                    .Prop(TextureRect.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Nano/ntlogo.svg.png"))
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#757575")),

                Element<Label>().Class("WindowFooterText")
                    .Prop(Label.StylePropertyFont, notoSans8)
                    .Prop(Label.StylePropertyFontColor, Color.FromHex("#757575")),

                // X Texture button ---
                Element<TextureButton>().Class("CrossButtonRed")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Nano/cross.svg.png"))
                    .Prop(Control.StylePropertyModulateSelf, DangerousRedFore),

                Element<TextureButton>().Class("CrossButtonRed").Pseudo(TextureButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#7F3636")),

                Element<TextureButton>().Class("CrossButtonRed").Pseudo(TextureButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#753131")),

                //
                Element<TextureButton>().Class("Refresh")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Nano/circular_arrow.svg.96dpi.png")),
                // ---

                // Profile Editor
                Element<TextureButton>().Class("SpeciesInfoDefault")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/VerbIcons/information.svg.192dpi.png")),

                Element<TextureButton>().Class("SpeciesInfoWarning")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/info.svg.192dpi.png"))
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#eeee11")),

                // The default look of paper in UIs. Pages can have components which override this
                Element<PanelContainer>().Class("PaperDefaultBorder")
                    .Prop(PanelContainer.StylePropertyPanel, paperBackground),
                Element<RichTextLabel>().Class("PaperWrittenText")
                    .Prop(Label.StylePropertyFont, notoSans12)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#111111")),

                Element<RichTextLabel>().Class("LabelSubText")
                    .Prop(Label.StylePropertyFont, notoSans10)
                    .Prop(Label.StylePropertyFontColor, Color.DarkGray),

                Element<LineEdit>().Class("PaperLineEdit")
                    .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty()),

                // Red Button ---
                Element<Button>().Class("ButtonColorRed")
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDefaultRed),

                Element<Button>().Class("ButtonColorRed").Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorDefaultRed),

                Element<Button>().Class("ButtonColorRed").Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorHoveredRed),
                // ---

                // Green Button ---
                Element<Button>().Class("ButtonColorGreen")
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodDefault),

                Element<Button>().Class("ButtonColorGreen").Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodDefault),

                Element<Button>().Class("ButtonColorGreen").Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodHovered),

                // Accept button (merge with green button?) ---
                Element<Button>().Class("ButtonAccept")
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodDefault),

                Element<Button>().Class("ButtonAccept").Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodDefault),

                Element<Button>().Class("ButtonAccept").Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodHovered),

                Element<Button>().Class("ButtonAccept").Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(Control.StylePropertyModulateSelf, ButtonColorGoodDisabled),

                // ---

                // Small Button ---
                Element<Button>().Class("ButtonSmall")
                    .Prop(ContainerButton.StylePropertyStyleBox, smallButtonBase),

                Child().Parent(Element<Button>().Class("ButtonSmall"))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFont, notoSans8),
                // ---

                Element<Label>().Class("StatusFieldTitle")
                    .Prop("font-color", NanoGold),

                Element<Label>().Class("Good")
                    .Prop("font-color", GoodGreenFore),

                Element<Label>().Class("Caution")
                    .Prop("font-color", ConcerningOrangeFore),

                Element<Label>().Class("Danger")
                    .Prop("font-color", DangerousRedFore),

                Element<Label>().Class("Disabled")
                    .Prop("font-color", DisabledFore),

                // Radial menu buttons
                Element<TextureButton>().Class("RadialMenuButton")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/button_normal.png")),
                Element<TextureButton>().Class("RadialMenuButton")
                    .Pseudo(TextureButton.StylePseudoClassHover)
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/button_hover.png")),

                Element<TextureButton>().Class("RadialMenuCloseButton")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/close_normal.png")),
                Element<TextureButton>().Class("RadialMenuCloseButton")
                    .Pseudo(TextureButton.StylePseudoClassHover)
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/close_hover.png")),

                Element<TextureButton>().Class("RadialMenuBackButton")
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/back_normal.png")),
                Element<TextureButton>().Class("RadialMenuBackButton")
                    .Pseudo(TextureButton.StylePseudoClassHover)
                    .Prop(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Radial/back_hover.png")),

                // CRT lobby/preferences theme.
                Element<PanelContainer>().Class(StyleClassCrtPanel)
                    .Prop(PanelContainer.StylePropertyPanel, crtPanel)
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

                Element<Label>().Class(StyleClassCrtText)
                    .Prop(Label.StylePropertyFont, crtTextFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor),

                Element<Label>().Class(StyleClassCrtDimText)
                    .Prop(Label.StylePropertyFont, crtDimFont)
                    .Prop(Label.StylePropertyFontColor, crtDimTextColor),

                Element<Label>().Class(StyleClassCrtHeading)
                    .Prop(Label.StylePropertyFont, crtHeadingFont)
                    .Prop(Label.StylePropertyFontColor, crtHeadingColor),

                Element<Label>().Class(StyleClassCrtHeadingBig)
                    .Prop(Label.StylePropertyFont, crtHeadingBigFont)
                    .Prop(Label.StylePropertyFontColor, crtHeadingColor),

                Element<Label>().Class(StyleClassCrtButtonLabel)
                    .Prop(Label.StylePropertyFont, crtButtonLabelFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                Child().Parent(Element<Button>().Class(StyleClassCrtButton))
                    .Child(Element<Label>())
                    .Prop(Label.StylePropertyFont, crtButtonLabelFont)
                    .Prop(Label.StylePropertyFontColor, crtTextColor),

                Element<Label>().Class(StyleClassCrtNativeButtonLabel)
                    .Prop(Label.StylePropertyFont, notoSans12)
                    .Prop(Label.StylePropertyFontColor, crtTextColor)
                    .Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),

                Element<RichTextLabel>().Class(StyleClassCrtRichText)
                    .Prop("font", crtRichTextFont),

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

                Element<Slider>().Class(StyleClassCrtSlider)
                    .Prop(Slider.StylePropertyBackground, crtSliderBackground)
                    .Prop(Slider.StylePropertyForeground, crtSliderForeground)
                    .Prop(Slider.StylePropertyFill, crtSliderFill)
                    .Prop(Slider.StylePropertyGrabber, crtSliderGrabber),

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
                    .Prop(StripeBack.StylePropertyBackground, crtInsetPanel),

                Element<TextureButton>().Class(StyleClassCrtIconButton)
                    .Prop(Control.StylePropertyModulateSelf, crtTextColor),

                //PDA - Backgrounds
                Element<PanelContainer>().Class("PdaContentBackground")
                    .Prop(PanelContainer.StylePropertyPanel, BaseButtonOpenBoth)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#25252a")),

                Element<PanelContainer>().Class("PdaBackground")
                    .Prop(PanelContainer.StylePropertyPanel, BaseButtonOpenBoth)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#000000")),

                Element<PanelContainer>().Class("PdaBackgroundRect")
                    .Prop(PanelContainer.StylePropertyPanel, BaseAngleRect)
                    .Prop(Control.StylePropertyModulateSelf, Color.FromHex("#717059")),

                Element<PanelContainer>().Class("PdaBorderRect")
                    .Prop(PanelContainer.StylePropertyPanel, AngleBorderRect),

                Element<PanelContainer>().Class("BackgroundDark")
                    .Prop(PanelContainer.StylePropertyPanel, new StyleBoxFlat(Color.FromHex("#25252A"))),

                //PDA - Buttons
                Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.NormalBgColor))
                    .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

                Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.HoverColor))
                    .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

                Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.PressedColor))
                    .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.EnabledFgColor)),

                Element<PdaSettingsButton>().Pseudo(ContainerButton.StylePseudoClassDisabled)
                    .Prop(PdaSettingsButton.StylePropertyBgColor, Color.FromHex(PdaSettingsButton.NormalBgColor))
                    .Prop(PdaSettingsButton.StylePropertyFgColor, Color.FromHex(PdaSettingsButton.DisabledFgColor)),

                Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassNormal)
                    .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.NormalBgColor)),

                Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.HoverColor)),

                Element<PdaProgramItem>().Pseudo(ContainerButton.StylePseudoClassPressed)
                    .Prop(PdaProgramItem.StylePropertyBgColor, Color.FromHex(PdaProgramItem.HoverColor)),

                //PDA - Text
                Element<Label>().Class("PdaContentFooterText")
                    .Prop(Label.StylePropertyFont, notoSans10)
                    .Prop(Label.StylePropertyFontColor, Color.FromHex("#757575")),

                Element<Label>().Class("PdaWindowFooterText")
                    .Prop(Label.StylePropertyFont, notoSans10)
                    .Prop(Label.StylePropertyFontColor, Color.FromHex("#333d3b")),

                // Fancy Tree
                Element<ContainerButton>().Identifier(TreeItem.StyleIdentifierTreeButton)
                    .Class(TreeItem.StyleClassEvenRow)
                    .Prop(ContainerButton.StylePropertyStyleBox, new StyleBoxFlat
                    {
                        BackgroundColor = CrtItemBackground.WithAlpha(0.42f),
                        BorderColor = CrtGreenDisabled.WithAlpha(0.18f),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                    }),

                Element<ContainerButton>().Identifier(TreeItem.StyleIdentifierTreeButton)
                    .Class(TreeItem.StyleClassOddRow)
                    .Prop(ContainerButton.StylePropertyStyleBox, new StyleBoxFlat
                    {
                        BackgroundColor = CrtPanelBackgroundAlt.WithAlpha(0.38f),
                        BorderColor = CrtGreenDisabled.WithAlpha(0.14f),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                    }),

                Element<ContainerButton>().Identifier(TreeItem.StyleIdentifierTreeButton)
                    .Class(TreeItem.StyleClassSelected)
                    .Prop(ContainerButton.StylePropertyStyleBox, new StyleBoxFlat
                    {
                        BackgroundColor = CrtItemSelectedBackground.WithAlpha(0.65f),
                        BorderColor = CrtGreen.WithAlpha(0.42f),
                        BorderThickness = new Thickness(1),
                    }),

                Element<ContainerButton>().Identifier(TreeItem.StyleIdentifierTreeButton)
                    .Pseudo(ContainerButton.StylePseudoClassHover)
                    .Prop(ContainerButton.StylePropertyStyleBox, new StyleBoxFlat
                    {
                        BackgroundColor = CrtItemSelectedBackground.WithAlpha(0.48f),
                        BorderColor = CrtGreen.WithAlpha(0.32f),
                        BorderThickness = new Thickness(1),
                    }),

                // Silicon law edit ui
                Element<Label>().Class(SiliconLawContainer.StyleClassSiliconLawPositionLabel)
                    .Prop(Label.StylePropertyFontColor, NanoGold),
                // Pinned button style
                new StyleRule(
                    new SelectorElement(typeof(TextureButton), new[] { StyleClassPinButtonPinned }, null, null),
                    new[]
                    {
                        new StyleProperty(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Bwoink/pinned.png"))
                    }),

                // Unpinned button style
                new StyleRule(
                    new SelectorElement(typeof(TextureButton), new[] { StyleClassPinButtonUnpinned }, null, null),
                    new[]
                    {
                        new StyleProperty(TextureButton.StylePropertyTexture, resCache.GetTexture("/Textures/Interface/Bwoink/un_pinned.png"))
                    }),

                Element<PanelContainer>()
                    .Class(StyleClassInset)
                    .Prop(PanelContainer.StylePropertyPanel, insetBack),

                Element<Label>().Class(StyleClassCharacterName)
                    .Prop(Label.StylePropertyFont, characterNameFont),

                Element<LineEdit>().Class(StyleClassCharacterNameInput)
                    .Prop("font", characterNameFont),

                // RMC14
                new StyleRule(new SelectorElement(typeof(Label), new[] { CMStyleClasses.CMLabelAlignLeft }, null, null), new[]
                {
                    new StyleProperty(Label.StylePropertyAlignMode, Label.AlignMode.Left),
                }),
            }).ToList());
        }
    }
}
