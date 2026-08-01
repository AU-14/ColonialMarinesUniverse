using System;
using Content.Server.Administration;
using Content.Server._AU14.Insurgency.Database;
using Content.Shared._AU14.Insurgency;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.Insurgency.Commands;

/// <summary>
///     Debug round-trip for the faction DB layer: saves a throwaway faction, reads it back, then
///     deletes it, reporting each step. Proves create / read / delete against the live SQLite (or
///     Postgres) DB without needing the editor UI. Not a shipping feature.
///
///     Runs as async void and awaits the DB. Never block the main thread on a DB task: the DB
///     manager marshals completion back through the game loop, so blocking (GetResult / Wait)
///     deadlocks the whole server.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class InsurgencyFactionDbTestCommand : IConsoleCommand
{
    public string Command => "insforfactiondbtest";
    public string Description => "Saves, reads back, and deletes a test faction to verify the DB round-trip.";
    public string Help => "Usage: insforfactiondbtest";

    public async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var entMan = IoCManager.Resolve<IEntityManager>();
        var db = entMan.System<InsurgencyFactionDbSystem>();

        var def = new FactionDefinition
        {
            Metadata =
            {
                Title = "DB Round-Trip Test",
                Description = "Written by insforfactiondbtest.",
                RoleplayText = "Delete me if I linger.",
            },
        };

        try
        {
            var id = await db.AddFactionAsync(def, isDefault: true);
            shell.WriteLine($"Saved test faction with id {id}.");

            var loaded = await db.GetFactionAsync(id);
            shell.WriteLine(loaded == null
                ? "ERROR: could not read the faction back."
                : $"Read back: \"{loaded.Metadata.Title}\" (schema v{loaded.SchemaVersion}).");

            var deleted = await db.DeleteFactionAsync(id);
            shell.WriteLine(deleted ? "Deleted the test faction. Round-trip OK." : "ERROR: delete reported no row.");
        }
        catch (Exception e)
        {
            shell.WriteError($"DB round-trip failed: {e.Message}");
        }
    }
}
