using Content.Shared.CMU14.ColonyEconomy;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.ColonyEconomy;

public sealed class SubmissionMarketBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SubmissionMarketWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<SubmissionMarketWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SubmissionMarketBuiState s)
            return;

        _window.UpdateState(s);
    }
}
