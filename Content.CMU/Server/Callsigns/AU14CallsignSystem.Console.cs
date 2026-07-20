using System.Linq;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared._RMC14.Overwatch;
using Content.Shared.Popups;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Callsigns;

public sealed partial class AU14CallsignSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAU14CallsignConsoleSystem _consoleAccess = default!;

    private void InitializeConsole()
    {
        Subs.BuiEvents<AU14CallsignConsoleComponent>(AU14CallsignConsoleUI.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<AU14CallsignRenameElementMsg>(OnRenameElement);
            subs.Event<AU14CallsignSetSuffixMsg>(OnSetSuffix);
            subs.Event<AU14CallsignCreateGroupMsg>(OnCreateGroup);
            subs.Event<AU14CallsignDeleteGroupMsg>(OnDeleteGroup);
            subs.Event<AU14CallsignAssignGroupMsg>(OnAssignGroup);
        });

        // overwatch laptops carry the directory too, the comms tab under fireteams
        // opens it without hunting down a standalone terminal
        Subs.BuiEvents<AU14CallsignConsoleComponent>(OverwatchConsoleUI.Key, subs =>
        {
            subs.Event<AU14CallsignOpenDirectoryMsg>(OnOpenDirectory);
        });

        // standalone directory terminals open on left click through ActivatableUI. that
        // refusal has to be predicted, so it lives in SharedAU14CallsignConsoleSystem -
        // subscribing here as well would double up the handler on the server
        SubscribeLocalEvent<AU14CallsignConsoleComponent, BoundUIClosedEvent>(OnConsoleClosed);
    }

    // the roster lives in the terminal's stored BUI state, and UserInterfaceComponent
    // replicates States to every client holding the entity in PVS - not just the ones
    // with it open. leaving the last roster parked there means an enemy standing near
    // the terminal has it on their client without ever touching it, so blank the state
    // once nobody is looking at it
    private void OnConsoleClosed(Entity<AU14CallsignConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(AU14CallsignConsoleUI.Key))
            return;

        if (_ui.IsUiOpen(ent.Owner, AU14CallsignConsoleUI.Key))
            return;

        _ui.SetUiState(
            ent.Owner,
            AU14CallsignConsoleUI.Key,
            new AU14CallsignConsoleState(ent.Comp.Faction, new List<AU14CallsignConsoleElement>(), new List<string>()));
    }

    private void OnOpenDirectory(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignOpenDirectoryMsg args)
    {
        if (!CanView(ent, args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-wrong-faction"), ent.Owner, args.Actor);
            return;
        }

        _ui.TryOpenUi(ent.Owner, AU14CallsignConsoleUI.Key, args.Actor);
    }

    // every open path funnels through here, so this is the gate that actually holds:
    // the roster is only ever pushed after the viewer clears CanView
    private void OnConsoleOpened(Entity<AU14CallsignConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!CanView(ent, args.Actor))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-wrong-faction"), ent.Owner, args.Actor);
            _ui.CloseUi(ent.Owner, AU14CallsignConsoleUI.Key, args.Actor);
            return;
        }

        UpdateConsoleState(ent);
    }

    private void OnRenameElement(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignRenameElementMsg args)
    {
        if (!CanEdit(ent, args.Actor))
            return;

        var word = SanitizeCallsignPart(args.Word, AU14Callsigns.MaxWordLength);

        if (string.IsNullOrWhiteSpace(word))
            return;

        if (args.Squad is { } netSquad)
        {
            if (!TryGetEntity(netSquad, out var squad) || !HasComp<SquadTeamComponent>(squad))
                return;

            _squadWords[squad.Value] = word;
        }
        else if (args.Category is { } category)
        {
            if (!ConsoleCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
                return;

            _categoryWords[(ent.Comp.Faction, category.ToUpperInvariant())] = word;
        }
        else
        {
            _commandWords[ent.Comp.Faction] = word;
        }

        RefreshFaction(ent.Comp.Faction);
    }

    private void OnSetSuffix(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignSetSuffixMsg args)
    {
        if (!CanEdit(ent, args.Actor))
            return;

        if (!TryGetEntity(args.Member, out var member) ||
            !TryComp(member, out AU14CallsignComponent? callsign) ||
            callsign.Faction != ent.Comp.Faction)
        {
            return;
        }

        var suffix = SanitizeCallsignPart(args.Suffix, AU14Callsigns.MaxSuffixLength);

        if (string.IsNullOrWhiteSpace(suffix))
            return;

        if (SuffixTaken(callsign.Faction, callsign.Squad, callsign.Group, callsign.Category, suffix, member.Value))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-suffix-taken", ("suffix", suffix)), ent.Owner, args.Actor);
            return;
        }

        callsign.Suffix = suffix;
        callsign.RoleSuffix = true;

        UpdateFullCallsign(member.Value, callsign);
    }

    private void OnCreateGroup(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignCreateGroupMsg args)
    {
        if (!CanEdit(ent, args.Actor))
            return;

        var word = SanitizeCallsignPart(args.Word, AU14Callsigns.MaxWordLength);

        if (string.IsNullOrWhiteSpace(word) || WordInUse(ent.Comp.Faction, word))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-group-taken", ("word", word)), ent.Owner, args.Actor);
            return;
        }

        _groups.GetOrNew(ent.Comp.Faction).Add(word);

        PushConsoleStates(ent.Comp.Faction);
    }

    private void OnDeleteGroup(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignDeleteGroupMsg args)
    {
        if (!CanEdit(ent, args.Actor))
            return;

        if (!_groups.TryGetValue(ent.Comp.Faction, out var groups) ||
            !groups.Remove(args.Word))
        {
            return;
        }

        // members fall back to their automatic squad/command callsign
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var callsign))
        {
            if (callsign.Faction != ent.Comp.Faction ||
                !string.Equals(callsign.Group, args.Word, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            callsign.Group = null;
            Assign(uid, callsign);
        }

        PushConsoleStates(ent.Comp.Faction);
    }

    private void OnAssignGroup(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignAssignGroupMsg args)
    {
        if (!CanEdit(ent, args.Actor))
            return;

        if (!TryGetEntity(args.Member, out var member) ||
            !TryComp(member, out AU14CallsignComponent? callsign) ||
            callsign.Faction != ent.Comp.Faction)
        {
            return;
        }

        if (args.Group is { } group)
        {
            if (!_groups.TryGetValue(ent.Comp.Faction, out var groups) || !groups.Contains(group))
                return;

            callsign.Group = group;
        }
        else
        {
            callsign.Group = null;
        }

        Assign(member.Value, callsign);
    }

    private bool WordInUse(string faction, string word)
    {
        if (_groups.TryGetValue(faction, out var groups) &&
            groups.Contains(word, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(GetCommandWord(faction), word, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var category in ConsoleCategories)
        {
            if (string.Equals(GetCategoryWord(faction, category), word, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var group = faction.ToUpperInvariant();
        var squads = EntityQueryEnumerator<SquadTeamComponent>();

        while (squads.MoveNext(out var squadUid, out var team))
        {
            if (team.Group == group &&
                string.Equals(GetSquadWord(squadUid), word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private bool CanEdit(Entity<AU14CallsignConsoleComponent> ent, EntityUid actor)
    {
        // viewing terminals never accept edits, no matter who is asking
        if (ent.Comp.ReadOnly)
            return false;

        if (!HasComp<ANPRCRadioUserComponent>(actor))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-not-authorized"), actor, actor);
            return false;
        }

        if (_consoleAccess.GetActorFaction(actor) != ent.Comp.Faction)
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-wrong-faction"), actor, actor);
            return false;
        }

        return true;
    }

    private bool CanView(Entity<AU14CallsignConsoleComponent> ent, EntityUid actor)
    {
        return _consoleAccess.CanView(ent, actor);
    }

    private void RefreshFaction(string faction)
    {
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var callsign))
        {
            if (callsign.Faction != faction)
                continue;

            callsign.Callsign = $"{GetElementWord(callsign)} {callsign.Suffix}";
        }

        PushConsoleStates(faction);
    }

    private void PushConsoleStates(string faction)
    {
        var query = EntityQueryEnumerator<AU14CallsignConsoleComponent>();

        while (query.MoveNext(out var uid, out var console))
        {
            if (console.Faction != faction)
                continue;

            if (_ui.IsUiOpen(uid, AU14CallsignConsoleUI.Key))
                UpdateConsoleState((uid, console));
        }
    }

    private void UpdateConsoleState(Entity<AU14CallsignConsoleComponent> ent)
    {
        var faction = ent.Comp.Faction;
        var group = faction.ToUpperInvariant();

        // command element first, then role sections, then the squads, then the
        // custom groups
        var elements = new List<AU14CallsignConsoleElement>
        {
            new(null,
                null,
                null,
                Loc.GetString("au14-callsign-console-command-element"),
                GetCommandWord(faction),
                CollectRows(faction, null, null, null)),
        };

        foreach (var category in ConsoleCategories)
        {
            var rows = CollectRows(faction, null, null, category);

            if (rows.Count == 0)
                continue;

            elements.Add(new AU14CallsignConsoleElement(
                null,
                null,
                category,
                Loc.GetString($"au14-callsign-console-category-{category.ToLowerInvariant()}"),
                GetCategoryWord(faction, category),
                rows));
        }

        var squads = EntityQueryEnumerator<SquadTeamComponent>();

        while (squads.MoveNext(out var squadUid, out var team))
        {
            if (team.Group != group)
                continue;

            elements.Add(new AU14CallsignConsoleElement(
                GetNetEntity(squadUid),
                null,
                null,
                Loc.GetString("au14-callsign-console-squad-element", ("squad", Name(squadUid).ToUpperInvariant())),
                GetSquadWord(squadUid),
                CollectRows(faction, squadUid, null, null)));
        }

        var groups = _groups.TryGetValue(faction, out var factionGroups)
            ? new List<string>(factionGroups)
            : new List<string>();

        foreach (var groupWord in groups)
        {
            elements.Add(new AU14CallsignConsoleElement(
                null,
                groupWord,
                null,
                Loc.GetString("au14-callsign-console-group-element"),
                groupWord,
                CollectRows(faction, null, groupWord, null)));
        }

        _ui.SetUiState(ent.Owner, AU14CallsignConsoleUI.Key, new AU14CallsignConsoleState(faction, elements, groups));
    }

    // fixed order the role sections are listed in below the command element
    private static readonly string[] ConsoleCategories = ["AIR", "ARMOR", "MP", "MEDICAL", "INTEL", "SYNTH"];

    private List<AU14CallsignConsoleRow> CollectRows(string faction, EntityUid? squad, string? group, string? category)
    {
        var rows = new List<(string SortKey, AU14CallsignConsoleRow Row)>();
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var callsign))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (callsign.Faction != faction)
                continue;

            if (!string.Equals(callsign.Group, group, StringComparison.OrdinalIgnoreCase))
                continue;

            // a task group assignment supersedes the role section; otherwise
            // categorized personnel appear only under their section
            if (group == null &&
                !string.Equals(callsign.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (group == null && category == null && callsign.Squad != squad)
                continue;

            rows.Add((SuffixSortKey(callsign.Suffix), new AU14CallsignConsoleRow(
                GetNetEntity(uid),
                callsign.Callsign,
                Name(uid),
                callsign.JobTitle)));
        }

        return rows
            .OrderBy(entry => entry.SortKey, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Row)
            .ToList();
    }

    // roster order the way a net is read: 6, 5, 7, ROMEO, air crew, OPS, then the
    // fireteam-numbered blocks (1-N, 2-N, ...)
    private static string SuffixSortKey(string suffix)
    {
        var rank = suffix.ToUpperInvariant() switch
        {
            "6" => 0,
            "ACTUAL" => 0,
            "5" => 1,
            "7" => 2,
            "ROMEO" => 3,
            _ when suffix.StartsWith("PAPA", StringComparison.OrdinalIgnoreCase) => 4,
            _ when suffix.StartsWith("CHIEF", StringComparison.OrdinalIgnoreCase) => 4,
            _ when suffix.StartsWith("OPS", StringComparison.OrdinalIgnoreCase) => 5,
            _ when suffix.Length > 1 && char.IsAsciiDigit(suffix[0]) && suffix.Contains('-') => 6,
            _ => 7,
        };

        // zero-pad both halves so 2-1 sorts after 1-10 and 1-10 after 1-9
        var fireteam = 0;
        var numeric = 0;
        var dash = suffix.LastIndexOf('-');

        if (dash >= 0 && int.TryParse(suffix[(dash + 1)..], out var parsed))
            numeric = parsed;

        if (dash > 0 && int.TryParse(suffix[..dash], out var parsedTeam))
            fireteam = parsedTeam;

        return $"{rank}-{fireteam:D2}-{numeric:D4}-{suffix}";
    }

    private static string SanitizeCallsignPart(string input, int maxLength)
    {
        var upper = input.ToUpperInvariant().Trim();

        var filtered = new string(upper
            .Where(c => char.IsLetterOrDigit(c) || c == '-' || c == ' ')
            .ToArray());

        return filtered.Length > maxLength
            ? filtered[..maxLength]
            : filtered;
    }
}
