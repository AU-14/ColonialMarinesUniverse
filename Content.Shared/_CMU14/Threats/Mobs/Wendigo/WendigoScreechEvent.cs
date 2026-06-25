using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Threats.Mobs.Wendigo;

[Serializable, NetSerializable]
public sealed class WendigoScreechEvent(SoundSpecifier sound, AudioParams audioParams, NetCoordinates coordinates)
    : EntityEventArgs
{
    public readonly AudioParams AudioParams = audioParams;
    public readonly NetCoordinates Coordinates = coordinates;
    public readonly SoundSpecifier Sound = sound;
}
