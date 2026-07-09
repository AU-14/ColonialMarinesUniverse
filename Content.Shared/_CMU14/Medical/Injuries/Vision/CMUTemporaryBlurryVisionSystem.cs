using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Rejuvenate;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Injuries.Vision;

public sealed partial class CMUTemporaryBlurryVisionSystem : EntitySystem
{
    [Dependency] private BlurryVisionSystem _blur = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;

    private static readonly CMUMedicalWorkKey BlurExpiryWork = new("temporary-blur-expiry");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, GetBlurEvent>(
            OnGetBlur,
            after: new[] { typeof(CMUBlurDelaySystem) });
        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, RejuvenateEvent>(OnRejuvenate);
        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, EntityUnpausedEvent>(OnUnpaused);
        SubscribeLocalEvent<CMUTemporaryBlurryVisionComponent, CMUMedicalWorkDueEvent>(OnBlurExpiryDue);
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

        ScheduleNextExpiry(uid, blur);
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
        _scheduler.Cancel(ent.Owner, BlurExpiryWork);
        _blur.UpdateBlurMagnitude(ent.Owner);
        RemCompDeferred<CMUTemporaryBlurryVisionComponent>(ent.Owner);
    }

    private void OnUnpaused(Entity<CMUTemporaryBlurryVisionComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var modifier in ent.Comp.Modifiers)
            modifier.ExpiresAt += args.PausedTime;
    }

    private void OnBlurExpiryDue(
        Entity<CMUTemporaryBlurryVisionComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != BlurExpiryWork)
            return;

        var now = _timing.CurTime;
        var removed = ent.Comp.Modifiers.RemoveAll(modifier => modifier.ExpiresAt <= now) > 0;
        if (removed)
            _blur.UpdateBlurMagnitude(ent.Owner);

        if (ent.Comp.Modifiers.Count == 0)
        {
            RemCompDeferred<CMUTemporaryBlurryVisionComponent>(ent.Owner);
            return;
        }

        ScheduleNextExpiry(ent.Owner, ent.Comp);
    }

    private void ScheduleNextExpiry(EntityUid uid, CMUTemporaryBlurryVisionComponent blur)
    {
        if (_net.IsClient || blur.Modifiers.Count == 0)
            return;

        var nextExpiry = blur.Modifiers[0].ExpiresAt;
        for (var i = 1; i < blur.Modifiers.Count; i++)
        {
            if (blur.Modifiers[i].ExpiresAt < nextExpiry)
                nextExpiry = blur.Modifiers[i].ExpiresAt;
        }

        _scheduler.Schedule(uid, BlurExpiryWork, nextExpiry);
    }
}
