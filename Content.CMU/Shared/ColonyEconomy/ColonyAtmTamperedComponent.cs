namespace Content.Shared.CMU14.ColonyEconomy;

/// <summary>
///     Added to an ATM permanently once a siphon rig has been used on it (survives the self-repair).
///     Multitool inspection warns about it and the ATM screen shows the tampered card reader.
/// </summary>
[RegisterComponent]
public sealed partial class ColonyAtmTamperedComponent : Component { }
