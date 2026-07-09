using Content.Shared._AU14.Chemistry.Reagents;
using Content.Shared.GameTicking;
using Robust.Shared.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

public abstract partial class SharedResearchDataTerminalSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private INetManager _net = default!;


    [ViewVariables(VVAccess.ReadOnly)]
    public int Clearance = 1; //6 is "X" clearance
    [ViewVariables(VVAccess.ReadOnly)]
    public int Credits = 0;
    [ViewVariables(VVAccess.ReadOnly)]
    public bool DDIDiscovered = false;

    protected readonly int _researchLevelIncreaseMult = 3;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<UpdateDataTerminalClearanceEvent>(OnUpdateClearance);
    }

    public void UpdateClearance(int points, int clearance)
    {
        var ev = new UpdateDataTerminalClearanceEvent(clearance, points);
        RaiseLocalEvent(ev);
        RaiseNetworkEvent(ev);
    }


    private void OnUpdateClearance(UpdateDataTerminalClearanceEvent args)
    {
        if(args.Clearance != -1)
        {
            Clearance = args.Clearance;
        }
        Credits = args.Credits;
    }
}
