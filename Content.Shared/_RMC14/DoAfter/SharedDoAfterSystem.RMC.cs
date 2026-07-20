using Content.Shared._RMC14.DoAfter;

namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    [Dependency] private RMCDoAfterSystem _rmcDoAfter = default!;

    private bool ShouldCancelRMC(DoAfter doAfter)
    {
        return _rmcDoAfter.ShouldCancel(doAfter);
    }
}
