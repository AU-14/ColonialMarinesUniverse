using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Tools;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.DoAfter;
using Content.Shared.Doors.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Dropship.MultiDeck;

public sealed partial class MohawkSystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly ProtoId<JobPrototype> QueenRole = "CMXenoQueen";
    private static readonly EntProtoId<SkillDefinitionComponent> EngineerSkill = "RMCSkillEngineer";
    private static readonly EntProtoId<SkillDefinitionComponent> PilotSkill = "RMCSkillPilot";

    private void InitializeControls()
    {
        SubscribeLocalEvent<MohawkControlComponent, GettingAttackedAttemptEvent>(OnControlAttacked);
        SubscribeLocalEvent<MohawkControlComponent, InteractUsingEvent>(OnRepairControls);
        SubscribeLocalEvent<MohawkControlComponent, MohawkSabotageDoAfterEvent>(OnSabotaged);
        SubscribeLocalEvent<MohawkControlComponent, MohawkRepairDoAfterEvent>(OnRepaired);
    }

    private void OnControlAttacked(Entity<MohawkControlComponent> control, ref GettingAttackedAttemptEvent args)
        => TrySabotage(control, args.Attacker);

    private void TrySabotage(Entity<MohawkControlComponent> control, EntityUid user)
    {
        if (control.Comp.Group == MohawkControlGroup.Hatch ||
            !TryComp<XenoComponent>(user, out var xeno) || xeno.Role != QueenRole ||
            !_dropships.TryGetGridDropship(control, out var ship) || !CanDeploy(ship) ||
            !TryComp<MohawkMechanismsComponent>(ship, out var state) ||
            state.BrokenControls.Contains(control.Comp.Group))
            return;

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(5),
            new MohawkSabotageDoAfterEvent(), control, target: control)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnSabotaged(Entity<MohawkControlComponent> control, ref MohawkSabotageDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_dropships.TryGetGridDropship(control, out var ship) ||
            !CanDeploy(ship) || !TryComp<MohawkMechanismsComponent>(ship, out var state))
            return;

        args.Handled = true;
        OpenGroup(ship, control.Comp.Group);
        state.BrokenControls.Add(control.Comp.Group);
        _popup.PopupEntity(Loc.GetString("cmu-mohawk-controls-sabotaged"), control);
    }

    private void OnRepairControls(Entity<MohawkControlComponent> control, ref InteractUsingEvent args)
    {
        if (args.Handled || !HasComp<MultitoolComponent>(args.Used) ||
            !_dropships.TryGetGridDropship(control, out var ship) ||
            !TryComp<MohawkMechanismsComponent>(ship, out var state) ||
            !state.BrokenControls.Contains(control.Comp.Group))
            return;

        args.Handled = true;
        var engineer = _skills.HasSkill(args.User, EngineerSkill, 2);
        if (!engineer && !_skills.HasSkill(args.User, PilotSkill, 1))
        {
            _popup.PopupEntity(Loc.GetString("cmu-mohawk-controls-unskilled"), control, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(engineer ? 5 : 8),
            new MohawkRepairDoAfterEvent(), control, target: control, used: args.Used)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        });
    }

    private void OnRepaired(Entity<MohawkControlComponent> control, ref MohawkRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_dropships.TryGetGridDropship(control, out var ship) ||
            !TryComp<MohawkMechanismsComponent>(ship, out var state))
            return;

        args.Handled = true;
        state.BrokenControls.Remove(control.Comp.Group);
        _popup.PopupEntity(Loc.GetString("cmu-mohawk-controls-repaired"), control);
    }

    private void OpenGroup(EntityUid ship, MohawkControlGroup group)
    {
        if (group == MohawkControlGroup.Ramp)
        {
            SetRampDeployed(ship, true);
            return;
        }
        var location = group == MohawkControlGroup.Port ? DoorLocation.Port : DoorLocation.Starboard;
        foreach (var uid in GetShipEntities(ship))
        {
            if (!TryComp<DoorComponent>(uid, out var door) || door.Location != location)
                continue;
            if (TryComp<DoorBoltComponent>(uid, out var bolts))
                _doors.SetBoltsDown((uid, bolts), false);
            _doors.TryOpen(uid, door);
        }
    }
}
