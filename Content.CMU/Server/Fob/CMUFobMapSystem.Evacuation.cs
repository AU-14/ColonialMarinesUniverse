using Content.Server.CMU14.WithdrawConsole;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobMapSystem
{
    // A depleted/deleted bank, lost map or routine flight cannot restore protected evacuation.
    // Only round cleanup releases the policy, alongside the other round-owned FOB state.
    private bool _contestedEvacuation;

    private void InitializeEvacuation()
    {
        SubscribeLocalEvent<CMUProtectedWithdrawalAttemptEvent>(OnProtectedWithdrawalAttempt);
    }

    private void OnProtectedWithdrawalAttempt(ref CMUProtectedWithdrawalAttemptEvent args)
    {
        if (!_enabled || args.Cancelled)
            return;

        // A request can arrive before the map selector's first update, or before the first landing.
        if (!_contestedEvacuation)
        {
            var maps = AllEntityQuery<CMUFobMapComponent>();
            while (maps.MoveNext(out var uid, out var map))
                TryPrepare((uid, map));
        }

        if (_contestedEvacuation)
            args.Cancelled = true;
    }
}
