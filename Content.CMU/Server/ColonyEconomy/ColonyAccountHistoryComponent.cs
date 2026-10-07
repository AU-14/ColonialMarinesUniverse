using Content.Shared.CMU14.ColonyEconomy;

namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
///     Recent money movements on an ID card's account, read at the ATM. Server-only like the PIN,
///     so no client ever receives another card's history.
/// </summary>
[RegisterComponent, Access(typeof(ColonyBankSystem))]
public sealed partial class ColonyAccountHistoryComponent : Component
{
    /// <summary>Oldest first, capped at <see cref="ColonyBankSystem.MaxHistoryEntries"/>.</summary>
    [ViewVariables]
    public List<ColonyAccountHistoryEntry> Entries = new();
}
