using System.Linq;
using System.Text;
using Content.Server.Administration.Managers;
using Content.Server.Afk;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.CMU14.Administration;

/// <summary>
/// One online admin as shown by <see cref="CMUShowAdminsCommand"/>.
/// </summary>
public readonly record struct CMUShowAdminsEntry(string Name, string? Title, bool Deadminned, bool Afk, bool Stealth);

/// <summary>
/// Player-facing counterpart to adminwho: lists every online admin, including deadminned and AFK ones.
/// Stealthed admins are never listed, regardless of who runs the command.
/// </summary>
[AnyCommand]
public sealed partial class CMUShowAdminsCommand : LocalizedCommands
{
    [Dependency] private IAfkManager _afkManager = default!;
    [Dependency] private IAdminManager _adminManager = default!;

    public override string Command => "showadmins";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entries = new List<CMUShowAdminsEntry>();
        foreach (var admin in _adminManager.AllAdmins)
        {
            if (_adminManager.GetAdminData(admin, includeDeAdmin: true) is not { } data)
                continue;

            entries.Add(new CMUShowAdminsEntry(
                admin.Name,
                data.Title,
                !_adminManager.ActiveAdmins.Contains(admin),
                _afkManager.IsAfk(admin),
                data.Stealth));
        }

        shell.WriteLine(Format(Loc, entries));
    }

    /// <summary>
    /// Builds the command output. Stealthed entries are dropped; the rest are sorted by name.
    /// </summary>
    public static string Format(ILocalizationManager loc, IEnumerable<CMUShowAdminsEntry> entries)
    {
        var sb = new StringBuilder();
        var first = true;
        foreach (var entry in entries
                     .Where(e => !e.Stealth)
                     .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(e => e.Name, StringComparer.Ordinal))
        {
            if (!first)
                sb.Append('\n');
            first = false;

            sb.Append(entry.Name);
            if (!string.IsNullOrEmpty(entry.Title))
                sb.Append($": [{entry.Title}]");

            if (entry.Deadminned)
                sb.Append(' ').Append(loc.GetString("cmu-showadmins-deadminned"));

            if (entry.Afk)
                sb.Append(' ').Append(loc.GetString("cmu-showadmins-afk"));
        }

        return first ? loc.GetString("cmu-showadmins-none") : sb.ToString();
    }
}
