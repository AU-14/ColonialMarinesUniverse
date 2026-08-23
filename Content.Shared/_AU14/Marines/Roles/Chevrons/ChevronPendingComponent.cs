using Content.Shared.Preferences;

namespace Content.Shared._AU14.Marines.Roles.Chevrons;

/// <summary>
/// Temporarily stored on a mob that has a player but no jumpsuit yet.
/// Consumed and removed by ChevronSystem when the first valid jumpsuit is equipped.
/// </summary>
[RegisterComponent]
public sealed partial class ChevronPendingComponent : Component
{
    public string? JobId;
    public HumanoidCharacterProfile? Profile;
}