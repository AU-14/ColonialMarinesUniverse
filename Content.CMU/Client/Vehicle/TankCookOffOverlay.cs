using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Vehicle;

public sealed partial class TankCookOffOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    public override void Initialize() => _overlays.AddOverlay(new TankCookOffOverlay(EntityManager));
    public override void Shutdown() => _overlays.RemoveOverlay<TankCookOffOverlay>();
}

public sealed partial class TankCookOffOverlay(IEntityManager entities) : Overlay
{
    private sealed record Playback(TimeSpan Epoch, TankCookOffParticleScene Scene);
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly MetaDataSystem _metadata = entities.System<MetaDataSystem>();
    private readonly IGameTiming _timing = IoCManager.Resolve<IGameTiming>();
    private readonly TankCookOffVolumeBatch _batch = new();
    private readonly Dictionary<EntityUid, Playback> _playback = new();
    private readonly HashSet<EntityUid> _visible = new();
    private readonly List<EntityUid> _remove = new();

    protected override void Draw(in OverlayDrawArgs args)
    {
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
        _batch.Clear();
        _visible.Clear();
        AddTanks(in args);
        _batch.Draw(args.WorldHandle);
        DrawTurrets(in args);
        _remove.Clear();
        foreach (var uid in _playback.Keys)
            if (!_visible.Contains(uid)) _remove.Add(uid);
        foreach (var uid in _remove) _playback.Remove(uid);
    }

    protected override void DisposeBehavior()
    {
        _batch.Dispose();
        base.DisposeBehavior();
    }
}
