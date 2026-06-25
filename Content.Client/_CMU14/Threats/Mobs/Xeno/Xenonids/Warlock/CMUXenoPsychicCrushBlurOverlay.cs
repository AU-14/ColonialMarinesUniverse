using System.Numerics;
using Content.Shared._CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Threats.Mobs.Xeno.Xenonids.Warlock;

public sealed partial class CMUXenoPsychicCrushBlurOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    private static readonly ProtoId<ShaderPrototype> BlurShader = "CMUPsychicCrushBlur";
    private const int MaxCount = 32;
    private const float CullPadding = 1.25f;
    private readonly float[] _alphas = new float[MaxCount];
    private readonly Vector2[] _positions = new Vector2[MaxCount];
    private readonly float[] _radii = new float[MaxCount];
    private readonly List<EntityUid> _remove = new();
    private readonly HashSet<EntityUid> _seen = new();
    private readonly ShaderInstance _shader;
    private readonly Dictionary<EntityUid, TimeSpan> _startedAt = new();
    private readonly float[] _strengths = new float[MaxCount];

    private readonly TransformSystem _transform;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private int _count;

    public CMUXenoPsychicCrushBlurOverlay()
    {
        IoCManager.InjectDependencies(this);

        _transform = _entity.System<TransformSystem>();
        _xformQuery = _entity.GetEntityQuery<TransformComponent>();
        _shader = _prototype.Index(BlurShader).Instance().Duplicate();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return false;

        Box2 cullBounds = args.WorldAABB.Enlarged(CullPadding);
        TimeSpan now = _timing.CurTime;

        _count = 0;
        _seen.Clear();

        AllEntityQueryEnumerator<CMUXenoPsychicCrushBlurComponent, TransformComponent> query = _entity
            .AllEntityQueryEnumerator<CMUXenoPsychicCrushBlurComponent, TransformComponent>();
        while (query.MoveNext(out EntityUid uid, out CMUXenoPsychicCrushBlurComponent? blur,
            out TransformComponent? xform))
        {
            _seen.Add(uid);
            if (xform.MapID != args.MapId)
                continue;

            Vector2 world = _transform.GetWorldPosition(xform, _xformQuery);
            if (!cullBounds.Contains(world))
                continue;

            Vector2 local = args.Viewport.WorldToLocal(world);
            Vector2 edgeLocal = args.Viewport.WorldToLocal(world + new Vector2(blur.Radius, 0));
            float alpha = GetAlpha(uid, now, blur.Duration);
            if (alpha <= 0f)
                continue;

            _positions[_count] = new(local.X / args.Viewport.Size.X,
                1f - local.Y / args.Viewport.Size.Y);
            _radii[_count] = Math.Abs(edgeLocal.X - local.X) / args.Viewport.Size.X;
            _strengths[_count] = blur.Strength;
            _alphas[_count] = alpha;
            _count++;

            if (_count == MaxCount)
                break;
        }

        PruneStartedAt();
        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("count", _count);
        _shader.SetParameter("position", _positions);
        _shader.SetParameter("radius", _radii);
        _shader.SetParameter("strength", _strengths);
        _shader.SetParameter("alpha", _alphas);

        DrawingHandleWorld worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }

    private float GetAlpha(EntityUid uid, TimeSpan now, TimeSpan duration)
    {
        if (!_startedAt.TryGetValue(uid, out TimeSpan startedAt))
        {
            startedAt = now;
            _startedAt[uid] = now;
        }

        float seconds = Math.Max(0.05f, (float)duration.TotalSeconds);
        float elapsed = Math.Max(0f, (float)(now - startedAt).TotalSeconds);
        return Math.Clamp(1f - elapsed / seconds, 0f, 1f);
    }

    private void PruneStartedAt()
    {
        _remove.Clear();
        foreach ((EntityUid uid, TimeSpan _) in _startedAt)
        {
            if (!_seen.Contains(uid))
                _remove.Add(uid);
        }

        foreach (EntityUid uid in _remove)
        {
            _startedAt.Remove(uid);
        }
    }
}
