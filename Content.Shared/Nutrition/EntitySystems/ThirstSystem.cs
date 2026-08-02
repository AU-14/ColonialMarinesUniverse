using System.Diagnostics.CodeAnalysis;
using Content.Shared.Alert;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.StatusIcon;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Nutrition.EntitySystems;

[UsedImplicitly]
public sealed partial class ThirstSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedJetpackSystem _jetpack = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly ProtoId<SatiationIconPrototype> ThirstIconOverhydratedId = "ThirstIconOverhydrated";
    private static readonly ProtoId<SatiationIconPrototype> ThirstIconThirstyId = "ThirstIconThirsty";
    private static readonly ProtoId<SatiationIconPrototype> ThirstIconParchedId = "ThirstIconParched";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThirstComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<ThirstComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ThirstComponent, RejuvenateEvent>(OnRejuvenate);
    }

    private void OnMapInit(EntityUid uid, ThirstComponent component, MapInitEvent args)
    {
        // Do not change behavior unless starting value is explicitly defined
        if (component.CurrentThirst < 0)
        {
            component.CurrentThirst = _random.Next(
                (int) component.ThirstThresholds[ThirstThreshold.Thirsty] + 10,
                (int) component.ThirstThresholds[ThirstThreshold.Okay] - 1);

            DirtyField(uid, component, nameof(ThirstComponent.CurrentThirst));
        }
        component.NextUpdateTime = _timing.CurTime;
        component.CurrentThirstThreshold = GetThirstThreshold(component, component.CurrentThirst);
        component.LastThirstThreshold = ThirstThreshold.Okay; // TODO: Potentially change this -> Used Okay because no effects.
        // TODO: Check all thresholds make sense and throw if they don't.
        UpdateEffects(uid, component);

        DirtyFields(uid, component, null, nameof(ThirstComponent.NextUpdateTime), nameof(ThirstComponent.CurrentThirstThreshold), nameof(ThirstComponent.LastThirstThreshold));

        TryComp(uid, out MovementSpeedModifierComponent? moveMod);
            _movement.RefreshMovementSpeedModifiers(uid, moveMod);
    }

    private void OnRefreshMovespeed(EntityUid uid, ThirstComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (_jetpack.IsUserFlying(uid))
            return;

        // How soon these kick in is entirely a function of BaseDecayRate and ThirstThresholds - see base.yml.
        if (component.CurrentThirstThreshold <= ThirstThreshold.Thirsty)
            args.ModifySpeed(component.ThirstySlowdownModifier, component.ThirstySlowdownModifier);

        if (component.CurrentThirstThreshold <= ThirstThreshold.Parched)
            args.ModifySpeed(component.ParchedSlowdownModifier, component.ParchedSlowdownModifier);
    }

    private void OnRejuvenate(EntityUid uid, ThirstComponent component, RejuvenateEvent args)
    {
        SetThirst(uid, component, component.ThirstThresholds[ThirstThreshold.Okay]);
    }

    /// <summary>
    /// Pops the "you need to drink" reminder while Thirsty or worse, throttled by <see cref="ThirstComponent.ReminderPopupInterval"/>.
    /// </summary>
    private void UpdateReminderPopup(EntityUid uid, ThirstComponent component)
    {
        if (component.CurrentThirstThreshold > ThirstThreshold.Thirsty)
            return;

        var reminder = EnsureComp<NutritionReminderComponent>(uid);
        var curTime = _timing.CurTime;
        if (curTime < reminder.NextReminderPopupTime)
            return;

        reminder.NextReminderPopupTime = curTime + component.ReminderPopupInterval;

        var message = component.CurrentThirstThreshold <= ThirstThreshold.Parched
            ? "nutrition-thirst-overdue-severe"
            : "nutrition-thirst-overdue-warning";
        _popup.PopupClient(Loc.GetString(message), uid, uid);
    }

    private ThirstThreshold GetThirstThreshold(ThirstComponent component, float amount)
    {
        ThirstThreshold result = ThirstThreshold.Dead;
        var value = component.ThirstThresholds[ThirstThreshold.OverHydrated];
        foreach (var threshold in component.ThirstThresholds)
        {
            if (threshold.Value <= value && threshold.Value >= amount)
            {
                result = threshold.Key;
                value = threshold.Value;
            }
        }

        return result;
    }

    public void ModifyThirst(EntityUid uid, ThirstComponent component, float amount)
    {
        SetThirst(uid, component, component.CurrentThirst + amount);
    }

    public void SetThirst(EntityUid uid, ThirstComponent component, float amount)
    {
        component.CurrentThirst = Math.Clamp(amount,
            component.ThirstThresholds[ThirstThreshold.Dead],
            component.ThirstThresholds[ThirstThreshold.OverHydrated]
        );

        DirtyField(uid, component, nameof(ThirstComponent.CurrentThirst));
    }

    /// <summary>
    /// Sets an entity's thirst to the value of the given threshold, e.g. to spawn them already Thirsty.
    /// </summary>
    public void SetThirstToThreshold(EntityUid uid, ThirstComponent component, ThirstThreshold threshold)
    {
        if (component.ThirstThresholds.TryGetValue(threshold, out var value))
            SetThirst(uid, component, value);
    }

    public bool TryGetStatusIconPrototype(ThirstComponent component, [NotNullWhen(true)] out SatiationIconPrototype? prototype)
    {
        switch (component.CurrentThirstThreshold)
        {
            case ThirstThreshold.OverHydrated:
                _prototype.TryIndex(ThirstIconOverhydratedId, out prototype);
                break;

            case ThirstThreshold.Thirsty:
                _prototype.TryIndex(ThirstIconThirstyId, out prototype);
                break;

            case ThirstThreshold.Parched:
                _prototype.TryIndex(ThirstIconParchedId, out prototype);
                break;

            default:
                prototype = null;
                break;
        }

        return prototype != null;
    }

    private void UpdateEffects(EntityUid uid, ThirstComponent component)
    {
        // Thirsty and worse all affect movement speed now, so just refresh unconditionally on every threshold change.
        _movement.RefreshMovementSpeedModifiers(uid);

        // Update UI
        if (ThirstComponent.ThirstThresholdAlertTypes.TryGetValue(component.CurrentThirstThreshold, out var alertId))
        {
            _alerts.ShowAlert(uid, alertId);
        }
        else
        {
            _alerts.ClearAlertCategory(uid, component.ThirstyCategory);
        }

        DirtyField(uid, component, nameof(ThirstComponent.LastThirstThreshold));
        DirtyField(uid, component, nameof(ThirstComponent.ActualDecayRate));

        switch (component.CurrentThirstThreshold)
        {
            case ThirstThreshold.OverHydrated:
                component.LastThirstThreshold = component.CurrentThirstThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 1.2f;
                return;

            case ThirstThreshold.Okay:
                component.LastThirstThreshold = component.CurrentThirstThreshold;
                component.ActualDecayRate = component.BaseDecayRate;
                return;

            case ThirstThreshold.Thirsty:
                // Same as okay except with UI icon saying drink soon.
                component.LastThirstThreshold = component.CurrentThirstThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 0.8f;
                return;
            case ThirstThreshold.Parched:
                component.LastThirstThreshold = component.CurrentThirstThreshold;
                component.ActualDecayRate = component.BaseDecayRate * 0.6f;
                return;

            case ThirstThreshold.Dead:
                return;

            default:
                Log.Error($"No thirst threshold found for {component.CurrentThirstThreshold}");
                throw new ArgumentOutOfRangeException($"No thirst threshold found for {component.CurrentThirstThreshold}");
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ThirstComponent>();
        while (query.MoveNext(out var uid, out var thirst))
        {
            if (_timing.CurTime < thirst.NextUpdateTime)
                continue;

            thirst.NextUpdateTime += thirst.UpdateRate;

            ModifyThirst(uid, thirst, -thirst.ActualDecayRate);
            UpdateReminderPopup(uid, thirst);
            var calculatedThirstThreshold = GetThirstThreshold(thirst, thirst.CurrentThirst);

            if (calculatedThirstThreshold == thirst.CurrentThirstThreshold)
                continue;

            thirst.CurrentThirstThreshold = calculatedThirstThreshold;
            UpdateEffects(uid, thirst);
        }
    }
}
