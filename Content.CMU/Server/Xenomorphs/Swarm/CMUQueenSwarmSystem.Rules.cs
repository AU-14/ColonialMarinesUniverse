using System.Linq;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Xenomorphs.Swarm;
using Content.Shared.NPC.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private void InitializeRules()
    {
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSwarmRulesActionEvent>(OnRules);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSwarmSetPassiveEvent>(OnSetPassive);
        SubscribeLocalEvent<CMUQueenSwarmComponent, CMUSwarmSetAllyEvent>(OnSetAlly);
    }

    private void OnRules(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmRulesActionEvent args)
    {
        if (args.Handled || !TryController(ent, out var hive, out var swarm))
            return;
        args.Handled = true;
        OpenRules(ent, hive, swarm);
    }

    private void OpenRules(EntityUid queen, EntityUid hive, CMUSwarmControllerComponent swarm)
    {
        var choices = new List<DialogOption>
        {
            new(Loc.GetString(swarm.Passive ? "cmu-swarm-enable-attacks" : "cmu-swarm-disable-attacks"),
                new CMUSwarmSetPassiveEvent(GetNetEntity(hive), !swarm.Passive)),
        };
        if (TryComp<HiveComponent>(hive, out var hiveComp) && hiveComp.CanMakeAlliances)
        {
            foreach (var faction in _prototypes.EnumeratePrototypes<NpcFactionPrototype>()
                         .Where(f => !f.Abstract && f.Name != null).OrderBy(f => f.Name))
            {
                var allied = _hive.HasFaction(hive, faction.ID);
                choices.Add(new DialogOption(
                    Loc.GetString(allied ? "cmu-swarm-remove-ally" : "cmu-swarm-add-ally", ("faction", faction.Name!)),
                    new CMUSwarmSetAllyEvent(GetNetEntity(hive), faction.ID, !allied)));
            }
        }
        _dialog.OpenOptions(queen, Loc.GetString("cmu-swarm-menu-rules"), choices,
            Loc.GetString(swarm.Passive ? "cmu-swarm-rules-passive" : "cmu-swarm-rules-automatic"));
    }

    private void OnSetPassive(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmSetPassiveEvent args)
    {
        if (!TryController(ent, out var hive, out var swarm) || GetNetEntity(hive) != args.Hive)
            return;
        swarm.Passive = args.Passive;
        ResetOrder(swarm);
        OpenRules(ent, hive, swarm);
    }

    private void OnSetAlly(Entity<CMUQueenSwarmComponent> ent, ref CMUSwarmSetAllyEvent args)
    {
        if (!TryController(ent, out var hive, out var swarm) || GetNetEntity(hive) != args.Hive ||
            !TryComp<HiveComponent>(hive, out var hiveComp) || !hiveComp.CanMakeAlliances ||
            !_prototypes.TryIndex(args.Faction, out var faction) || faction.Abstract || faction.Name == null)
            return;
        _hive.SetHiveFactionAlly(args.Faction, hive, args.Allied);
        ResetOrder(swarm);
        OpenRules(ent, hive, swarm);
    }
}
