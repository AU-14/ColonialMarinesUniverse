using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Server.Database;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._RMC14.LinkAccount;

[AdminCommand(AdminFlags.Ban)]
public sealed partial class RMCBoostyCommand : LocalizedCommands
{
    [Dependency] private IPlayerLocator _playerLocator = default!;
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private LinkAccountManager _linkAccount = default!;

    public override string Command => "rmcboosty";
    public override string Description => "Manage local Boosty sponsor tiers.";
    public override string Help => "rmcboosty seed|tiers|patrons|reload|grant <player-name-or-id> <tier-name>";

    private sealed record TierSeed(
        string Name,
        ulong DiscordRole,
        int Priority,
        bool ShowOnCredits,
        bool GhostColor,
        bool NamedItems,
        bool Figurines,
        bool LobbyMessage,
        bool RoundEndShoutout);

    private static readonly TierSeed[] Tiers =
    [
        new("\u041a\u043e\u043b\u043e\u043d\u0438\u0441\u0442", 1512833966292471868UL, 7, true, false, false, false, false, false),
        new("\u041d\u043e\u0432\u043e\u0431\u0440\u0430\u043d\u0435\u0446", 1512833998106132621UL, 6, true, true, false, false, false, false),
        new("\u0411\u043e\u0435\u0446", 1512834040233721906UL, 5, true, true, true, false, false, false),
        new("\u0428\u0442\u0443\u0440\u043c\u043e\u0432\u0438\u043a", 1512834069350846604UL, 4, true, true, true, true, false, false),
        new("\u0420\u0430\u0437\u0432\u0435\u0434\u0447\u0438\u043a", 1512834091488120973UL, 3, true, true, true, true, true, false),
        new("\u041a\u043e\u043c\u0430\u043d\u0434\u0438\u0440 \u044f\u0447\u0435\u0439\u043a\u0438", 1512834125331959919UL, 2, true, true, true, true, true, true),
        new("\u041b\u0438\u0434\u0435\u0440 \u0432\u043e\u0441\u0441\u0442\u0430\u043d\u0438\u044f", 1512834158966079682UL, 1, true, true, true, true, true, true),
    ];

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "seed":
                    await Seed(shell);
                    break;
                case "tiers":
                    await ListTiers(shell);
                    break;
                case "patrons":
                    await ListPatrons(shell);
                    break;
                case "grant":
                    await Grant(shell, args.Skip(1).ToList());
                    break;
                case "reload":
                    await Reload(shell);
                    break;
                default:
                    shell.WriteError($"Unknown rmcboosty command '{args[0]}'.");
                    shell.WriteLine(Help);
                    break;
            }
        }
        catch (Exception e)
        {
            shell.WriteError(e.ToString());
        }
    }

    private async Task Seed(IConsoleShell shell)
    {
        foreach (var tier in Tiers)
        {
            await _db.UpsertPatronTier(
                tier.Name,
                tier.DiscordRole,
                tier.Priority,
                tier.ShowOnCredits,
                tier.GhostColor,
                tier.NamedItems,
                tier.Figurines,
                tier.LobbyMessage,
                tier.RoundEndShoutout);
        }

        await ReloadPatrons();
        shell.WriteLine("Boosty sponsor tiers seeded.");
    }

    private async Task ListTiers(IConsoleShell shell)
    {
        var tiers = await _db.GetPatronTiers();
        if (tiers.Count == 0)
        {
            shell.WriteLine("No sponsor tiers found.");
            return;
        }

        foreach (var tier in tiers)
        {
            shell.WriteLine($"{tier.Priority}: {tier.Name} role={tier.DiscordRole} patrons={tier.Patrons.Count} credits={tier.ShowOnCredits} ghost={tier.GhostColor} named={tier.NamedItems} figurines={tier.Figurines} lobby={tier.LobbyMessage} shoutout={tier.RoundEndShoutout}");
        }
    }

    private async Task ListPatrons(IConsoleShell shell)
    {
        var patrons = await _db.GetAllPatrons();
        if (patrons.Count == 0)
        {
            shell.WriteLine("No sponsors found.");
            return;
        }

        foreach (var patron in patrons.OrderBy(p => p.Tier.Priority).ThenBy(p => p.Player.LastSeenUserName))
        {
            shell.WriteLine($"{patron.Player.LastSeenUserName} {patron.PlayerId} -> {patron.Tier.Name}");
        }
    }

    private async Task Grant(IConsoleShell shell, List<string> args)
    {
        if (args.Count != 2)
        {
            shell.WriteError("Usage: rmcboosty grant <player-name-or-id> <tier-name>");
            shell.WriteLine("Example: rmcboosty grant \"Nickname\" \"Лидер восстания\"");
            return;
        }

        var playerNameOrId = args[0];
        var tierName = args[1];
        var player = await _playerLocator.LookupIdByNameOrIdAsync(playerNameOrId);
        if (player == null)
        {
            shell.WriteLine($"No player found with name or id {playerNameOrId}.");
            return;
        }

        switch (await _db.SetPatronTier(player.UserId.UserId, tierName))
        {
            case SetPatronTierResult.Success:
                break;
            case SetPatronTierResult.PlayerNotFound:
                shell.WriteLine($"Player {playerNameOrId} was found externally, but has no local database record. Join this local server as that player once, then run the command again.");
                return;
            case SetPatronTierResult.TierNotFound:
                shell.WriteLine($"No sponsor tier found with name {tierName}. Run 'rmcboosty tiers' to see available names.");
                return;
        }

        await ReloadPatrons();
        await _linkAccount.RefreshPatron(player.UserId);
        shell.WriteLine($"Granted {tierName} to {player.Username}.");
    }

    private async Task Reload(IConsoleShell shell)
    {
        await ReloadPatrons();
        shell.WriteLine("Sponsor cache reloaded.");
    }

    private async Task ReloadPatrons()
    {
        await _linkAccount.RefreshAllPatrons();
        await _linkAccount.RefreshConnectedPatrons();
        _linkAccount.SendPatronsToAll();
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(["seed", "tiers", "patrons", "grant", "reload"], "subcommand");

        if (args.Length == 3 && args[0].Equals("grant", StringComparison.OrdinalIgnoreCase))
            return CompletionResult.FromHintOptions(Tiers.Select(t => t.Name), "tier");

        return CompletionResult.Empty;
    }
}
