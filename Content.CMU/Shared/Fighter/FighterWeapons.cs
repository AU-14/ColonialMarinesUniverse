using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Fighter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FighterWeaponsComponent : Component
{
    public const int ExternalPoints = 6;
    public const int GauSlot = ExternalPoints;
    [DataField, AutoNetworkedField] public List<EntityUid> Hardpoints = [];
    [DataField, AutoNetworkedField] public List<FighterWeaponStatus> Loadout = [];
    [DataField, AutoNetworkedField] public List<FighterTarget> Targets = [];
    [DataField, AutoNetworkedField] public float RunRange = 65;
    [DataField, AutoNetworkedField] public float NearbyRange = 18;
    [DataField, AutoNetworkedField] public float RunHalfAngle = 35;
    [DataField] public TimeSpan MissileDelay = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan RocketDelay = TimeSpan.FromSeconds(2);
    [DataField] public TimeSpan GauDelay = TimeSpan.FromSeconds(5);
    [DataField] public TimeSpan GauTravelTime = TimeSpan.FromSeconds(.15);
    [DataField] public TimeSpan RocketTravelTime = TimeSpan.FromSeconds(.65);
    [DataField, AutoNetworkedField] public TimeSpan LaserLifetime = TimeSpan.FromSeconds(10);
    [DataField, AutoNetworkedField] public TimeSpan LaserLockDuration = TimeSpan.FromSeconds(2);
    [DataField, AutoNetworkedField] public string? Faction;
    public TimeSpan NextRefresh;
}

/// <summary>A serviceable pylon, or the ammunition feed of the fixed internal cannon.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterHardpointComponent : Component
{
    public const string EquipmentContainer = "fighter-equipment";
    public const string AmmoContainer = "fighter-ammo";
    [DataField, AutoNetworkedField] public EntityUid? Aircraft;
    [DataField, AutoNetworkedField] public int Index;
    [DataField, AutoNetworkedField] public bool Internal;
    [DataField, AutoNetworkedField] public EntityUid? Equipment;
    [DataField, AutoNetworkedField] public EntityUid? Ammo;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ReadyAt;
}

[Serializable, NetSerializable]
public enum FighterWeaponKind : byte { Empty, Missile, Rockets, Gau }

[Serializable, NetSerializable]
public enum FighterFireStatus : byte { Ready, NoWeapon, Empty, PilotOnly, NoTarget, OutOfRange, Clouds, Protected, NeedRun, OffCourse, Cooldown, MissilesOnly, Locking, TargetExpiring, Retreat, Grounded, OutsideAO }

[Serializable, NetSerializable]
public sealed record FighterWeaponStatus(int Slot, FighterWeaponKind Kind, string Name, int Rounds, int PerShot, TimeSpan ReadyAt);

[Serializable, NetSerializable]
public sealed record FighterTarget(NetEntity Id, string Name, Vector2 Position, bool CanStrike, bool Laser = false,
    TimeSpan ExpiresAt = default, bool Incoming = false);

/// <summary>A visible world designation, owned by one seat and valid only for its aircraft.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class FighterLaserComponent : Component
{
    [DataField, AutoNetworkedField] public EntityUid Aircraft;
    [DataField, AutoNetworkedField] public EntityUid Seat;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
    [DataField, AutoNetworkedField] public bool Incoming;
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan IncomingAt;
    [DataField] public SoundSpecifier IncomingSound = new SoundPathSpecifier("/Audio/CMU14/Fighter/manpad-incoming.wav");
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[Serializable, NetSerializable]
public sealed class FighterSelectWeaponEvent(int slot) : EntityEventArgs
{
    public int Slot = slot;
}

[Serializable, NetSerializable]
public sealed class FighterSelectTargetEvent(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

/// <summary>Shared release cues. The server also revalidates the physical ammo and live designation at release.</summary>
public static class FighterWeapons
{
    public static bool CanOperate(bool pilot, FighterWeaponKind kind) =>
        kind == FighterWeaponKind.Missile || pilot && kind is FighterWeaponKind.Rockets or FighterWeaponKind.Gau;

    public static FighterFireStatus Status(FighterAircraftComponent a, FighterWeaponsComponent weapons,
        FighterSeatComponent seat, FighterWeaponStatus? weapon, FighterTarget? flare, TimeSpan now)
    {
        if (a.ForcedRetreat) return FighterFireStatus.Retreat;
        if (a.GroundState != FighterGroundState.Airborne) return FighterFireStatus.Grounded;
        if (weapon == null || weapon.Kind == FighterWeaponKind.Empty) return FighterFireStatus.NoWeapon;
        if (!CanOperate(seat.Pilot, weapon.Kind)) return FighterFireStatus.PilotOnly;
        if (weapon.Rounds < weapon.PerShot || weapon.PerShot <= 0) return FighterFireStatus.Empty;
        if (flare == null || flare.Laser && now >= flare.ExpiresAt) return FighterFireStatus.NoTarget;
        if (flare.Laser && weapon.Kind != FighterWeaponKind.Missile) return FighterFireStatus.MissilesOnly;
        if (!flare.CanStrike) return FighterFireStatus.Protected;
        if (!FighterFlight.InAttackRun(a)) return FighterFireStatus.OutsideAO;
        if (!FighterFlight.DesignationInRange(a, flare.Position)) return FighterFireStatus.OutOfRange;
        if (FighterOptics.CloudsBlock(a, flare.Position, now)) return FighterFireStatus.Clouds;
        if (weapon.Kind != FighterWeaponKind.Missile)
        {
            var delta = flare.Position - a.Position;
            var distance = delta.Length();
            if (distance > weapons.RunRange) return FighterFireStatus.OutOfRange;
            if (distance > weapons.NearbyRange && Vector2.Dot(delta, FighterFlight.Forward(a.Heading)) <
                distance * MathF.Cos(weapons.RunHalfAngle * MathF.PI / 180)) return FighterFireStatus.OffCourse;
        }
        if (now < weapon.ReadyAt) return FighterFireStatus.Cooldown;
        if (flare.Laser)
        {
            if (seat.LaserLockTarget == flare.Id && seat.LaserLockSlot == weapon.Slot)
                return now < seat.LaserLockReadyAt ? FighterFireStatus.Locking : FighterFireStatus.Ready;
            if (now + weapons.LaserLockDuration >= flare.ExpiresAt) return FighterFireStatus.TargetExpiring;
        }
        return FighterFireStatus.Ready;
    }
}
