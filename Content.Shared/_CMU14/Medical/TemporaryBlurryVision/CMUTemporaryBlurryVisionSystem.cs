using Content.Shared._CMU14.Medical.Organs.Eyes;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.TemporaryBlurryVision;

public sealed partial class CMUTemporaryBlurryVisionSystem : EntitySystem
{
    [Dependency] private BlurryVisionSystem _blur = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, GetBlurEvent>(
            OnGetBlur,
            after: new[] { typeof(CMUBlurDelaySystem) });
        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, RejuvenateEvent>(OnRejuvenate);
    }

    public void AddTemporaryBlurModifier(
        EntityUid uid,
        TimeSpan duration,
        float strength,
        CMUTemporaryBlurryVisionComponent? blur = null)
    {
        if (_net.IsClient || duration <= TimeSpan.Zero || strength <= 0f)
            return;

        blur ??= EnsureComp<CMUTemporaryBlurryVisionComponent>(uid);
        blur.Modifiers.Add(new CMUTemporaryBlurModifier
        {
            ExpiresAt = _timing.CurTime + duration,
            Strength = strength,
        });

        blur.NextUpdate = _timing.CurTime + blur.UpdateRate;
        _blur.UpdateBlurMagnitude(uid);
    }

    private void OnGetBlur(Entity<CMUTemporaryBlurryVisionComponent> ent, ref GetBlurEvent args)
    {
        var now = _timing.CurTime;
        var strongest = 0f;
        var hasActive = false;
        foreach (var modifier in ent.Comp.Modifiers)
        {
            if (modifier.ExpiresAt > now)
            {
                strongest = MathF.Max(strongest, modifier.Strength);
                hasActive = true;
            }
        }

        if (hasActive)
        {
            args.Blur = MathF.Max(args.Blur, strongest);
            args.CorrectionPower = 1.0f;
            args.DistortionPower = 1.0f;
        }
    }

    private void OnRejuvenate(Entity<CMUTemporaryBlurryVisionComponent> ent, ref RejuvenateEvent args)
    {
        ent.Comp.Modifiers.Clear();
        _blur.UpdateBlurMagnitude(ent.Owner);
        RemCompDeferred<CMUTemporaryBlurryVisionComponent>(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUTemporaryBlurryVisionComponent>();
        while (query.MoveNext(out var uid, out var blur))
        {
            if (blur.NextUpdate > now)
                continue;

            blur.NextUpdate = now + blur.UpdateRate;
            var removed = blur.Modifiers.RemoveAll(modifier => modifier.ExpiresAt <= now) > 0;
            if (!removed)
                continue;

            _blur.UpdateBlurMagnitude(uid);
            if (blur.Modifiers.Count == 0)
                RemCompDeferred<CMUTemporaryBlurryVisionComponent>(uid);
        }
    }
}
