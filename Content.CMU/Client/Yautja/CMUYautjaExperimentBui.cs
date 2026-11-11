using Content.Shared.CMU14.Yautja;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Yautja;

[UsedImplicitly]
public sealed class CMUYautjaExperimentBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUYautjaExperimentWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUYautjaExperimentWindow>();
        _window.SetBracer(Owner);
        _window.OnCommand += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMUYautjaExperimentUiState guide)
            _window?.UpdateState(guide);
    }
}
