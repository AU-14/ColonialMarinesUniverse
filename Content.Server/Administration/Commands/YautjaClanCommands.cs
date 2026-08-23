using System;
using System.Linq;
using Content.Server._CMU14.Yautja;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.Players.JobWhitelist;
using Content.Shared._CMU14.Yautja;
using Content.Shared.Administration;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Clans)]
public sealed partial class YautjaClanSetMemberCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "yautjaclanset";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 3)
        {
            shell.WriteError("Usage: yautjaclanset <player> <clan id> <rank>");
            return;
        }

        var player = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError($"Player '{args[0]}' was not found.");
            return;
        }

        if (!int.TryParse(args[1], out var clanId) || (await _db.GetYautjaClanAsync(clanId)) is not { Active: true })
        {
            shell.WriteError("Clan must be an existing active clan id.");
            return;
        }

        if (!YautjaRankCommand.TryParsePersistentRank(args[2], out var rank))
        {
            shell.WriteError("Invalid Yautja rank.");
            return;
        }

        var existing = await _db.GetYautjaClanMemberAsync(player.UserId.UserId);
        await _db.UpsertYautjaClanMemberAsync(new YautjaClanMemberRecord(
            player.UserId.UserId,
            clanId,
            (int) rank,
            (int) YautjaClanManager.PermissionsForRank(rank),
            existing?.Honor ?? 0,
            false));
        await _rankManager.Refresh(player.UserId);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{shell.Player?.Name ?? "Console"} set Yautja clan membership for {player.Username} ({player.UserId}) to clan {clanId} at rank {rank}.");
        shell.WriteLine($"Set {player.Username}'s clan membership to {clanId} at rank {rank}.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(), "player"),
            2 => CompletionResult.FromHint("existing clan id"),
            3 => CompletionResult.FromHintOptions(YautjaRankCommand.PersistentRankNames, "rank"),
            _ => CompletionResult.Empty,
        };
    }
}

[AdminCommand(AdminFlags.Clans)]
public sealed partial class YautjaClanCreateCommand : LocalizedCommands
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public override string Command => "yautja" + "clancreate";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError("Usage: yautja" + "clancreate <name> <description> [color]");
            return;
        }

        var color = args.Length > 2 ? args[^1] : "#ffffff";
        var descriptionEnd = args.Length > 2 ? args.Length - 1 : args.Length;
        var description = string.Join(' ', args.Skip(1).Take(descriptionEnd - 1));
        var id = await _db.CreateYautjaClanAsync(args[0], description, 0, color);
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{shell.Player?.Name ?? "Console"} created Yautja clan {id} ({args[0]}).");
        shell.WriteLine($"Created Yautja clan {id} ({args[0]}).");
    }
}

[AdminCommand(AdminFlags.Clans)]
public sealed partial class YautjaClanWhitelistCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private YautjaRankManager _rankManager = default!;
    [Dependency] private JobWhitelistManager _jobWhitelist = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private UserDbDataManager _userDb = default!;

    public override string Command => "yautjawhitelist";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: yautjawhitelist <player> <none|yautja|legacy|council|council_legacy|leader>");
            return;
        }

        var player = await _playerLocator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError($"Player '{args[0]}' was not found.");
            return;
        }

        var flags = args[1].Trim().ToLowerInvariant() switch
        {
            "none" => YautjaWhitelistFlags.None,
            "yautja" => YautjaWhitelistFlags.Yautja,
            "legacy" => YautjaWhitelistFlags.Legacy,
            "council" => YautjaWhitelistFlags.Council,
            "council_legacy" => YautjaWhitelistFlags.CouncilLegacy,
            "leader" => YautjaWhitelistFlags.Leader,
            _ => (YautjaWhitelistFlags?) null,
        };
        if (flags == null)
        {
            shell.WriteError("Invalid whitelist flag.");
            return;
        }

        if (_players.TryGetSessionById(player.UserId, out var session) && !_userDb.IsLoadComplete(session))
            await _userDb.WaitLoadComplete(session);

        await _db.SetYautjaWhitelistFlagsAsync(player.UserId.UserId, (int) flags.Value);
        await _rankManager.Refresh(player.UserId);
        await _jobWhitelist.RefreshYautjaWhitelist(player.UserId);
        _adminLog.Add(
            LogType.AdminCommands,
            LogImpact.Medium,
            $"{shell.Player?.Name ?? "Console"} set Yautja whitelist flags for {player.Username} ({player.UserId}) to {flags.Value}.");
        shell.WriteLine($"Set {player.Username}'s Yautja whitelist flags to {flags.Value}.");
    }
}
