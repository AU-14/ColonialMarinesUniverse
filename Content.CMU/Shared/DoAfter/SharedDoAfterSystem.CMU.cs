namespace Content.Shared.DoAfter;

public abstract partial class SharedDoAfterSystem
{
    /// <summary>
    /// CMU14: starts a running do-after's bar part-way, as if it had begun that fraction of its delay ago.
    /// </summary>
    public void CMUSetProgress(DoAfterId id, float fraction)
    {
        if (!TryComp(id.Uid, out DoAfterComponent? comp) ||
            !comp.DoAfters.TryGetValue(id.Index, out var doAfter))
        {
            return;
        }

        doAfter.StartTime = GameTiming.CurTime - doAfter.Args.Delay * Math.Clamp(fraction, 0f, 1f);
        Dirty(id.Uid, comp);
    }
}
