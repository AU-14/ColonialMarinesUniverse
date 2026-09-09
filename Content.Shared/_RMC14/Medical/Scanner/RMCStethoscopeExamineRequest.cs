namespace Content.Shared._RMC14.Medical.Scanner;

/// <summary>
/// Server-side extension of the item's real interaction and examine-verb routes.
/// Handling includes rejected or cancelled examinations: it must never fall through
/// to a second diagnostic implementation.
/// </summary>
[ByRefEvent]
public record struct RMCStethoscopeExamineRequest(
    EntityUid User,
    EntityUid Patient,
    Entity<RMCStethoscopeComponent> Tool,
    bool FromVerb,
    bool Handled = false);
