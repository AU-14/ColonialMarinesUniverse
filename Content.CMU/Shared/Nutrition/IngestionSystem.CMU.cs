using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared.Nutrition.EntitySystems;

public sealed partial class IngestionSystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private INetConfigurationManager _netConfig = default!;

    private bool ShouldAutoIngest(EntityUid consumer)
    {
        if (_net.IsClient)
            return _cfg.GetCVar(CCVars.CMUAutoIngestEnabled);

        return !TryComp<ActorComponent>(consumer, out var actor) ||
            _netConfig.GetClientCVar(actor.PlayerSession.Channel, CCVars.CMUAutoIngestEnabled);
    }
}
