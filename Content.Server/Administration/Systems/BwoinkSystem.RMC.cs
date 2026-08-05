namespace Content.Server.Administration.Systems;

public sealed partial class BwoinkSystem
{
    public void ClearRmcRelayMessages()
    {
        _relayMessages.Clear();
    }
}
