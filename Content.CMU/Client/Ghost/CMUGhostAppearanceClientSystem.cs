using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.CMU14.Ghost;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Ghost;

/// <summary>
///     Dresses a ghost in the sprite of the body it left, once that body is known to this client. The ghost keeps its
///     own draw depth and visibility, and stands upright.
/// </summary>
public sealed class CMUGhostAppearanceClientSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly HumanoidVisualLayers[] BodyLayers =
    [
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
    ];

    /// <summary>
    ///     How long to wait for every limb before copying anyway, for a body that really is missing one.
    /// </summary>
    private static readonly TimeSpan LimbWait = TimeSpan.FromSeconds(2);

    /// <summary>
    ///     The body's own layers, as opposed to clothes. A ghost's body is a little fainter than what it's wearing.
    /// </summary>
    private static readonly HumanoidVisualLayers[] FaintLayers =
    [
        HumanoidVisualLayers.Special,
        HumanoidVisualLayers.Tail,
        HumanoidVisualLayers.TailOverlay,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.UndergarmentTop,
        HumanoidVisualLayers.UndergarmentBottom,
        HumanoidVisualLayers.Chest,
        HumanoidVisualLayers.Head,
        HumanoidVisualLayers.Snout,
        HumanoidVisualLayers.SnoutCover,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
        HumanoidVisualLayers.Eyes,
        HumanoidVisualLayers.RArm,
        HumanoidVisualLayers.LArm,
        HumanoidVisualLayers.RHand,
        HumanoidVisualLayers.LHand,
        HumanoidVisualLayers.RLeg,
        HumanoidVisualLayers.LLeg,
        HumanoidVisualLayers.RFoot,
        HumanoidVisualLayers.LFoot,
    ];

    /// <summary>How opaque the body is next to its clothes.</summary>
    private const float BodyOpacity = 0.7f;

    private readonly HashSet<EntityUid> _applied = new();
    private readonly Dictionary<EntityUid, TimeSpan> _waitingSince = new();

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
        _waitingSince.Remove(ent);
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
                !BodyLayersReady(uid, (source, sourceSprite)))
            {
                continue;
            }

            _applied.Add(uid);
            _waitingSince.Remove(uid);

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
            FadeBody((uid, sprite), (source, sourceSprite));
        }
    }

    /// <summary>
    ///     Makes the copied body layers fainter than the clothes. The copied layers keep the body's order, so the body's
    ///     layer map finds them.
    /// </summary>
    private void FadeBody(Entity<SpriteComponent> ghost, Entity<SpriteComponent> source)
    {
        var layers = ghost.Comp.AllLayers.ToList();
        foreach (var key in FaintLayers)
        {
            if (!_sprite.LayerMapTryGet(source.AsNullable(), key, out var index, false) || index >= layers.Count)
                continue;

            var color = layers[index].Color;
            _sprite.LayerSetColor(ghost.AsNullable(), index, color.WithAlpha(color.A * BodyOpacity));
        }
    }

    /// <summary>
    ///     A humanoid's body layers are filled in by its organs and limbs, which can land on this client after the body
    ///     does, the limbs after the chest. Copying before that leaves a ghost made of nothing but burns, bandages and
    ///     blood, or one with no arms and legs. A body still missing a limb after a short wait is copied as it is.
    /// </summary>
    private bool BodyLayersReady(EntityUid ghost, Entity<SpriteComponent> source)
    {
        if (!_sprite.LayerMapTryGet(source.AsNullable(), HumanoidVisualLayers.Chest, out var chest, false))
            return true;

        if (!_sprite.LayerGetRsiState(source.AsNullable(), chest).IsValid)
            return false;

        var now = _timing.RealTime;
        if (!_waitingSince.TryGetValue(ghost, out var since))
            _waitingSince[ghost] = since = now;

        if (now - since >= LimbWait)
            return true;

        foreach (var layer in BodyLayers)
        {
            if (_sprite.LayerMapTryGet(source.AsNullable(), layer, out var index, false) &&
                !_sprite.LayerGetRsiState(source.AsNullable(), index).IsValid)
            {
                return false;
            }
        }

        return true;
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
