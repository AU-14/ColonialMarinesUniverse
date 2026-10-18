namespace Content.Shared.Stacks;

/// <summary>
///     Raised on the original stack entity when it is split to create another.
/// </summary>
/// <param name="NewId">The entity id of the new stack.</param>
[ByRefEvent]
public readonly record struct StackSplitEvent(EntityUid NewId, EntityUid? user);

/// <summary>
///     Raised on the recipient stack entity when it is merged with another stack.
///     This is raised before the stack counts have been updated.
/// </summary>
/// <param name="Donor">The entity id of the donor stack. May get deleted after event is resolved from merging.</param>
/// <param name="Recipient">The entity id of the recipient stack.</param>
/// <param name="Amount">The amount in the stack transferred from the donor to the recipient</param>
[ByRefEvent]
public readonly record struct StackMergeEvent(EntityUid Donor, EntityUid Recipient, int Amount);

/// <summary>
///     RUMC: raised on the original stack when <c>GetOne</c> takes one item off it into a new entity (the other way a stack is split), so that the new item
///     may be given what the original has (the handmade cartridges).
/// </summary>
/// <param name="NewId">The entity id of the new item.</param>
[ByRefEvent]
public readonly record struct StackOneTakenEvent(EntityUid NewId);

/// <summary>
///     RUMC: raised on the recipient before two stacks of the same type are merged; a system may refuse it (the handmade cartridges of the gunsmith's benches
///     stack only with the ones that are made the same).
/// </summary>
[ByRefEvent]
public record struct StackMergeAttemptEvent(EntityUid Donor, EntityUid Recipient, bool Cancelled = false);
