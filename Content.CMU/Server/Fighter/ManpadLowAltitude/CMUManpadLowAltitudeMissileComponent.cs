using Content.Shared.CMU14.Fighter;
using Robust.Shared.Audio;

namespace Content.Server.CMU14.Fighter;

[RegisterComponent, UnsavedComponent]
public sealed partial class CMUManpadLowAltitudeMissileComponent : Component
{
    public EntityUid Shooter;
    public EntityUid Target;
    public TimeSpan ImpactAt;
    public CMUManpadLowAltitudeHitProfile Hit = default!;
    public SoundSpecifier? ImpactSound;
    public float SoundRange;
}
