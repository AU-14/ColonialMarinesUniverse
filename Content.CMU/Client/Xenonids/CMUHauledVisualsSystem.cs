using Content.Shared._RMC14.Xenonids.Devour;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.CMU14.Xenonids;

/// <summary>
///     Ported from cmss13 PR 8442: a hauled host is carried in the xeno's grasp, drawn just under it and greyed out.
/// </summary>
public sealed class CMUHauledVisualsSystem : EntitySystem
{
    private static readonly Color HauledColor = Color.FromHex("#5f5f5f");

    [Dependency] private SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, (Color Color, int DrawDepth)> _hauled = new();
    private readonly List<EntityUid> _released = new();

    public override void Shutdown()
    {
        base.Shutdown();
        foreach (var uid in _hauled.Keys)
        {
            Restore(uid);
        }

        _hauled.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<DevouredComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (_hauled.ContainsKey(uid))
                continue;

            _hauled[uid] = (sprite.Color, sprite.DrawDepth);
            _sprite.SetColor((uid, sprite), HauledColor);
            _sprite.SetDrawDepth((uid, sprite), (int) DrawDepth.BelowMobs);
        }

        _released.Clear();
        foreach (var uid in _hauled.Keys)
        {
            if (!HasComp<DevouredComponent>(uid))
                _released.Add(uid);
        }

        foreach (var uid in _released)
        {
            Restore(uid);
            _hauled.Remove(uid);
        }
    }

    private void Restore(EntityUid uid)
    {
        if (!_hauled.TryGetValue(uid, out var saved) ||
            TerminatingOrDeleted(uid) ||
            !TryComp(uid, out SpriteComponent? sprite))
        {
            return;
        }

        _sprite.SetColor((uid, sprite), saved.Color);
        _sprite.SetDrawDepth((uid, sprite), saved.DrawDepth);
    }
}
