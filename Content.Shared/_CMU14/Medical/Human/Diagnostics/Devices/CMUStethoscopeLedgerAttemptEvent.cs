using Content.Shared._RMC14.Medical.Scanner;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

[ByRefEvent]
public record struct CMUStethoscopeLedgerAttemptEvent(
    Entity<RMCStethoscopeComponent> Stethoscope,
    EntityUid User,
    EntityUid Target)
{
    public bool Handled;
}
