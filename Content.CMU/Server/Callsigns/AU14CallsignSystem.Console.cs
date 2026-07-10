using System.Linq;
using Content.Shared._AU14.Callsigns;
using Content.Shared._AU14.Radio;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Squads;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Server._AU14.Callsigns;

public sealed partial class AU14CallsignSystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private void InitializeConsole()
    {
        SubscribeLocalEvent<AU14CallsignConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleGetVerbs);

        Subs.BuiEvents<AU14CallsignConsoleComponent>(AU14CallsignConsoleUI.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<AU14CallsignRenameElementMsg>(OnRenameElement);
            subs.Event<AU14CallsignSetSuffixMsg>(OnSetSuffix);
        });
    }

    private void OnConsoleGetVerbs(Entity<AU14CallsignConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("au14-callsign-console-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () => _ui.TryOpenUi(ent.Owner, AU14CallsignConsoleUI.Key, user),
        });
    }

    private void OnConsoleOpened(Entity<AU14CallsignConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateConsoleState(ent);
    }

    private void OnRenameElement(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignRenameElementMsg args)
    {
        if (!CanEdit(args.Actor, ent.Comp.Faction))
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
        else
        {
            _commandWords[ent.Comp.Faction] = word;
        }

        RefreshFaction(ent.Comp.Faction);
    }

    private void OnSetSuffix(Entity<AU14CallsignConsoleComponent> ent, ref AU14CallsignSetSuffixMsg args)
    {
        if (!CanEdit(args.Actor, ent.Comp.Faction))
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

        if (SuffixTaken(callsign.Faction, callsign.Squad, suffix, member.Value))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-suffix-taken", ("suffix", suffix)), ent.Owner, args.Actor);
            return;
        }

        callsign.Suffix = suffix;
        callsign.RoleSuffix = true;

        UpdateFullCallsign(member.Value, callsign);
    }

    private bool CanEdit(EntityUid actor, string faction)
    {
        if (!HasComp<ANPRCRadioUserComponent>(actor))
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-not-authorized"), actor, actor);
            return false;
        }

        var actorFaction = HasComp<CLFMemberComponent>(actor)
            ? "clf"
            : CompOrNull<MarineComponent>(actor)?.Faction;

        if (actorFaction != faction)
        {
            _popup.PopupEntity(Loc.GetString("au14-callsign-console-wrong-faction"), actor, actor);
            return false;
        }

        return true;
    }

    private void RefreshFaction(string faction)
    {
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var callsign))
        {
            if (callsign.Faction != faction)
                continue;

            var word = callsign.Squad is { } squad
                ? GetSquadWord(squad)
                : GetCommandWord(faction);

            callsign.Callsign = $"{word} {callsign.Suffix}";
            Dirty(uid, callsign);
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

        // command element first, then the squads
        var elements = new List<AU14CallsignConsoleElement>
        {
            new(null,
                Loc.GetString("au14-callsign-console-command-element"),
                GetCommandWord(faction),
                CollectRows(faction, null)),
        };

        var squads = EntityQueryEnumerator<SquadTeamComponent>();

        while (squads.MoveNext(out var squadUid, out var team))
        {
            if (team.Group != group)
                continue;

            elements.Add(new AU14CallsignConsoleElement(
                GetNetEntity(squadUid),
                Loc.GetString("au14-callsign-console-squad-element", ("squad", Name(squadUid).ToUpperInvariant())),
                GetSquadWord(squadUid),
                CollectRows(faction, squadUid)));
        }

        _ui.SetUiState(ent.Owner, AU14CallsignConsoleUI.Key, new AU14CallsignConsoleState(faction, elements));
    }

    private List<AU14CallsignConsoleRow> CollectRows(string faction, EntityUid? squad)
    {
        var rows = new List<(string SortKey, AU14CallsignConsoleRow Row)>();
        var query = EntityQueryEnumerator<AU14CallsignComponent>();

        while (query.MoveNext(out var uid, out var callsign))
        {
            if (callsign.Faction != faction || callsign.Squad != squad)
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

    // roster order the way a net is read: 6, 5, 7, ROMEO, OPS, then 1-N by number
    private static string SuffixSortKey(string suffix)
    {
        var rank = suffix.ToUpperInvariant() switch
        {
            "6" => 0,
            "5" => 1,
            "7" => 2,
            "ROMEO" => 3,
            _ when suffix.StartsWith("OPS", StringComparison.OrdinalIgnoreCase) => 4,
            _ when suffix.StartsWith("1-", StringComparison.Ordinal) => 5,
            _ => 6,
        };

        // zero-pad trailing numbers so 1-10 sorts after 1-9
        var numeric = 0;
        var dash = suffix.LastIndexOf('-');

        if (dash >= 0 && int.TryParse(suffix[(dash + 1)..], out var parsed))
            numeric = parsed;

        return $"{rank}-{numeric:D4}-{suffix}";
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
