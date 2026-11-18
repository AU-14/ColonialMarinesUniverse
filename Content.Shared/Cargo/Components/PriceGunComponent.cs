using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Cargo.Components;

/// <summary>
///     This is used for the price gun, which calculates the price of any object it appraises.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PriceGunComponent : Component
{
    /// <summary>
    /// If true, the tool displays ASRS sale eligibility and value.
    /// </summary>
    [DataField]
    public bool OnlyASRSSellable; // CMU14

    /// <summary>
    /// The sound that plays when the price gun appraises an object.
    /// </summary>
    [DataField]
    public SoundSpecifier AppraisalSound  = new SoundPathSpecifier("/Audio/Items/appraiser.ogg");
}
