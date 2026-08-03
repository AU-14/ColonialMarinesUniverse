using Content.Client.Stylesheets.SheetletConfigs;
using Content.Client.Stylesheets.Stylesheets;
using Content.Client.UserInterface.Systems.Chat.Controls;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using static Content.Client.Stylesheets.StylesheetHelpers;

namespace Content.Client.Stylesheets.Sheetlets.Hud;

[CommonSheetlet]
public sealed class ChatSheetlet<T> : Sheetlet<T> where T: PalettedStylesheet, IButtonConfig
{
    public override StyleRule[] GetRules(T sheet, object config)
    {
        IButtonConfig btnCfg = sheet;

        var chatBg = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#07090B"),
            BorderColor = Color.FromHex("#263039"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 2,
            ContentMarginRightOverride = 2,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        var chatSubBg = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#101317"),
            BorderColor = Color.FromHex("#2F3941"),
            BorderThickness = new Thickness(1),
        };
        chatSubBg.SetContentMarginOverride(StyleBox.Margin.All, 2);

        var chatChannelButtonTex =
            sheet.GetTextureOr(btnCfg.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot);
        var chatChannelButton = new StyleBoxTexture
        {
            Texture = chatChannelButtonTex,
        };
        chatChannelButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatChannelButton.SetPadding(StyleBox.Margin.All, 2);

        var chatFilterButtonTex =
            sheet.GetTextureOr(btnCfg.RoundedButtonBorderedPath, NanotrasenStylesheet.TextureRoot);
        var chatFilterButton = new StyleBoxTexture
        {
            Texture = chatFilterButtonTex,
        };
        chatFilterButton.SetPatchMargin(StyleBox.Margin.All, 5);
        chatFilterButton.SetPadding(StyleBox.Margin.All, 2);

        return
        [
            E<PanelContainer>()
                .Class(ChatInputBox.StyleClassChatPanel)
                .Panel(chatBg),
            E<PanelContainer>()
                .Class(StyleNano.StyleClassChatSubPanel)
                .Panel(chatSubBg),
            E<LineEdit>()
                .Class(ChatInputBox.StyleClassChatLineEdit)
                .Prop(LineEdit.StylePropertyStyleBox, new StyleBoxEmpty())
                .Prop("font-color", Color.FromHex("#D6DCE0")),
            E<Button>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatChannelButton),
            E<ContainerButton>().Class(ChatInputBox.StyleClassChatFilterOptionButton).Box(chatFilterButton),
        ];
    }
}
