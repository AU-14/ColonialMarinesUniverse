using Content.Shared.CMU14.Explosion;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    private bool CMUIsExplosionImmovable(EntityUid uid)
    {
        return HasComp<CMUExplosionImmovableComponent>(uid);
    }
}
