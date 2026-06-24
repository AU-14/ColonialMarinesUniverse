namespace Content.Shared.Stacks;

/// <summary>
///     Raised on the recipient stack after another stack has been merged into it.
/// </summary>
/// <param name="Donor">The stack entity that lost items.</param>
/// <param name="Recipient">The stack entity that gained items.</param>
/// <param name="Transferred">The amount transferred from donor to recipient.</param>
[ByRefEvent]
public readonly record struct StackMergedEvent(EntityUid Donor, EntityUid Recipient, int Transferred);
