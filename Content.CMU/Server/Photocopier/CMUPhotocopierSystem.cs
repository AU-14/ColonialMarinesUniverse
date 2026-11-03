using Content.Shared.CMU14.Photocopier;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Photocopier;

/// <summary>
/// Paper copying with a toner pool, per CM-SS13's photocopier. Insert a sheet, activate to copy.
/// </summary>
public sealed class CMUPhotocopierSystem : EntitySystem
{
    public const string PaperSlot = "cmu_photocopier_paper";

    private static readonly EntProtoId CopyPrototype = "CMPaper";

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
        SubscribeLocalEvent<CMUPhotocopierComponent, GetVerbsEvent<InteractionVerbEvent>>(OnGetVerbs);
        SubscribeLocalEvent<CMUPhotocopierComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<CMUPhotocopierComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<PaperComponent>(args.Used, out _))
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
            BreakOnUserMove = true,
        };

        _doAfter.TryStartDoAfter(doAfter);
        _popup.PopupEntity(Loc.GetString("cmu-photocopier-copying"), ent, user);
    }

    private void OnCopyDoAfter(Entity<CMUPhotocopierComponent> ent, ref CMUPhotocopierCopyDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.User is not { Valid: true } user)
            return;

        args.Handled = true;

        if (!TryGetPaper(ent, out var original) ||
            !TryComp<PaperComponent>(original, out var source))
            return;

        if (ent.Comp.Toner <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-photocopier-no-toner"), ent, user, PopupType.MediumCaution);
            return;
        }

        ent.Comp.Toner--;

        var copy = Spawn(CopyPrototype, _transform.GetMapCoordinates(ent.Owner));
        if (TryComp<PaperComponent>(copy, out var paper))
        {
            paper.Content = source.Content;
            paper.ContentSize = source.ContentSize;
            paper.StampedBy = new List<StampDisplayInfo>(source.StampedBy);
            paper.StampState = source.StampState;
            Dirty(copy, paper);
        }

        _metaData.SetEntityName(copy, Name(original));
        _popup.PopupEntity(Loc.GetString("cmu-photocopier-printed"), ent, user);
    }

    private void OnGetVerbs(Entity<CMUPhotocopierComponent> ent, ref GetVerbsEvent<InteractionVerbEvent> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryGetPaper(ent, out _))
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-photocopier-verb-copy"),
            Act = () => StartCopy(ent, args.User),
        });

        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-photocopier-verb-eject"),
            Act = () => EjectPaper(ent, args.User),
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
            container.ContainedEntity is { } contained)
            paper = contained;

        return paper != default;
    }
}
