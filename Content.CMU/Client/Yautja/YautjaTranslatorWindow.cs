using System.Numerics;
using Content.Shared._CMU14.Yautja;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Yautja;

public sealed class YautjaTranslatorWindow : DefaultWindow
{
    private readonly Label _powerLabel;
    private readonly Label _limitLabel;
    private readonly TextEdit _messageEdit;
    private readonly Button _sendButton;

    private int _maxLength = 160;

    public event Action<string>? OnSend;

    public YautjaTranslatorWindow()
    {
        Title = Loc.GetString("cmu-yautja-translator-window-title");
        SetSize = new Vector2(360, 190);
        MinSize = new Vector2(320, 160);

        var rootPanel = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.Surface, YautjaBracerUiStyle.Border, new Thickness(2));
        AddChild(rootPanel);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 6,
            Margin = new Thickness(7),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rootPanel.AddChild(root);

        var header = YautjaBracerUiStyle.Panel(YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder);
        root.AddChild(header);

        var statusRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(7, 4),
            HorizontalExpand = true,
        };
        header.AddChild(statusRow);

        _powerLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        _powerLabel.HorizontalExpand = true;
        statusRow.AddChild(_powerLabel);

        _limitLabel = YautjaBracerUiStyle.Label(string.Empty, YautjaBracerUiStyle.Muted, "LabelSubText");
        statusRow.AddChild(_limitLabel);

        var close = YautjaBracerUiStyle.CloseButton();
        close.OnPressed += _ => Close();
        statusRow.AddChild(close);

        _messageEdit = new TextEdit
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            MinHeight = 64,
            Placeholder = new Rope.Leaf(Loc.GetString("cmu-yautja-translator-placeholder")),
        };
        _messageEdit.OnTextChanged += args => UpdateLimit(Rope.Collapse(args.TextRope));
        root.AddChild(YautjaBracerUiStyle.Wrap(_messageEdit, YautjaBracerUiStyle.DeepCard, YautjaBracerUiStyle.MutedBorder, new Thickness(5), new Thickness(1)));

        _sendButton = BuildSendButton(
            Loc.GetString("cmu-yautja-translator-send"),
            YautjaBracerUiStyle.HotRed);
        _sendButton.OnPressed += _ => Send();
        root.AddChild(_sendButton);

        UpdateLimit(string.Empty);
    }

    public void UpdateState(YautjaTranslatorBuiState state)
    {
        _maxLength = state.MaxLength;
        _powerLabel.Text = Loc.GetString("cmu-yautja-translator-power", ("charge", state.Charge), ("max", state.MaxCharge), ("cost", state.Cost));
        UpdateLimit(Rope.Collapse(_messageEdit.TextRope));
    }

    private void UpdateLimit(string text)
    {
        var length = text.Length;
        _limitLabel.Text = Loc.GetString("cmu-yautja-translator-limit", ("count", length), ("max", _maxLength));
        _limitLabel.FontColorOverride = length > _maxLength ? YautjaBracerUiStyle.HotRed : YautjaBracerUiStyle.Muted;
    }

    private static Button BuildSendButton(string title, Color accent)
    {
        return new Button
        {
            Text = title,
            HorizontalExpand = true,
            MinHeight = 34,
            SetHeight = 34,
            StyleBoxOverride = YautjaBracerUiStyle.Flat(YautjaBracerUiStyle.DeepCard, accent),
        };
    }

    private void Send()
    {
        var text = Rope.Collapse(_messageEdit.TextRope).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > _maxLength)
            text = text[.._maxLength];

        OnSend?.Invoke(text);
        _messageEdit.TextRope = Rope.Leaf.Empty;
        UpdateLimit(string.Empty);
    }
}
