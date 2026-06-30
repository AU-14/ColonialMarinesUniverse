using Content.Shared.Audio;
using Robust.Client.Graphics;
using Robust.Shared.Enums;

namespace Content.Client.Audio;

/// <summary>
/// Debug overlay that shows all ambientsound sources in range
/// </summary>
public sealed class AmbientSoundOverlay : Overlay
{
    private readonly IEntityManager _entManager;
    private readonly AmbientSoundSystem _ambient;
    private readonly EntityLookupSystem _lookup;
    private readonly HashSet<EntityUid> _intersecting = new();

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public AmbientSoundOverlay(IEntityManager entManager, AmbientSoundSystem ambient, EntityLookupSystem lookup)
    {
        _entManager = entManager;
        _ambient = ambient;
        _lookup = lookup;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        var ambientQuery = _entManager.GetEntityQuery<AmbientSoundComponent>();
        var xformQuery = _entManager.GetEntityQuery<TransformComponent>();
        var xformSystem = _entManager.System<SharedTransformSystem>();

        const float Size = 0.25f;
        const float Alpha = 0.25f;

        _intersecting.Clear();
        _lookup.GetEntitiesIntersecting(args.MapId, args.WorldAABB, _intersecting);
        foreach (var ent in _intersecting)
        {
            if (!ambientQuery.TryGetComponent(ent, out var ambientSound) ||
                !xformQuery.TryGetComponent(ent, out var xform)) continue;

            var worldPosition = xformSystem.GetWorldPosition(xform);
            if (!args.WorldBounds.Contains(worldPosition))
                continue;

            if (ambientSound.Enabled)
            {
                if (_ambient.IsActive((ent, ambientSound)))
                {
                    worldHandle.DrawCircle(worldPosition, Size, Color.LightGreen.WithAlpha(Alpha * 2f));
                }
                else
                {
                    worldHandle.DrawCircle(worldPosition, Size, Color.Orange.WithAlpha(Alpha));
                }
            }
            else
            {
                worldHandle.DrawCircle(worldPosition, Size, Color.Red.WithAlpha(Alpha));
            }
        }
    }
}
