using Content.Shared.CMU14.Fighter;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Fighter;

public sealed partial class CMUManpadLowAltitudeLockSystem : EntitySystem
{
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(100);

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUManpadLowAltitudeSystem _manpadLowAltitude = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUManpadLowAltitudeTargetingSystem _targeting = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<Entity<CMUManpadLowAltitudeComponent>> _stopQueue = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, CMUManpadLowAltitudeAimStartedEvent>(OnAimStarted);
        SubscribeLocalEvent<CMUManpadLowAltitudeComponent, CMUManpadLowAltitudeAimStoppedEvent>(OnAimStopped);
    }

    private void OnAimStarted(Entity<CMUManpadLowAltitudeComponent> ent, ref CMUManpadLowAltitudeAimStartedEvent args)
    {
        EnsureComp<CMUManpadLowAltitudeAimingComponent>(ent);
    }

    private void OnAimStopped(Entity<CMUManpadLowAltitudeComponent> ent, ref CMUManpadLowAltitudeAimStoppedEvent args)
    {
        RemComp<CMUManpadLowAltitudeAimingComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUManpadLowAltitudeAimingComponent, CMUManpadLowAltitudeComponent>();
        while (query.MoveNext(out var uid, out var aiming, out var manpad))
        {
            var launcher = new Entity<CMUManpadLowAltitudeComponent>(uid, manpad);
            if (manpad.LockState != CMUManpadLowAltitudeLockState.None && now >= aiming.NextBeep)
                Beep(launcher, aiming, now);

            if (now < aiming.NextScan)
                continue;

            aiming.NextScan = now + ScanInterval;

            if (!_manpadLowAltitude.IsAiming(launcher))
            {
                _stopQueue.Add(launcher);
                continue;
            }

            switch (manpad.LockState)
            {
                case CMUManpadLowAltitudeLockState.None:
                    if (now >= aiming.NextAcquire && _targeting.TryFindTarget(launcher, out var target, out var kind))
                        BeginLock(launcher, aiming, target, kind, now);
                    break;
                case CMUManpadLowAltitudeLockState.Locking:
                    if (!IsTargetTrackable(launcher, aiming))
                        LoseLock(launcher, aiming, now);
                    else if (now >= aiming.LockCompleteAt)
                        CompleteLock(launcher, aiming, now);
                    break;
                case CMUManpadLowAltitudeLockState.Locked:
                    if (!IsTargetTrackable(launcher, aiming))
                        LoseLock(launcher, aiming, now);
                    break;
            }
        }

        foreach (var launcher in _stopQueue)
        {
            _manpadLowAltitude.StopAiming(launcher);
            RemComp<CMUManpadLowAltitudeAimingComponent>(launcher);
        }

        _stopQueue.Clear();
    }

    public void ResetLock(Entity<CMUManpadLowAltitudeComponent> launcher, CMUManpadLowAltitudeAimingComponent aiming)
    {
        aiming.Target = null;
        _manpadLowAltitude.SetLock(launcher, CMUManpadLowAltitudeLockState.None, CMUManpadLowAltitudeTargetKind.None);
    }

    private bool IsTargetTrackable(Entity<CMUManpadLowAltitudeComponent> launcher, CMUManpadLowAltitudeAimingComponent aiming)
    {
        return aiming.Target is { } target && _targeting.CanTrack(launcher, target);
    }

    private void Beep(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        CMUManpadLowAltitudeAimingComponent aiming,
        TimeSpan now)
    {
        if (launcher.Comp.AimingUser is not { } user)
            return;

        var comp = launcher.Comp;
        var locked = comp.LockState == CMUManpadLowAltitudeLockState.Locked;
        _audio.PlayGlobal(locked ? comp.LockedSound : comp.LockingSound, user);

        var interval = comp.BeepIntervalEnd;
        if (!locked)
        {
            var progress = comp.LockTime > TimeSpan.Zero
                ? Math.Clamp((now - aiming.LockStartedAt) / comp.LockTime, 0, 1)
                : 1;
            interval = comp.BeepIntervalStart + (comp.BeepIntervalEnd - comp.BeepIntervalStart) * progress;
        }

        aiming.NextBeep = now + interval;
    }

    private void BeginLock(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        CMUManpadLowAltitudeAimingComponent aiming,
        EntityUid target,
        CMUManpadLowAltitudeTargetKind kind,
        TimeSpan now)
    {
        if (launcher.Comp.AimingUser is not { } user)
            return;

        aiming.Target = target;
        aiming.LockStartedAt = now;
        aiming.LockCompleteAt = now + launcher.Comp.LockTime;
        aiming.NextBeep = now;
        _manpadLowAltitude.SetLock(launcher, CMUManpadLowAltitudeLockState.Locking, kind);
        _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-locking", ("target", _manpadLowAltitude.GetTargetName(kind))), user, user);
    }

    private void CompleteLock(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        CMUManpadLowAltitudeAimingComponent aiming,
        TimeSpan now)
    {
        if (launcher.Comp.AimingUser is not { } user)
            return;

        aiming.NextBeep = now;
        var kind = launcher.Comp.TargetKind;
        _manpadLowAltitude.SetLock(launcher, CMUManpadLowAltitudeLockState.Locked, kind);
        _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-locked", ("target", _manpadLowAltitude.GetTargetName(kind))), user, user, PopupType.Medium);
    }

    private void LoseLock(
        Entity<CMUManpadLowAltitudeComponent> launcher,
        CMUManpadLowAltitudeAimingComponent aiming,
        TimeSpan now)
    {
        if (launcher.Comp.AimingUser is { } user)
            _popup.PopupEntity(Loc.GetString("cmu-manpad-low-altitude-lock-lost"), user, user, PopupType.SmallCaution);

        aiming.NextAcquire = now + launcher.Comp.LockDelay;
        ResetLock(launcher, aiming);
    }
}
