using System.Numerics;
using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Sheetlets;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.Resources;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using static Robust.Client.UserInterface.StylesheetHelpers;

namespace Content.Client.Stylesheets;

public static class CrtThemePalette
{
    public static bool Enabled { get; private set; } = true;
    public static Color Background { get; private set; }
    public static Color PanelBackground { get; private set; }
    public static Color PanelBackgroundAlt { get; private set; }
    public static Color InsetBackground { get; private set; }
    public static Color HeaderBackground { get; private set; }
    public static Color ButtonBackground { get; private set; }
    public static Color ButtonHoverBackground { get; private set; }
    public static Color ButtonPressedBackground { get; private set; }
    public static Color ButtonDisabledBackground { get; private set; }
    public static Color SliderForeground { get; private set; }
    public static Color ProgressForeground { get; private set; }
    public static Color ItemBackground { get; private set; }
    public static Color ItemSelectedBackground { get; private set; }
    public static Color ItemDisabledBackground { get; private set; }
    public static Color Accent { get; private set; }
    public static Color AccentDim { get; private set; }
    public static Color AccentSoft { get; private set; }
    public static Color AccentDisabled { get; private set; }

    static CrtThemePalette()
    {
        Set(true, CCVars.CrtUiColorGreen);
    }

    public static void Set(bool enabled, string color)
    {
        Enabled = enabled;
        var accent = color switch
        {
            CCVars.CrtUiColorBlue => Color.FromHex("#58CCFF"),
            CCVars.CrtUiColorOrange => Color.FromHex("#FFB454"),
            CCVars.CrtUiColorRed => Color.FromHex("#FF4E5E"),
            CCVars.CrtUiColorPurple => Color.FromHex("#C45BFF"),
            CCVars.CrtUiColorGreen => Color.FromHex("#46FF8E"),
            _ => Color.TryFromHex(color, out var customColor)
                ? customColor
                : Color.FromHex(CCVars.CrtUiColorDefault),
        };

        if (!enabled)
            accent = StyleNano.NanoGold;

        var hsv = Color.ToHsv(accent);
        var hue = hsv.X;
        var saturation = Math.Clamp(hsv.Y, 0.05f, 1f);
        var value = Math.Clamp(hsv.Z, 0.55f, 1f);
        var backgroundSaturation = Math.Clamp(saturation * 0.85f, 0.02f, 0.85f);

        Color Hsv(float sat, float val) => Color.FromHsv(new Vector4(
            hue,
            Math.Clamp(sat, 0f, 1f),
            Math.Clamp(val, 0f, 1f),
            1f));

        Background = Hsv(backgroundSaturation, 0.04f);
        PanelBackground = Hsv(backgroundSaturation, 0.075f);
        PanelBackgroundAlt = Hsv(backgroundSaturation, 0.135f);
        InsetBackground = Hsv(backgroundSaturation, 0.055f);
        HeaderBackground = Hsv(saturation, 0.23f);
        ButtonBackground = Hsv(saturation, 0.115f);
        ButtonHoverBackground = Hsv(saturation, 0.23f);
        ButtonPressedBackground = Hsv(saturation, 0.37f);
        ButtonDisabledBackground = Hsv(backgroundSaturation, 0.07f);
        SliderForeground = Hsv(saturation, 0.14f);
        ProgressForeground = Hsv(saturation, 0.30f);
        ItemBackground = Hsv(saturation, 0.08f);
        ItemSelectedBackground = Hsv(saturation, 0.23f);
        ItemDisabledBackground = Hsv(backgroundSaturation, 0.035f);
        Accent = Hsv(saturation, value);
        AccentDim = Hsv(saturation, value * 0.50f);
        AccentSoft = Hsv(saturation * 0.30f, 1f);
        AccentDisabled = Hsv(saturation * 0.60f, 0.21f);
    }
}

