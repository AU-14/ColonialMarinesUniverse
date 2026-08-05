namespace Content.Shared.Roles;

/// <summary>
/// Marker component. Attach to a job (directly via <c>roundComponents</c>/<c>roundForceComponents</c>,
/// or on a shared abstract job so every descendant inherits it - see <c>AU14JobMilitaryBase</c>) to
/// require a UCMJ/SOP-compliant "regulation" appearance for that job. On spawn, the mob's hairstyle,
/// hair color, facial hair, and facial hair color are taken from the player's Regulation Appearance
/// character-editor selections instead of their normal civilian selections. The player's normal
/// profile is left untouched - only the spawned mob's appearance is overridden.
/// </summary>
[RegisterComponent]
public sealed partial class RegulationAppearanceComponent : Component;
