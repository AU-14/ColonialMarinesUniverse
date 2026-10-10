using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.Rider;

public sealed partial class RiderLatchActionEvent : EntityTargetActionEvent;

public sealed partial class RiderPunishActionEvent : InstantActionEvent;

public sealed partial class RiderSeizeActionEvent : InstantActionEvent;

public sealed partial class RiderExitActionEvent : InstantActionEvent;

public sealed partial class HostResistActionEvent : InstantActionEvent;

public sealed partial class RiderSurgeActionEvent : InstantActionEvent;

public sealed partial class RiderCoaxActionEvent : InstantActionEvent;

public sealed partial class RiderSustainActionEvent : InstantActionEvent;

public sealed partial class RiderMuteActionEvent : InstantActionEvent;

public sealed partial class RiderManifestActionEvent : InstantActionEvent;

public sealed partial class RiderWithdrawActionEvent : InstantActionEvent;

/// <summary>
/// Answer to the rider's latch offer, raised on the host entity by its dialog.
/// </summary>
[Serializable, NetSerializable]
public sealed record RiderOfferEvent(NetEntity Rider, bool Accept);

[Serializable, NetSerializable]
public sealed partial class RiderLatchDoAfterEvent : SimpleDoAfterEvent;
