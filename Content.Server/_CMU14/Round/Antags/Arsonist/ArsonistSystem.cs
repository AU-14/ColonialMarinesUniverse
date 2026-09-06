using Content.Server.AU14.Systems;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.Arsonist;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.Arsonist;

/// <summary>
/// Counts structure fires while an arsonist is active. Fires are counted regardless of
/// who lit them, so xenomorphs burning the colony also feed the count.
/// </summary>
public sealed partial class ArsonistSystem : EntitySystem
{
    [Dependency] private readonly WantedSystem _wanted = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<OnFireComponent, ComponentStartup>(OnIgnited);
    }

    private void OnIgnited(EntityUid uid, OnFireComponent onFire, ComponentStartup args)
    {
        if (HasComp<MobStateComponent>(uid))
            return;

        var enumerator = EntityManager.AllEntityQueryEnumerator<ArsonistComponent>();
        while (enumerator.MoveNext(out var arsonistUid, out var arsonist))
        {
            arsonist.FiresCount++;

            if (!arsonist.Alerted && arsonist.FiresCount >= arsonist.AlertThreshold)
            {
                arsonist.Alerted = true;
                SendCmbFax("Arson Reported",
                    "Multiple structure fires have broken out across the colony in circumstances " +
                    "suggesting arson. Evacuate the civilians and find whoever is holding the torch.");
            }

            if (arsonist.FiresCount >= arsonist.WantedThreshold && !HasComp<ColonyBountyComponent>(arsonistUid))
            {
                var bounty = EnsureComp<ColonyBountyComponent>(arsonistUid);
                bounty.Bounty = 1500;
                bounty.Reason = "Serial arson - colony infrastructure aflame";
                bounty.RecordName = "The Arsonist (Unknown)";
                bounty.CapturedFaxPaper = "CMUPaperColonyAntagCaptured";
                SendCmbFax("Arson Bounty Posted",
                    "The colony has burned enough. A bounty has been posted for the arsonist. " +
                    "Bring them in, or bring what is left of them.");
            }
        }
    }

    private void SendCmbFax(string heading, string body)
    {
        _wanted.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            heading,
            ColonyCmbFax.Build(heading, body),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            }, ColonyCmbFax.CmbPaperPrototype);
    }
}
