using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared.CMU14.Atmos;

/// <summary>
/// Rad-shielded clothing: reduces the radiation dose its wearer receives.
/// On suits this pairs with <see cref="CMURadCoverageComponent"/>, which gives
/// part of the suit's reduction back while the head is not itself shielded.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMURadProtectionComponent : Component
{
    /// <summary>
    /// Percentage to reduce the received dose by, subtracted not multiplicative.
    /// 0.35 means 35% less radiation.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public float Reduction;

    /// <summary>
    /// LocId for message that will be shown on detailed examine.
    /// </summary>
    [DataField]
    public LocId ExamineMessage = "rad-protection-reduction-value";
}
