using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Administration;

/// <summary>
/// Placed on a mind role entity to give it an icon in admin tooling (player overlay and Players tab).
/// The server reads it while building <see cref="Content.Shared.Administration.PlayerInfo"/>, so it is not networked.
/// </summary>
[RegisterComponent]
public sealed partial class CMUAdminRoleIconComponent : Component
{
    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;
}
