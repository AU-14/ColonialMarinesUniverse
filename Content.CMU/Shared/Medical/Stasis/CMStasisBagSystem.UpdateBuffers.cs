namespace Content.Shared._RMC14.Medical.Stasis;

public sealed partial class CMStasisBagSystem
{
    // Opening a bag publishes container events and can empty its contents during iteration.
    private readonly Stack<List<EntityUid>> _cmuStasisSnapshots = new();
}
