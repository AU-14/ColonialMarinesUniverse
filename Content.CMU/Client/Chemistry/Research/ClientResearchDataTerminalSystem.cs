using Content.Shared.CMU14.Chemistry.Reagents;
using Content.Shared.CMU14.Chemistry.Research;
using Content.Shared.GameTicking;
using System;
using System.Collections.Generic;
using System.Text;

namespace Content.Client.CMU14.Chemistry.Research;

public sealed partial class ClientResearchDataTerminalSystem : SharedResearchDataTerminalSystem
{
    public List<GeneratedReagentData> Choices = [];
    public TimeSpan NextReroll = TimeSpan.MaxValue;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<UpdateResearchConsoleEvent>(OnConsoleUpdate);
    }

    private void OnConsoleUpdate(UpdateResearchConsoleEvent args)
    {
        NextReroll = args.NextUpdate;
        Choices = args.Reagents;
    }
}
