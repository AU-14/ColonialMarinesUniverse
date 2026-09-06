using Content.Server.GameTicking.Rules;
using Content.Server.AU14.Systems;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.StrikeOrganizer;

public sealed partial class StrikeOrganizerRuleSystem : GameRuleSystem<StrikeOrganizerRuleComponent>
{
    [Dependency] private WantedSystem _wantedSystem = default!;
    [Dependency] private IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StrikeOrganizerComponent, ComponentStartup>(OnStrikeOrganizerSpawned);
    }

    private void OnStrikeOrganizerSpawned(EntityUid uid, StrikeOrganizerComponent component, ComponentStartup args)
    {
        var organizerName = _entityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Unknown";

        var faxContent = ColonyCmbFax.Build("Labor Unrest Alert",
            $"We've received reports of a labor organizer, [bold]{organizerName}[/bold], stirring up unrest in your colony. " +
            "Keep an eye on the situation and ensure it doesn't get out of hand.");

        _wantedSystem.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Labor Unrest Alert",
            faxContent,
            "paper_stamp-cmb",
            new System.Collections.Generic.List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" }
            }, ColonyCmbFax.CmbPaperPrototype);
    }
}

