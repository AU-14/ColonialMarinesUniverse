namespace Content.Shared.CMU14.Round.Antags.EarlyWarning.Abilities;

/// <summary>
/// The Grand Machine's Hand (phase E5, balance A-11): the Joe's summoned, serrated machete. Hits on organic
/// targets tear out blood at once and spray it behind them, and seed internal bleeding; synthetic parts ignore it (P6). The item is unremoveable and deleted
/// on drop, toggle, stand-down or the Joe's death.
/// </summary>
[RegisterComponent]
public sealed partial class EWJoeMacheteComponent : Component
{
    /// <summary>The Joe that summoned it.</summary>
    [DataField]
    public EntityUid? Joe;

    [DataField]
    public float BleedRate = 0.3f;

    [DataField]
    public string BleedSource = "ew-joe-machete";

    /// <summary>Blood a serrated hit tears out of an organic at once, besides the wound it leaves (25 of 560).</summary>
    [DataField]
    public float BloodSpill = 25f;

    /// <summary>How far, in tiles, a hit throws the blood on the far side of the victim.</summary>
    [DataField]
    public float SprayRange = 2f;

    /// <summary>Splatters left along the spray.</summary>
    [DataField]
    public int SprayDecals = 2;
}
