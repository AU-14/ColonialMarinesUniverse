using Content.Shared.CMU14.Photocopier;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Photography;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Photocopier;

/// <summary>
/// Paper and photograph copying with a toner pool, per CM-SS13's photocopier. Insert a sheet, activate to copy.
/// </summary>
public sealed class CMUPhotocopierSystem : EntitySystem
{
    public const string PaperSlot = "cmu_photocopier_paper";

    private static readonly EntProtoId CopyPrototype = "CMPaper";

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUPhotocopierComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUPhotocopierComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CMUPhotocopierComponent, CMUPhotocopierCopyDoAfterEvent>(OnCopyDoAfter);
        SubscribeLocalEvent<CMUPhotocopierComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPhotocopierComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<CMUPhotocopierComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<PaperComponent>(args.Used, out _) &&
            !TryComp<PhotographComponent>(args.Used, out _))
            return;

        args.Handled = true;

        var slot = _container.EnsureContainer<ContainerSlot>(ent.Owner, PaperSlot);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photocopier-occupied"), ent, args.User);
            return;
        }

        if (_container.Insert(args.Used, slot))
            _popup.PopupEntity(Loc.GetString("cmu-photocopier-inserted"), ent, args.User);
    }

    private void OnActivate(Entity<CMUPhotocopierComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        StartCopy(ent, args.User);
    }

    private void StartCopy(Entity<CMUPhotocopierComponent> ent, EntityUid user)
    {
        if (!TryGetPaper(ent, out _))
            return;

        if (ent.Comp.Toner <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photocopier-no-toner"), ent, user, PopupType.MediumCaution);
            return;
        }

        var ev = new CMUPhotocopierCopyDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, user, ent.Comp.CopyDelay, ev, ent.Owner, ent.Owner)
        {
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        // Only a running do-after clears this, so never set it when none started
        _appearance.SetData(ent.Owner, CMUPhotocopierVisuals.Copying, true);
        _popup.PopupEntity(Loc.GetString("cmu-photocopier-copying"), ent, user);
    }

    private void OnCopyDoAfter(Entity<CMUPhotocopierComponent> ent, ref CMUPhotocopierCopyDoAfterEvent args)
    {
        // Every exit clears the scanning state, a cancelled copy must not leave the scanner stuck mid-sweep
        _appearance.SetData(ent.Owner, CMUPhotocopierVisuals.Copying, false);

        if (args.Cancelled || args.Handled || args.User is not { Valid: true } user)
            return;

        args.Handled = true;

        if (!TryGetPaper(ent, out var original))
            return;

        if (ent.Comp.Toner <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photocopier-no-toner"), ent, user, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Toner--;

        EntityUid copy;
        if (TryComp<PaperComponent>(original, out var source))
        {
            copy = Spawn(CopyPrototype, _transform.GetMapCoordinates(ent.Owner));
            if (TryComp<PaperComponent>(copy, out var paper))
            {
                paper.Content = source.Content;
                paper.ContentSize = source.ContentSize;
                paper.StampedBy = new List<StampDisplayInfo>(source.StampedBy);
                paper.StampState = source.StampState;
                Dirty(copy, paper);
            }
        }
        else if (TryComp<PhotographComponent>(original, out var photo))
        {
            // A photograph copy keeps the original's prototype so the frame matches
            if (MetaData(original).EntityPrototype is not { } proto)
                return;

            copy = Spawn(proto.ID, _transform.GetMapCoordinates(ent.Owner));
            if (TryComp<PhotographComponent>(copy, out var newPhoto))
            {
                newPhoto.NameText = photo.NameText;
                // Examine mutates the message, the copy must not share it with the original
                newPhoto.Description = photo.Description == null ? null : new FormattedMessage(photo.Description);
                Dirty(copy, newPhoto);
            }
        }
        else
        {
            return;
        }

        _metaData.SetEntityName(copy, Name(original));
        _popup.PopupEntity(Loc.GetString("cmu-photocopier-printed"), ent, user);
    }

    private void OnGetVerbs(Entity<CMUPhotocopierComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryGetPaper(ent, out _))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-photocopier-verb-copy"),
            Act = () => StartCopy(ent, user),
        });

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-photocopier-verb-eject"),
            Act = () => EjectPaper(ent, user),
        });
    }

    private void EjectPaper(Entity<CMUPhotocopierComponent> ent, EntityUid user)
    {
        if (!TryGetPaper(ent, out var paper))
            return;

        _container.Remove(paper, _container.GetContainer(ent.Owner, PaperSlot));
        _hands.TryPickupAnyHand(user, paper);
        _popup.PopupEntity(Loc.GetString("cmu-photocopier-ejected"), ent, user);
    }

    private void OnExamined(Entity<CMUPhotocopierComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushText(Loc.GetString("cmu-photocopier-toner", ("toner", ent.Comp.Toner), ("max", ent.Comp.TonerMax)));

        if (TryGetPaper(ent, out _))
            args.PushText(Loc.GetString("cmu-photocopier-paper-inside"));
    }

    private bool TryGetPaper(Entity<CMUPhotocopierComponent> ent, out EntityUid paper)
    {
        paper = default;

        if (_container.TryGetContainer(ent.Owner, PaperSlot, out var container) &&
            container.ContainedEntities.Count > 0)
            paper = container.ContainedEntities[0];

        return paper != default;
    }
}
