using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Xenonids.Punch;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

public sealed partial class CMUWendigoPunchSwingSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CMUWendigoAllySystem _ally = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;

    private readonly List<EntityUid> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUWendigoPunchSwingComponent, CMUWendigoPunchSwingActionEvent>(OnToggleAction);

        // The Wendigo is its own melee weapon, so its MeleeHitEvent is raised on itself.
        SubscribeLocalEvent<CMUWendigoPunchSwingComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnToggleAction(Entity<CMUWendigoPunchSwingComponent> ent, ref CMUWendigoPunchSwingActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetEnabled(ent, !ent.Comp.Enabled);

        var msg = ent.Comp.Enabled ? "cmu-wendigo-punch-swing-on" : "cmu-wendigo-punch-swing-off";
        _popup.PopupEntity(Loc.GetString(msg), ent.Owner, ent.Owner);
    }

    public void SetEnabled(Entity<CMUWendigoPunchSwingComponent> ent, bool enabled)
    {
        ent.Comp.Enabled = enabled;
        Dirty(ent);

        foreach (var action in _rmcActions.GetActionsWithEvent<CMUWendigoPunchSwingActionEvent>(ent))
        {
            _actions.SetToggled(action.AsNullable(), enabled);
        }
    }

    private void OnMeleeHit(Entity<CMUWendigoPunchSwingComponent> ent, ref MeleeHitEvent args)
    {
        // Wide swings only; never mark the hit handled so normal melee damage still lands.
        if (!ent.Comp.Enabled || !args.IsHit || args.Direction == null || args.HitEntities.Count == 0)
            return;

        if (args.User != ent.Owner)
            return;

        TryPunchSwing(ent, args.HitEntities);
    }

    /// <summary>
    /// Punches every non-allied living mob in <paramref name="hit"/> when the punch action is off cooldown,
    /// then starts that cooldown. Returns the number of mobs punched.
    /// </summary>
    public int TryPunchSwing(Entity<CMUWendigoPunchSwingComponent> ent, IReadOnlyList<EntityUid> hit)
    {
        if (!HasComp<XenoPunchComponent>(ent))
            return 0;

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoPunchActionEvent>(ent))
        {
            if (_actions.IsCooldownActive(action.Comp))
                return 0;
        }

        _targets.Clear();
        foreach (var target in hit)
        {
            if (target == ent.Owner || _targets.Contains(target))
                continue;

            if (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target))
                continue;

            if (_ally.IsWendigoAlly(ent, target))
                continue;

            // MeleeHitEvent fires before melee's own per-target attack filter, so apply it here.
            if (!_actionBlocker.CanAttack(ent, target))
                continue;

            _targets.Add(target);
        }

        if (_targets.Count == 0)
            return 0;

        var punched = 0;
        foreach (var target in _targets)
        {
            if (TerminatingOrDeleted(target))
                continue;

            var punch = new XenoPunchActionEvent
            {
                Target = target,
                Performer = ent,
            };
            RaiseLocalEvent(ent, punch);

            if (punch.Handled)
                punched++;
        }

        if (punched == 0)
            return 0;

        foreach (var action in _rmcActions.GetActionsWithEvent<XenoPunchActionEvent>(ent))
        {
            _actions.StartUseDelay(action.AsNullable());
        }

        return punched;
    }
}
