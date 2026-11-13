using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Yautja.BadBlood;

/// <summary>
/// The "return to the lobby" answer of the Blood Reserve dialog. The dialog sends it back to the server.
/// </summary>
[Serializable, NetSerializable]
public sealed record BadBloodReserveLobbyEvent(NetUserId UserId);
