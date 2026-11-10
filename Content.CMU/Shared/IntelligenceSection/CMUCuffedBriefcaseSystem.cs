using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tools.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared.CMU14.IntelligenceSection;

/// <summary>
///     Runs the Case Officer's cuffed briefcase and its key: cuffing, uncuffing, cutting the cuff, opening the case
///     only for whoever holds the key, and swallowing the key.
/// </summary>
public sealed partial class CMUCuffedBriefcaseSystem : EntitySystem
{
    private static readonly string[] KeyPockets = ["pocket2", "pocket1"];

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IngestionSystem _ingestion = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, MapInitEvent>(OnBriefcaseMapInit);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, GotEquippedHandEvent>(OnBriefcasePickedUp);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, GotUnequippedHandEvent>(OnBriefcaseLeftHand);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, InteractUsingEvent>(OnBriefcaseInteractUsing,
            before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, StorageInteractAttemptEvent>(OnBriefcaseStorageAttempt);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, ExaminedEvent>(OnBriefcaseExamined);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, CMUBriefcaseCuffDoAfterEvent>(OnCuffDoAfter);
        SubscribeLocalEvent<CMUCuffedBriefcaseComponent, CMUBriefcaseCutDoAfterEvent>(OnCutDoAfter);

        // Every subscription to one event from a system must share the same ordering.
        SubscribeLocalEvent<CMUCuffedBriefcaseWearerComponent, InteractUsingEvent>(OnWearerInteractUsing,
            before: [typeof(SharedStorageSystem)]);
        SubscribeLocalEvent<CMUCuffedBriefcaseWearerComponent, GetVerbsEvent<UtilityVerb>>(OnWearerUtilityVerbs);
        SubscribeLocalEvent<CMUCuffedBriefcaseWearerComponent, ExaminedEvent>(OnWearerExamined);

        SubscribeLocalEvent<CMUBriefcaseKeyComponent, GetVerbsEvent<InteractionVerb>>(OnKeyVerbs);
        SubscribeLocalEvent<CMUBriefcaseKeyComponent, CMUBriefcaseKeySwallowDoAfterEvent>(OnSwallowDoAfter);

        SubscribeLocalEvent<CMUSwallowedItemsComponent, TryVomitEvent>(OnSwallowerVomit);
        SubscribeLocalEvent<CMUSwallowedItemsComponent, EntityTerminatingEvent>(OnSwallowerTerminating);
    }

    #region Briefcase

    private void OnBriefcaseMapInit(Entity<CMUCuffedBriefcaseComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || ent.Comp.Key != null)
            return;

        var key = Spawn(ent.Comp.KeyPrototype, Transform(ent).Coordinates);
        var keyComp = EnsureComp<CMUBriefcaseKeyComponent>(key);
        keyComp.Briefcase = ent;
        Dirty(key, keyComp);

