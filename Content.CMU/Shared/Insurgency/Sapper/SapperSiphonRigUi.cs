using Content.Shared.CMU14.ColonyEconomy;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Insurgency.Sapper;

[Serializable, NetSerializable]
public enum SapperSiphonRigUiKey : byte
{
    Key,
}

/// <summary>
///     The card logins a siphon rig has leaked out of hacked ATMs.
/// </summary>
[Serializable, NetSerializable]
public sealed class SapperSiphonRigBuiState(List<SkimmedAccount> accounts) : BoundUserInterfaceState
{
    public readonly List<SkimmedAccount> Accounts = accounts;
}
