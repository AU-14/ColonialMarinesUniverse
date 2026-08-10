using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.IoC;
using Robust.Shared.Timing;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class BlightCoreVoteWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Label _timerLabel;
    private readonly BoxContainer _candidateList;

    private NetEntity? _myVote;
    private TimeSpan _endsAt;

    public event Action<NetEntity>? OnVote;

    public BlightCoreVoteWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "Overmind Ascension Vote";
        Resizable = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8)
        };

        root.AddChild(new Label
        {
            Text = "Vote for who should become the Overmind:"
        });

        _timerLabel = new Label
        {
            Text = "35 seconds remaining..."
        };
        root.AddChild(_timerLabel);

        _candidateList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4
        };
        root.AddChild(_candidateList);

        Contents.AddChild(root);
    }

    public void SetEndTime(TimeSpan endsAt)
    {
        _endsAt = endsAt;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var remaining = (float)(_endsAt - _timing.CurTime).TotalSeconds;
        _timerLabel.Text = $"{Math.Max(0, remaining):0} seconds remaining...";
    }

    public void UpdateCandidates(List<BlightCoreVoteCandidate> candidates)
    {
        _candidateList.RemoveAllChildren();

        foreach (var candidate in candidates)
        {
            var isMine = candidate.Candidate == _myVote;

            var button = new Button
            {
                Text = $"{candidate.Name} — {candidate.Votes} vote(s)" +
                       (isMine ? " (your vote)" : ""),
                ToggleMode = true,
                Pressed = isMine
            };

            var captured = candidate.Candidate;
            button.OnPressed += _ =>
            {
                _myVote = captured;
                OnVote?.Invoke(captured);
                UpdateCandidates(candidates);
            };

            _candidateList.AddChild(button);
        }
    }
}