using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Threats.Mobs.Abomination.Abilities;

/// <summary>
///     Acid spit. Spawns a projectile at the abomination and launches it at the
///     targeted world tile. No plasma/resource cost — gated by the action useDelay.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AbominationSpitComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId Projectile = "XenoSpitProjectile";

    /// <summary>Max distance in tiles before the projectile auto-despawns.</summary>
    [DataField, AutoNetworkedField]
    public float Range = 12f;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? Sound = new SoundCollectionSpecifier("XenoSpitAcid");

    [DataField, AutoNetworkedField]
    public float Speed = 18f;
}

public sealed partial class AbominationSpitActionEvent : WorldTargetActionEvent;
