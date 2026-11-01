namespace Content.Server.CMU14.ColonyEconomy;

/// <summary>
///     A character whose job pays them into their bank account, so their character notes always list their
///     account number and PIN, whatever the game mode.
/// </summary>
[RegisterComponent]
public sealed partial class CMUBankAccountHolderComponent : Component;
