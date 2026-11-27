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

    /// <summary>
    /// Per-listener limit for concurrent copies of a transient combat audio file.
    /// Admission happens before creating an audio entity; zero suppresses these sounds.
    /// </summary>
    public static readonly CVarDef<int> CMUTransientAudioPerFile =
        CVarDef.Create("cmu.audio_transient_per_file", 16, CVar.REPLICATED);
}
