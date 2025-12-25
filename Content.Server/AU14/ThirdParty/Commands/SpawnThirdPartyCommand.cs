using Content.Server.Administration;
using Content.Shared.AU14.Threats;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server.AU14.ThirdParty.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class SpawnThirdPartyCommand : LocalizedEntityCommands
{
    public override string Command => "spawnthirdparty";
    public override string Help => "spawnthirdparty [third party name] [dropship (true/false)]\nSpawns a third party. If dropship is true, they will enter by shuttle.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: spawnthirdparty [third party name] [dropship (true/false)]");
            return;
        }

        var thirdPartyName = args[0];
        if (!bool.TryParse(args[1], out var dropship))
        {
            shell.WriteError("Second argument must be true or false for dropship.");
            return;
        }

        var entitySystemManager = IoCManager.Resolve<IEntitySystemManager>();
        var protoManager = IoCManager.Resolve<IPrototypeManager>();
        var thirdPartySystem = entitySystemManager.GetEntitySystem<AuThirdPartySystem>();

        if (!protoManager.TryIndex<AuThirdPartyPrototype>(thirdPartyName, out var party))
        {
            shell.WriteError($"No third party prototype found with ID: {thirdPartyName}");
            return;
        }


        if (dropship && (string.IsNullOrEmpty(party.dropshippath.ToString())))
        {
            shell.WriteError($"Third party '{thirdPartyName}' does not have a valid dropshippath for dropship spawn.");
            return;
        }

        // Fix: get the PartySpawn prototype and pass it to SpawnThirdParty
        if (!protoManager.TryIndex<PartySpawnPrototype>(party.PartySpawn, out var partySpawnProto))
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
}
