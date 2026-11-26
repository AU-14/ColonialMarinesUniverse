// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Per-listener limit for concurrent copies of a transient combat audio file.
    /// Admission happens before creating an audio entity; zero suppresses these sounds.
    /// </summary>
    public static readonly CVarDef<int> CMUTransientAudioPerFile =
        CVarDef.Create("cmu.audio_transient_per_file", 16, CVar.REPLICATED);
}
