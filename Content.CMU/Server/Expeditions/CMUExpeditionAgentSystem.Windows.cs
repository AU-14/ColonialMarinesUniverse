using System.Linq;
using Content.Server.Destructible;
using Content.Shared.Damage.Components;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    private static readonly ProtoId<TagPrototype> WindowTag = "Window";
    private static readonly ProtoId<TagPrototype> WindowFrameTag = "WindowFrame";
    private readonly Dictionary<EntityUid, bool> _windowSightCache = new();

    private bool TransparentWindow(EntityUid uid)
    {
        if (_windowSightCache.TryGetValue(uid, out var transparent))
            return transparent;
        // RMC full-tile glass uses WallLayer, so neither the movement mask nor Opaque
        // collision bit describes sight. Honour real occluders (tint/shutters) instead.
        transparent = !(TryComp<OccluderComponent>(uid, out var occluder) && occluder.Enabled) &&
            (_tags.HasTag(uid, WindowTag) || MetaData(uid).EntityPrototype is { } prototype &&
                ProtoMan.EnumerateParents<EntityPrototype>(prototype.ID).Any(parent => parent.ID == "CMBaseWindowIndestructible"));
        _windowSightCache[uid] = transparent;
        return transparent;
    }

    private bool WindowAllowsShot(EntityUid shooter, EntityUid uid)
    {
        // Shattered RMC windows leave frames whose BulletImpassable fixture is ignored
        // by native projectiles unless deliberately targeted. Keep that lane usable.
        if (_tags.HasTag(uid, WindowFrameTag) && TryComp<RequireProjectileTargetComponent>(uid, out var targetOnly) &&
            targetOnly.Active && targetOnly.AlwaysHitWhitelist == null && !_containers.IsEntityOrParentInContainer(shooter))
            return true;
        // This authorizes a trigger pull, not penetration. Native bullets damage the pane;
        // only a pane with real destruction thresholds can be shot out to reach the enemy.
        return TransparentWindow(uid) && HasComp<DamageableComponent>(uid) &&
            TryComp<DestructibleComponent>(uid, out var destructible) && destructible.Thresholds.Count > 0;
    }
}
