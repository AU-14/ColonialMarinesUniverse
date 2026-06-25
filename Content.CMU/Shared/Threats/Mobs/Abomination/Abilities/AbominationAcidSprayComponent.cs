using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination.Abilities;

/// <summary>
///     Boiler-style acid spray. Launches a fan of acid projectiles in a cone
///     toward the target so the grunt can lay down area denial.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationAcidSprayComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Projectile = "XenoSpitProjectile";

    /// <summary>How far each projectile travels before despawn.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 7f;

    /// <summary>Number of projectiles in the fan.</summary>
    [DataField, AutoNetworkedField]
    public int Shots = 7;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("XenoSpitAcid");

    [DataField, AutoNetworkedField]
    public float Speed = 14f;

    /// <summary>Spread angle of the cone, in degrees.</summary>
    [DataField, AutoNetworkedField]
    public float SpreadDegrees = 50f;
}

public sealed partial class AbominationAcidSprayActionEvent : WorldTargetActionEvent;
