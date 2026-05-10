using Content.Server.Administration;
using Content.Shared._RMC14.Bioscan;
using Content.Shared.Administration;
using Robust.Shared.Map;
using Robust.Shared.Toolshed;

namespace Content.Server._RMC14.Bioscan;

[ToolshedCommand, AdminCommand(AdminFlags.Moderator)]
public sealed class BioscanCommand : ToolshedCommand
{
    private BioscanSystem? _bioscan;

    [CommandImplementation("all")]
    public void All()
    {
        Marine();
        Xeno();
    }

    [CommandImplementation("marine")]
    public void Marine()
    {
        _bioscan ??= GetSys<BioscanSystem>();

        var bioscans = EntityManager.EntityQueryEnumerator<BioscanComponent>();
        var found = false;
        while (bioscans.MoveNext(out var uid, out var bioscan))
        {
            found = true;
            _bioscan.TryBioscanARES((uid, bioscan), true);
        }

        if (found)
            return;

        var temporary = Spawn(null, MapCoordinates.Nullspace);
        var temporaryBioscan = EnsureComp<BioscanComponent>(temporary);
        _bioscan.TryBioscanARES((temporary, temporaryBioscan), true);
        Del(temporary);
    }

    [CommandImplementation("xeno")]
    public void Xeno()
    {
        _bioscan ??= GetSys<BioscanSystem>();

        var bioscans = EntityManager.EntityQueryEnumerator<BioscanComponent>();
        var found = false;
        while (bioscans.MoveNext(out var uid, out var bioscan))
        {
            found = true;
            _bioscan.TryBioscanQueenMother((uid, bioscan), true);
        }

        if (found)
            return;

        var temporary = Spawn(null, MapCoordinates.Nullspace);
        var temporaryBioscan = EnsureComp<BioscanComponent>(temporary);
        _bioscan.TryBioscanQueenMother((temporary, temporaryBioscan), true);
        Del(temporary);
    }
}
