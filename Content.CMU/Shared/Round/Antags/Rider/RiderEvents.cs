using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.Rider;

[Serializable, NetSerializable]
public enum RiderVoiceUiKey : byte
{
    Key,
}

public sealed partial class RiderLatchActionEvent : EntityTargetActionEvent;

public sealed partial class RiderVoiceActionEvent : InstantActionEvent;

public sealed partial class RiderPunishActionEvent : InstantActionEvent;

public sealed partial class RiderSeizeActionEvent : InstantActionEvent;

public sealed partial class RiderExitActionEvent : InstantActionEvent;

public sealed partial class HostResistActionEvent : InstantActionEvent;

public sealed partial class RiderSurgeActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class RiderLatchDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Sent by the rider's voice window. SpeakThrough spends grip and uses the
/// host's mouth; a whisper is free and only the host hears it.
/// </summary>
[Serializable, NetSerializable]
public sealed class RiderVoiceMessage : BoundUserInterfaceMessage
{
    public string Text;
    public bool SpeakThrough;

    public RiderVoiceMessage(string text, bool speakThrough)
    {
        Text = text;
        SpeakThrough = speakThrough;
    }
}
