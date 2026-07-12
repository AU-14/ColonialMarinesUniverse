// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// If true, ghosts see a command link next to supported chat messages that follows the sender.
    /// </summary>
    public static readonly CVarDef<bool> ChatGhostFollowButton =
        CVarDef.Create("chat.ghost_follow_button", true, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);
}
