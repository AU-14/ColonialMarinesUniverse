using Content.Shared._CMU14.CraftIntoPipeBomb;
using Content.Shared._RMC14.Repairable;
using Content.Shared._RMC14.Smokeables;
using Content.Shared._RMC14.Stack;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Prototypes;
using System.Security.Principal;

namespace Content.Shared._CMU14.CraftIntoPipeBomb;

public sealed class CraftIntoPipeBombSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly RMCRepairableSystem _repair = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public static readonly ProtoId<TagPrototype> RMCMetal = "RMCSheetMetal";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CraftIntoPipeBombComponent, InteractUsingEvent>(CraftInteractEvent);
        SubscribeLocalEvent<CraftIntoPipeBombComponent, CraftIntoPipeBombDoAfterEvent>(OnCraftIntoDoAfter);
    }

    private void CraftInteractEvent(Entity<CraftIntoPipeBombComponent> ent, ref InteractUsingEvent args)
    {
        var used = args.Used;

        if (ent.Comp.Blowtorch == true && !HasComp<BlowtorchComponent>(used))
            return;

        if (ent.Comp.Wires == true && !HasComp<RMCCableCoilComponent>(used))
            return;

        if (ent.Comp.Lighter == true && !HasComp<RMCLighterComponent>(used))
            return;

        if (ent.Comp.Blowtorch == true && HasComp<BlowtorchComponent>(used))
            if (!TryComp<StackComponent>(ent, out var stack))
            {
                if (stack == null)
                    return;

                if (_tags.HasTag(ent, RMCMetal))
                    if (stack.Count >= 5)
                    {
                        return;
                    }
            }

        if (ent.Comp.Wires == true && HasComp<RMCCableCoilComponent>(used))
            if (!TryComp<StackComponent>(used, out var stacks))
            {
                if (stacks == null)
                    return;

                if (!HasComp<RMCCableCoilComponent>(used))
                    if (stacks.Count <= 5)
                    {
                        return;
                    }
            }

        var ev = new CraftIntoPipeBombDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, ev, ent, ent, args.Used)
        {
            BreakOnMove = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCraftIntoDoAfter(Entity<CraftIntoPipeBombComponent> ent, ref CraftIntoPipeBombDoAfterEvent args)
    {
        if (_net.IsClient)
            return;

        var used = args.Used;

        if (used == null)
            return;

        if (ent.Comp.Lighter == true && !HasComp<RMCLighterComponent>(used))
            return;

        if (ent.Comp.Blowtorch == true && !HasComp<BlowtorchComponent>(used))
            return;

        if (ent.Comp.Wires == true && !HasComp<RMCCableCoilComponent>(used))
            return;

        // How many if statement? Yes.
        if (ent.Comp.Blowtorch == true && HasComp<BlowtorchComponent>(used))
        {
            var countStack = 0;
            if (TryComp<StackComponent>(ent, out var stack))
            {
                countStack = stack.Count;
            }
            if (countStack >= 5 && _repair.UseFuel(used.Value, args.User, 5))
            {
                _stack.Use(ent, 5, stack);
            }
            else
            {
                return;
            }
        }

        if (ent.Comp.Wires == true && HasComp<RMCCableCoilComponent>(used))
        {
            var countStack = 0;
            if (TryComp<StackComponent>(used, out var stacks))
            {
                countStack = stacks.Count;
            }

            if (HasComp<RMCCableCoilComponent>(used))
                if (countStack >= 5)
                {
                    _stack.Use(used.Value, 5, stacks);
                }
                else
                {
                    return;
                }
        }

        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var coords = _transform.GetMoverCoordinates(ent);
        var crafteditem = Spawn(ent.Comp.Spawn, coords);

        if (ent.Comp.DestroyReqAfterResult == true && !HasComp<StackComponent>(used))
            PredictedQueueDel(used);

        if (!HasComp<StackComponent>(ent))
            Del(ent);

        _hands.TryPickupAnyHand(args.User, crafteditem);
    }
}
