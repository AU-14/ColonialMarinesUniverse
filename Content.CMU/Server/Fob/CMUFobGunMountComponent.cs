using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Fob;

/// <summary>Persistent mapper-approved slot. Its initial magazine grant survives destruction of the weapon.</summary>
[RegisterComponent, Access(typeof(CMUFobDefenseSystem))]
public sealed partial class CMUFobGunMountComponent : Component
{
    [ViewVariables] public EntityUid? Network;
    [ViewVariables] public EntityUid? Gun;
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public Angle Facing;
    [ViewVariables] public bool MagazineIssued;
    [ViewVariables] public bool RemovedByOperator;
    [ViewVariables] public EntProtoId WeaponPrototype = "CMUFobSupportGun";
}

/// <summary>The pair cannot be replaced or expanded during this network's lifetime.</summary>
[RegisterComponent, Access(typeof(CMUFobDefenseSystem))]
public sealed partial class CMUFobDefenseNetworkComponent : Component
{
    [ViewVariables] public List<EntityUid> Mounts = new();
    [ViewVariables] public int WarnedActivation;
}
