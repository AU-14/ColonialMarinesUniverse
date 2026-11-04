using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BulletBoxSystem))]
public sealed partial class BulletBoxComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Amount = 600;

    [DataField, AutoNetworkedField]
    public int Max = 600;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId BulletType;

    /// <summary>
    /// The cartridge/shell prototype to chamber when this box loads into an empty ready slot.
    /// Null means leave the gun's currently configured ammo prototype untouched, so existing
    /// boxes that don't care about ammo variants keep working unmodified.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? AmmoProto;

    [DataField, AutoNetworkedField]
    public string? UsedIn;

    [DataField, AutoNetworkedField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1.5);
}
