// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Don't draw the music notes over people listening to a cassette player.
    /// </summary>
    public static readonly CVarDef<bool> CMUHideCassetteMusicNotes =
        CVarDef.Create("cmu.hide_cassette_music_notes", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
