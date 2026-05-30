using Robust.Shared.GameStates;

namespace Content.Shared.AU14.WithdrawConsole;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WithdrawConsoleComponent : Component
{
    /// <summary>The faction this console belongs to. Must be "opfor" or "govfor".</summary>
    [DataField(required: true), AutoNetworkedField]
    public string Faction = string.Empty;

    /// <summary>NetEntity IDs of original ID card owners who have authorized. Max 2.</summary>
    [DataField, AutoNetworkedField]
    public List<NetEntity> SwipedOwners = new();

    /// <summary>True once two valid faction IDs have been swiped.</summary>
    [DataField, AutoNetworkedField]
    public bool IsUnlocked;

    /// <summary>True while the withdrawal countdown is running.</summary>
    [DataField, AutoNetworkedField]
    public bool WithdrawActive;

    /// <summary>Server game time when withdrawal was initiated.</summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? WithdrawStartTime;

    /// <summary>True if this faction's operator has toggled stalemate.</summary>
    [DataField, AutoNetworkedField]
    public bool StalemateToggled;

    /// <summary>15-minute announcement has been sent.</summary>
    [DataField, AutoNetworkedField]
    public bool AnnouncementSent;

    /// <summary>Hijack lock has been applied at the 10-minute mark.</summary>
    [DataField, AutoNetworkedField]
    public bool HijackLockApplied;

    /// <summary>Cycle-down block has been applied at the 5-minute mark.</summary>
    [DataField, AutoNetworkedField]
    public bool DropdownLockApplied;

    /// <summary>Round end has been triggered — prevents double-firing.</summary>
    [DataField, AutoNetworkedField]
    public bool RoundEndTriggered;

    /// <summary>How many IDs must be swiped before the console unlocks. Default 2 for military factions, 1 for colony.</summary>
    [DataField, AutoNetworkedField]
    public int RequiredIdCount = 2;

    /// <summary>When true, uses AccessReader to check authorization instead of faction-matching ID cards.</summary>
    [DataField, AutoNetworkedField]
    public bool UseAccessCheck;

    /// <summary>
    /// How long after withdrawal starts the cancel option remains available.
    /// If null, defaults to half the total withdraw duration.
    /// </summary>
    [DataField]
    public TimeSpan? CancelWindowOverride;

    /// <summary>
    /// How long after withdrawal starts the mid-withdrawal announcement fires.
    /// If null, defaults to half the total withdraw duration.
    /// </summary>
    [DataField]
    public TimeSpan? AnnouncementElapsedOverride;

    /// <summary>
    /// How much remaining time triggers the hijack lock.
    /// If null, defaults to 1/3 of the total withdraw duration.
    /// </summary>
    [DataField]
    public TimeSpan? HijackLockRemainingOverride;

    /// <summary>
    /// How much remaining time triggers the cycle-down (dropdown) lock.
    /// If null, defaults to 1/6 of the total withdraw duration.
    /// </summary>
    [DataField]
    public TimeSpan? DropdownLockRemainingOverride;

    /// <summary>Total time from withdrawal initiation to round end.</summary>
    [DataField]
    public TimeSpan WithdrawDuration = TimeSpan.FromMinutes(30);
}
