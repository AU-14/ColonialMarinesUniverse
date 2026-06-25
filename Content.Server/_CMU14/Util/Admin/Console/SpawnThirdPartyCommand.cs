using System.Linq;
using Content.Server.Administration;
using Content.Shared._CMU14.Threats;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;
using ThirdPartySystem = Content.Server._CMU14.Ops.ThirdParty.ThirdPartySystem;

namespace Content.Server._CMU14.Util.Admin.Console;

[AdminCommand(AdminFlags.Admin)]
public sealed partial class SpawnThirdPartyCommand : LocalizedEntityCommands
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override string Command => "spawnthirdparty";

    public override string Help
        => "spawnthirdparty [third party name] [dropship (true/false)]\nSpawns a third party. If dropship is true, they will enter by shuttle.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: spawnthirdparty [third party name] [dropship (true/false)]");
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

        if (dropship)
            thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false, null, true);
        else
            thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false);

        shell.WriteLine($"Spawned third party '{party.ID}' (dropship={dropship})");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(GetThirdPartyCompletions(), "<thirdPartyId>"),
            2 => CompletionResult.FromHintOptions(CompletionHelper.Booleans, "<dropship>"),
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
