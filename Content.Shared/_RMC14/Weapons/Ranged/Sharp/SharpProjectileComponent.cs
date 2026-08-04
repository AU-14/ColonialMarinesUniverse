using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Weapons.Ranged.Sharp;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpProjectileComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public SharpProjectileKind Kind;

    [DataField, AutoNetworkedField]
    public EntProtoId Mine = "RMCSharpExplosiveDartMine";

    [DataField, AutoNetworkedField]
    public EntProtoId DirectEffect = "RMCSharpExplosiveDirectEffect";

    [DataField, AutoNetworkedField]
    public TimeSpan DirectDelay = TimeSpan.FromSeconds(5);

    public bool Resolved;
}
