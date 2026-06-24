using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Wendigo;

[Serializable, NetSerializable]
public sealed class WendigoScreechEvent(SoundSpecifier sound, AudioParams audioParams, NetCoordinates coordinates) : EntityEventArgs
{
    public readonly SoundSpecifier Sound = sound;
    public readonly AudioParams AudioParams = audioParams;
    public readonly NetCoordinates Coordinates = coordinates;
}
