using Content.Server.Administration;
using Content.Shared._AU14.Insurgency;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._AU14.Insurgency.Commands;

/// <summary>
///     Debug helper for Phase 0. Builds a minimal in-memory <see cref="FactionDefinition"/> and
///     applies it so the apply pipeline (briefing text + members popup) can be verified in-game
///     before the Default-faction editor and DB (Phase 1) exist. Not a shipping feature.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class InsurgencyApplyTestCommand : IConsoleCommand
{
    public string Command => "insforapplytest";
    public string Description => Loc.GetString("cmd-insforapplytest-desc");
    public string Help => Loc.GetString("cmd-insforapplytest-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var title = args.Length >= 1 ? string.Join(' ', args) : "Test Liberation Cell";

        var definition = new FactionDefinition
        {
            Metadata =
            {
                Title = title,
                Description = "A ragtag cell testing the INSFOR apply pipeline.",
                RoleplayText = "Play it scrappy and improvised. You are locals, not soldiers.",
            },
        };

        var entMan = IoCManager.Resolve<IEntityManager>();
        var apply = entMan.System<InsurgencyFactionApplySystem>();
        apply.ApplyFaction(definition);

        // Count current members so the tester can confirm the announcement reached them.
        var count = 0;
        var query = entMan.EntityQueryEnumerator<CLFMemberComponent>();
        while (query.MoveNext(out _, out _))
            count++;

        shell.WriteLine(Loc.GetString("cmd-insforapplytest-applied", ("title", title), ("count", count)));
    }
}
