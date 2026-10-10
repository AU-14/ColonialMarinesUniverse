using Content.Server.CMU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Round.Antags.Fugitive;

public sealed partial class FugitiveRuleSystem : GameRuleSystem<FugitiveRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FugitiveComponent, ComponentStartup>(OnFugitiveSpawned);
    }

    private void OnFugitiveSpawned(EntityUid uid, FugitiveComponent component, ComponentStartup args)
        => _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperFugitive");
}
