using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Ranged.Components;

/// <summary>
///     Responsible for handling recharging a basic entity ammo provider over time.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class RechargeBasicEntityAmmoComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("rechargeCooldown")]
    [AutoNetworkedField]
    public float RechargeCooldown = 1.5f;

    [DataField("rechargeChance")]
    [AutoNetworkedField]
    public float RechargeChance = 1f;

    [DataField("rechargeSound")]
    [AutoNetworkedField]
    public SoundSpecifier? RechargeSound = new SoundPathSpecifier("/Audio/Magic/forcewall.ogg")
    {
        Params = AudioParams.Default.WithVolume(-5f)
    };

    [ViewVariables(VVAccess.ReadWrite),
     DataField("nextCharge", customTypeSerializer:typeof(TimeOffsetSerializer)),
    AutoNetworkedField]
    [AutoPausedField]
    public TimeSpan? NextCharge;

    [DataField, AutoNetworkedField]
    public bool ShowExamineText = true;

    [DataField, AutoNetworkedField]
    public bool StartWithCooldown;

    [DataField, AutoNetworkedField]
    public bool StrictCooldownBoundary;

    [DataField, AutoNetworkedField]
    public bool AdvanceOnFailedRecharge = true;

    [DataField, AutoNetworkedField]
    public bool PreserveCooldownWhenFull;

    [DataField, AutoNetworkedField]
    public bool ResetOverdueCooldown = true;
}
