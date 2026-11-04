using Content.Shared.Alert;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.StatusEffectNew;

/// <summary>
/// Handles displaying status effects that should show an alert, optionally with a duration.
/// </summary>
public sealed partial class StatusEffectAlertSystem : EntitySystem
{
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectAlertComponent, StatusEffectAppliedEvent>(OnStatusEffectApplied);
        SubscribeLocalEvent<StatusEffectAlertComponent, StatusEffectRemovedEvent>(OnStatusEffectRemoved);
        SubscribeLocalEvent<StatusEffectAlertComponent, StatusEffectEndTimeUpdatedEvent>(OnEndTimeUpdated);
    }

    private void OnStatusEffectApplied(Entity<StatusEffectAlertComponent> ent, ref StatusEffectAppliedEvent args)
    {
        RefreshAlert(args.Target, ent.Comp.Alert, ent.Owner);
    }

    private void OnStatusEffectRemoved(Entity<StatusEffectAlertComponent> ent, ref StatusEffectRemovedEvent args)
    {
        RefreshAlert(args.Target, ent.Comp.Alert);
    }

    private void OnEndTimeUpdated(Entity<StatusEffectAlertComponent> ent, ref StatusEffectEndTimeUpdatedEvent args)
    {
        RefreshAlert(args.Target, ent.Comp.Alert);
    }

    private void RefreshAlert(EntityUid target, ProtoId<AlertPrototype> alert, EntityUid? applying = null)
    {
        var found = false;
        TimeSpan? cooldown = TimeSpan.Zero;

        foreach (var effect in _statusEffects.EnumerateStatusEffects<StatusEffectAlertComponent>((target, null)))
        {
            if (effect.Comp2.Alert != alert || (!effect.Comp1.Applied && effect.Owner != applying))
                continue;

            found = true;
            if (!effect.Comp2.ShowDuration || effect.Comp1.EndEffectTime is null)
            {
                cooldown = null;
                break;
            }

            var end = effect.Comp1.EndEffectTime.Value;
            if (cooldown is { } current && end > current)
                cooldown = end;
        }

        if (found)
            _alerts.UpdateAlert(target, alert, cooldown: cooldown);
        else
            _alerts.ClearAlert(target, alert);
    }
}
