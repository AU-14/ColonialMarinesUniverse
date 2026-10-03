using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Actions;

public sealed partial class ActionsSystem
{
    private readonly Queue<CmuQueuedAction> _cmuRequestedActions = new();

    private void InitializeCmuActionQueue()
    {
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => _cmuRequestedActions.Clear());
        UpdatesOutsidePrediction = true;
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Replay is driven by the predictive events emitted here, never by draining input again.
        if (!GameTiming.InSimulation || !GameTiming.IsFirstTimePredicted)
            return;

        while (_cmuRequestedActions.TryDequeue(out var request))
        {
            if (_playerManager.LocalEntity != request.User ||
                !HasAction(request.User, request.Action) ||
                !TryComp<ActionComponent>(request.Action, out var action) ||
                action.AttachedEntity != request.User || !action.Enabled || action.ClientExclusive)
            {
                continue;
            }

            // Pair the current predicted dispatch pose with the snapshot visible at dispatch.
            var lastRealTick = _rmcLagCompensation.GetLastRealTick(null);
            var actionId = GetNetEntity(request.Action);
            var ev = request.Coordinates is { } coords
                ? new RequestPerformActionEvent(actionId, request.Target, coords, lastRealTick)
                : request.Target is { } target
                    ? new RequestPerformActionEvent(actionId, target, lastRealTick)
                    : new RequestPerformActionEvent(actionId, lastRealTick);
            RaisePredictiveEvent(ev);
        }
    }

    private void QueueCmuAction(EntityUid user, EntityUid action, NetEntity? target = null, NetCoordinates? coordinates = null)
    {
        _cmuRequestedActions.Enqueue(new CmuQueuedAction(user, action, target, coordinates));
    }

    private readonly record struct CmuQueuedAction(EntityUid User, EntityUid Action, NetEntity? Target, NetCoordinates? Coordinates);
}
