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

    /// <summary>
    /// Whether the detailed "you can see" character examine breakdown is also echoed to the examiner's chat log.
    /// </summary>
    public static readonly CVarDef<bool> ExamineLogInChat =
        CVarDef.Create("cmu.examine_log_in_chat", true, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Whether the full text of whatever you examine (the same content shown in the examine tooltip)
    /// is also echoed to your chat log.
    /// </summary>
    public static readonly CVarDef<bool> ExamineFullTextInChat =
        CVarDef.Create("cmu.examine_full_text_in_chat", false, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);
}
