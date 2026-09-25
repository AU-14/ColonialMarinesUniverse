using Content.Server.CMU14.Systems;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.Spreader;
using Content.Shared.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.CMU14.Round.Antags.Arsonist;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Item;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.Arsonist;

/// <summary>
/// Counts structure fires while an arsonist is active. Fires are counted regardless of
/// who lit them, so xenomorphs burning the colony also feed the count.
/// </summary>
public sealed partial class ArsonistSystem : EntitySystem
{
    [Dependency] private readonly WantedSystem _wanted = default!;
    [Dependency] private readonly ColonyBountySystem _colonyBounty = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> WeedTileTag = "XenoWeedTile";

    public override void Initialize()
    {
        SubscribeLocalEvent<OnFireComponent, ComponentStartup>(OnIgnited);
    }

    private void OnIgnited(EntityUid uid, OnFireComponent onFire, ComponentStartup args)
    {
        if (HasComp<MobStateComponent>(uid)
            || HasComp<ItemComponent>(uid)
            || HasComp<KudzuComponent>(uid)
            || _tag.HasTag(uid, WeedTileTag))
            return;

        var enumerator = EntityManager.AllEntityQueryEnumerator<ArsonistComponent>();
        while (enumerator.MoveNext(out var arsonistUid, out var arsonist))
        {
            if (EntityManager.GetComponentOrNull<MobStateComponent>(arsonistUid)?.CurrentState is MobState.Dead)
                continue;

            if (!arsonist.Burned.Add(uid))
                continue;

            arsonist.FiresCount++;

            if (!arsonist.Alerted && arsonist.FiresCount >= arsonist.AlertThreshold)
            {
                arsonist.Alerted = true;
                SendCmbFax("Arson Reported",
                    "Multiple structure fires have broken out across the colony in circumstances " +
                    "suggesting arson. Evacuate the civilians and find whoever is holding the torch.");
            }

            if (arsonist.FiresCount >= arsonist.WantedThreshold)
            {
                // First crossing posts the bounty, later fires raise the price on the existing record.
                var posted = HasComp<ColonyBountyComponent>(arsonistUid);
                var bounty = EnsureComp<ColonyBountyComponent>(arsonistUid);

                if (!posted)
                {
                    bounty.Bounty = 1500;
                    bounty.Reason = "Serial arson - colony infrastructure aflame";
                    bounty.RecordName = "The Arsonist (Unknown)";
                    bounty.CapturedFaxPaper = "CMUPaperColonyAntagCaptured";
                    SendCmbFax("Arson Bounty Posted",
                        "The colony has burned enough. A bounty has been posted for the arsonist, " +
                        "and it rises with every new fire. Bring them in, or bring what is left of them.");
                }
                else
                {
                    bounty.Bounty = Math.Min(bounty.Bounty + arsonist.BountyPerFire, arsonist.MaxBounty);
                    _colonyBounty.SyncRecordBounty(arsonistUid, bounty);
                }
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
