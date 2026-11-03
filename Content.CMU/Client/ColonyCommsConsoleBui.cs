using Content.Client.CMU14.ColonyEconomy;
using Content.Shared.CCVar;
using Content.Shared.CMU14;
using Content.Shared.CMU14.VoiceChat;
using Robust.Shared.Configuration;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14;

public sealed partial class ColonyCommsConsoleBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{

    [Dependency] private IConfigurationManager _config = default!;

    private ColonyCommsConsole? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ColonyCommsConsole>();

        _window.Emergency.OnPressed += _ =>  SendPredictedMessage(new ColonyCommsConsoleSirenBuiMsg());
        _window.VoiceAnnounceButton.Visible = _config.GetCVar(CCVars.CMUVoiceChatEnabled);
        _window.VoiceAnnounceButton.OnPressed += _ => SendMessage(new CMUVoiceAnnounceToggleBuiMsg());
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
