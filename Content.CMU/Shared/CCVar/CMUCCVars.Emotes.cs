// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Which emote prototype ID (if any) is fired by each of the 8 emote keybind slots.
    /// Empty string means the slot has no emote assigned. See CMUKeyFunctions.CMUEmoteSlot1-8.
    /// </summary>
    public static readonly CVarDef<string> EmoteSlot1 =
        CVarDef.Create("cmu.emote_slot_1", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot2 =
        CVarDef.Create("cmu.emote_slot_2", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot3 =
        CVarDef.Create("cmu.emote_slot_3", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot4 =
        CVarDef.Create("cmu.emote_slot_4", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot5 =
        CVarDef.Create("cmu.emote_slot_5", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot6 =
        CVarDef.Create("cmu.emote_slot_6", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot7 =
        CVarDef.Create("cmu.emote_slot_7", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> EmoteSlot8 =
        CVarDef.Create("cmu.emote_slot_8", string.Empty, CVar.CLIENTONLY | CVar.ARCHIVE);
}
