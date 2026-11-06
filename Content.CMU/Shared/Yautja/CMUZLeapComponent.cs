using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Yautja;

/// <summary>
/// CMU14: the yautja Z-level leap on a mob that is not a yautja (first user: the Early Warning Joe at
/// level 4). <c>YautjaAbilitySystem</c> accepts it beside <see cref="YautjaComponent"/>, with the same
/// defaults; the action (<c>YautjaLeapActionEvent</c>) is granted by whoever adds this component.
/// It also lets the mob aim at the level above or below without a gun, the way yautja and xenos can.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUZLeapComponent : Component
{
    /// <summary>Shared fall and aim code reads it on the leaper's own client; nobody else needs it.</summary>
    public override bool SendOnlyToOwner => true;

    [DataField]
    public float ThrowSpeed = 5f;

    [DataField]
    public float MaxRange = 7f;

    [DataField]
    public TimeSpan Windup = TimeSpan.FromSeconds(0.6);

    [DataField]
    public TimeSpan UpWindup = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DownWindup = TimeSpan.FromSeconds(0.6);

    [DataField]
    public EntProtoId WarningPrototype = "CMUYautjaLeapWarning";

    [DataField]
    public SoundSpecifier Sound = new SoundCollectionSpecifier("CMUYautjaLeap");

    /// <summary>A controlled descent: dropping a level deals no fall damage, as for a yautja.</summary>
    [DataField]
    public bool IgnoreFallDamage = true;
}
