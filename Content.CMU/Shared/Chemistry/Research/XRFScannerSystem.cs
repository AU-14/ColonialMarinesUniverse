using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.DoAfter;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

public sealed partial class XRFScannerSystem : EntitySystem
{
    [Dependency] private SharedResearchDataTerminalSystem _data = default!;
    [Dependency] private SharedPopupSystem _popups = default!;
    [Dependency] private SharedContainerSystem _consys = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedDoAfterSystem _doafter = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    private int _sample = 1;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XRFScannerComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<XRFScannerComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<XRFScannerComponent, XRFDoAfterEvent>(WhenDoAfterEnds);
    }



    public void OnInteractUsing(Entity<XRFScannerComponent> ent, ref InteractUsingEvent args)
    {
        if (ent.Comp.Processing)
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-processing"), args.User);
            return;
        }
        //TODO: skillcheck here

        _consys.TryGetContainer(ent.Owner, "sample", out var sample);
        if (sample is null)
            return;
        if (!TryComp<VialComponent>(args.Used, out var vial))
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-only-vials"), args.User);
            return;
        }
        if (sample.ContainedEntities.Count > 0)
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-full"), args.User);
            return;
        }
        if (_consys.Insert(args.Used, sample))
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-config"), args.User);
            var dargs = new DoAfterArgs(EntityManager, args.User, 1, new XRFDoAfterEvent(), args.Target, args.Target, args.Target)
            {
                BlockDuplicate = true,
                CancelDuplicate = true,
                DuplicateCondition = DuplicateConditions.All,
                BreakOnMove = true,
                BreakOnDamage = true,
                BreakOnRest = true
            };
            _doafter.TryStartDoAfter(dargs);
        }

    }
    public void WhenDoAfterEnds(Entity<XRFScannerComponent> ent, ref XRFDoAfterEvent args)
    {
        _consys.TryGetContainer(ent.Owner, "sample", out var sample);
        if (sample is null)
            return;
        if (sample.Count == 0)
        {
            _popups.PopupEntity("research-xrf-scanner-conflict", ent.Owner);
            return;
        }
        ent.Comp.Processing = true;
        Timer.Spawn(ent.Comp.Inefficiency, () =>
        {
            if (sample.Count == 0)
            {
                PrintResult(ent, false, Loc.GetString("research-xrf-sample-missing"));
                _sample++;
                return;
            }
            if (_solutions.TryGetSolution(sample.ContainedEntities[0], null, out var solution) &&
            (solution is null || solution.Value.Comp.Solution.Volume == FixedPoint2.Zero))
            {
                PrintResult(ent, false, Loc.GetString("research-xrf-sample-empty"));
                _sample++;
                return;
            }
            if (solution is null)
                return;
            if (solution.Value.Comp.Solution.Volume < 30)
            {
                PrintResult(ent, false, Loc.GetString("research-xrf-sample-insufficient"));
                _sample++;
                return;
            }
        });
    }

    public void OnInteractHand(Entity<XRFScannerComponent> ent, ref InteractHandEvent args)
    {
        if (ent.Comp.Processing)
        {
            _popups.PopupClient(Loc.GetString("research-xrf-scanner-processing"), args.User);
        }
        if (_consys.TryGetContainer(ent.Owner, "sample", out var container))
        {
            if (container.Count == 0)
            {
                _popups.PopupClient(Loc.GetString("research-xrf-scanner-empty"), args.User);
            }
            else
            {
                //_consys.Remove(container.ContainedEntities[0], container);
                _hands.PickupOrDrop(args.User, container.ContainedEntities[0]);
            }
        }
    }

    public void PrintResult(Entity<XRFScannerComponent> ent, bool result, string reason)
    {

    }
}
