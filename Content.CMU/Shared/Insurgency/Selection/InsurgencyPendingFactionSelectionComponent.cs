using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Insurgency.Selection;

/// <summary>
///     Marks the CLF cell leader while the round's faction has not been chosen yet. The client shows a
///     small "Choose faction" button in the game viewport for the local player who carries this, so a
///     leader who closes the selection popup can reopen it. Removed once a faction is applied.
///
///     Networked with no fields: its mere presence is the signal the client watches for.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class InsurgencyPendingFactionSelectionComponent : Component
{
}
