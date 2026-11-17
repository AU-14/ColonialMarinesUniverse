using Content.Shared.CMU14;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Comms;

public sealed class ColonyGarrisonLiaisonBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ColonyGarrisonLiaisonWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyGarrisonLiaisonWindow>();
        _window.OnSend += message => SendPredictedMessage(new ColonyCommsConsoleSendMessageBuiMsg(message));
        _window.OnTransfer += (toColony, amount) => SendMessage(new ColonyGarrisonLiaisonTransferBuiMsg(toColony, amount));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (message is ColonyGarrisonLiaisonFundsBuiMsg funds)
            _window?.UpdateFunds(funds);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is ColonyGarrisonLiaisonBuiState s)
            _window?.UpdateState(s);
    }
}
