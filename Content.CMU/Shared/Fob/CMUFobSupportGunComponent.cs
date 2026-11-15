using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared.Tag;

namespace Content.Shared.CMU14.Fob;

/// <summary>Dedicated support weapon. Runtime fields are owned by the server's FOB defense system.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentPause]
public sealed partial class CMUFobSupportGunComponent : Component
{
    [DataField] public float Range = 6;
    [DataField] public Angle HalfArc = Angle.FromDegrees(60);
    [DataField] public TimeSpan AcquisitionDelay = TimeSpan.FromSeconds(1.5);
    [DataField] public TimeSpan ReloadDelay = TimeSpan.FromSeconds(7);
    [DataField] public bool RequiresSensor;
    [DataField] public EntProtoId MagazinePrototype = "CMUFob30mmMagazine";
    [DataField] public ProtoId<TagPrototype> MagazineTag = "CMUFob30mmMagazine";
    [ViewVariables] public EntityUid? WarningMarker;
    [ViewVariables] public EntityUid? Mount;
    [ViewVariables] public EntityUid? Target;
    [ViewVariables] public bool RequiresRestart = true;
    [ViewVariables] public bool RestoreCollision;
    [ViewVariables, AutoPausedField] public TimeSpan ReadyAt;
    [ViewVariables] public DoAfterId? Reload;
}

[Serializable, NetSerializable]
public enum CMUFobGunVisuals : byte { Warning, Installed }

/// <summary>Denied unless the server is executing a validated autonomous shot.</summary>
[ByRefEvent]
public record struct CMUFobFireAuthorizationEvent(EntityUid User, bool Authorized = false);

[Serializable, NetSerializable]
public sealed partial class CMUFobReloadDoAfterEvent : SimpleDoAfterEvent;
