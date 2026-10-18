using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fax;

/// <summary>
///     A fax machine packed up to carry on your back. Use it in hand to set it up as a working fax machine where
///     you're standing. It can't receive faxes while it's packed up.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPortableFaxComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Deployed;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4.5);

    /// <summary>The fax machine's name, kept while it's packed up.</summary>
    [DataField]
    public string? FaxName;
}

/// <summary>
///     A set-up portable fax machine, which can be packed back up with its verb once there's nothing in it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUPortableFaxDeployedComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4.5);
}

[Serializable, NetSerializable]
public sealed partial class CMUPortableFaxDeployDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUPortableFaxPackDoAfterEvent : SimpleDoAfterEvent;
