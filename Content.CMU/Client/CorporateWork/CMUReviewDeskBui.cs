using Content.Shared.CMU14.CorporateWork;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.CorporateWork;

public sealed class CMUReviewDeskBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CMUReviewDeskWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CMUReviewDeskWindow>();
        _window.OnStart += tier => SendMessage(new CMUReviewDeskStartBuiMsg(tier));
        _window.OnSubmit += decisions => SendMessage(new CMUReviewDeskSubmitBuiMsg(decisions));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not CMUReviewDeskBuiState s)
            return;

        _window.UpdateState(s);
    }
}
