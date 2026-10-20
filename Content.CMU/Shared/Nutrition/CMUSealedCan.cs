using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Nutrition;

/// <summary>
///     Ported from cmss13 PR 7895. A sealed food can that can't be opened by hand: a can opener does it quickly, a
///     blade crudely and slowly.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUSealedCanComponent : Component
{
    [DataField]
    public EntityWhitelist Openers = new() { Tags = new() { "CMUCanOpener" } };

    [DataField]
    public EntityWhitelist CrudeOpeners = new() { Tags = new() { "Knife" } };

    [DataField]
    public TimeSpan OpenTime = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan CrudeOpenTime = TimeSpan.FromSeconds(12);

    [DataField]
    public SoundSpecifier OpenSound = new SoundPathSpecifier("/Audio/CMU14/Items/Cans/can_open2.ogg");

    [DataField]
    public SoundSpecifier CrudeOpenSound = new SoundPathSpecifier("/Audio/CMU14/Items/Cans/can_open1.ogg");

    [DataField]
    public SoundSpecifier OpenedSound = new SoundPathSpecifier("/Audio/CMU14/Items/Cans/can_opened.ogg");
}

[Serializable, NetSerializable]
public sealed partial class CMUOpenCanDoAfterEvent : SimpleDoAfterEvent;

public sealed class CMUSealedCanSystem : EntitySystem
{
    private static readonly string[] Hisses = ["cmu-can-hiss-nice", "cmu-can-hiss-none", "cmu-can-hiss-small"];

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private OpenableSystem _openable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUSealedCanComponent, OpenableOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<CMUSealedCanComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUSealedCanComponent, CMUOpenCanDoAfterEvent>(OnOpenDoAfter);
        SubscribeLocalEvent<CMUSealedCanComponent, ExaminedEvent>(OnExamined);
    }

    private void OnOpenAttempt(Entity<CMUSealedCanComponent> ent, ref OpenableOpenAttemptEvent args)
    {
        // Only the do-after below opens these.
        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupClient(Loc.GetString("cmu-can-needs-opener", ("can", ent)), ent, user);
    }

    private void OnInteractUsing(Entity<CMUSealedCanComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_openable.IsClosed(ent))
            return;

        TimeSpan delay;
        SoundSpecifier sound;
        string message;
        if (_whitelist.IsWhitelistPass(ent.Comp.Openers, args.Used))
        {
            if (TryComp(args.Used, out ItemToggleComponent? toggle) && !_itemToggle.IsActivated((args.Used, toggle)))
            {
                args.Handled = true;
                _popup.PopupClient(Loc.GetString("cmu-can-unfold-opener", ("opener", args.Used)), ent, args.User);
                return;
            }

            delay = ent.Comp.OpenTime;
            sound = ent.Comp.OpenSound;
            message = "cmu-can-open-start";
        }
        else if (_whitelist.IsWhitelistPass(ent.Comp.CrudeOpeners, args.Used))
        {
            delay = ent.Comp.CrudeOpenTime;
            sound = ent.Comp.CrudeOpenSound;
            message = "cmu-can-open-crude-start";
        }
        else
        {
            return;
        }

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager, args.User, delay, new CMUOpenCanDoAfterEvent(), ent, ent, args.Used)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPredicted(sound, ent, args.User);
        var hiss = Loc.GetString(Hisses[(int) (ent.Owner.Id % Hisses.Length)]);
        _popup.PopupClient(Loc.GetString(message, ("hiss", hiss)), ent, args.User);
    }

    private void OnOpenDoAfter(Entity<CMUSealedCanComponent> ent, ref CMUOpenCanDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !_openable.IsClosed(ent))
            return;

        args.Handled = true;
        _openable.SetOpen(ent, true, user: args.User);
        _audio.PlayPredicted(ent.Comp.OpenedSound, ent, args.User);
        _popup.PopupClient(Loc.GetString("cmu-can-opened"), ent, args.User);
    }

    private void OnExamined(Entity<CMUSealedCanComponent> ent, ref ExaminedEvent args)
    {
        if (_openable.IsClosed(ent))
            args.PushMarkup(Loc.GetString("cmu-can-examine-sealed"));
    }
}
