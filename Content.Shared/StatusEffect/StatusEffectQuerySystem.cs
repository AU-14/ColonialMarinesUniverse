using System.Diagnostics.CodeAnalysis;
using Content.Shared._RMC14.StatusEffect;
using Content.Shared.Alert;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.StatusEffect;

public sealed partial class StatusEffectQuerySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private AlertsSystem _alertsSystem = default!;

    public bool TryAddStatusEffect<T>(
        EntityUid uid,
        string key,
        TimeSpan time,
        bool refresh,
        StatusEffectsComponent? status = null,
        bool force = false)
        where T : IComponent, new()
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (!TryAddStatusEffect(uid, key, time, refresh, status, force: force))
            return false;

        if (HasComp<T>(uid))
        {
            status.ActiveEffects[key].RelevantComponent = Factory.GetComponentName<T>();
            return true;
        }

        AddComp<T>(uid);
        status.ActiveEffects[key].RelevantComponent = Factory.GetComponentName<T>();
        return true;
    }

    public bool TryAddStatusEffect(
        EntityUid uid,
        string key,
        TimeSpan time,
        bool refresh,
        string component,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (!TryAddStatusEffect(uid, key, time, refresh, status))
            return false;

        if (!HasComp(uid, Factory.GetRegistration(component).Type))
        {
            var newComponent = (Component) Factory.GetComponent(component);
            AddComp(uid, newComponent);
            status.ActiveEffects[key].RelevantComponent = component;
        }

        return true;
    }

    public bool TryAddStatusEffect(
        EntityUid uid,
        string key,
        TimeSpan time,
        bool refresh,
        StatusEffectsComponent? status = null,
        TimeSpan? startTime = null,
        bool force = false)
    {
        if (!Resolve(uid, ref status, false) ||
            !CanApplyEffect(uid, key, status, force))
        {
            return false;
        }

        var ev = new RMCStatusEffectTimeEvent(key, time);
        RaiseLocalEvent(uid, ref ev);
        time = ev.Duration;

        var proto = _prototypeManager.Index<StatusEffectPrototype>(key);
        var start = startTime ?? _gameTiming.CurTime;
        (TimeSpan, TimeSpan) cooldown = (start, start + time);

        if (HasStatusEffect(uid, key, status))
        {
            status.ActiveEffects[key].CooldownRefresh = refresh;
            if (refresh)
            {
                if (status.ActiveEffects[key].Cooldown.Item2 - _gameTiming.CurTime < time)
                    status.ActiveEffects[key].Cooldown = cooldown;
            }
            else
            {
                status.ActiveEffects[key].Cooldown.Item2 += time;
            }
        }
        else
        {
            status.ActiveEffects.Add(key, new StatusEffectState(cooldown, refresh));
            EnsureComp<ActiveStatusEffectsComponent>(uid);
        }

        if (proto.Alert != null)
        {
            var cooldown1 = GetAlertCooldown(proto.Alert.Value, status);
            _alertsSystem.ShowAlert(uid, proto.Alert.Value, null, cooldown1);
        }

        Dirty(uid, status);
        RaiseLocalEvent(uid, new StatusEffectAddedEvent(uid, key));
        return true;
    }

    public bool TryRemoveStatusEffect(
        EntityUid uid,
        string key,
        StatusEffectsComponent? status = null,
        bool remComp = true)
    {
        if (!Resolve(uid, ref status, false) ||
            !status.ActiveEffects.TryGetValue(key, out var state) ||
            !_prototypeManager.TryIndex<StatusEffectPrototype>(key, out var proto))
        {
            return false;
        }

        if (remComp &&
            state.RelevantComponent != null &&
            Factory.TryGetRegistration(state.RelevantComponent, out var registration))
        {
            RemComp(uid, registration.Type);
        }

        if (proto.Alert != null)
            _alertsSystem.ClearAlert(uid, proto.Alert.Value);

        status.ActiveEffects.Remove(key);
        if (status.ActiveEffects.Count == 0)
            RemComp<ActiveStatusEffectsComponent>(uid);

        Dirty(uid, status);
        RaiseLocalEvent(uid, new StatusEffectEndedEvent(uid, key));
        return true;
    }

    public bool TryRemoveAllStatusEffects(
        EntityUid uid,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        var keys = new List<string>(status.ActiveEffects.Keys);
        var removedAll = true;

        foreach (var key in keys)
        {
            if (!TryRemoveStatusEffect(uid, key, status))
                removedAll = false;
        }

        Dirty(uid, status);
        return removedAll;
    }

    public bool HasStatusEffect(
        EntityUid uid,
        string key,
        StatusEffectsComponent? status = null)
    {
        return Resolve(uid, ref status, false) &&
               status.ActiveEffects.ContainsKey(key);
    }

    public bool CanApplyEffect(
        EntityUid uid,
        string key,
        StatusEffectsComponent? status = null,
        bool force = false)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (!force)
        {
            var ev = new BeforeStatusEffectAddedEvent(key);
            RaiseLocalEvent(uid, ref ev);
            if (ev.Cancelled)
                return false;
        }

        if (!_prototypeManager.TryIndex<StatusEffectPrototype>(key, out var proto))
            return false;

        return status.AllowedEffects.Contains(key) || proto.AlwaysAllowed;
    }

    public bool TryAddTime(
        EntityUid uid,
        string key,
        TimeSpan time,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false) ||
            !status.ActiveEffects.TryGetValue(key, out var state))
        {
            return false;
        }

        var timer = state.Cooldown;
        timer.Item2 += time;
        state.Cooldown = timer;

        RefreshAlert(uid, key, status);
        Dirty(uid, status);
        return true;
    }

    public bool TryRemoveTime(
        EntityUid uid,
        string key,
        TimeSpan time,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false) ||
            !status.ActiveEffects.TryGetValue(key, out var state))
        {
            return false;
        }

        var timer = state.Cooldown;
        if (time > timer.Item2)
            return false;

        timer.Item2 -= time;
        state.Cooldown = timer;

        RefreshAlert(uid, key, status);
        Dirty(uid, status);
        return true;
    }

    public bool TrySetTime(
        EntityUid uid,
        string key,
        TimeSpan time,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false) ||
            !status.ActiveEffects.TryGetValue(key, out var state))
        {
            return false;
        }

        state.Cooldown = (_gameTiming.CurTime, _gameTiming.CurTime + time);
        Dirty(uid, status);
        return true;
    }

    public bool TryGetTime(
        EntityUid uid,
        string key,
        [NotNullWhen(true)] out (TimeSpan, TimeSpan)? time,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false) ||
            !status.ActiveEffects.TryGetValue(key, out var state))
        {
            time = null;
            return false;
        }

        time = state.Cooldown;
        return true;
    }

    private void RefreshAlert(EntityUid uid, string key, StatusEffectsComponent status)
    {
        if (_prototypeManager.TryIndex<StatusEffectPrototype>(key, out var proto) &&
            proto.Alert != null)
        {
            var cooldown = GetAlertCooldown(proto.Alert.Value, status);
            _alertsSystem.ShowAlert(uid, proto.Alert.Value, null, cooldown);
        }
    }

    private (TimeSpan, TimeSpan)? GetAlertCooldown(
        ProtoId<AlertPrototype> alert,
        StatusEffectsComponent status)
    {
        (TimeSpan, TimeSpan)? maxCooldown = null;

        foreach (var (key, state) in status.ActiveEffects)
        {
            var proto = _prototypeManager.Index<StatusEffectPrototype>(key);
            if (proto.Alert != alert)
                continue;

            if (maxCooldown == null || state.Cooldown.Item2 > maxCooldown.Value.Item2)
                maxCooldown = state.Cooldown;
        }

        return maxCooldown;
    }
}
