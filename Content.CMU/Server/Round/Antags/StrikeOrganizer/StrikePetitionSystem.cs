using Content.Server.CMU14.Systems;
using Content.Server.Popups;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.CMU14.Round.Antags.StrikeOrganizer;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Server.CMU14.Round.Antags.StrikeOrganizer;

public sealed partial class StrikePetitionSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SuspectDescriptionSystem _suspectDescription = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StrikePetitionComponent, EntGotInsertedIntoContainerMessage>(OnPetitionInserted);
        SubscribeLocalEvent<StrikePetitionComponent, UseInHandEvent>(OnPetitionUsed);
        SubscribeLocalEvent<StrikePetitionComponent, ExaminedEvent>(OnPetitionExamined);
    }

    // Gear spawns at world coordinates and only lands in the organizer's inventory via
    // TryEquip afterwards; at ComponentStartup the parent chain is still petition-grid-map
    private void OnPetitionInserted(Entity<StrikePetitionComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (ent.Comp.Organizer != null)
            return;

        var current = ent.Owner;
        while (EntityManager.TryGetComponent(current, out TransformComponent? xform) && xform.ParentUid.IsValid())
        {
            current = xform.ParentUid;
            if (HasComp<StrikeOrganizerComponent>(current))
            {
                ent.Comp.Organizer = current;
                return;
            }
        }
    }

    private void OnPetitionUsed(EntityUid uid, StrikePetitionComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var signer = MetaData(args.User).EntityName;
        if (comp.Signatures.Contains(signer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-petition-already-signed"), args.User, args.User);
            args.Handled = true;
            return;
        }

        comp.Signatures.Add(signer);
        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-petition-signed",
            ("count", comp.Signatures.Count), ("goal", comp.Goal)), args.User, args.User);

        if (!comp.FaxedHalf && comp.Signatures.Count >= (comp.Goal + 1) / 2)
        {
            comp.FaxedHalf = true;
            SendUnrestFax("Labor Unrest Escalation",
                "The strike petition circulating in your colony is gaining momentum. " +
                "Mediate with the organizers before the situation gets out of hand.",
                comp.Organizer);
        }

        if (!comp.FaxedFull && comp.Signatures.Count >= comp.Goal)
        {
            comp.FaxedFull = true;
            SendUnrestFax("Strike Vote Passed",
                $"A petition of {comp.Goal} signatures has been filed. The colony's workforce is now " +
                "in a legal strike position. Corporate production contracts are in jeopardy.",
                comp.Organizer);
        }
    }

    private void OnPetitionExamined(EntityUid uid, StrikePetitionComponent comp, ExaminedEvent args)
    {
        var names = comp.Signatures.Count == 0
            ? Loc.GetString("cmu-petition-empty")
            : string.Join(", ", comp.Signatures);
        args.PushText(Loc.GetString("cmu-petition-examine",
            ("count", comp.Signatures.Count), ("goal", comp.Goal), ("names", names)));
    }

    private void SendUnrestFax(string heading, string body, EntityUid? organizer)
    {
        // A striker talked: the organizer becomes describable once the strike actually escalates
        if (organizer is { } org && !TerminatingOrDeleted(org))
            body += " A sympathizer described one of the organizers as: "
                + _suspectDescription.Describe(org, _suspectDescription.RandomWitness(org, null))
                + ".";

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
