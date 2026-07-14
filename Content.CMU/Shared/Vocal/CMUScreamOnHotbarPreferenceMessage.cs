using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Vocal;

/// <summary>
///     Sent by the client the moment it changes its "pin scream to hotbar" preference, so the server can
///     apply the change to the client's currently attached entity immediately instead of waiting for
///     their next spawn. See <see cref="Content.Shared.CCVar.CCVars.CMUScreamOnHotbarEnabled"/>.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUScreamOnHotbarPreferenceMessage(bool enabled) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}
