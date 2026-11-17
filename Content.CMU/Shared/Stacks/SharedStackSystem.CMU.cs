// ReSharper disable CheckNamespace
namespace Content.Shared.Stacks;

// CMU14 class
public abstract partial class SharedStackSystem
{
    // raised on both stacks so a stack can refuse donors that don't carry its marker either
    private bool CMUMergeBlocked(EntityUid donor, EntityUid recipient)
    {
        var ev = new CMUStackMergeAttemptEvent(donor, recipient);
        RaiseLocalEvent(donor, ref ev);
        if (!ev.Cancelled)
            RaiseLocalEvent(recipient, ref ev);

        return ev.Cancelled;
    }
}

// CMU14 event
[ByRefEvent]
public record struct CMUStackMergeAttemptEvent(EntityUid Donor, EntityUid Recipient, bool Cancelled = false);
