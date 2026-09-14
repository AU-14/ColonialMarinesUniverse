using Content.Server.EUI;
using Content.Shared.CMU14.PersistentEconomy;
using Content.Shared.Eui;

namespace Content.Server.CMU14.PersistentEconomy;

public sealed class CMUEconomyEui(CMUPersistentEconomySystem economy, EntityUid? atm) : BaseEui
{
    private Guid _token = Guid.NewGuid();
    private string _status = "";

    public override EuiStateBase GetNewState() => economy.GetState(Player, atm, _token, _status);
    public override void Opened() => StateDirty();
    public override void Closed() => economy.Closed(Player.UserId, this);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown || msg is not CMUEconomyMessage request || request.Token != _token)
            return;
        // A server-issued single-use token covers retries and all actions in this UI.
        _token = Guid.NewGuid();
        try
        {
            _status = Loc.GetString(economy.Handle(Player, atm, request) ? "cmu-economy-ok" : "cmu-economy-rejected");
        }
        catch (Exception e)
        {
            Robust.Shared.Log.Logger.Error($"Economy operation failed for {Player.UserId}: {e}");
            _status = Loc.GetString("cmu-economy-unavailable");
        }
        StateDirty();
    }
}
