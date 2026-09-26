using System.Numerics;
using System.Linq;
using Content.Client.Lobby;
using Content.Shared.Administration;
using Content.Shared.CMU14.Lobby;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.State;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Client.CMU14.Lobby;

/// <summary>Local preview data only: does not create sessions or change server readiness.</summary>
[AnyCommand]
public sealed partial class LobbyLineupShowcaseCommand : LocalizedCommands
{
    [Dependency] private IStateManager _states = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private DefaultWindow? _window;
    private bool _pendingWindow;
    private bool _pendingDebug;
    private int _pendingCount = 60;
    private LobbyPartyShow? _pendingShow;

    public override string Command => "lobbyshowcase";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = 60;
        var window = false;
        var debug = false;
        LobbyPartyShow? show = null;
        if (args.Length > 4)
        {
            shell.WriteError(Help);
            return;
        }
        foreach (var arg in args)
        {
            if (arg == "window")
                window = true;
            else if (arg == "debug")
                debug = true;
            else if (arg == "flyby")
                show = LobbyPartyShow.Flyby;
            else if (arg == "parade")
                show = LobbyPartyShow.Parade;
            else if (arg == "supply")
                show = LobbyPartyShow.SupplyScramble;
            else if (arg == "off" && args.Length == 1)
                break;
            else if (!int.TryParse(arg, out count) || count is < 1 or > 256)
            {
                shell.WriteError(Help);
                return;
            }
        }

        _window?.Close();
        _window?.Dispose();
        _window = null;
        _states.OnStateChanged -= OnStateChanged;
        var lobby = (_states.CurrentState as LobbyState)?.Lobby;
        if (args.Length == 1 && args[0] == "off")
        {
            lobby?.Lineup.SetShowcase(null);
            return;
        }

        if (_states.CurrentState is not LobbyState)
        {
            _pendingWindow = window;
            _pendingDebug = debug;
            _pendingCount = count;
            _pendingShow = show;
            _states.OnStateChanged += OnStateChanged;
            shell.WriteLine(Loc.GetString("cmu-lobby-lineup-showcase-pending"));
            return;
        }
        Show(window, count, debug, show);
    }

    private void OnStateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is not LobbyState)
            return;
        _states.OnStateChanged -= OnStateChanged;
        Show(_pendingWindow, _pendingCount, _pendingDebug, _pendingShow);
    }

    private void Show(bool window, int count, bool debug, LobbyPartyShow? show)
    {
        var lobby = (_states.CurrentState as LobbyState)?.Lobby;
        var entries = CreateEntries(_prototypes, count);
        if (lobby != null && !window)
        {
            lobby.Lineup.SetShowcase(entries, debug, show);
            return;
        }

        var panel = new LobbyLineupPanel();
        _window = new DefaultWindow
        {
            Title = Loc.GetString("cmu-lobby-lineup-showcase"),
            MinSize = new Vector2(480, 360),
            SetSize = new Vector2(1080, 820),
        };
        _window.Contents.AddChild(panel);
        _window.OpenCentered();
        panel.SetShowcase(entries, debug, show);
    }

    public static List<LobbyLineupEntry> CreateEntries(IPrototypeManager prototypes, int count = 60)
    {
        var entries = new List<LobbyLineupEntry>();
        Add("gov-command", "GOVFOR / Command", "#E3CE94",
            ("AU14JobGOVFORPlatCo", "Elena Voss"), ("AU14JobGOVFORPlatOp", "Marcus Hale"));
        Add("op-command", "OPFOR / Command", "#ED685D",
            ("AU14JobOPFORPlatCo", "Nadia Volkov"), ("AU14JobOPFORPlatOp", "Ivan Petrov"));
        Add("gov-alpha", "GOVFOR / Alpha", "#F0433E",
            ("AU14JobGOVFORSquadSergeant", "Gabriel Reyes"), ("AU14JobGOVFORSquadRifleman", "Juno Park"),
            ("AU14JobGOVFORPlatoonCorpsman", "Amara Okafor"), ("AU14JobGOVFORSquadAutomaticRifleman", "Rafael Costa"));
        Add("gov-bravo", "GOVFOR / Bravo", "#C7B646",
            ("AU14JobGOVFORSquadSergeant", "Maya Chen"), ("AU14JobGOVFORSquadCombatTech", "Jonah Reed"),
            ("AU14JobGOVFORSquadRifleman", "Sofia Anders"));
        Add("op-squad", "OPFOR / Assault squad", "#ED685D",
            ("AU14JobOPFORSquadSergeant", "Viktor Sokolov"), ("AU14JobOPFORSquadRifleman", "Anya Morozova"),
            ("AU14JobOPFORPlatoonCorpsman", "Pavel Orlov"));
        Add("grom", "UPP / GROM", "#93B979",
            ("AU14UPPGROMCommander", "Zofia Kowalska"), ("AU14UPPGROMMachinegunner", "Marek Nowak"));
        Add("support", "GOVFOR / Aircrew & support", "#75B9DD",
            ("AU14JobGOVFORDSPilot", "Theo Ward"), ("AU14JobGOVFORMilitaryDoctor", "Leila Hassan"));
        Add("colony", "COLONY / Civilian personnel", "#B7A1D9",
            ("AU14JobCivilianColonySecurityOfficer", "Alex Mercer"), ("AU14JobCivilianColonist", "Nina Alvarez"));
        var templates = entries.ToArray();
        string[] firstNames = ["Casey", "Rowan", "Morgan", "Jamie", "Sasha", "Robin", "Drew", "Taylor", "Ellis", "Blake"];
        string[] lastNames = ["Navarro", "Kim", "Singh", "Kovac", "Okeke", "Miller", "Tanaka", "Salim", "Ortiz", "Novak"];
        for (var i = entries.Count; i < count && templates.Length > 0; i++)
        {
            var template = templates[i % templates.Length];
            var name = firstNames[i % firstNames.Length] + " " + lastNames[i / firstNames.Length % lastNames.Length];
            var profile = HumanoidCharacterProfile.RandomWithSpecies().WithName(name);
            entries.Add(new LobbyLineupEntry(new NetUserId(Guid.NewGuid()), profile, template.Job, null, null,
                template.SectionId, template.SectionName, template.Color, template.Order));
        }
        return entries.Take(count).ToList();

        void Add(string section, string title, string color, params (string Job, string Name)[] characters)
        {
            foreach (var (jobId, name) in characters)
            {
                if (!prototypes.TryIndex<JobPrototype>(jobId, out var job))
                    continue;
                var profile = HumanoidCharacterProfile.RandomWithSpecies().WithName(name);
                entries.Add(new LobbyLineupEntry(new NetUserId(Guid.NewGuid()), profile, job.ID, null, null,
                    section, title, Color.FromHex(color), entries.Count));
            }
        }
    }
}
