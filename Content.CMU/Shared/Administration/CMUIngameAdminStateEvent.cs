using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Administration;

/// <summary>
/// Tells an admin's client whether it is in in-game admin mode: inactive as an admin, but still handling AHelps.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUIngameAdminStateEvent : EntityEventArgs
{
    public readonly bool IngameAdmin;

    public CMUIngameAdminStateEvent(bool ingameAdmin)
    {
        IngameAdmin = ingameAdmin;
    }
}
