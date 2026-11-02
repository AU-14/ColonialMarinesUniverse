using System.Numerics;
using Content.Client.CMU14.Interface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// Bottom-right stack of who you can hear on voice chat right now: one row per speaker with their name,
/// how they're reaching you (radio channel, PA, shout, whisper) and a scrolling level graph. Your own row
/// appears while you talk. Rows hold briefly after a speaker stops, then fade.
/// </summary>
public sealed class CMUVoiceSpeakerList : Control
{
    private const int MaxEntries = 8;
    private const float MinAudibility = 0.01f;

    private readonly BoxContainer _entries;
    private readonly Dictionary<int, CMUVoiceSpeakerEntry> _byStream = new();
    private readonly List<int> _expired = new();
    private CMUVoiceSpeakerEntry? _self;

    public CMUVoiceSpeakerList()
    {
        MouseFilter = MouseFilterMode.Ignore;
        _entries = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Bottom,
            MouseFilter = MouseFilterMode.Ignore,
        };
        AddChild(_entries);
    }

    public void SetInset(float right, float bottom)
    {
        var margin = new Thickness(0, 0, right, bottom);
        if (_entries.Margin != margin)
            _entries.Margin = margin;
    }

    public void Update(float frameTime, CMUVoiceChatSystem voice)
    {
        var added = false;
        foreach (var (key, stream) in voice.Streams)
        {
            if (_byStream.Count >= MaxEntries)
                break;

            if (_byStream.ContainsKey(key) ||
                voice.IsSelf(stream.Speaker) ||
                !stream.Playing ||
                stream.Audibility < MinAudibility)
            {
                continue;
            }

            var entry = new CMUVoiceSpeakerEntry();
            _byStream[key] = entry;
            _entries.AddChild(entry);
            added = true;
        }

        foreach (var (key, entry) in _byStream)
        {
            if (voice.Streams.TryGetValue(key, out var stream))
            {
                var active = stream.Activity > 0f && stream.Audibility >= MinAudibility;
                var level = stream.Level * (0.25f + 0.75f * stream.Audibility);
                entry.Update(frameTime, active, level, voice.GetSpeakerName(stream.Speaker),
                    voice.GetRouteLabel(stream), voice.GetRouteColor(stream));
            }
            else
            {
                entry.Update(frameTime, false, 0f, null, null, null);
            }

            if (entry.Expired)
                _expired.Add(key);
        }

        foreach (var key in _expired)
        {
            if (_byStream.Remove(key, out var entry))
                entry.Orphan();
        }

        _expired.Clear();
        UpdateSelf(frameTime, voice, added);
    }

    public void Clear()
    {
        _byStream.Clear();
        _self = null;
        _entries.RemoveAllChildren();
    }

    private void UpdateSelf(float frameTime, CMUVoiceChatSystem voice, bool reorder)
    {
        var self = voice.Self;
        if (_self == null)
        {
            if (self.Activity <= 0f)
                return;

            _self = new CMUVoiceSpeakerEntry();
            _entries.AddChild(_self);
        }
        else if (reorder)
        {
            _self.SetPositionLast();
        }

        _self.Update(frameTime, self.Activity > 0f, self.Level, Loc.GetString("cmu-voice-speaker-self"),
            voice.GetSelfLabel(), voice.GetSelfColor());

        if (!_self.Expired)
            return;

        _self.Orphan();
        _self = null;
    }
}

/// <summary>
/// One speaker row: a coloured edge, the name, an optional route tag and the level history drawn behind them.
/// </summary>
public sealed class CMUVoiceSpeakerEntry : Control
{
    private const int HistoryLength = 64;
    private const float SampleInterval = 0.05f;
    private const float HoldTime = 1.2f;
    private const float FadeTime = 0.4f;
    private const float MinVisibility = 0.01f;

    private readonly float[] _history = new float[HistoryLength];
    private readonly Label _name;
    private readonly Label _route;
    private int _head;
    private int _count;
    private float _sinceSample;
    private float _level;
    private float _silentFor;
    private Color _color = Color.White;

    public bool Expired => _silentFor >= HoldTime + FadeTime;

    public CMUVoiceSpeakerEntry()
    {
        MinSize = new Vector2(210, 22);
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;

        _name = new Label
        {
            HorizontalExpand = true,
            ClipText = true,
            VerticalAlignment = VAlignment.Center,
            FontColorOverride = CrtTerminalPalette.TextBright,
        };
        _route = new Label
        {
            VerticalAlignment = VAlignment.Center,
            Visible = false,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(9, 0, 6, 0),
        };
        row.AddChild(_name);
        row.AddChild(_route);
        AddChild(row);
    }

    /// <param name="name">Null keeps the last name, for a speaker whose stream just ended.</param>
    public void Update(float frameTime, bool active, float level, string? name, string? route, Color? color)
    {
        _level = active ? level : 0f;
        _silentFor = active ? 0f : _silentFor + frameTime;

        if (color is { } newColor)
            _color = newColor;

        if (name != null && _name.Text != name)
            _name.Text = name;

        if (name != null)
        {
            _route.Visible = route != null;
            if (route != null && _route.Text != route)
                _route.Text = route;
        }

        _route.FontColorOverride = _color;

        _sinceSample = MathF.Min(_sinceSample + frameTime, SampleInterval * HistoryLength);
        while (_sinceSample >= SampleInterval)
        {
            _sinceSample -= SampleInterval;
            _history[_head] = _level;
            _head = (_head + 1) % HistoryLength;
            _count = Math.Min(_count + 1, HistoryLength);
        }

        var fade = _silentFor <= HoldTime ? 1f : 1f - (_silentFor - HoldTime) / FadeTime;
        Modulate = Color.White.WithAlpha(Math.Clamp(fade, 0f, 1f));
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var width = (float) PixelWidth;
        var height = (float) PixelHeight;
        handle.DrawRect(new UIBox2(0f, 0f, width, height), CrtTerminalPalette.Void.WithAlpha(0.7f));

        var fill = _color.WithAlpha(0.3f);
        var barWidth = width / (HistoryLength - 1);
        var scroll = _sinceSample / SampleInterval * barWidth;

        DrawColumn(handle, width - scroll, width, _level, height, fill);
        for (var i = 0; i < _count; i++)
        {
            var right = width - scroll - i * barWidth;
            if (right <= 0f)
                break;

            var level = _history[(_head - 1 - i + HistoryLength) % HistoryLength];
            DrawColumn(handle, MathF.Max(0f, right - barWidth), right, level, height, fill);
        }

        handle.DrawRect(new UIBox2(0f, 0f, MathF.Max(2f, 3f * UIScale), height), _color);
    }

    private static void DrawColumn(DrawingHandleScreen handle, float left, float right, float level, float height, Color color)
    {
        if (level <= MinVisibility || right <= left)
            return;

        var half = level * (height - 4f) * 0.5f;
        var center = height * 0.5f;
        handle.DrawRect(new UIBox2(left, center - half, right, center + half), color);
    }
}
