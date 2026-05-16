using Content.Shared._AU14.Abominations;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._AU14.Abominations;

[UsedImplicitly]
public sealed class AbominationMimicBui : BoundUserInterface
{
    [ViewVariables]
    private AbominationMimicWindow? _window;

    public AbominationMimicBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<AbominationMimicWindow>();
        _window.OnFormSelected += idx => SendPredictedMessage(new AbominationMimicSelectFormMessage(idx));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is AbominationMimicBuiState s)
            _window?.Populate(s.ProfileNames, s.ActiveIndex);
    }
}
