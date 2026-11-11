using Robust.Client.GameStates;

// ReSharper disable once CheckNamespace
namespace Content.Client.Entry;

public sealed partial class EntryPoint
{
    [Dependency] private IClientGameStateManager _clientGameStates = default!;

    private void ClearDisconnectedServerState()
    {
        // RunLevel.Initialize follows entity shutdown. Entity systems have already unsubscribed
        // by then, so cleanup belongs to the content entry point's process-lifetime callback.
        // The engine resets its state buffer but retains this prediction cache until the next
        // full server state. No prediction can use it while disconnected.
        var states = _clientGameStates.GetFullRep();
        states.Clear();
        states.TrimExcess();
    }
}
