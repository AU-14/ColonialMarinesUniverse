using Content.Shared.CMU14.Pointing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Pointing;

/// <summary>
///     Draws the outline a squad leader's point puts on a mob, then takes it off again.
/// </summary>
public sealed class CMUPointOutlineClientSystem : EntitySystem
{
    private const string ShaderKey = "cmu-point-outline";
    private static readonly ProtoId<ShaderPrototype> OutlineShader = "RMCAuraOutline";

    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _outlined = new();
    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<CMUPointOutlineEvent>(OnPointOutline);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var uid in _outlined.Keys)
        {
            Remove(uid);
        }

        _outlined.Clear();
    }

    private void OnPointOutline(CMUPointOutlineEvent ev)
    {
        if (!TryGetEntity(ev.Target, out var target) || !TryComp(target, out SpriteComponent? sprite))
            return;

        var shader = _prototypes.Index(OutlineShader).InstanceUnique();
        shader.SetParameter("outline_color", ev.Color);
        shader.SetParameter("outline_width", ev.Width);
        _sprite.SetPostShader((target.Value, sprite), new SpriteComponent.PostShaderArgs(ShaderKey, shader));
        _outlined[target.Value] = _timing.CurTime + ev.Duration;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_outlined.Count == 0)
            return;

        var now = _timing.CurTime;
        _expired.Clear();
        foreach (var (uid, until) in _outlined)
        {
            if (now >= until)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            _outlined.Remove(uid);
            Remove(uid);
        }
    }

    private void Remove(EntityUid uid)
    {
        if (!TerminatingOrDeleted(uid) && TryComp(uid, out SpriteComponent? sprite))
            _sprite.RemovePostShader((uid, sprite), ShaderKey);
    }
}
