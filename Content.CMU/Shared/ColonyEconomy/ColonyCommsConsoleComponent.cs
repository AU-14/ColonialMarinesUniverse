using Robust.Shared.GameStates;
using Robust.Shared.Localization;

namespace Content.Shared.CMU14;

[RegisterComponent]
public sealed partial class ColonyCommsConsoleComponent : Component
{
    public bool SirenActive = false;

    /// <summary>
    ///     Locale id of the sender shown on the announcement. Null keeps the default colony sender.
    /// </summary>
    [DataField]
    public LocId? AnnouncementTitle;

    /// <summary>
    ///     Minimum time between announcements from this console. Zero means no limit.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.Zero;

    /// <summary>
    ///     Longest announcement the console will send. Zero means no limit.
    /// </summary>
    [DataField]
    public int MaxLength;

    [ViewVariables]
    public TimeSpan NextSendAt = TimeSpan.Zero;
}
