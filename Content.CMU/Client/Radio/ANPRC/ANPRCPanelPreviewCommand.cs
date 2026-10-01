using System.Linq;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio;
using Robust.Client.UserInterface;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Opens the AN/PRC-117G panel on MOCK state, for looking at the layout without staging a
///     round. The window is not bound to any radio: its buttons go nowhere. Pairs with
///     <c>--cvar cmu.startup_command="anprc_panel_preview ready"</c> for screenshots.
/// </summary>
public sealed partial class ANPRCPanelPreviewCommand : IConsoleCommand
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public string Command => "anprc_panel_preview";
    public string Description => "Opens the AN/PRC-117G panel on mock state (layout preview, not bound to a radio).";
    public string Help => "anprc_panel_preview <fresh|ready|trouble|search> [intro] [expert] [nets|log|security|search|settings]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var scenario = args.Length > 0 ? args[0].ToLowerInvariant() : "ready";

        // flags only for the look of the preview; they do change the player's saved choice
        var config = IoCManager.Resolve<Robust.Shared.Configuration.IConfigurationManager>();
        config.SetCVar(Content.Shared.CMU14.CCVar.AU14CCVars.AnprcIntroSeen, !args.Contains("intro"));
        config.SetCVar(Content.Shared.CMU14.CCVar.AU14CCVars.AnprcExpertView, args.Contains("expert"));

        var window = new ANPRCRadioWindow();
        window.OpenCentered();
        window.UpdateState(Build(scenario));

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "log": window.OpenPage(ANPRCPage.Log); break;
                case "security": window.OpenPage(ANPRCPage.Security); break;
                case "search": window.OpenPage(ANPRCPage.Search); break;
                case "settings": window.OpenPage(ANPRCPage.Settings); break;
                case "nets": window.OpenPage(ANPRCPage.Nets); break;
            }
        }
    }

    private ANPRCRadioState Build(string scenario)
    {
        var frequencies = _prototypes.EnumeratePrototypes<RadioChannelPrototype>()
            .Where(proto => proto.Faction == "govfor" && proto.Frequency != RadioFrequency.Off)
            .ToDictionary(proto => proto.ID, proto => proto.Frequency);

        var standard = new List<ANPRCStandardNet>
        {
            new("SQUAD", "radioGovforAlpha", true),
            new("CMD", "radioGovforCommand", false),
        };

        var presets = new Dictionary<int, ProtoId<RadioChannelPrototype>>();
        var labels = new Dictionary<int, string>();
        var log = new List<ANPRCNetLogEntry>();
        var contacts = new List<ANPRCSweepContact>();

        var enabled = true;
        var worn = true;
        var active = -1;
        var mode = RadioMode.FrequencyHopping;
        var monitor = false;
        var sweep = false;
        var fill = "govfor";
        var relayed = new List<ProtoId<RadioChannelPrototype>>();
        var link = -1f;

        switch (scenario)
        {
            case "fresh":
                // what a traffic operator spawns with: pack on, set off, nothing in memory
                enabled = false;
                break;

            case "trouble":
                labels[0] = "CMD";
                presets[0] = "radioGovforCommand";
                labels[1] = "P2";
                active = 0;
                monitor = true;
                fill = string.Empty;
                relayed.Add("radioGovforCommand");
                link = 1f;
                break;

            case "search":
                labels[0] = "SQUAD";
                presets[0] = "radioGovforAlpha";
                labels[1] = "CMD";
                presets[1] = "radioGovforCommand";
                active = 0;
                sweep = true;
                contacts.Add(new ANPRCSweepContact(RadioFrequency.FromKilohertz(147_900), 2.1f, true, "OPFOR Command", 4, 4, false));
                contacts.Add(new ANPRCSweepContact(RadioFrequency.FromKilohertz(160_000), 0.8f, false, string.Empty, 2, 4, false));
                contacts.Add(new ANPRCSweepContact(RadioFrequency.FromKilohertz(250_200), 1f, true, "Alpha", 4, 4, true));
                break;

            default:
                labels[0] = "SQUAD";
                presets[0] = "radioGovforAlpha";
                labels[1] = "CMD";
                presets[1] = "radioGovforCommand";
                labels[2] = "JTAC";
                presets[2] = "radioGovforJTAC";
                active = 0;
                relayed.AddRange(presets.Values);
                link = 1f;
                break;
        }

        log.Add(new ANPRCNetLogEntry(612f, "HAVOC 6", "Command", "ALL STATIONS, THIS IS HAVOC 6, RADIO CHECK, OVER."));
        log.Add(new ANPRCNetLogEntry(640f, "RED 1-1", "Alpha", "Contact north, two hostiles moving east along the treeline, over."));
        log.Add(new ANPRCNetLogEntry(702f, "UNKNOWN STATION", "147.900 MHz", "Volk actual, hold the bridge until relieved.", true));

        return new ANPRCRadioState(
            presets,
            new Dictionary<int, RadioFrequency>(),
            labels,
            active,
            enabled,
            worn,
            monitor,
            mode,
            false,
            3,
            RadioTxPower.Medium,
            false,
            string.Empty,
            "RED ROMEO",
            new List<string> { "PLT ACTUAL", "PLT MAIN", "SUNRAY" },
            fill.Length > 0 ? "GOV-3" : string.Empty,
            fill,
            false,
            "govfor",
            log,
            0.72f,
            true,
            "WHIP",
            frequencies,
            sweep,
            RadioFrequency.FromKilohertz(183_400),
            contacts,
            new ANPRCPanelInfo(standard, relayed.Count > 0, relayed, 30f, 45f, link, true, true, false, TimeSpan.Zero, TimeSpan.Zero),
            new ANPRCExpertState
            {
                CarrierName = "RED ROMEO",
                CarrierBearingDegrees = 47f,
                CarrierDistance = 12f,
                DrawPerSecond = 3f,
                BatteryMinutes = 40f,
            });
    }
}
