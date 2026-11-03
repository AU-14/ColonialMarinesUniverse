using Content.Client.CMU14.Interface;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Input;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// The once-per-join voice chat explainer, in the CMU lobby theme like the other join popups: the basics,
/// the talk keys as currently bound, and two choice cards to connect now or later.
/// </summary>
public sealed class CMUVoiceChatIntroWindow : DefaultWindow
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IStylesheetManager _stylesheetManager = default!;

    public event Action? OpenPageRequested;

    private readonly CmuChoiceCard _open;
    private readonly CmuChoiceCard _later;
    private readonly CheckBox _dontShowAgain;

    public bool DontShowAgain => _dontShowAgain.Pressed;

    public CMUVoiceChatIntroWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-voice-intro-title");
        MinWidth = CmuPanelMetrics.ChoiceWindowWidth;

        var prompt = new Label
        {
            Text = Loc.GetString("cmu-voice-intro-heading"),
            StyleClasses = { "CrtHeading" },
        };

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = CmuPanelMetrics.PanelPadding,
            SeparationOverride = CmuPanelMetrics.RowSeparation,
        };

        stack.AddChild(prompt);
        stack.AddChild(Text(Loc.GetString("cmu-voice-intro-body")));

        stack.AddChild(Heading(Loc.GetString("cmu-voice-intro-keys-heading")));
        stack.AddChild(Text(Loc.GetString("cmu-voice-intro-keys",
            ("ptt", Key(CMUKeyFunctions.CMUVoicePushToTalk)),
            ("radio", Key(CMUKeyFunctions.CMUVoiceRadioPushToTalk)),
            ("cycle", Key(CMUKeyFunctions.CMUVoiceRadioCycleChannel)))));

        stack.AddChild(Heading(Loc.GetString("cmu-voice-intro-tips-heading")));
        stack.AddChild(Text(Loc.GetString("cmu-voice-intro-tips")));

        _open = new CmuChoiceCard(
            Loc.GetString("cmu-voice-intro-open"),
            Loc.GetString("cmu-voice-intro-open-desc"),
            CmuChoiceCard.Terminal,
            buttonOnLeft: true);
        _later = new CmuChoiceCard(
            Loc.GetString("cmu-voice-intro-close"),
            Loc.GetString("cmu-voice-intro-close-desc"),
            CmuChoiceCard.Terminal,
            buttonOnLeft: false);

        _open.Button.OnPressed += _ => OpenPageRequested?.Invoke();
        _later.Button.OnPressed += _ => Close();
        stack.AddChild(_open);
        stack.AddChild(_later);

        _dontShowAgain = new CheckBox { Text = Loc.GetString("cmu-voice-intro-dont-show") };
        stack.AddChild(_dontShowAgain);
        stack.AddChild(new Label
        {
            Text = Loc.GetString("cmu-voice-intro-settings"),
            StyleClasses = { "CrtDimText" },
        });

        ContentsContainer.AddChild(new PanelContainer
        {
            StyleClasses = { "CrtPanelFill" },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { stack },
        });

        Stylesheet = _stylesheetManager.SheetNano;
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
        _open.ApplyPalette();
        _later.ApplyPalette();
    }

    private static Label Heading(string text)
    {
        return new Label { Text = text, StyleClasses = { "CrtHeading" }, Margin = new Thickness(0, 4, 0, 0) };
    }

    private static RichTextLabel Text(string markup)
    {
        var label = new RichTextLabel
        {
            HorizontalExpand = true,
            MaxWidth = CmuPanelMetrics.ChoiceWindowWidth - CmuPanelMetrics.PanelPaddingHorizontal * 2,
            StyleClasses = { "CrtRichText" },
        };
        label.SetMessage(FormattedMessage.FromMarkupPermissive(markup));
        return label;
    }

    private string Key(BoundKeyFunction function)
    {
        var key = _input.TryGetKeyBinding(function, out _)
            ? _input.GetKeyFunctionButtonString(function)
            : Loc.GetString("cmu-ui-options-voice-chat-key-unbound");

        return FormattedMessage.EscapeText(key);
    }
}
