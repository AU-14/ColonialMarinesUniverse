using Content.Shared._CMU14.Xenomorphs.Pathogen.Overmind;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Xenomorphs.Pathogen.Overmind;

public sealed class BlightCoreAcceptWindow : DefaultWindow
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Label _timerLabel;
    private TimeSpan _endsAt;

    public event Action? OnAccept;
    public event Action? OnDecline;

    public BlightCoreAcceptWindow()
    {
        IoCManager.InjectDependencies(this);

        Title = "Overmind Ascension";
        Resizable = false;

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8)
        };

        root.AddChild(new Label
        {
            Text = "The Blight Core calls to you. Will you become the Overmind?"
        });

        _timerLabel = new Label { Text = "" };
        root.AddChild(_timerLabel);

        var buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8
        };

        var accept = new Button { Text = "Accept" };
        var decline = new Button { Text = "Decline" };

        accept.OnPressed += _ => OnAccept?.Invoke();
        decline.OnPressed += _ => OnDecline?.Invoke();

        buttons.AddChild(accept);
        buttons.AddChild(decline);
        root.AddChild(buttons);

        Contents.AddChild(root);
    }

    public void SetEndTime(TimeSpan endsAt) => _endsAt = endsAt;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        var remaining = (float)(_endsAt - _timing.CurTime).TotalSeconds;
        _timerLabel.Text = $"{Math.Max(0, remaining):0} seconds remaining...";
    }
}