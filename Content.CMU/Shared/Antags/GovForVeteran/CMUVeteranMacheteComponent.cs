using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Antags.GovForVeteran;

/// <summary>
///     CMU14: the A.W. Veteran's machete.
///     It flies faster and further than a normal throw and knocks down whatever it hits.
///     Ownership is enforced on the item itself (WhitelistPickupBy): only the veteran can take it into a hand,
///     though anyone can still drag it off the deck.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVeteranMacheteComponent : Component
{
    /// <summary>
    ///     How long a mob hit by the thrown machete is knocked down for.
    /// </summary>
    [DataField]
    public TimeSpan Knockdown = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Multiplier applied to the thrower's base throw speed.
    /// </summary>
    [DataField]
    public float ThrowSpeedMultiplier = 1.5f;
}
