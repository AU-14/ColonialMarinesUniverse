using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.CMU14.Round.Antags.EarlyWarning.Combat;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Abilities;

// COUPLING-03: Judgement (Test of Faith). Subscriptions stay in EWJoeCombatSystem.cs.
public sealed partial class EWJoeCombatSystem
{
    #region Judgement

    private void OnJudgement(Entity<EWJoeComponent> ent, ref EWJoeJudgementActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        // ARCH-05: an Exalted Joe judges a wider cone
        var empowered = _liturgy.TryConsumeEmpowered(ent, null, ccBonus: false, "Judgement");
        var hit = Judge(ent, empowered);
        var message = hit == 0
            ? Loc.GetString("cmu-ew-joe-judgement-none")
            : Loc.GetString("cmu-ew-joe-judgement-hit", ("count", hit));
        _popup.PopupEntity(message, ent, ent);

        if (hit > 0)
            _liturgy.AbilityLanded(ent, null);
    }

    /// <summary>
    /// Test of Faith as a cone (balance A-1): every living target within range, inside the cone around
    /// the Joe's facing and in line of sight is flashed. No mutual-facing test. <paramref name="empowered"/>
    /// widens the cone by the Liturgy's multiplier, up to a full circle.
    /// </summary>
    public int Judge(Entity<EWJoeComponent> joe, bool empowered = false)
    {
        var origin = _transform.GetMapCoordinates(joe);
        var facing = _transform.GetWorldRotation(joe);
        var cone = joe.Comp.JudgementConeDegrees;
        if (empowered && TryComp(joe, out EWJoeLiturgyComponent? liturgy))
            cone = Math.Min(360f, cone * liturgy.EmpoweredJudgementConeMultiplier);

        var halfAngle = Angle.FromDegrees(cone / 2f);
        var range = joe.Comp.JudgementRange;

        _targets.Clear();
        _lookup.GetEntitiesInRange(origin, range, _targets, LookupFlags.Uncontained);

        var hit = 0;
        foreach (var target in _targets)
        {
            if (target.Owner == joe.Owner || !_mobState.IsAlive(target, target.Comp))
                continue;

            var toTarget = _transform.GetMapCoordinates(target).Position - origin.Position;
            if (!EWJoeTargetRules.InCone(toTarget, facing, halfAngle))
                continue;

            if (!_examine.InRangeUnOccluded(joe, target, range))
                continue;

            if (_ability.TryFlash(joe, target, judgement: true))
                hit++;
        }

        return hit;
    }

    #endregion
}
