using Content.Shared.CMU14.Threats.Mobs.Biomorph;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Threats.Mobs.Biomorphs;

[UsedImplicitly]
public sealed class BiomorphMimicBui : BoundUserInterface
{
    [ViewVariables]
    private BiomorphMimicWindow? _window;

    public BiomorphMimicBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BiomorphMimicWindow>();
        _window.OnFormSelected += idx => SendPredictedMessage(new BiomorphMimicSelectFormMessage(idx));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BiomorphMimicBuiState s)
            _window?.Populate(s.ProfileNames, s.ActiveIndex);
    }
}
