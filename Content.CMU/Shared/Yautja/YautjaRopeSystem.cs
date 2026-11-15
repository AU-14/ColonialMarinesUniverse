using Content.Shared._RMC14.Xenonids;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Rejuvenate;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaRopeSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> KnifeTag = "Knife";
    private static readonly ProtoId<ToolQualityPrototype> SlicingQuality = "Slicing";
    private static readonly SoundSpecifier CutDownSound = new SoundPathSpecifier("/Audio/Weapons/bladeslice.ogg");

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private SharedToolSystem _tool = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedWeatherSystem _weather = default!;

    public override void Initialize()
    {
        // before the corpse sees the click, the medical and trophy interactions on bodies would eat it
        SubscribeLocalEvent<YautjaRopeComponent, BeforeRangedInteractEvent>(OnBeforeInteract);
        SubscribeLocalEvent<YautjaRopeComponent, YautjaRopeSecureDoAfterEvent>(OnSecureDoAfter);
        SubscribeLocalEvent<YautjaRopeComponent, YautjaRopeHangDoAfterEvent>(OnHangDoAfter);

        SubscribeLocalEvent<YautjaHungComponent, PullAttemptEvent>(OnHungPullAttempt);
        SubscribeLocalEvent<YautjaHungComponent, InteractUsingEvent>(OnHungInteractUsing);
        SubscribeLocalEvent<YautjaHungComponent, MobStateChangedEvent>(OnHungMobStateChanged);
        SubscribeLocalEvent<YautjaHungComponent, RejuvenateEvent>(OnHungRejuvenate);
    }

    private void OnBeforeInteract(Entity<YautjaRopeComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !_mobState.IsDead(target))
            return;

        args.Handled = true;
        var user = args.User;
        if (!CanHang(user, target, popup: true))
            return;

        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-rope-secure-start-self"),
            Loc.GetString("cmu-yautja-rope-secure-start-others", ("user", user)),
            user,
            user);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.SecureDelay, new YautjaRopeSecureDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        });
    }

    private void OnSecureDoAfter(Entity<YautjaRopeComponent> ent, ref YautjaRopeSecureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanHang(args.User, target, popup: true))
            return;

        args.Handled = true;
        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-rope-secured-self"),
            Loc.GetString("cmu-yautja-rope-secured-others", ("user", args.User)),
            args.User,
            args.User);
        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-rope-hang-start-self", ("target", target)),
            Loc.GetString("cmu-yautja-rope-hang-start-others", ("user", args.User), ("target", target)),
            args.User,
            args.User,
            PopupType.MediumCaution);

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.HangDelay, new YautjaRopeHangDoAfterEvent(), ent, target, ent)
        {
            BreakOnMove = true,
            NeedHand = true,
            BlockDuplicate = true,
        });
    }

    private void OnHangDoAfter(Entity<YautjaRopeComponent> ent, ref YautjaRopeHangDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !CanHang(args.User, target, popup: true))
            return;

        args.Handled = true;
        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-rope-hung-self", ("target", target)),
            Loc.GetString("cmu-yautja-rope-hung-others", ("user", args.User), ("target", target)),
            args.User,
            args.User,
            PopupType.MediumCaution);

        // the body, the stack and the anchor are all networked, let the server own them
        if (_net.IsClient)
            return;

        if (TryComp(target, out PullableComponent? pullable) && pullable.BeingPulled)
            _pulling.TryStopPull(target, pullable);

        var hung = EnsureComp<YautjaHungComponent>(target);
        hung.Swing = _random.Next(3, 7) / 32f;
        Dirty(target, hung);
        _transform.AnchorEntity(target);
        _stack.TryUse((ent.Owner, null), 1);
    }

    private bool CanHang(EntityUid user, EntityUid target, bool popup)
    {
        if (!_mobState.IsDead(target) || HasComp<YautjaHungComponent>(target))
            return false;

        if (!HasComp<HumanoidProfileComponent>(target) || HasComp<XenoComponent>(target))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cmu-yautja-rope-wrong-body", ("target", target)), user, user);
            return false;
        }

        // cmss13 checks TRAIT_SUPER_STRONG, which yautja and linked thralls have
        if (!HasComp<YautjaComponent>(user) && !HasComp<YautjaThrallHeroComponent>(user))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cmu-yautja-rope-not-strong", ("target", target)), user, user);
            return false;
        }

        if (!HasCeiling(target))
        {
            if (popup)
                _popup.PopupClient(Loc.GetString("cmu-yautja-rope-no-ceiling"), user, user);
            return false;
        }

        return true;
    }

    // no ceiling concept in RMC areas, so open sky is "weather would land here", same as the cloak's rain check
    private bool HasCeiling(EntityUid target)
    {
        var xform = Transform(target);
        if (xform.GridUid is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return false;

        var tile = _map.GetTileRef(gridUid, grid, xform.Coordinates);
        return !_weather.CanWeatherAffect((gridUid, grid, null), tile);
    }

    private void OnHungPullAttempt(Entity<YautjaHungComponent> ent, ref PullAttemptEvent args)
    {
        if (args.PulledUid == ent.Owner)
            args.Cancelled = true;
    }

    private void OnHungInteractUsing(Entity<YautjaHungComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !IsSharp(args.Used))
            return;

        args.Handled = true;
        _popup.PopupPredicted(
            Loc.GetString("cmu-yautja-rope-cut-self", ("target", ent.Owner), ("tool", args.Used)),
            Loc.GetString("cmu-yautja-rope-cut-others", ("user", args.User), ("target", ent.Owner), ("tool", args.Used)),
            args.User,
            args.User);
        _audio.PlayPredicted(CutDownSound, ent, args.User);
        CutDown(ent);
    }

    private void OnHungMobStateChanged(Entity<YautjaHungComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-rope-fall", ("target", ent.Owner)), ent, PopupType.MediumCaution);
        CutDown(ent);
    }

    private void OnHungRejuvenate(Entity<YautjaHungComponent> ent, ref RejuvenateEvent args)
    {
        CutDown(ent);
    }

    private void CutDown(Entity<YautjaHungComponent> ent)
    {
        if (_net.IsClient)
            return;

        _transform.Unanchor(ent.Owner, Transform(ent));
        RemCompDeferred<YautjaHungComponent>(ent);
    }

    private bool IsSharp(EntityUid item)
    {
        return _tags.HasTag(item, KnifeTag) ||
               _tool.HasQuality(item, SlicingQuality) ||
               TryComp(item, out MeleeWeaponComponent? melee) &&
               melee.Damage.DamageDict.TryGetValue("Slash", out var slash) &&
               slash > 0;
    }
}
