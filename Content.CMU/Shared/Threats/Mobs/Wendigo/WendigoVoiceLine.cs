using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.Wendigo;

[Serializable, NetSerializable]
public sealed class WendigoVoiceLine
{
    public string EmoteId = string.Empty;
    public string DisplayName = string.Empty;
    public string Category = string.Empty;
}
