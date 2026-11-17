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
    List<string> weapons) : EntityEventArgs
{
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
