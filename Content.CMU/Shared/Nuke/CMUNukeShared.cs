using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Nuke;

[RegisterComponent]
public sealed partial class CMUNukeComponent : Component
{
    [DataField]
    public TimeSpan DetonationDelay = TimeSpan.FromMinutes(5);
    public TimeSpan? DecryptionEndsAt;
    public TimeSpan? NextProgressAnnouncementAt;
    public TimeSpan? DetonationAt;
    [DataField]
    public bool Decrypted;
    public bool Exploded;
    [DataField]
    public bool SafetyEnabled = true;
    public bool CommandLockout;
    public bool BeingUsed;
    public bool DetonationSoundPlayed;
    public CMUNukeWarningStatus WarningStatus;
    public int LastDecodeSeconds = -1;
    public int LastDetonationSeconds = -1;
}

[Serializable, NetSerializable]
public sealed class CMUNukeUiState : BoundUserInterfaceState
{
    public CMUNukeStatus Status;
    public int DecodeTime;
    public int CooldownTime;
    public bool IsAnchored;
    public bool IsPublic;
    public bool AllowArm;
    public bool CanStartDecryption;
    public bool CanAnchor;
    public bool SafetyEnabled;
    public bool CommandLockout;
    public bool BeingUsed;
}

[Serializable, NetSerializable]
public sealed partial class CMUNukeControlDoAfterEvent : SimpleDoAfterEvent
{
    public CMUNukeControlAction Action;

    public CMUNukeControlDoAfterEvent(CMUNukeControlAction action)
    {
        Action = action;
    }
}

[Serializable, NetSerializable]
public sealed partial class CMUNukeXenoSabotageDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class CMUNukeAnchorMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUNukeStartDecryptionMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUNukeActivateMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUNukeToggleSafetyMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CMUNukeToggleCommandLockoutMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public enum CMUNukeControlAction : byte
{
    ToggleSafety,
    ToggleAnchor,
    StartDecryption,
    StopDecryption,
    Activate,
    Deactivate,
}

[Serializable, NetSerializable]
public enum CMUNukeUiKey
{
    Key,
}

public enum CMUNukeStatus : byte
{
    AwaitDecode,
    Decrypting,
    Decrypted,
    Cooldown,
    DetonationCountdown,
}

[Serializable, NetSerializable]
public enum CMUNukeVisuals : byte
{
    Deployed,
    Decrypting,
    Activated,
}

[Serializable, NetSerializable]
public enum CMUNukeWarningStatus : byte
{
    None,
    HalfwayWarningSent,
    OneMinuteWarningSent,
    TenSecondWarningSent,
    AllSent,
}
