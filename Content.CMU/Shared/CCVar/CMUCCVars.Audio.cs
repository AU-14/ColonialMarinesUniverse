// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// How loud jukeboxes play for this client, as a gain multiplier.
    /// </summary>
    public static readonly CVarDef<float> CMUJukeboxVolume =
        CVarDef.Create("cmu.jukebox_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
