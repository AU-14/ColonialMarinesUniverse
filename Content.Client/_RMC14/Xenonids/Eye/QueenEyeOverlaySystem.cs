using Content.Shared._RMC14.Xenonids.Eye;
using Content.Shared._RMC14.Xenonids.Watch;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._RMC14.Xenonids.Eye;

public sealed partial class QueenEyeOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlay = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedEyeSystem _eye = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<QueenEyeActionComponent, AfterAutoHandleStateEvent>(OnUpdated);
        SubscribeLocalEvent<QueenEyeActionComponent, QueenEyeActionUpdated>(OnUpdated);
        SubscribeLocalEvent<QueenEyeActionComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<QueenEyeActionComponent, LocalPlayerDetachedEvent>(OnDetached);
    }

    private void OnUpdated<T>(Entity<QueenEyeActionComponent> ent, ref T args)
    {
        Updated(ent);
        RefreshQueenEyeTarget(ent);
    }

    private void OnAttached(Entity<QueenEyeActionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        Updated(ent);
        RefreshQueenEyeTarget(ent);
    }

    private void OnDetached(Entity<QueenEyeActionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _overlay.RemoveOverlay<QueenEyeOverlay>();
    }

    private void Updated(Entity<QueenEyeActionComponent> ent)
    {
        if (_player.LocalEntity != ent)
            return;

        if (ent.Comp.Eye == null)
        {
            _overlay.RemoveOverlay<QueenEyeOverlay>();
            return;
        }

        if (!_overlay.HasOverlay<QueenEyeOverlay>())
            _overlay.AddOverlay(new QueenEyeOverlay());
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_player.LocalEntity is not { } local ||
            !TryComp(local, out QueenEyeActionComponent? queenEye))
        {
            return;
        }

        RefreshQueenEyeTarget((local, queenEye));
    }

    private void RefreshQueenEyeTarget(Entity<QueenEyeActionComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner ||
            ent.Comp.Eye is not { } queenEye ||
            !TryComp(ent.Owner, out EyeComponent? eye))
        {
            return;
        }

        var target = queenEye;
        if (TryComp(ent.Owner, out XenoWatchingComponent? watching) &&
            watching.Watching is { } watched)
        {
            target = watched;
        }

        if (eye.Target == target)
            return;

        _eye.SetTarget(ent.Owner, target, eye);
    }
}
