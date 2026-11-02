using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Shared.Radio;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// The headset's voice radio settings, in the CRT theme: which channel you talk on, and which channels you hear.
/// </summary>
public sealed class CMUVoiceRadioWindow : DefaultWindow
{
    /// <summary>One channel row as the window shows it.</summary>
    public readonly record struct ChannelRow(RadioChannelPrototype Channel, bool CanTalk, bool Talking, bool Heard);

    public event Action<RadioChannelPrototype>? TalkPressed;
    public event Action<RadioChannelPrototype, bool>? HearToggled;

    private readonly CrtConsoleKit _kit = new();
    private readonly Label _subheading;
    private readonly BoxContainer _channels;
    private readonly Label _footer;

    public EntityUid Headset { get; private set; }

    public CMUVoiceRadioWindow()
    {
        Title = Loc.GetString("cmu-voice-radio-window-title");
        MinSize = new Vector2(420, 300);
        SetSize = new Vector2(460, 420);

        _kit.Root.AddChild(_kit.Header(Loc.GetString("cmu-voice-radio-window-heading"), out _subheading));

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        var section = _kit.Section(Loc.GetString("cmu-voice-radio-window-channels"), out _channels);
        scroll.AddChild(section);
        _kit.Root.AddChild(scroll);

        _kit.Root.AddChild(_kit.Footer(out _footer));
        _footer.ClipText = false;

        Contents.AddChild(_kit.Tube);
    }

    public void Populate(EntityUid headset, string headsetName, IReadOnlyList<ChannelRow> rows, string radioKey, string cycleKey)
    {
        Headset = headset;
        _subheading.Text = headsetName;
        _footer.Text = Loc.GetString("cmu-voice-radio-window-footer", ("radio", radioKey), ("cycle", cycleKey));

        // Keep the section heading and its rule; replace the rows under them.
        while (_channels.ChildCount > 2)
        {
            _channels.GetChild(_channels.ChildCount - 1).Orphan();
        }

        if (rows.Count == 0)
        {
            _channels.AddChild(CrtConsoleKit.Text(Loc.GetString("cmu-voice-radio-window-none"), _kit.Body, CrtTerminalPalette.TextDim));
            return;
        }

        foreach (var row in rows)
        {
            _channels.AddChild(BuildRow(row));
        }
    }

    private Control BuildRow(ChannelRow row)
    {
        var line = CrtConsoleKit.Row();

        // The channel's own colour, as text radio shows it.
        line.AddChild(new PanelContainer
        {
            MinSize = new Vector2(4, 22),
            VerticalAlignment = VAlignment.Center,
            PanelOverride = new StyleBoxFlat { BackgroundColor = row.Channel.Color },
        });

        var name = CrtConsoleKit.Text(row.Channel.LocalizedName, _kit.Bold,
            row.Heard ? CrtTerminalPalette.TextBright : CrtTerminalPalette.TextDim);
        name.HorizontalExpand = true;
        name.ClipText = true;
        name.VerticalAlignment = VAlignment.Center;
        line.AddChild(name);

        var hear = new CheckBox
        {
            Text = Loc.GetString("cmu-voice-radio-window-hear"),
            Pressed = row.Heard,
            VerticalAlignment = VAlignment.Center,
        };
        hear.OnToggled += args => HearToggled?.Invoke(row.Channel, args.Pressed);
        line.AddChild(hear);

        if (row.Talking)
        {
            line.AddChild(_kit.Chip(Loc.GetString("cmu-voice-radio-window-talking"), CrtTerminalPalette.Accent));
        }
        else if (row.CanTalk)
        {
            var talk = _kit.Button(Loc.GetString("cmu-voice-radio-window-talk"));
            talk.MinWidth = 90;
            talk.OnPressed += _ => TalkPressed?.Invoke(row.Channel);
            line.AddChild(talk);
        }
        else
        {
            var listenOnly = CrtConsoleKit.Text(Loc.GetString("cmu-voice-radio-window-listen-only"), _kit.Small, CrtTerminalPalette.TextDim);
            listenOnly.MinWidth = 90;
            listenOnly.VerticalAlignment = VAlignment.Center;
            line.AddChild(listenOnly);
        }

        return line;
    }
}
