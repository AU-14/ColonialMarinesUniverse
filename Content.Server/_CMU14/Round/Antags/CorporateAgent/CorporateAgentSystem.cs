using Content.Server.AU14.Systems;
using Content.Server.Popups;
using Content.Server._CMU14.Round.Antags.ColonyBounty;
using Content.Shared._CMU14.Round.Antags.CorporateAgent;
using Content.Shared._CMU14.Round.Objectives.Type;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server._CMU14.Round.Antags.CorporateAgent;

public sealed partial class CorporateAgentSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

    private EntityQuery<FetchItemComponent> _fetchQuery = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CorporateDataLinkComponent, UseInHandEvent>(OnBeaconUsed);
        _fetchQuery = GetEntityQuery<FetchItemComponent>();
    }

    private void OnBeaconUsed(EntityUid uid, CorporateDataLinkComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(args.User, out CorporateAgentComponent? agent) || agent.Completed)
            return;

        var held = CountFetchItems(args.User);
        if (held < agent.RequiredItems)
        {
            _popup.PopupEntity(Loc.GetString("cmu-corporate-beacon-insufficient",
                ("count", held), ("required", agent.RequiredItems)), args.User, args.User);
            args.Handled = true;
            return;
        }

        agent.Completed = true;
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-corporate-beacon-transmit",
            ("corporation", agent.Corporation)), args.User, args.User);

        _wanted.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Exfiltration Confirmed",
            $"[head=3]{agent.Corporation}[/head]\n\n" +
            $"  Operative has exfiltrated {held} high-value items from the colony. Payment authorized.\n\n" +
            $"[bolditalic]{agent.Corporation}[/bolditalic]",
            "paper_stamp-centcom",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#1a3a6e"), StampedName = agent.Corporation },
            }, agent.PaperPrototype);

        // Weyland-Yutani counterintelligence notices the Laselle traffic.
        if (agent.WyCounterIntel)
            _wanted.SendFaxToGroup(
                ColonyCmbFax.MarshalBureauFaxGroup,
                "Corporate Espionage Alert",
                ColonyCmbFax.Build("Corporate Espionage Alert",
                    "Our monitoring division has intercepted unauthorized Laselle Bionational transmissions " +
                    "originating from your colony. Corporate spies are stealing Weyland-Yutani research assets. " +
                    "Detain anyone found with WY research property; the bounty has been authorized."),
                "paper_stamp-cmb",
                new List<StampDisplayInfo>
                {
                    new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
                }, ColonyCmbFax.CmbPaperPrototype);
    }

    private int CountFetchItems(EntityUid holder)
    {
        var count = 0;
        var enumerator = EntityManager.AllEntityQueryEnumerator<FetchItemComponent>();
        while (enumerator.MoveNext(out var item, out _))
        {
            if (IsHeldBy(item, holder))
                count++;
        }
        return count;
    }

    private bool IsHeldBy(EntityUid item, EntityUid holder)
    {
        var current = item;
        while (EntityManager.TryGetComponent(current, out TransformComponent? xform))
        {
            if (xform.ParentUid == holder)
                return true;

            if (!xform.ParentUid.IsValid() || xform.ParentUid == current)
                return false;

            current = xform.ParentUid;
        }
        return false;
    }
}
