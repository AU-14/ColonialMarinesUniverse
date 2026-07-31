using Content.Shared._AU14.Radio;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Radio;

public sealed class TunableFrequencyBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private TunableFrequencyWindow? _window;

    public TunableFrequencyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<TunableFrequencyWindow>();
        _window.OnSetFrequency += text => SendMessage(new TunableFrequencySetMsg(text));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is TunableFrequencyState s)
            _window?.UpdateState(s);
    }
}
