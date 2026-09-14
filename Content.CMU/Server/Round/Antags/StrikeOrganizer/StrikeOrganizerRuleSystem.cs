using Content.Server.GameTicking.Rules;
using Content.Server.CMU14.Systems;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Robust.Shared.GameObjects;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

public sealed partial class StrikeOrganizerRuleSystem : GameRuleSystem<StrikeOrganizerRuleComponent>
{
    [Dependency] private WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrikeOrganizerComponent, ComponentStartup>(OnStrikeOrganizerSpawned);
    }

    private void OnStrikeOrganizerSpawned(EntityUid uid, StrikeOrganizerComponent component, ComponentStartup args)
        => _wantedSystem.SendPaperToGroup(ColonyCmbFax.MarshalBureauFaxGroup, "AUPaperStrikeOrganizer");
}
