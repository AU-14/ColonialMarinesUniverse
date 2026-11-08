using Content.Shared._RMC14.Xenonids.Strain;
using Content.Shared.CMU14.Xenonids.Strain;
using Content.Shared.Popups;
using Content.Shared.Prototypes;

namespace Content.Shared._RMC14.Xenonids.Evolution;

public sealed partial class XenoEvolutionSystem
{
    private void OnDestrainMapInit(Entity<XenoStrainComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.BaseCaste is not { } baseCaste || HasComp<XenoEvolutionComponent>(ent))
            return;

        // Some strains do not evolve further, but still need the menu and a place to keep their points.
        var evolution = AddComp<XenoEvolutionComponent>(ent);
        if (_prototypes.TryIndex(baseCaste, out var prototype) &&
            prototype.TryComp(out XenoEvolutionComponent? baseEvolution, _compFactory))
        {
            evolution.Max = baseEvolution.Max;
        }

        evolution.PointsPerSecond = 0;
        evolution.EarlyPointsPerSecond = 0;
        Dirty(ent, evolution);
    }

    private bool DestrainCooldownCheckPopup(EntityUid xeno)
    {
        if (!TryComp(xeno, out CMUXenoDestrainCooldownComponent? cooldown) ||
            cooldown.NextDestrainAt <= _timing.CurTime)
        {
            return true;
        }

        var seconds = (int)Math.Ceiling((cooldown.NextDestrainAt - _timing.CurTime).TotalSeconds);
        _popup.PopupEntity(Loc.GetString("cmu-xeno-destrain-cooldown",
            ("minutes", seconds / 60), ("seconds", seconds % 60)), xeno, xeno, PopupType.MediumCaution);
        return false;
    }

    private void StartDestrainCooldown(EntityUid xeno)
    {
        var cooldown = EnsureComp<CMUXenoDestrainCooldownComponent>(xeno);
        cooldown.NextDestrainAt = _timing.CurTime + cooldown.Cooldown;
    }

    private void TransferDestrainCooldown(EntityUid oldXeno, EntityUid newXeno)
    {
        if (!TryComp(oldXeno, out CMUXenoDestrainCooldownComponent? oldCooldown))
            return;

        var newCooldown = EnsureComp<CMUXenoDestrainCooldownComponent>(newXeno);
        newCooldown.Cooldown = oldCooldown.Cooldown;
        newCooldown.NextDestrainAt = oldCooldown.NextDestrainAt;
    }
}
