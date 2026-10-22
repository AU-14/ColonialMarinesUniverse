using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;

namespace Content.Client.CMU14.Ghost;

/// <summary>
///     Dresses a ghost in the sprite of the body it left, once that body is known to this client. The ghost keeps its
///     own draw depth and visibility, and stands upright.
/// </summary>
public sealed class CMUGhostAppearanceClientSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _applied = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGhostAppearanceComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMUGhostAppearanceStandInComponent, ComponentStartup>(OnStandInStartup);
    }

    // The stand-in body only exists to be copied; the ghost carrying it is what you see.
    private void OnStandInStartup(Entity<CMUGhostAppearanceStandInComponent> ent, ref ComponentStartup args)
    {
        if (TryComp(ent, out SpriteComponent? sprite))
            _sprite.SetVisible((ent, sprite), false);
    }

    private void OnShutdown(Entity<CMUGhostAppearanceComponent> ent, ref ComponentShutdown args)
    {
        _applied.Remove(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<CMUGhostAppearanceComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var ghost, out var sprite))
        {
            if (_applied.Contains(uid) ||
                ghost.Source is not { } source ||
                !TryComp(source, out SpriteComponent? sourceSprite) ||
                !sourceSprite.AllLayers.Any() ||
                !BodyLayersReady((source, sourceSprite)))
            {
                continue;
            }

            _applied.Add(uid);

            var drawDepth = sprite.DrawDepth;
            var visible = sprite.Visible;
            var color = sprite.Color;
            var noRotation = sprite.NoRotation;
            _sprite.CopySprite((source, sourceSprite), (uid, sprite));
            _sprite.SetDrawDepth((uid, sprite), drawDepth);
            _sprite.SetVisible((uid, sprite), visible);
            // a ghost stays upright however it's turned, even when the body it copies doesn't (e.g. a lobby stand-in)
            sprite.NoRotation = noRotation;
            // the body is opaque, keep the ghost see-through
            _sprite.SetColor((uid, sprite), color);
            _sprite.SetRotation((uid, sprite), Angle.Zero);
            _sprite.SetOffset((uid, sprite), Vector2.Zero);
            ShowAlive((uid, sprite), (source, sourceSprite));
        }
    }

    /// <summary>
    ///     A humanoid's body layers are filled in by its organs, which can land on this client after the body does.
    ///     Copying before that leaves a ghost made of nothing but burns, bandages and blood.
    /// </summary>
    private bool BodyLayersReady(Entity<SpriteComponent> source)
    {
        if (!_sprite.LayerMapTryGet(source.AsNullable(), HumanoidVisualLayers.Chest, out var chest, false))
            return true;

        return _sprite.LayerGetRsiState(source.AsNullable(), chest).IsValid;
    }

    /// <summary>
    ///     A dead xeno's sprite is its corpse; its ghost looks like the living xeno instead. The copied layers keep the
    ///     body's order, so the body's layer map finds the base layer.
    /// </summary>
    private void ShowAlive(Entity<SpriteComponent> ghost, Entity<SpriteComponent> source)
    {
        if (!_sprite.LayerMapTryGet(source.AsNullable(), XenoVisualLayers.Base, out var layer, false) ||
            layer >= ghost.Comp.AllLayers.Count() ||
            _sprite.LayerGetEffectiveRsi(ghost.AsNullable(), layer) is not { } rsi ||
            !rsi.TryGetState("alive", out _))
        {
            return;
        }

        _sprite.LayerSetRsiState(ghost.AsNullable(), layer, "alive");
    }
}
