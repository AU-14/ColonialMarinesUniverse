using Content.Client._RMC14.SupplyDrop;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client._AU14.Chemistry.Research;

[UsedImplicitly]
public sealed partial class ResearchDataTerminalBui(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ResearchDataTerminalWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ResearchDataTerminalWindow>();
        Refresh();
    }

    public void Refresh()
    {

    }
}
