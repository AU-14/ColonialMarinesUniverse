// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
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

    /// <summary>
    /// After sending a message on any channel other than Local, switch the chat input back to Local.
    /// Does nothing if Local can't be selected, e.g. as a ghost.
    /// </summary>
    /// <summary>
    /// Whether this player hears the talking blips when people speak nearby.
    /// </summary>
    public static readonly CVarDef<bool> ChatSpeechSounds =
        CVarDef.Create("cmu.chat_speech_sounds", true, CVar.CLIENT | CVar.REPLICATED | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ChatResetToLocal =
        CVarDef.Create("cmu.chat_reset_to_local", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// When the chat keybind is pressed, move the chat input to the middle of the screen until the message is sent.
    /// </summary>
    public static readonly CVarDef<bool> ChatCenterInput =
        CVarDef.Create("cmu.chat_center_input", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
