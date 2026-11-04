using Content.Client.CMU14.ColonyEconomy;
using Content.Shared.CMU14;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14;

public sealed class ColonyCommsConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{

    private ColonyCommsConsole? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyCommsConsole>();

        _window.Emergency.OnPressed += _ =>  SendPredictedMessage(new ColonyCommsConsoleSirenBuiMsg());
        _window.SendMessage.OnPressed += _ =>
        {
            var message = _window.Announcement.Text;
            SendPredictedMessage(new ColonyCommsConsoleSendMessageBuiMsg(message));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {

    }
}
