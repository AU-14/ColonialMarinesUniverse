using System.Numerics;
using Content.Shared.CMU14.Round.Antags.Rider;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.Round.Antags.Rider;

public sealed class RiderVoiceWindow : DefaultWindow
{
    public event Action<string, bool>? OnVoiceSent;

    private readonly LineEdit _input;

    public RiderVoiceWindow()
    {
        Title = Loc.GetString("rider-voice-title");
        Resizable = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        root.AddChild(new Label
        {
            Text = Loc.GetString("rider-voice-description"),
            Margin = new Thickness(0, 0, 0, 8),
        });

        _input = new LineEdit
        {
            PlaceHolder = Loc.GetString("rider-voice-placeholder"),
            Margin = new Thickness(0, 0, 0, 8),
        };
        root.AddChild(_input);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
        };

        var whisper = new Button { Text = Loc.GetString("rider-voice-whisper") };
        whisper.OnPressed += _ => Send(false);
        buttons.AddChild(whisper);

        var speak = new Button { Text = Loc.GetString("rider-voice-speak") };
        speak.OnPressed += _ => Send(true);
        buttons.AddChild(speak);

        root.AddChild(buttons);
        Contents.AddChild(root);
        SetSize = new Vector2(360, 150);
    }

    private void Send(bool speakThrough)
    {
        var text = _input.Text.Trim();
        if (text.Length == 0)
            return;

        OnVoiceSent?.Invoke(text, speakThrough);
        _input.Clear();
    }
}
