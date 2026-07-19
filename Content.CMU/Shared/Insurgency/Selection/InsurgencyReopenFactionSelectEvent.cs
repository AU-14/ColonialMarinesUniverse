using Robust.Shared.Serialization;

namespace Content.Shared._AU14.Insurgency.Selection;

/// <summary>
///     Sent by the leader's client when they press the in-viewport "Choose faction" button after
///     closing the selection popup. The server re-validates (still the leader, still no faction applied)
///     before reopening the EUI, so the message is only ever a request, never a trusted action.
/// </summary>
[Serializable, NetSerializable]
public sealed class InsurgencyReopenFactionSelectEvent : EntityEventArgs
{
}
