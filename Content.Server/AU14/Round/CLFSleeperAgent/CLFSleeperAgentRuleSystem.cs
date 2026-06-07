using Content.Server.AU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared.AU14.Round.CLFSleeperAgent;
using Content.Shared.Paper;
using Robust.Shared.Maths;

namespace Content.Server.AU14.Round.CLFSleeperAgent;

public sealed partial class CLFSleeperAgentRuleSystem : GameRuleSystem<CLFSleeperAgentRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFSleeperAgentComponent, ComponentStartup>(OnSleeperSpawned);
    }

    private void OnSleeperSpawned(EntityUid uid, CLFSleeperAgentComponent comp, ComponentStartup args)
    {
        _wantedSystem.SendCustomFax(
            "Colony Liberation Front",
            "Operational Briefing",
            BuildClfFax(),
            "paper_stamp-clf",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" }
            });

        _wantedSystem.SendCustomFax(
            "govfor",
            "Security Advisory",
            BuildGovforFax(),
            "paper_stamp-centcom",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#1a3a6e"), StampedName = "INTEL" }
            });
    }

    private static string BuildClfFax()
    {
        return "[head=3][color=#2e5a1e]Colony Liberation Front[/color][/head]\n\n" +
               "[color=#2e5a1e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
               "[bold]To:[/bold] [italic]CLF Field Operatives[/italic]\n" +
               "[bold]From:[/bold] [bold]CLF High Command[/bold]\n" +
               "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
               "Comrades,\n" +
               "  Intelligence confirms a sleeper operative has been embedded within the Government Forces " +
               "unit currently deployed to this colony. They are operating under deep cover in a leadership " +
               "position. Do not attempt direct contact — support their efforts by maintaining pressure " +
               "on the occupiers, and do not compromise their identity.\n\n" +
               "Freedom or death,\n" +
               "[color=#2e5a1e][bolditalic]CLF High Command[/bolditalic][/color]\n" +
               "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
    }

    private static string BuildGovforFax()
    {
        return "[head=3][color=#1a3a6e]Intelligence Advisory — CONFIDENTIAL[/color][/head]\n\n" +
               "[color=#1a3a6e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
               "[bold]To:[/bold] [italic]Platoon Commander[/italic]\n" +
               "[bold]From:[/bold] [bold]UA Intelligence Branch[/bold]\n" +
               "[color=#1a3a6e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
               "Commander,\n" +
               "  Pre-deployment intelligence suggests CLF operatives may have infiltrated your unit " +
               "prior to embarkation. Exercise caution with personnel in leadership and security roles. " +
               "Conduct internal security screening at your discretion and report any suspicious activity " +
               "to command. Treat this advisory with the highest confidentiality — do not distribute.\n\n" +
               "Signed,\n" +
               "[color=#1a3a6e][bolditalic]UA Intelligence Branch[/bolditalic][/color]\n" +
               "[color=#1a3a6e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
    }
}
