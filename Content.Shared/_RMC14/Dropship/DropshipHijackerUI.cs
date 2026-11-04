using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Dropship;

[Serializable, NetSerializable]
public sealed class DropshipHijackerBuiState(
    // CMU14: Force on Force roles, hijacking, announcements and identification.
    bool canHijack,
    bool canDeclineHijack) : BoundUserInterfaceState
{
    // CMU14: Force on Force roles, hijacking, announcements and identification.
    public bool CanHijack = canHijack;
    public bool CanDeclineHijack = canDeclineHijack;
}

[Serializable, NetSerializable]
// CMU14: Force on Force roles, hijacking, announcements and identification.
public sealed class DropshipHijackerInitiateBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class DropshipHijackerDeclineBuiMsg : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum DropshipHijackerUiKey
{
    Key,
}
