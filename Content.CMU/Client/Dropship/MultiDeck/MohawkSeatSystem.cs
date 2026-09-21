using System.Numerics;
using Content.Client._RMC14.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.CMU14.Dropship.MultiDeck;

/// <summary>Applies the source seats' pixel offsets without placing passengers inside nearby walls.</summary>
public sealed class MohawkSeatSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SpriteTreeSystem _spriteTree = default!;
    [Dependency] private IEyeManager _eye = default!;

    private readonly Dictionary<EntityUid, RiderVisual> _riders = new();
    private readonly List<EntityUid> _removed = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MohawkSeatComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MohawkSeatComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MohawkSeatComponent, StrappedEvent>(OnStrapped);
        SubscribeLocalEvent<MohawkSeatComponent, UnstrappedEvent>(OnUnstrapped);
        SubscribeLocalEvent<MohawkSeatComponent, RMCBuckleVisualsUpdatedEvent>(OnStrapState);
    }

    private void OnStartup(Entity<MohawkSeatComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<StrapComponent>(ent, out var strap))
            Synchronize(ent, strap);
    }

    private void OnShutdown(Entity<MohawkSeatComponent> ent, ref ComponentShutdown args)
        => Synchronize(ent, null);

    private void OnStrapState(Entity<MohawkSeatComponent> ent, ref RMCBuckleVisualsUpdatedEvent args)
    {
        if (TryComp<StrapComponent>(ent, out var strap))
            Synchronize(ent, strap);
    }

    private void OnStrapped(Entity<MohawkSeatComponent> ent, ref StrappedEvent args)
        => AddRider(args.Buckle.Owner, ent);

    private void OnUnstrapped(Entity<MohawkSeatComponent> ent, ref UnstrappedEvent args)
        => RemoveRider(args.Buckle.Owner);

    private void Synchronize(EntityUid seat, StrapComponent? strap)
    {
        IReadOnlySet<EntityUid>? occupants = strap?.BuckledEntities;
        _removed.Clear();
        foreach (var (uid, visual) in _riders)
        {
            if (visual.Seat == seat && (occupants == null || !occupants.Contains(uid)))
                _removed.Add(uid);
        }
        foreach (var uid in _removed)
            RemoveRider(uid);
        if (strap == null)
            return;
        foreach (var uid in strap.BuckledEntities)
            AddRider(uid, seat);
    }

    private void AddRider(EntityUid uid, EntityUid seat)
    {
        if (_riders.TryGetValue(uid, out var current))
        {
            if (current.Seat == seat)
                return;
            RemoveRider(uid);
        }
        _riders.Add(uid, new RiderVisual(seat));
    }

    private void RemoveRider(EntityUid uid)
    {
        if (!_riders.Remove(uid, out var visual) || visual.OriginalOffset is not { } offset ||
            TerminatingOrDeleted(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return;
        _sprite.SetOffset((uid, sprite), offset);
        _spriteTree.QueueTreeUpdate(uid, sprite);
    }

    public override void FrameUpdate(float frameTime)
    {
        // Only occupied seats are indexed. Recompute for ship and camera rotation.
        foreach (var (uid, visual) in _riders)
        {
            if (TerminatingOrDeleted(uid) || !TryComp<SpriteComponent>(uid, out var sprite) ||
                !TryComp<MohawkSeatComponent>(visual.Seat, out var seat))
                continue;
            visual.OriginalOffset ??= sprite.Offset;
            var worldOffset = _transform.GetWorldRotation(visual.Seat).RotateVec(seat.VisualOffset);
            var renderRotation = sprite.NoRotation ? -_eye.CurrentEye.Rotation : _transform.GetWorldRotation(uid);
            var offset = visual.OriginalOffset.Value + (-renderRotation).RotateVec(worldOffset);
            if (sprite.Offset == offset)
                continue;
            _sprite.SetOffset((uid, sprite), offset);
            _spriteTree.QueueTreeUpdate(uid, sprite);
        }
    }

    private sealed class RiderVisual(EntityUid seat)
    {
        public readonly EntityUid Seat = seat;
        public Vector2? OriginalOffset;
    }
}
