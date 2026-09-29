using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Vehicle;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TankCookOffComponent : Component
{
    [DataField, AutoNetworkedField] public bool Enabled = true;
    [DataField] public float CriticalChance = .15f;
    [DataField, AutoNetworkedField] public float RuptureDelay = 2.4f;
    [DataField] public float EjectionDistance = 4;
    [DataField] public float EjectionSpeed = 8;
    [DataField, AutoNetworkedField] public SpriteSpecifier TurretSprite = new SpriteSpecifier.Rsi(
        new ResPath("_RMC14/Structures/Vehicles/tank.rsi"), "tank_turret_0");
    [DataField] public SoundSpecifier RuptureSound = new SoundPathSpecifier("/Audio/_RMC14/Explosion/bigboom3.ogg");
}

/// <summary>Only designated anti-armour rockets and tank shells may roll a critical cook-off.</summary>
[RegisterComponent]
public sealed partial class TankCookOffProjectileComponent : Component;

/// <summary>Irreversible ammunition ignition. Persists on the wreck and prevents repairs.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class ActiveTankCookOffComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField, AutoNetworkedField] public bool Ruptured;
}

[ByRefEvent]
public readonly record struct TankCookOffRupturedEvent;
