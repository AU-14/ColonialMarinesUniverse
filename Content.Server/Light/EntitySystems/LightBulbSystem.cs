using Content.Shared.Light.EntitySystems;

namespace Content.Server.Light.EntitySystems
{
    public sealed partial class LightBulbSystem : EntitySystem
    {
        [Dependency] private SharedAppearanceSystem _appearance = default!;
        [Dependency] private SharedAudioSystem _audio = default!;

public sealed class LightBulbSystem : SharedLightBulbSystem;
