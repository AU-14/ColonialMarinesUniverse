using Content.Shared._AU14.Chemistry.Reagents;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Shared._AU14.Chemistry.Research;

public abstract partial class SharedResearchDataTerminalSystem : EntitySystem
{
    public int Clearance = 1; //6 is "X" clearance
    public int Credits = 0;
    public bool DDIDiscovered = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<UpdateDataTerminalClearanceEvent>(OnUpdateClearance);
    }


    private void OnUpdateClearance(UpdateDataTerminalClearanceEvent args)
    {
        if(args.Clearance != -1)
        {
            Clearance = args.Clearance;
        }
        if (args.Credits != 0)
        {
            Credits += args.Credits;
        }
    }


}
