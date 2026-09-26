using Content.Client.Message;
using Content.Client._RMC14.UserInterface;
using Content.Shared._RMC14.Dropship;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Dropship;

[UsedImplicitly]
public sealed class DropshipHijackerBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private DropshipHijackerWindow? _window;

    private ConfirmationWindow? _declineConfirmation;
    private ConfirmationWindow? _finalDeclineConfirmation;

    protected override void Open()
    {
        base.Open();
        if (State is DropshipHijackerBuiState s)
            Set(s);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DropshipHijackerBuiState s)
            Set(s);
    }

    private void Set(DropshipHijackerBuiState s)
    {
        if (_window == null)
        {
            _window = this.CreateWindow<DropshipHijackerWindow>();
            // CMU14: Force on Force roles, hijacking, announcements and identification.
            _window.Header.SetMarkup(Loc.GetString("cmu-dropship-hijack-random-header"));
            _window.DeclineHijackButton.OnPressed += _ => OpenDeclineConfirmation();
            // CMU14: Force on Force roles, hijacking, announcements and identification.
            _window.InitiateHijackButton.OnPressed += _ =>
            {
                SendPredictedMessage(new DropshipHijackerInitiateBuiMsg());
                Close();
            };
        }

        _window.DeclineSeparator.Visible = s.CanDeclineHijack;
        _window.DeclineHijackButton.Visible = s.CanDeclineHijack;

        // CMU14: Force on Force roles, hijacking, announcements and identification.
        _window.InitiateHijackButton.Disabled = !s.CanHijack;
    }

    private void OpenDeclineConfirmation()
    {
        _declineConfirmation?.Close();

        var confirmation = new ConfirmationWindow();
        _declineConfirmation = confirmation;
        confirmation.Setup(
            Loc.GetString("cmu-dropship-hijack-decline-confirm-title"),
            Loc.GetString("cmu-dropship-hijack-decline-confirm-text"),
            Loc.GetString("cmu-dropship-hijack-decline-confirm-accept"),
            Loc.GetString("cmu-dropship-hijack-decline-confirm-deny"));

        confirmation.OnClose += () =>
        {
            if (_declineConfirmation == confirmation)
                _declineConfirmation = null;
        };
        confirmation.DenyButton.OnPressed += _ => confirmation.Close();
        confirmation.AcceptButton.OnPressed += _ =>
        {
            confirmation.Close();
            OpenFinalDeclineConfirmation();
        };
        confirmation.OpenCentered();
    }

    private void OpenFinalDeclineConfirmation()
    {
        _finalDeclineConfirmation?.Close();

        var confirmation = new ConfirmationWindow();
        _finalDeclineConfirmation = confirmation;
        confirmation.Setup(
            Loc.GetString("cmu-dropship-hijack-decline-final-title"),
            Loc.GetString("cmu-dropship-hijack-decline-final-text"),
            Loc.GetString("cmu-dropship-hijack-decline-final-accept"),
            Loc.GetString("cmu-dropship-hijack-decline-final-deny"));

        confirmation.OnClose += () =>
        {
            if (_finalDeclineConfirmation == confirmation)
                _finalDeclineConfirmation = null;
        };
        confirmation.DenyButton.OnPressed += _ => confirmation.Close();
        confirmation.AcceptButton.OnPressed += _ =>
        {
            SendPredictedMessage(new DropshipHijackerDeclineBuiMsg());
            confirmation.Close();
            Close();
        };
        confirmation.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _declineConfirmation?.Close();
        _finalDeclineConfirmation?.Close();
    }
}
