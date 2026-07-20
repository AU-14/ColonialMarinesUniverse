using Content.Shared._RMC14.Actions;
using Content.Shared.Actions;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;
using CurrentStatusEffectAppliedEvent = Content.Shared.StatusEffectNew.StatusEffectAppliedEvent;
using CurrentStatusEffectRemovedEvent = Content.Shared.StatusEffectNew.StatusEffectRemovedEvent;
using CurrentStatusEffectsSystem = Content.Shared.StatusEffectNew.StatusEffectsSystem;
using LegacyStatusEffectsComponent = Content.Shared.StatusEffect.StatusEffectsComponent;

namespace Content.Shared._RMC14.Stun;

public sealed partial class RMCDazedSystem : EntitySystem
{
    [Dependency] private SharedChargesSystem _charges = default!;
    [Dependency] private CurrentStatusEffectsSystem _statusEffect = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedStutteringSystem _stutter = default!;

    public static readonly EntProtoId StatusEffectDazed = "Dazed";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCDazedComponent, CurrentStatusEffectAppliedEvent>(OnDazed);
        SubscribeLocalEvent<RMCDazedComponent, CurrentStatusEffectRemovedEvent>(OnDazedEnd);
    }

    /// <summary>
    ///     Put actions with the RMCDazeableActionComponent on cooldown for the given duration, only if the current
    ///     cooldown isn't higher already.
    /// </summary>
    /// <seealso cref="RMCDazeableActionComponent"/>
    private void OnDazed(Entity<RMCDazedComponent> ent, ref CurrentStatusEffectAppliedEvent args)
    {
        foreach (var (actionId, _) in _actions.GetActions(ent))
        {
            if (TryComp(actionId, out RMCDazeableActionComponent? _))
            {
                _actions.SetEnabled(actionId, false);

                if (HasComp<LimitedChargesComponent>(actionId))
                    _charges.SetCharges(actionId, 0);
            }
        }
    }

    private void OnDazedEnd(Entity<RMCDazedComponent> ent, ref CurrentStatusEffectRemovedEvent args)
    {
        foreach (var (actionId, _) in _actions.GetActions(ent))
        {
            if (TryComp(actionId, out RMCDazeableActionComponent? _))
            {
                _actions.SetEnabled(actionId, true);
                _charges.ResetCharges(actionId);
            }
        }
    }

    public bool TryDaze(EntityUid uid, TimeSpan time, bool refresh = false, LegacyStatusEffectsComponent? status = null, bool stutter = false)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (time <= TimeSpan.Zero)
            return false;

        var appliedEffect = false;
        if (refresh)
        {
            _statusEffect.TryUpdateStatusEffectDuration(uid, StatusEffectDazed, time);
            appliedEffect = true;
        }
        else if (_statusEffect.TryAddStatusEffectDuration(uid, StatusEffectDazed, time))
            appliedEffect = true;

        if (appliedEffect && stutter)
            _stutter.DoStutter(uid, time, true, status);

        return false;
    }
}
