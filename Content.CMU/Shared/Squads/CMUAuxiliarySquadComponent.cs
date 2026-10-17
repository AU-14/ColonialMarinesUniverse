namespace Content.Shared.CMU14.Squads;

/// <summary>
/// Marks a support squad, like Auxiliary. Its name is left off members' ID card job titles, its squad
/// leader keeps their own job icon, and its members are never put in fireteams.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAuxiliarySquadComponent : Component;
