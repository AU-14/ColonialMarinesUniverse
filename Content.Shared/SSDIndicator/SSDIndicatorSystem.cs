using Content.Shared.CCVar;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.SSDIndicator;

/// <summary>
///     Handle changing player SSD indicator status
/// </summary>
public sealed class SSDIndicatorSystem : EntitySystem
{
    public static readonly EntProtoId StatusEffectSSDSleeping = "StatusEffectSSDSleeping";

    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStatusEffectsSystem _statusEffects = default!;

    private bool _icSsdSleep;
    private float _icSsdSleepTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<SSDIndicatorComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<SSDIndicatorComponent, MapInitEvent>(OnMapInit);

        _cfg.OnValueChanged(CCVars.ICSSDSleep, obj => _icSsdSleep = obj, true);
        _cfg.OnValueChanged(CCVars.ICSSDSleepTime, obj => _icSsdSleepTime = obj, true);
    }

    private void OnPlayerAttached(EntityUid uid, SSDIndicatorComponent component, PlayerAttachedEvent args)
    {
        component.IsSSD = false;

        // Removes force sleep and resets the time to zero
        if (_icSsdSleep)
        {
            component.FallAsleepTime = TimeSpan.Zero;
            _statusEffects.TryRemoveStatusEffect(uid, StatusEffectSSDSleeping);
        }

        Dirty(uid, component);
    }

    private void OnPlayerDetached(EntityUid uid, SSDIndicatorComponent component, PlayerDetachedEvent args)
    {
        component.IsSSD = true;

        // Sets the time when the entity should fall asleep
        if (_icSsdSleep)
        {
            component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        }

        Dirty(uid, component);
    }

    // Prevents mapped mobs to go to sleep immediately
    private void OnMapInit(EntityUid uid, SSDIndicatorComponent component, MapInitEvent args)
    {
        if (_icSsdSleep &&
            component.IsSSD &&
            component.FallAsleepTime == TimeSpan.Zero)
        {
            component.FallAsleepTime = _timing.CurTime + TimeSpan.FromSeconds(_icSsdSleepTime);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Sleep is server-authoritative and networked back to clients via the status effect entity.
        // Running this on the client just churns prediction (it spawns the predicted effect each frame
        // until the server's confirmation lands).
        if (!_net.IsServer || !_icSsdSleep)
            return;

        var query = EntityQueryEnumerator<SSDIndicatorComponent>();

        while (query.MoveNext(out var uid, out var ssd))
        {
            // Forces the entity to sleep when the time has come
            if (ssd.IsSSD &&
                ssd.FallAsleepTime <= _timing.CurTime &&
                !TerminatingOrDeleted(uid))
            {
                if (!_statusEffects.TrySetStatusEffectDuration(uid, StatusEffectSSDSleeping, null))
                    continue;

                // Don't keep retrying every tick once the effect is applied — TrySetStatusEffectDuration
                // walks ActiveStatusEffects on the target every call. Reset on PlayerDetached.
                ssd.FallAsleepTime = TimeSpan.MaxValue;
                Dirty(uid, ssd);
            }
        }
    }
}
