using System.Linq;
using Content.Server.Administration;
using Content.Shared.CMU14.Threats;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using ThirdPartySystem = Content.Server.CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.Server.CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SpawnThirdPartyCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "spawnthirdparty";

    public override string Help
        => "spawnthirdparty [third party name] [dropship (true/false)] [summoning faction (govfor/opfor/none), optional] [now (true/false), optional]\n" +
           "Spawns a third party. If dropship is true, they will enter by shuttle. Give a summoning faction to spawn them " +
           "as if that faction called them in through intel, so they're held to its ship's third party pad while it's shipside. " +
           "With now set to true they spawn straight away as open ghost roles, without waiting for enough interested players.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 4)
        {
            shell.WriteError("Usage: spawnthirdparty [third party name] [dropship (true/false)] [summoning faction (govfor/opfor/none), optional] [now (true/false), optional]");
            return;
        }

        string? summoningFaction = null;
        if (args.Length >= 3 && !args[2].Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            summoningFaction = args[2].ToLowerInvariant();
            if (summoningFaction is not ("govfor" or "opfor"))
            {
                shell.WriteError("Summoning faction must be govfor, opfor or none.");
                return;
            }
        }

        var now = false;
        if (args.Length == 4 && !bool.TryParse(args[3], out now))
        {
            shell.WriteError("Fourth argument must be true or false for spawning now.");
            return;
        }

        string thirdPartyName = args[0];
        if (!bool.TryParse(args[1], out bool dropship))
        {
            shell.WriteError("Second argument must be true or false for dropship.");
            return;
        }

        var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var thirdPartySystem = entitySystemManager.GetEntitySystem<ThirdPartySystem>();

        if (!protoManager.TryIndex(thirdPartyName, out ThirdPartyPrototype? party))
        {
            shell.WriteError($"No third party prototype found with ID: {thirdPartyName}");
            return;
        }


        if (dropship && string.IsNullOrEmpty(party.dropshippath.ToString()))
        {
            shell.WriteError($"Third party '{thirdPartyName}' does not have a valid dropshippath for dropship spawn.");
            return;
        }

        // Fix: get the PartySpawn prototype and pass it to SpawnThirdParty
        if (!protoManager.TryIndex(party.PartySpawn, out PartySpawnPrototype? partySpawnProto))
        {
            shell.WriteError($"No PartySpawn prototype found with ID: {party.PartySpawn}");
            return;
        }

        if (now)
        {
            if (thirdPartySystem.SpawnThirdPartyImmediately(party, partySpawnProto, dropship ? true : null, summoningFaction))
                shell.WriteLine($"Spawned third party '{party.ID}' now as open ghost roles (dropship={dropship}" +
                                (summoningFaction != null ? $", summoned by {summoningFaction})" : ")"));
            else
                shell.WriteError($"Couldn't spawn third party '{party.ID}'; check the server log for why.");
            return;
        }

        if (dropship)
            thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false, null, true, summoningFaction);
        else
            thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false, summoningFaction: summoningFaction);

        shell.WriteLine($"Queued third party '{party.ID}' for player interest (dropship={dropship}" +
                        (summoningFaction != null ? $", summoned by {summoningFaction})" : ")"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(GetThirdPartyCompletions(), "<thirdPartyId>"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "<dropship>"),
            3 => CompletionResult.FromHintOptions(["govfor", "opfor", "none"], "[summoning faction]"),
            4 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "[now]"),
            _ => CompletionResult.Empty
        };
    }

    private IEnumerable<CompletionOption> GetThirdPartyCompletions()
    {
        return _prototype.EnumeratePrototypes<ThirdPartyPrototype>()
            .OrderBy(prototype => prototype.ID)
            .Select(prototype => new CompletionOption(
                prototype.ID,
                prototype.DisplayName ?? prototype.ID));
    }
}
