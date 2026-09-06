using Content.Server.AU14.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Paper;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.Fugitive;

public sealed partial class FugitiveRuleSystem : GameRuleSystem<FugitiveRuleComponent>
{
    [Dependency] private readonly WantedSystem _wantedSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FugitiveComponent, ComponentStartup>(OnFugitiveSpawned);
    }

    private void OnFugitiveSpawned(EntityUid uid, FugitiveComponent component, ComponentStartup args)
    {
        var name = EntityManager.GetComponentOrNull<MetaDataComponent>(uid)?.EntityName ?? "Fugitive";

        _wantedSystem.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Fugitive Alert",
            ColonyCmbFax.Build("Fugitive Alert",
                $"A long time criminal, [bold]{name}[/bold], has been located hiding out near your duty station. " +
                "He has a sizeable bounty and is wanted ALIVE. Bring him in and get that bounty, more information on your records console."),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            }, ColonyCmbFax.CmbPaperPrototype);
    }
}
