using Content.Shared.ActionBlocker;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Fob;

public sealed partial class CMUFobCrateSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedEntityStorageSystem _storage = default!;
    [Dependency] private SharedToolSystem _tools = default!;

    private static readonly ProtoId<ToolQualityPrototype> WrenchQuality = "Anchoring";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUFobCrateComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUFobCrateComponent, StorageOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMUFobCrateComponent, CMUFobCrateOpenDoAfterEvent>(OnOpenDoAfter);
        SubscribeLocalEvent<CMUFobCrateComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUFobCrateComponent, ExaminedEvent>(OnExamined);
    }

    private void OnOpenAttempt(Entity<CMUFobCrateComponent> ent, ref StorageOpenAttemptEvent args)
    {
        if (ent.Comp.OpeningUser == args.User || !TryComp<EntityStorageComponent>(ent, out var storage)
            || storage.Open || storage.Contents.Contains(args.User))
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupEntity(Loc.GetString("cmu-fob-crate-wrench"), ent, args.User);
    }

    private void OnInteractUsing(Entity<CMUFobCrateComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled && TryStartOpening(ent, args.User, args.Used))
            args.Handled = true;
    }

    private bool CanUseWrench(EntityUid crate, EntityUid user, EntityUid tool)
    {
        return !_storage.IsOpen(crate)
               && _hands.TryGetActiveItem(user, out var held) && held == tool
               && _tools.HasQuality(tool, WrenchQuality)
               && _blocker.CanInteract(user, crate)
               && _interaction.InRangeUnobstructed(user, crate);
    }

    // the server vetoes opening on the dropship landing footprint, the terminal can't be deployed there
    private bool CanOpenHere(EntityUid crate, EntityUid user)
    {
        var ev = new CMUFobCrateOpenAttemptEvent(user);
        RaiseLocalEvent(crate, ref ev);
        return !ev.Cancelled;
    }

    private bool TryStartOpening(Entity<CMUFobCrateComponent> ent, EntityUid user, EntityUid tool)
    {
        return CanUseWrench(ent, user, tool)
               && CanOpenHere(ent, user)
               && _tools.UseTool(tool, user, ent, (float) ent.Comp.OpenDuration.TotalSeconds, WrenchQuality,
                   new CMUFobCrateOpenDoAfterEvent());
    }

    private void OnOpenDoAfter(Entity<CMUFobCrateComponent> ent, ref CMUFobCrateOpenDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Used is not { } tool || !CanUseWrench(ent, args.User, tool) ||
            !CanOpenHere(ent, args.User))
            return;

        args.Handled = true;
        ent.Comp.OpeningUser = args.User;
        try
        {
            // Keep ordinary storage locks, welding checks, contents, sounds and animation events.
            _storage.TryOpenStorage(args.User, ent);
        }
        finally
        {
            ent.Comp.OpeningUser = null;
        }
    }

    private void OnGetVerbs(Entity<CMUFobCrateComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } tool || !CanUseWrench(ent, args.User, tool))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-fob-crate-open"),
            Act = () => TryStartOpening(ent, user, tool),
        });
    }

    private void OnExamined(Entity<CMUFobCrateComponent> ent, ref ExaminedEvent args)
    {
        if (args.IsInDetailsRange && !_storage.IsOpen(ent))
            args.PushMarkup(Loc.GetString("cmu-fob-crate-wrench"));
    }
}
