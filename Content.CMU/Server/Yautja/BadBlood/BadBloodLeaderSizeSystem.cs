using System.Numerics;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Sprite;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 ECS-611: the clan's current Leader is drawn slightly larger than other Bad Bloods so marines can tell
/// them apart. The scale follows leadership through the broadcast <see cref="BadBloodLeaderChangedEvent"/> (the
/// Leader component's own directed lifecycle events are already taken by BadBloodHijackSystem). Visual only:
/// fixtures, hitbox and speed are unchanged. <see cref="BadBloodLeaderScaledComponent"/> marks a body this system
/// enlarged, so only that size is ever taken off again.
/// </summary>
public sealed class BadBloodLeaderSizeSystem : EntitySystem
{
    [Dependency] private SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);
    }

    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent ev)
    {
        if (ev.Old is { } old && old != ev.New)
            Shrink(old);

        if (ev.New is { } leader && TryComp(leader, out BadBloodLeaderComponent? comp))
            Grow((leader, comp));
    }

    private void Grow(Entity<BadBloodLeaderComponent> ent)
    {
        // never fight another effect that already rescaled this body
        if (HasComp<ScaleVisualsComponent>(ent) && !HasComp<BadBloodLeaderScaledComponent>(ent))
            return;

        _scale.SetSpriteScale(ent, new Vector2(ent.Comp.SpriteScale));
        EnsureComp<BadBloodLeaderScaledComponent>(ent);
    }

    private void Shrink(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !RemComp<BadBloodLeaderScaledComponent>(uid))
            return;

        // removing the component resets the sprite to its original scale
        RemComp<ScaleVisualsComponent>(uid);
    }
}
