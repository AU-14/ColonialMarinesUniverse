using System.Numerics;
using Content.Client.Eui;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Timing;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.CMU14.Yautja.BadBlood;

// FLOW-113: the two windows of the ascension vote. The server opens them by class name.

[UsedImplicitly]
public sealed class BadBloodAscensionPromptEui : BaseEui
{
    private readonly BadBloodAscensionPromptWindow _window = new();
    private bool _answered;

    public BadBloodAscensionPromptEui()
    {
        _window.AcceptButton.OnPressed += _ => Respond(true);
        _window.DeclineButton.OnPressed += _ => Respond(false);
        _window.OnClose += () => Respond(false);
    }

    private void Respond(bool accept)
    {
        if (_answered)
            return;

        _answered = true;
        SendMessage(new BadBloodAscensionPromptMessage(accept));
        _window.Close();
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is BadBloodAscensionPromptState prompt)
            _window.SetFromBracer(prompt.FromBracer);
    }

    public override void Closed()
    {
        _answered = true;
        _window.Close();
    }
}

public sealed class BadBloodAscensionPromptWindow : DefaultWindow
{
    public readonly Button AcceptButton;
    public readonly Button DeclineButton;

    private readonly Label _text;

    public BadBloodAscensionPromptWindow()
    {
        Title = Loc.GetString("cmu-bad-blood-ascension-prompt-title");

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                (_text = new Label { Text = Loc.GetString("cmu-bad-blood-ascension-prompt-text") }),
                new Control { MinSize = new Vector2(0, 8) },
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    Align = AlignMode.Center,
                    Children =
                    {
                        (AcceptButton = new Button { Text = Loc.GetString("cmu-bad-blood-ascension-prompt-accept") }),
                        new Control { MinSize = new Vector2(20, 0) },
                        (DeclineButton = new Button { Text = Loc.GetString("cmu-bad-blood-ascension-prompt-decline") }),
                    },
                },
            },
        });
    }

    public void SetFromBracer(bool fromBracer)
    {
        _text.Text = Loc.GetString(fromBracer
            ? "cmu-bad-blood-ascension-prompt-text-bracer"
            : "cmu-bad-blood-ascension-prompt-text");
    }
}

[UsedImplicitly]
public sealed class BadBloodAscensionVoteEui : BaseEui
{
    private readonly BadBloodAscensionVoteWindow _window = new();

    public BadBloodAscensionVoteEui()
    {
        _window.OnVote += candidate => SendMessage(new BadBloodAscensionVoteMessage(candidate));
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not BadBloodAscensionVoteState vote)
            return;

        _window.SetState(vote);

        // closed by hand earlier: a new candidate brings it back
        if (!_window.IsOpen)
            _window.OpenCentered();
    }

    public override void Closed()
    {
        _window.Close();
    }
}

public sealed class BadBloodAscensionVoteWindow : DefaultWindow
{
    public event Action<NetEntity>? OnVote;

    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly Label _time;
    private readonly BoxContainer _rows;
    private TimeSpan _endsAt;

    public BadBloodAscensionVoteWindow()
    {
        Title = Loc.GetString("cmu-bad-blood-ascension-vote-title");
        MinSize = new Vector2(420, 200);

        ContentsContainer.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Children =
            {
                new Label { Text = Loc.GetString("cmu-bad-blood-ascension-vote-text") },
                (_time = new Label()),
                new Control { MinSize = new Vector2(0, 8) },
                (_rows = new BoxContainer { Orientation = LayoutOrientation.Vertical }),
            },
        });
    }

    public void SetState(BadBloodAscensionVoteState state)
    {
        _endsAt = state.EndsAt;
        _rows.RemoveAllChildren();

        foreach (var candidate in state.Candidates)
        {
            var mine = state.VotedFor == candidate.Entity;
            var button = new Button
            {
                Text = Loc.GetString(mine ? "cmu-bad-blood-ascension-vote-voted" : "cmu-bad-blood-ascension-vote-button"),
                Disabled = mine,
            };

            var entity = candidate.Entity;
            button.OnPressed += _ => OnVote?.Invoke(entity);

            _rows.AddChild(new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Children =
                {
                    new Label { Text = candidate.Name, HorizontalExpand = true },
                    new Label
                    {
                        Text = Loc.GetString("cmu-bad-blood-ascension-vote-kills", ("kills", candidate.Kills)),
                        MinSize = new Vector2(80, 0),
                    },
                    new Label
                    {
                        Text = Loc.GetString("cmu-bad-blood-ascension-vote-votes", ("votes", candidate.Votes)),
                        MinSize = new Vector2(80, 0),
                    },
                    button,
                },
            });
        }

        RefreshTime();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        RefreshTime();
    }

    private void RefreshTime()
    {
        var seconds = Math.Max(0, (int) Math.Ceiling((_endsAt - _timing.CurTime).TotalSeconds));
        _time.Text = Loc.GetString("cmu-bad-blood-ascension-vote-time", ("seconds", seconds));
    }
}