[CommonSheetlet]
public sealed class CrtThemeSheetlet<T> : Sheetlet<T> where T : PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        if (!CrtThemePalette.Enabled)
        {
            var fallbackPanel = StyleBoxHelpers.BaseStyleBox(sheet);
            var fallbackInset = new StyleBoxFlat
            {
                BackgroundColor = sheet.SecondaryPalette.BackgroundDark,
                BorderColor = sheet.PrimaryPalette.Background,
                BorderThickness = new Thickness(2f),
            };
            var fallbackQuiet = new StyleBoxFlat
            {
                BackgroundColor = sheet.SecondaryPalette.BackgroundDark,
            };
            var fallbackHeader = new StyleBoxFlat
            {
                BackgroundColor = sheet.HighlightPalette.Background,
            };

            return
            [
                Element<PanelContainer>().Class(StyleNano.StyleClassCrtPanel)
                    .Prop(PanelContainer.StylePropertyPanel, fallbackPanel)
                    .Prop(Control.StylePropertyModulateSelf, sheet.SecondaryPalette.Background),
                Element<PanelContainer>().Class(StyleNano.StyleClassCrtInsetPanel)
                    .Prop(PanelContainer.StylePropertyPanel, fallbackInset),
                Element<PanelContainer>().Class(StyleNano.StyleClassCrtQuietPanel)
                    .Prop(PanelContainer.StylePropertyPanel, fallbackQuiet),
                Element<PanelContainer>().Class(StyleNano.StyleClassCrtHeaderPanel)
                    .Prop(PanelContainer.StylePropertyPanel, fallbackHeader),
            ];
        }

        var uavOsdStack = new[]
        {
            "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf",
            "/Fonts/NotoSans/NotoSans-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols-Regular.ttf",
            "/Fonts/NotoSans/NotoSansSymbols2-Regular.ttf",
        };
        var textFont = ResCache.GetFont(uavOsdStack, 8);
        var headingFont = ResCache.GetFont(uavOsdStack, 10);
        var headingBigFont = ResCache.GetFont(uavOsdStack, 12);
        var nativeFont = ResCache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 12);
        var textColor = CrtThemePalette.AccentSoft;
        var dimTextColor = CrtThemePalette.AccentDim;
        var headingColor = CrtThemePalette.Accent;
        var selectionColor = CrtThemePalette.AccentDim.WithAlpha(0.65f);

        CrtStyleBox Panel(Color background, Color border, Thickness thickness, int seed, float margin) => new()
        {
            BackgroundColor = background,
            BorderColor = border,
            CornerColor = CrtThemePalette.Accent.WithAlpha(0.28f),
            ScanlineColor = CrtThemePalette.Accent.WithAlpha(0.016f),
            NoiseColor = CrtThemePalette.AccentSoft.WithAlpha(0.04f),
            PixelationColor = CrtThemePalette.Accent.WithAlpha(0.03f),
            PixelationShadowColor = CrtThemePalette.Background.WithAlpha(0.16f),
            BorderThickness = thickness,
            DrawPixelation = true,
            PixelationSeed = seed,
            PixelationBlockSize = 2,
            PixelationSpacing = 140,
            PixelationChance = 14,
            PixelationClusterSize = 1,
            NoiseSeed = seed + 4,
            NoiseSpacing = 11,
            NoiseChance = 10,
            MaxScanlines = 2,
            ContentMarginLeftOverride = margin,
            ContentMarginRightOverride = margin,
            ContentMarginTopOverride = margin,
            ContentMarginBottomOverride = margin,
        };

        var window = Panel(CrtThemePalette.PanelBackground, CrtThemePalette.AccentDim.WithAlpha(0.72f), new Thickness(1), 53, 0);
        window.DrawPixelation = false;
        window.DrawCornerTicks = false;
        window.MaxScanlines = 1;
        var windowHeader = Panel(CrtThemePalette.HeaderBackground, CrtThemePalette.AccentDim, new Thickness(0, 0, 0, 1), 57, 4);
        windowHeader.DrawPixelation = false;
        windowHeader.DrawCornerTicks = false;
        var panel = Panel(CrtThemePalette.PanelBackground, CrtThemePalette.AccentDim, new Thickness(1), 61, 8);
        var inset = Panel(CrtThemePalette.InsetBackground, CrtThemePalette.AccentDim, new Thickness(1), 67, 6);
        var quiet = Panel(CrtThemePalette.InsetBackground, CrtThemePalette.AccentDim.WithAlpha(0.28f), new Thickness(0), 69, 4);
        quiet.DrawPixelation = false;
        quiet.DrawCornerTicks = false;
        var header = Panel(CrtThemePalette.HeaderBackground, CrtThemePalette.Accent, new Thickness(0, 0, 0, 1), 71, 3);
        header.DrawCornerTicks = false;

        var button = Panel(CrtThemePalette.ButtonBackground, CrtThemePalette.AccentDim, new Thickness(1), 73, 3);
        button.ContentMarginLeftOverride = 12;
        button.ContentMarginRightOverride = 12;
        var buttonHover = new CrtStyleBox(button)
        {
            BackgroundColor = CrtThemePalette.ButtonHoverBackground,
            BorderColor = CrtThemePalette.Accent,
        };
        var buttonPressed = new CrtStyleBox(button)
        {
            BackgroundColor = CrtThemePalette.ButtonPressedBackground,
            BorderColor = CrtThemePalette.Accent,
        };
        var buttonDisabled = new CrtStyleBox(button)
        {
            BackgroundColor = CrtThemePalette.ButtonDisabledBackground,
            BorderColor = CrtThemePalette.AccentDisabled,
        };
        var attention = new CrtStyleBox(buttonHover);

        var lineEdit = Panel(CrtThemePalette.Background, CrtThemePalette.AccentDim, new Thickness(1), 79, 2);
        lineEdit.DrawCornerTicks = false;
        lineEdit.ContentMarginLeftOverride = 5;
        lineEdit.ContentMarginRightOverride = 5;
        var nativeLineEdit = new CrtStyleBox(lineEdit) { DrawPixelation = false, BorderThickness = new Thickness(0, 0, 0, 1) };
        var sliderBackground = Panel(CrtThemePalette.Background, CrtThemePalette.AccentDim, new Thickness(1), 83, 8);
        sliderBackground.DrawCornerTicks = false;
        var sliderForeground = new StyleBoxFlat { BackgroundColor = CrtThemePalette.SliderForeground };
        sliderForeground.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);
        var sliderFill = new StyleBoxFlat { BackgroundColor = CrtThemePalette.AccentDim };
        sliderFill.SetContentMarginOverride(StyleBox.Margin.Vertical, 8);
        var sliderGrabber = new StyleBoxFlat
        {
            BackgroundColor = CrtThemePalette.Accent,
            BorderColor = Color.White,
            BorderThickness = new Thickness(1),
        };
        sliderGrabber.SetContentMarginOverride(StyleBox.Margin.Horizontal, 5);
        var progressBackground = Panel(CrtThemePalette.Background, CrtThemePalette.AccentDim, new Thickness(1), 107, 10);
        progressBackground.DrawCornerTicks = false;
        var progressForeground = Panel(CrtThemePalette.ProgressForeground, CrtThemePalette.Accent, new Thickness(1), 113, 10);
        progressForeground.DrawCornerTicks = false;
        var tabActive = Panel(CrtThemePalette.HeaderBackground, CrtThemePalette.Accent, new Thickness(1, 1, 1, 0), 83, 3);
        var tabInactive = Panel(CrtThemePalette.InsetBackground, CrtThemePalette.AccentDim, new Thickness(1, 1, 1, 0), 89, 3);
        tabInactive.DrawCornerTicks = false;
        var itemBackground = new StyleBoxFlat(CrtThemePalette.ItemBackground.WithAlpha(0.42f));
        var itemSelected = new StyleBoxFlat(CrtThemePalette.ItemSelectedBackground.WithAlpha(0.72f));
        var itemDisabled = new StyleBoxFlat(CrtThemePalette.ItemDisabledBackground.WithAlpha(0.64f));
        var scroll = new StyleBoxFlat
        {
            BackgroundColor = CrtThemePalette.AccentDim.WithAlpha(0.78f),
            BorderColor = CrtThemePalette.Accent.WithAlpha(0.42f),
            BorderThickness = new Thickness(1),
        };
        scroll.SetContentMarginOverride(StyleBox.Margin.All, 8);
        var scrollHover = new StyleBoxFlat(scroll) { BackgroundColor = CrtThemePalette.Accent.WithAlpha(0.5f) };
        var scrollPressed = new StyleBoxFlat(scroll) { BackgroundColor = CrtThemePalette.Accent.WithAlpha(0.72f) };

        return
        [
            Child().Parent(Element<DefaultWindow>().Class(StyleNano.StyleClassCrtWindow)).Child(Element<PanelContainer>()).Prop(PanelContainer.StylePropertyPanel, window),
            Element<PanelContainer>().Class(StyleNano.StyleClassCrtWindowHeader).Prop(PanelContainer.StylePropertyPanel, windowHeader),
            Element<Label>().Class(StyleNano.StyleClassCrtWindowTitle).Prop(Label.StylePropertyFont, headingFont).Prop(Label.StylePropertyFontColor, headingColor),
            Element<PanelContainer>().Class(StyleNano.StyleClassCrtPanel).Prop(PanelContainer.StylePropertyPanel, panel),
            Element<PanelContainer>().Class(StyleNano.StyleClassCrtInsetPanel).Prop(PanelContainer.StylePropertyPanel, inset),
            Element<PanelContainer>().Class(StyleNano.StyleClassCrtQuietPanel).Prop(PanelContainer.StylePropertyPanel, quiet),
            Element<PanelContainer>().Class(StyleNano.StyleClassCrtHeaderPanel).Prop(PanelContainer.StylePropertyPanel, header),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtButton)
                .Prop(ContainerButton.StylePropertyStyleBox, button)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtButton).Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, button)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtButton).Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonHover)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtButton).Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtButton).Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonDisabled)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtAttentionButton)
                .Prop(ContainerButton.StylePropertyStyleBox, attention)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtAttentionButton).Pseudo(ContainerButton.StylePseudoClassNormal)
                .Prop(ContainerButton.StylePropertyStyleBox, attention)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtAttentionButton).Pseudo(ContainerButton.StylePseudoClassHover)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtAttentionButton).Pseudo(ContainerButton.StylePseudoClassPressed)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonPressed)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<ContainerButton>().Class(ContainerButton.StyleClassButton).Class(StyleNano.StyleClassCrtAttentionButton).Pseudo(ContainerButton.StylePseudoClassDisabled)
                .Prop(ContainerButton.StylePropertyStyleBox, buttonDisabled)
                .Prop(Control.StylePropertyModulateSelf, Color.White),
            Element<Label>().Class(StyleNano.StyleClassCrtText).Prop(Label.StylePropertyFont, textFont).Prop(Label.StylePropertyFontColor, textColor),
            Element<Label>().Class(StyleNano.StyleClassCrtDimText).Prop(Label.StylePropertyFont, textFont).Prop(Label.StylePropertyFontColor, dimTextColor),
            Element<Label>().Class(StyleNano.StyleClassCrtHeading).Prop(Label.StylePropertyFont, headingFont).Prop(Label.StylePropertyFontColor, headingColor),
            Element<Label>().Class(StyleNano.StyleClassCrtHeadingBig).Prop(Label.StylePropertyFont, headingBigFont).Prop(Label.StylePropertyFontColor, headingColor),
            Element<Label>().Class(StyleNano.StyleClassCrtButtonLabel).Prop(Label.StylePropertyFont, textFont).Prop(Label.StylePropertyFontColor, textColor).Prop(Label.StylePropertyAlignMode, Label.AlignMode.Center),
            Element<Label>().Class(StyleNano.StyleClassCrtNativeButtonLabel).Prop(Label.StylePropertyFont, nativeFont).Prop(Label.StylePropertyFontColor, textColor),
            Element<RichTextLabel>().Class(StyleNano.StyleClassCrtRichText).Prop("font", textFont),
            Element<ItemList>().Class(StyleNano.StyleClassCrtItemList).Prop(ItemList.StylePropertyBackground, inset).Prop(ItemList.StylePropertyItemBackground, itemBackground).Prop(ItemList.StylePropertySelectedItemBackground, itemSelected).Prop(ItemList.StylePropertyDisabledItemBackground, itemDisabled).Prop("font", textFont).Prop("font-color", textColor),
            Element<VScrollBar>().Class(StyleNano.StyleClassCrtScrollBar).Prop(ScrollBar.StylePropertyGrabber, scroll),
            Element<VScrollBar>().Class(StyleNano.StyleClassCrtScrollBar).Pseudo(ScrollBar.StylePseudoClassHover).Prop(ScrollBar.StylePropertyGrabber, scrollHover),
            Element<VScrollBar>().Class(StyleNano.StyleClassCrtScrollBar).Pseudo(ScrollBar.StylePseudoClassGrabbed).Prop(ScrollBar.StylePropertyGrabber, scrollPressed),
            Element<HScrollBar>().Class(StyleNano.StyleClassCrtScrollBar).Prop(ScrollBar.StylePropertyGrabber, scroll),
            Element<LineEdit>().Class(StyleNano.StyleClassCrtLineEdit).Prop(LineEdit.StylePropertyStyleBox, lineEdit).Prop("font", textFont).Prop("font-color", textColor).Prop(LineEdit.StylePropertyCursorColor, headingColor).Prop(LineEdit.StylePropertySelectionColor, selectionColor),
            Element<LineEdit>().Class(StyleNano.StyleClassCrtNativeLineEdit).Prop(LineEdit.StylePropertyStyleBox, nativeLineEdit).Prop("font", nativeFont).Prop("font-color", textColor).Prop(LineEdit.StylePropertyCursorColor, headingColor).Prop(LineEdit.StylePropertySelectionColor, selectionColor),
            Element<Slider>().Class(StyleNano.StyleClassCrtSlider).Prop(Slider.StylePropertyBackground, sliderBackground).Prop(Slider.StylePropertyForeground, sliderForeground).Prop(Slider.StylePropertyFill, sliderFill).Prop(Slider.StylePropertyGrabber, sliderGrabber),
            Element<ProgressBar>().Class(StyleNano.StyleClassCrtProgressBar).Prop(ProgressBar.StylePropertyBackground, progressBackground).Prop(ProgressBar.StylePropertyForeground, progressForeground),
            Element<TabContainer>().Class(StyleNano.StyleClassCrtTabContainer).Prop(TabContainer.StylePropertyPanelStyleBox, inset).Prop(TabContainer.StylePropertyTabStyleBox, tabActive).Prop(TabContainer.StylePropertyTabStyleBoxInactive, tabInactive).Prop(TabContainer.stylePropertyTabFontColor, headingColor).Prop(TabContainer.StylePropertyTabFontColorInactive, dimTextColor),
            Element<StripeBack>().Class(StyleNano.StyleClassCrtStripeBack).Prop(StripeBack.StylePropertyBackground, inset),
            Element<TextureButton>().Class(StyleNano.StyleClassCrtIconButton).Prop(Control.StylePropertyModulateSelf, textColor),
        ];
    }
}