        ent.Comp.Key = key;
        Dirty(ent);
    }

    /// <summary>The first pickup, such as a Case Officer's spawn, cuffs the case on and pockets its key.</summary>
    private void OnBriefcasePickedUp(Entity<CMUCuffedBriefcaseComponent> ent, ref GotEquippedHandEvent args)
    {
        if (_net.IsClient || !ent.Comp.CuffOnFirstPickup)
            return;

        ent.Comp.CuffOnFirstPickup = false;
        Dirty(ent);

        if (ent.Comp.Key is { } key && !_container.IsEntityInContainer(key))
        {
            foreach (var pocket in KeyPockets)
            {
                if (_inventory.TryEquip(args.User, key, pocket, silent: true, force: true))
                    break;
            }
        }

        Cuff(ent, args.User, silent: true);
    }

    /// <summary>Forced out of the hand (the hand or body was lost): the cuff no longer holds anyone.</summary>
    private void OnBriefcaseLeftHand(Entity<CMUCuffedBriefcaseComponent> ent, ref GotUnequippedHandEvent args)
    {
        // The cuff's no-drop rule removes itself here, so only the cuff state is cleared.
        if (ent.Comp.CuffedTo == args.User)
            ClearCuff(ent, removeUnremoveable: false);
    }

    private void OnBriefcaseInteractUsing(Entity<CMUCuffedBriefcaseComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp(args.Used, out CMUBriefcaseKeyComponent? key))
        {
            args.Handled = true;
            TryStartCuffing(ent, args.User, (args.Used, key));
            return;
        }

        if (ent.Comp.CuffedTo != null && _tool.HasQuality(args.Used, ent.Comp.CutQuality))
        {
            args.Handled = true;
            TryStartCutting(ent, args.User, args.Used);
        }
    }

    /// <summary>The case only opens for someone holding its key in hand.</summary>
    private void OnBriefcaseStorageAttempt(Entity<CMUCuffedBriefcaseComponent> ent, ref StorageInteractAttemptEvent args)
    {
        if (args.Cancelled || IsHoldingKey(args.User, ent.Comp))
            return;

        args.Cancelled = true;
        if (!args.Silent)
            _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-locked", ("briefcase", ent.Owner)), ent, args.User);
    }

    private void OnBriefcaseExamined(Entity<CMUCuffedBriefcaseComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.CuffedTo is { } wearer)
            args.PushMarkup(Loc.GetString("cmu-intel-briefcase-examine-cuffed", ("wearer", Identity.Entity(wearer, EntityManager))));
        else if (ent.Comp.CuffCut)
            args.PushMarkup(Loc.GetString("cmu-intel-briefcase-examine-cut"));
        else
            args.PushMarkup(Loc.GetString("cmu-intel-briefcase-examine-free"));
    }

    private void OnCuffDoAfter(Entity<CMUCuffedBriefcaseComponent> ent, ref CMUBriefcaseCuffDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } key || ent.Comp.Key != key)
            return;

        args.Handled = true;
        if (ent.Comp.CuffedTo is { } wearer)
        {
            Uncuff(ent, args.User, wearer);
            return;
        }

        if (!ent.Comp.CuffCut && _hands.IsHolding(args.User, ent.Owner))
            Cuff(ent, args.User, silent: false);
    }

    private void OnCutDoAfter(Entity<CMUCuffedBriefcaseComponent> ent, ref CMUBriefcaseCutDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.CuffedTo is not { } wearer)
            return;

        args.Handled = true;
        ClearCuff(ent);
        ent.Comp.CuffCut = true;
        Dirty(ent);

        _audio.PlayPredicted(ent.Comp.UncuffSound, wearer, args.User);
        _popup.PopupPredicted(
            Loc.GetString("cmu-intel-briefcase-cut-self", ("briefcase", ent.Owner)),
            Loc.GetString("cmu-intel-briefcase-cut-others",
                ("user", Identity.Entity(args.User, EntityManager)),
                ("briefcase", ent.Owner),
                ("wearer", Identity.Entity(wearer, EntityManager))),
            wearer,
            args.User);
    }

    #endregion

    #region Wearer

    /// <summary>Someone else uses the case's key on its wearer.</summary>
    private void OnWearerInteractUsing(Entity<CMUCuffedBriefcaseWearerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || ent.Comp.Briefcase is not { } briefcase
            || !TryComp(briefcase, out CMUCuffedBriefcaseComponent? comp)
            || !TryComp(args.Used, out CMUBriefcaseKeyComponent? key))
        {
            return;
        }

        args.Handled = true;
        TryStartCuffing((briefcase, comp), args.User, (args.Used, key));
    }

    private void OnWearerUtilityVerbs(Entity<CMUCuffedBriefcaseWearerComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess
            || !args.CanInteract
            || args.Using is not { } used
            || ent.Comp.Briefcase is not { } briefcase
            || !TryComp(briefcase, out CMUCuffedBriefcaseComponent? comp))
        {
            return;
        }

        var user = args.User;
        if (TryComp(used, out CMUBriefcaseKeyComponent? key))
        {
            args.Verbs.Add(new UtilityVerb
            {
                Text = Loc.GetString("cmu-intel-briefcase-verb-unlock"),
                Act = () => TryStartCuffing((briefcase, comp), user, (used, key)),
            });
        }
        else if (_tool.HasQuality(used, comp.CutQuality))
        {
            args.Verbs.Add(new UtilityVerb
            {
                Text = Loc.GetString("cmu-intel-briefcase-verb-cut"),
                Act = () => TryStartCutting((briefcase, comp), user, used),
            });
        }
    }

    private void OnWearerExamined(Entity<CMUCuffedBriefcaseWearerComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Briefcase is { } briefcase)
        {
            args.PushMarkup(Loc.GetString("cmu-intel-briefcase-examine-wearer",
                ("wearer", Identity.Entity(ent, EntityManager)),
                ("briefcase", briefcase)));
        }
    }

    #endregion

    #region Key

    private void OnKeyVerbs(Entity<CMUBriefcaseKeyComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_hands.IsHolding(args.User, ent.Owner))
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString("cmu-intel-briefcase-key-verb-swallow"),
            Act = () => TryStartSwallowing(ent, user),
        });
    }

    /// <summary>Starts <paramref name="user"/> swallowing a key they are holding.</summary>
    public void TryStartSwallowing(Entity<CMUBriefcaseKeyComponent> ent, EntityUid user)
    {
        if (!_ingestion.HasMouthAvailable(user))
        {
            _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-key-mouth-blocked"), user, user);
            return;
        }

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.SwallowDelay, new CMUBriefcaseKeySwallowDoAfterEvent(), ent, ent, ent)
        {
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(args))
        {
            _popup.PopupPredicted(Loc.GetString("cmu-intel-briefcase-key-swallow-start", ("key", ent.Owner)),
                Loc.GetString("cmu-intel-briefcase-key-swallow-start-others", ("user", Identity.Entity(user, EntityManager))),
                user,
                user);
        }
    }

    private void OnSwallowDoAfter(Entity<CMUBriefcaseKeyComponent> ent, ref CMUBriefcaseKeySwallowDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_hands.IsHolding(args.User, ent.Owner) || !_ingestion.HasMouthAvailable(args.User))
            return;

        args.Handled = true;
        EnsureComp<CMUSwallowedItemsComponent>(args.User);
        var stomach = _container.EnsureContainer<Container>(args.User, CMUSwallowedItemsComponent.ContainerId);
        if (!_container.Insert(ent.Owner, stomach))
            return;

        _popup.PopupPredicted(Loc.GetString("cmu-intel-briefcase-key-swallowed", ("key", ent.Owner)),
            Loc.GetString("cmu-intel-briefcase-key-swallowed-others", ("user", Identity.Entity(args.User, EntityManager))),
            args.User,
            args.User);
    }

    #endregion

    #region Swallowed items

    /// <summary>Whatever was swallowed comes back up with the vomit.</summary>
    private void OnSwallowerVomit(Entity<CMUSwallowedItemsComponent> ent, ref TryVomitEvent args)
    {
        if (!_container.TryGetContainer(ent, CMUSwallowedItemsComponent.ContainerId, out var stomach)
            || stomach.ContainedEntities.Count == 0)
        {
            return;
        }

        _container.EmptyContainer(stomach, force: true, destination: Transform(ent).Coordinates);
        _popup.PopupPredicted(Loc.GetString("cmu-intel-swallowed-vomit-self"),
            Loc.GetString("cmu-intel-swallowed-vomit-others", ("user", Identity.Entity(ent, EntityManager))),
            ent,
            ent);
    }

    /// <summary>A destroyed body leaves what it swallowed behind instead of deleting it.</summary>
    private void OnSwallowerTerminating(Entity<CMUSwallowedItemsComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!_container.TryGetContainer(ent, CMUSwallowedItemsComponent.ContainerId, out var stomach))
            return;

        var xform = Transform(ent);
        if (TerminatingOrDeleted(xform.MapUid))
            return;

        _container.EmptyContainer(stomach, force: true, destination: xform.Coordinates);
    }

    #endregion

    private void TryStartCuffing(Entity<CMUCuffedBriefcaseComponent> ent, EntityUid user, Entity<CMUBriefcaseKeyComponent> key)
    {
        if (ent.Comp.Key != key.Owner)
        {
            _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-wrong-key", ("briefcase", ent.Owner)), ent, user);
            return;
        }

        if (ent.Comp.CuffedTo == null)
        {
            if (ent.Comp.CuffCut)
            {
                _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-cuff-gone", ("briefcase", ent.Owner)), ent, user);
                return;
            }

            if (!_hands.IsHolding(user, ent.Owner))
            {
                _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-hold-to-cuff", ("briefcase", ent.Owner)), ent, user);
                return;
            }
        }

        var target = ent.Comp.CuffedTo ?? user;
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.CuffDelay, new CMUBriefcaseCuffDoAfterEvent(), ent, target, key)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        _doAfter.TryStartDoAfter(args);
    }

    private void TryStartCutting(Entity<CMUCuffedBriefcaseComponent> ent, EntityUid user, EntityUid tool)
    {
        if (ent.Comp.CuffedTo is not { } wearer)
            return;

        if (!_tool.UseTool(tool, user, ent, ent.Comp.CutDelay, [ent.Comp.CutQuality], new CMUBriefcaseCutDoAfterEvent(), out _))
            return;

        _popup.PopupPredicted(
            Loc.GetString("cmu-intel-briefcase-cut-start-self", ("briefcase", ent.Owner)),
            Loc.GetString("cmu-intel-briefcase-cut-start-others",
                ("user", Identity.Entity(user, EntityManager)),
                ("wearer", Identity.Entity(wearer, EntityManager))),
            user,
            user,
            PopupType.MediumCaution);
    }

    /// <summary>Cuffs the case to <paramref name="wearer"/>, who must already be holding it.</summary>
    public void Cuff(Entity<CMUCuffedBriefcaseComponent> ent, EntityUid wearer, bool silent)
    {
        if (ent.Comp.CuffCut)
            return;

        ent.Comp.CuffedTo = wearer;
        Dirty(ent);

        var unremoveable = EnsureComp<UnremoveableComponent>(ent);
        unremoveable.DeleteOnDrop = false;
        Dirty(ent, unremoveable);

        var wearerComp = EnsureComp<CMUCuffedBriefcaseWearerComponent>(wearer);
        wearerComp.Briefcase = ent;
        Dirty(wearer, wearerComp);

        if (silent)
            return;

        _audio.PlayPredicted(ent.Comp.CuffSound, wearer, wearer);
        _popup.PopupClient(Loc.GetString("cmu-intel-briefcase-cuffed", ("briefcase", ent.Owner)), wearer, wearer);
    }

    private void Uncuff(Entity<CMUCuffedBriefcaseComponent> ent, EntityUid user, EntityUid wearer)
    {
        ClearCuff(ent);
        _audio.PlayPredicted(ent.Comp.UncuffSound, wearer, user);
        _popup.PopupPredicted(
            Loc.GetString("cmu-intel-briefcase-uncuffed", ("briefcase", ent.Owner)),
            Loc.GetString("cmu-intel-briefcase-uncuffed-others",
                ("user", Identity.Entity(user, EntityManager)),
                ("briefcase", ent.Owner)),
            wearer,
            user);
    }

    private void ClearCuff(Entity<CMUCuffedBriefcaseComponent> ent, bool removeUnremoveable = true)
    {
        if (ent.Comp.CuffedTo is { } wearer)
            RemComp<CMUCuffedBriefcaseWearerComponent>(wearer);

        if (removeUnremoveable)
            RemComp<UnremoveableComponent>(ent);
        ent.Comp.CuffedTo = null;
        Dirty(ent);
    }

    private bool IsHoldingKey(EntityUid user, CMUCuffedBriefcaseComponent briefcase)
    {
        return briefcase.Key is { } key && _hands.IsHolding(user, key);
    }
}
