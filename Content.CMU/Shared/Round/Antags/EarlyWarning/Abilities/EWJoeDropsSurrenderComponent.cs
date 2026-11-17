namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// On a hostile Early Warning Joe action: a Joe that uses it while surrendered lowers its hands, so it cannot
/// fake a surrender and keep grabbing, drowning or striking with its hands up.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeDropsSurrenderComponent : Component;
