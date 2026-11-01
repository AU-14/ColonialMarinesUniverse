using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Overwatch;

/// <summary>
/// The camera feeds an overwatch operator has up beside the console, at most <see cref="MaxFeeds"/>.
/// Each feed is a small eye entity riding on the watched camera that the operator is subscribed to.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUOverwatchFeedsComponent : Component
{
    public const int MaxFeeds = 4;

    public override bool SendOnlyToOwner => true;

    /// <summary>
    /// The console the feeds were opened from; they close with its window.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Console;

    [DataField, AutoNetworkedField]
    public List<CMUOverwatchFeed> Feeds = new();
}

[DataDefinition, Serializable, NetSerializable]
public partial record struct CMUOverwatchFeed
{
    /// <summary>
    /// The marine or tripod being watched.
    /// </summary>
    [DataField]
    public NetEntity Target;

    /// <summary>
    /// The eye entity the feed renders from.
    /// </summary>
    [DataField]
    public NetEntity Eye;

    [DataField]
    public string Name;

    public CMUOverwatchFeed(NetEntity target, NetEntity eye, string name)
    {
        Target = target;
        Eye = eye;
        Name = name;
    }
}

/// <summary>
/// Opens a feed on the target, or closes it if it is already up.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUOverwatchFeedToggleBuiMsg(NetEntity target) : BoundUserInterfaceMessage
{
    public readonly NetEntity Target = target;
}

/// <summary>
/// Closes every feed.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUOverwatchFeedClearBuiMsg : BoundUserInterfaceMessage;
