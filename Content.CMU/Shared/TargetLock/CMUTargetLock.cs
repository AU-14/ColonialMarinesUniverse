using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.TargetLock;

/// <summary>
///     A scanner that locks on to a person: hold it on someone and after <see cref="LockTime"/> it reads them, showing
///     their picture, name, position, a health estimate, their criminal and medical records, and warning in chat if
///     they are carrying a weapon.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUTargetLockComponent : Component
{
    [DataField]
    public TimeSpan LockTime = TimeSpan.FromSeconds(3);

    /// <summary>The action the wearer of the helmet gets while this visor is on it.</summary>
    [DataField]
    public EntProtoId ActionId = "ActionCMUTargetLock";

    [DataField]
    public EntityUid? ActionEntity;

    /// <summary>How far away something can be and still be locked on to.</summary>
    [DataField]
    public float Range = 7f;

    /// <summary>A melee item only counts as a weapon if it does at least this much damage.</summary>
    [DataField]
    public float MeleeWeaponDamage = 10f;

    /// <summary>Shots from the wearer hit a locked target this many times as hard.</summary>
    [DataField]
    public float AimBuff = 1.5f;

    /// <summary>How long the target stays locked for the aimed-shot buff.</summary>
    [DataField]
    public TimeSpan AimBuffTime = TimeSpan.FromSeconds(20);

    /// <summary>The ammo warning shows when the gun in hand has this fraction of its magazine or less.</summary>
    [DataField]
    public float LowAmmoFraction = 0.25f;

    /// <summary>The medical warning shows when the wearer has this fraction of their health or less.</summary>
    [DataField]
    public float LowHealthFraction = 0.4f;
}

/// <summary>On someone who has been locked on to: the one who locked them gets a damage bonus with shots, for a while.</summary>
[RegisterComponent]
public sealed partial class CMUTargetLockedComponent : Component
{
    public EntityUid Owner;
    public TimeSpan Until;
    public float Multiplier = 1.5f;
}

[Serializable, NetSerializable]
public sealed partial class CMUTargetLockDoAfterEvent : SimpleDoAfterEvent;

/// <summary>How hurt someone looks. Only an estimate, so it is never exact.</summary>
[Serializable, NetSerializable]
public enum CMUTargetLockHealth : byte
{
    Healthy,
    Light,
    Wounded,
    Critical,
    Dead,
}

/// <summary>Everything the scanner read off someone, sent to the person holding it.</summary>
[Serializable, NetSerializable]
public sealed class CMUTargetLockReportEvent(
    NetEntity target,
    string name,
    string position,
    CMUTargetLockHealth health,
    int healthPercent,
    string criminalRecord,
    string medicalRecord,
    List<string> weapons,
    string advice) : EntityEventArgs
{
    /// <summary>Where to shoot them, given what they are wearing.</summary>
    public readonly string Advice = advice;

    public readonly NetEntity Target = target;
    public readonly string Name = name;
    public readonly string Position = position;
    public readonly CMUTargetLockHealth Health = health;

    /// <summary>The estimate rounded to the nearest ten, 100 being unhurt.</summary>
    public readonly int HealthPercent = healthPercent;

    public readonly string CriminalRecord = criminalRecord;
    public readonly string MedicalRecord = medicalRecord;
    public readonly List<string> Weapons = weapons;
}

/// <summary>The wearer picked someone to lock on to.</summary>
public sealed partial class CMUTargetLockActionEvent : EntityTargetActionEvent;
