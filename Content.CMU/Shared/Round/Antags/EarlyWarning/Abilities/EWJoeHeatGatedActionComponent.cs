namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// On an Early Warning Joe action that needs a working core (Overclock, Z-Level Leap): while the core is
/// overheated the action is refused at the attempt, before its cooldown starts (ABIL-07).
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeHeatGatedActionComponent : Component;
