using Content.Server.CMU14.Round;
using Content.Server.CMU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Shared.CMU14.util;
using Content.Shared.Paper;
using CLFSleeperAgentComponent = Content.Shared.CMU14.Round.Antags.CLFSleeperAgent.CLFSleeperAgentComponent;
using CLFSleeperAgentRuleComponent = Content.Shared.CMU14.Round.Antags.CLFSleeperAgent.CLFSleeperAgentRuleComponent;
using Content.Server.CMU14.Round.Antags.ColonyBounty;

namespace Content.Server.CMU14.Round.Antags.CLFSleeperAgent;

public sealed partial class CLFSleeperAgentRuleSystem : GameRuleSystem<CLFSleeperAgentRuleComponent>
{
    // matches IntelConsoleClaimSystem's group naming; "govfor" never had a fax machine
    private const string ClfFaxGroup = "clf";
    private const string ClfPaperPrototype = "CMUPaperCLF";
    // The advisory fans out to every command surface so burning one copy cannot bury it.
    private static readonly string[] AdvisoryFaxGroups =
    {
        "military-command",
        "marshal-bureau",
        "colony-command",
        "warship-command",
    };

    [Dependency] private WantedSystem _wantedSystem = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawn = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CLFSleeperAgentComponent, ComponentStartup>(OnSleeperSpawned);
    }

    private void OnSleeperSpawned(EntityUid uid, CLFSleeperAgentComponent comp, ComponentStartup args)
    {
        _wantedSystem.SendFaxToGroup(
            ClfFaxGroup,
            "Operational Briefing",
            BuildClfFax(),
            "paper_stamp-clf",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#2e5a1e"), StampedName = "CLF" }
            }, ClfPaperPrototype);

        // Platoon voting means govfor is not always USCM; the advisory must wear
        // the deployed platoon's own letterhead and intelligence branch.
        var sender = AdvisorySender(_platoonSpawn.SelectedGovforPlatoon);
        foreach (var group in AdvisoryFaxGroups)
        {
            _wantedSystem.SendFaxToGroup(
                group,
                "Security Advisory",
                BuildGovforFax(sender.Branch),
                "paper_stamp-centcom",
                new List<StampDisplayInfo>
                {
                    new() { StampedColor = Color.FromHex("#1a3a6e"), StampedName = "INTEL" }
                }, sender.Paper);
        }
    }

    private string BuildClfFax()
    {
        return "[head=3][color=#2e5a1e]Colony Liberation Front[/color][/head]\n\n" +
               "[color=#2e5a1e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
               "[bold]To:[/bold] [italic]CLF Field Operatives[/italic]\n" +
               "[bold]From:[/bold] [bold]CLF High Command[/bold]\n" +
               "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
               "Comrades,\n" +
               Loc.GetString("cmu-clf-sleeper-fax-briefing") + "\n\n" +
               "Freedom or death,\n" +
               "[color=#2e5a1e][bolditalic]CLF High Command[/bolditalic][/color]\n" +
               "[color=#2e5a1e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
    }

    private static (string Paper, string Branch) AdvisorySender(PlatoonPrototype? platoon)
        => platoon?.Allegiance?.ToString() switch
        {
            "UA" => ("CMUPaperUSCM", "UA Intelligence Branch"),
            "UPP" => ("CMUPaperUPP", "UPP Committee for State Security"),
            "TWE" => ("CMUPaperTWE", "TWE Royal Intelligence Service"),
            // allegiances without their own letterhead get a neutral sender
            _ => ("CMPaper", "Command Intelligence"),
        };

    private string BuildGovforFax(string intelBranch)
    {
        return "[head=3][color=#1a3a6e]Intelligence Advisory | CONFIDENTIAL[/color][/head]\n\n" +
               "[color=#1a3a6e]▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄▄[/color]\n\n" +
               "[bold]To:[/bold] [italic]Platoon Commander[/italic]\n" +
               $"[bold]From:[/bold] [bold]{intelBranch}[/bold]\n" +
               "[color=#1a3a6e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]\n" +
               "Commander,\n" +
               Loc.GetString("cmu-clf-sleeper-fax-advisory") + "\n\n" +
               "Signed,\n" +
               $"[color=#1a3a6e][bolditalic]{intelBranch}[/bolditalic][/color]\n" +
               "[color=#1a3a6e]‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾[/color]";
    }
}
