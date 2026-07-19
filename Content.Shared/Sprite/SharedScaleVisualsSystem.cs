using System.Numerics;
using Robust.Shared.Serialization;

namespace Content.Shared.Sprite;

public abstract partial class SharedScaleVisualsSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScaleVisualsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ScaleVisualsComponent, ComponentShutdown>(OnComponentShutdown);
    }

    private void OnMapInit(Entity<ScaleVisualsComponent> ent, ref MapInitEvent args)
    {
        SetSpriteScale(ent.Owner, ent.Comp.Scale);
    }

    private void OnComponentShutdown(Entity<ScaleVisualsComponent> ent, ref ComponentShutdown args)
    {
        ResetScale(ent);
    }

    protected virtual void ResetScale(Entity<ScaleVisualsComponent> ent)
    {
        _appearance.RemoveData(ent.Owner, ScaleVisuals.Scale);
        var ev = new ScaleEntityEvent(ent.Owner, Vector2.One);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    public void SetSpriteScale(EntityUid uid, Vector2 scale)
    {
        var comp = EnsureComp<ScaleVisualsComponent>(uid);
        comp.Scale = scale;
        Dirty(uid, comp);

        var appearance = EnsureComp<AppearanceComponent>(uid);
        _appearance.SetData(uid, ScaleVisuals.Scale, scale, appearance);

        var ev = new ScaleEntityEvent(uid, scale);
        RaiseLocalEvent(uid, ref ev);
    }

    public Vector2 GetSpriteScale(EntityUid uid)
    {
        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearance.TryGetData<Vector2>(uid, ScaleVisuals.Scale, out var scale, appearance))
        {
            return Vector2.One;
        }

        return scale;
    }
}

[ByRefEvent]
public readonly record struct ScaleEntityEvent(EntityUid Uid, Vector2 Scale);

[Serializable, NetSerializable]
public enum ScaleVisuals : byte
{
    Scale,
}
