using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Vendors;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
using Content.Server._CMU14.Language;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.NameModifier.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Speech;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaThrallSystem : EntitySystem
{
    private const int MaxMessageLength = 160;
    private const int BloodedNameMaxLength = 64;
    private const string HivebreakerDishonoredReason = "cmu-yautja-hivebreaker-dishonored-reason";
    private const string BadBloodHiveName = "cmu-yautja-badblood-hive-name";
    private const string BloodedNamePrompt = "cmu-yautja-blooded-name-prompt";
    private const string BloodedNameTitle = "cmu-yautja-blooded-name-title";
    private static readonly EntProtoId BadBloodHivePrototype = "CMXenoHive";
    private static readonly ProtoId<NpcFactionPrototype> BadBloodHiveFaction = "CMUYautjaBadBlood";
    private static readonly ProtoId<NpcFactionPrototype> BloodedThrallNpcFaction = "CMUYautja";
    private static readonly EntProtoId<IFFFactionComponent> BloodedThrallIffFaction = "FactionYautja";
    private static readonly TimeSpan ThrallSelfDestructDialogTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan WarningEvery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ThrallStunTime = TimeSpan.FromSeconds(10);
    private static readonly Color MessageColor = Color.FromHex("#b85440");
    private static readonly SoundSpecifier ThrallShockSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");
    private static readonly Dictionary<string, int> Cmss13YautjaBuyCategoryUses = new()
    {
        ["CMUYautjaEssentials"] = 0,
        ["CMUYautjaArmor"] = 0,
        ["CMUYautjaPrimary"] = 0,
        ["CMUYautjaBracer"] = 0,
        ["CMUYautjaRanged"] = 0,
        ["CMUYautjaSupport"] = 0,
        ["CMUYautjaAccessory"] = 0,
    };

    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private GunIFFSystem _iff = default!;
    [Dependency] private SharedXenoHiveSystem _hive = default!;
    [Dependency] private CMUXenoLanguageSystem _xenoLanguage = default!;
    [Dependency] private SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private YautjaMarkSystem _marks = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private NameModifierSystem _nameModifier = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedCMAutomatedVendorSystem _vendors = default!;
    [Dependency] private XenoSystem _xeno = default!;
    [Dependency] private YautjaYoungbloodSystem _youngblood = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkAttemptEvent>(OnMarkAttempt);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkAppliedEvent>(OnMarkApplied);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkRemoveAttemptEvent>(OnMarkRemoveAttempt);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkRemovedEvent>(OnMarkRemoved);

        SubscribeLocalEvent<YautjaThrallComponent, ComponentRemove>(OnThrallRemoved);
        SubscribeLocalEvent<YautjaThrallComponent, YautjaThrallSelfDestructConfirmEvent>(OnThrallSelfDestructConfirm);
        SubscribeLocalEvent<YautjaHivebrokenXenoComponent, RefreshNameModifiersEvent>(OnHivebrokenRefreshName);

        SubscribeLocalEvent<YautjaBracerComponent, YautjaLinkThrallBracerActionEvent>(OnLinkThrallBracer);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaTransmitThrallMessageActionEvent>(OnMasterMessage);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaStunThrallActionEvent>(OnStunThrall);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructThrallActionEvent>(OnSelfDestructThrall);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaBloodedThrallNameEvent>(OnBloodedThrallName);

        SubscribeLocalEvent<YautjaThrallBracerComponent, GetItemActionsEvent>(OnGetThrallBracerActions);
        SubscribeLocalEvent<YautjaThrallBracerComponent, GotEquippedEvent>(OnThrallBracerEquipped);
        SubscribeLocalEvent<YautjaThrallBracerComponent, GotUnequippedEvent>(OnThrallBracerUnequipped);
        SubscribeLocalEvent<YautjaThrallBracerComponent, EntityTerminatingEvent>(OnThrallBracerTerminating);
        SubscribeLocalEvent<YautjaThrallBracerComponent, BeingUnequippedAttemptEvent>(OnThrallBracerUnequipAttempt);
        SubscribeLocalEvent<YautjaThrallBracerComponent, YautjaTransmitThrallMessageActionEvent>(OnThrallMessage);
        SubscribeLocalEvent<YautjaThrallBracerComponent, YautjaToggleBracerNotificationSoundActionEvent>(OnToggleThrallNotificationSound);
        SubscribeLocalEvent<YautjaThrallBracerComponent, YautjaToggleThrallBracerLockActionEvent>(OnToggleThrallBracerLock);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaThrallMessageUIKey.Key, subs =>
        {
            subs.Event<YautjaThrallSendMessageMsg>(OnMasterSendMessage);
        });

        Subs.BuiEvents<YautjaThrallBracerComponent>(YautjaThrallMessageUIKey.Key, subs =>
        {
            subs.Event<YautjaThrallSendMessageMsg>(OnThrallSendMessage);
        });
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaThrallBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (!bracer.SelfDestructArmed)
                continue;

            if (now >= bracer.SelfDestructAt)
            {
                DetonateThrallBracer((uid, bracer));
                continue;
            }

            if (bracer.User is not { } user || now < bracer.NextSelfDestructWarning)
                continue;

            var seconds = Math.Max(1, (int) Math.Ceiling((bracer.SelfDestructAt - now).TotalSeconds));
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-warning", ("seconds", seconds)), user, user, PopupType.LargeCaution);
            _audio.PlayPvs(bracer.SelfDestructWarningSound, user);
            bracer.NextSelfDestructWarning = now + WarningEvery;
        }
    }

    private void OnMarkAttempt(Entity<YautjaMarkComponent> ent, ref YautjaMarkAttemptEvent args)
    {
        _youngblood.HandleMarkAttempt(ref args);
        if (HasComp<YautjaYoungbloodComponent>(args.Target))
            return;

        if (args.Kind != YautjaMarkKind.Thrall && args.Kind != YautjaMarkKind.Blooded)
            return;

        if (!HasComp<YautjaComponent>(args.Hunter))
        {
            args.Cancelled = true;
            return;
        }

        if (!HasComp<HumanoidAppearanceComponent>(args.Target) ||
            HasComp<YautjaComponent>(args.Target) ||
            _mob.IsDead(args.Target))
        {
            args.Cancelled = true;
            return;
        }

        if (args.Kind == YautjaMarkKind.Blooded)
        {
            if (TryComp(args.Target, out YautjaThrallComponent? existingBlooded) && existingBlooded.Blooded)
            {
                _popup.PopupEntity(
                    Loc.GetString(
                        "cmu-yautja-thrall-already-blooded",
                        ("target", args.Target),
                        ("hunter", existingBlooded.BloodedBy ?? existingBlooded.Master),
                        ("reason", existingBlooded.BloodingReason)),
                    args.Hunter,
                    args.Hunter,
                    PopupType.MediumCaution);
                args.Cancelled = true;
                return;
            }

            if (!TryGetThrall(args.Hunter, args.Target, out _))
            {
                _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-requires-thrall"), args.Hunter, args.Hunter, PopupType.SmallCaution);
                args.Cancelled = true;
            }

            return;
        }

        if (TryFindThrall(args.Hunter, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-has"), args.Hunter, args.Hunter, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (TryComp(args.Target, out YautjaThrallComponent? targetThrall) && targetThrall.Master != args.Hunter)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "cmu-yautja-thrall-already-claimed",
                    ("target", args.Target),
                    ("hunter", targetThrall.Master),
                    ("reason", targetThrall.Reason)),
                args.Hunter,
                args.Hunter,
                PopupType.SmallCaution);
            args.Cancelled = true;
        }
    }

    private void OnMarkApplied(Entity<YautjaMarkComponent> ent, ref YautjaMarkAppliedEvent args)
    {
        _youngblood.HandleMarkApplied(ref args);
        if (HasComp<YautjaYoungbloodComponent>(args.Target))
            return;

        if (args.Kind == YautjaMarkKind.Thrall)
        {
            MakeThrall(args.Hunter, args.Target, args.Reason);
            return;
        }

        if (args.Kind == YautjaMarkKind.Blooded)
            BloodThrall(args.Hunter, args.Target, args.Reason);
    }

    private void OnMarkRemoveAttempt(Entity<YautjaMarkComponent> ent, ref YautjaMarkRemoveAttemptEvent args)
    {
        _youngblood.HandleMarkRemoveAttempt(ref args);
        if (args.Kind != YautjaMarkKind.Thrall && args.Kind != YautjaMarkKind.Blooded)
            return;

        if (!TryComp(args.Target, out YautjaThrallComponent? thrall))
            return;

        if (args.Kind == YautjaMarkKind.Blooded && thrall.Master == args.Hunter)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-cannot-remove"), args.Hunter, args.Hunter, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (thrall.Master == args.Hunter)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-your-thrall"), args.Hunter, args.Hunter, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnMarkRemoved(Entity<YautjaMarkComponent> ent, ref YautjaMarkRemovedEvent args)
    {
        _youngblood.HandleMarkRemoved(ref args);
        if (!TryComp(args.Target, out YautjaThrallComponent? thrall) || thrall.Master != args.Hunter)
            return;

        if (args.Kind == YautjaMarkKind.Blooded)
        {
            if (thrall.BloodedBy == args.Hunter)
                thrall.BloodedBy = null;
            Dirty(args.Target, thrall);
            return;
        }

        if (args.Kind != YautjaMarkKind.Thrall)
            return;

        if (args.TargetDestroyed)
            DestroyThrall(args.Target, thrall, args.Hunter);
        else
            ReleaseThrall(args.Target, thrall, args.Hunter);
    }

    private void OnThrallRemoved(Entity<YautjaThrallComponent> ent, ref ComponentRemove args)
    {
        RestoreHivebrokenXeno(ent.Owner, ent.Comp);
        ClearThrallLinks(ent.Owner, ent.Comp);
        RemCompDeferred<YautjaTechAuthorizedComponent>(ent.Owner);
    }

    private void OnLinkThrallBracer(Entity<YautjaBracerComponent> ent, ref YautjaLinkThrallBracerActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryLinkThrallBracer(ent, args.Performer);
    }

    private void OnMasterMessage(Entity<YautjaBracerComponent> ent, ref YautjaTransmitThrallMessageActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryOpenMasterThrallTransmission(ent, args.Performer);
    }

    private void OnThrallMessage(Entity<YautjaThrallBracerComponent> ent, ref YautjaTransmitThrallMessageActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanUseThrallMessageBracer(ent, args.Performer))
            return;

        if (!TryGetReceiverFromThrall(ent, args.Performer, out _, out _))
            return;

        _ui.TryOpenUi(ent.Owner, YautjaThrallMessageUIKey.Key, args.Performer);
    }

    private void OnStunThrall(Entity<YautjaBracerComponent> ent, ref YautjaStunThrallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryStunLinkedThrall(ent, args.Performer);
    }

    private void OnSelfDestructThrall(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructThrallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleLinkedThrallSelfDestruct(ent, args.Performer);
    }

    public bool TryLinkThrallBracer(Entity<YautjaBracerComponent> masterBracer, EntityUid master)
    {
        if (!CanUseMasterBracer(masterBracer, master))
            return false;

        if (!TryFindThrall(master, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-none"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (TryGetLinkedThrallSilent(master, thrall, out _))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-link-already"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetWornThrallBracer(thrall.Owner, out var thrallBracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-no-bracer"), master, master, PopupType.SmallCaution);
            return false;
        }

        LinkBracers(masterBracer, master, thrall, thrallBracer);
        return true;
    }

    public bool TryOpenMasterThrallTransmission(Entity<YautjaBracerComponent> masterBracer, EntityUid master)
    {
        if (!CanUseMasterBracer(masterBracer, master) ||
            !TryGetReceiverFromMaster(masterBracer, master, out _, out _))
        {
            return false;
        }

        _ui.TryOpenUi(masterBracer.Owner, YautjaThrallMessageUIKey.Key, master);
        return true;
    }

    public bool TryStunLinkedThrall(Entity<YautjaBracerComponent> masterBracer, EntityUid master)
    {
        if (!CanUseMasterBracer(masterBracer, master))
            return false;

        if (!TryFindThrall(master, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-stun-none"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (HasComp<StunnedComponent>(thrall.Owner) || HasComp<KnockedDownComponent>(thrall.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-stunned"), master, master, PopupType.SmallCaution);
            return false;
        }

        var stunTime = ThrallStunTime;
        var shockSound = ThrallShockSound;
        if (TryGetLinkedThrallSilent(master, thrall, out var bracer))
        {
            stunTime = bracer.Comp.StunTime;
            shockSound = bracer.Comp.ShockSound;
        }

        _stun.TryParalyze(thrall.Owner, stunTime, true, force: true);
        _audio.PlayPvs(shockSound, thrall.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-stunned-master"), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-stunned-target"), thrall.Owner, thrall.Owner, PopupType.LargeCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(master):hunter} remotely stunned thrall {ToPrettyString(thrall.Owner):thrall}");
        return true;
    }

    public bool TryToggleLinkedThrallSelfDestruct(Entity<YautjaBracerComponent> masterBracer, EntityUid master)
    {
        if (!CanUseMasterBracer(masterBracer, master))
            return false;

        if (_mob.IsDead(master))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-dead"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (_mob.IsCritical(master))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-critical"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (!_mob.IsAlive(master))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-unconscious"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (!TryFindThrall(master, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-none"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (IsInHuntingGrounds(thrall.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-self-destruct-preserve"), master, master, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetThrallRemoteSelfDestructBracer(masterBracer, master, thrall, out var bracer))
            return false;

        if (bracer.Comp.SelfDestructArmed)
            return false;

        OpenThrallSelfDestructDialog(master, thrall.Owner, bracer);
        return true;
    }

    private void OnThrallSelfDestructConfirm(Entity<YautjaThrallComponent> ent, ref YautjaThrallSelfDestructConfirmEvent args)
    {
        if (!TryGetEntity(args.Master, out var master) ||
            !TryGetEntity(args.ThrallBracer, out var bracerId) ||
            !TryComp(bracerId, out YautjaThrallBracerComponent? bracer) ||
            ent.Comp.Master != master.Value ||
            bracer.User != ent.Owner ||
            !TryFindThrall(master.Value, out var currentThrall) ||
            currentThrall.Owner != ent.Owner ||
            !TryGetWornThrallBracer(ent.Owner, out var wornBracer) ||
            wornBracer.Owner != bracerId)
        {
            return;
        }

        if (bracer.SelfDestructArmed)
            return;

        ArmThrallSelfDestruct((bracerId.Value, bracer), master.Value, ent.Owner);
    }

    private void OnGetThrallBracerActions(Entity<YautjaThrallBracerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        if (HasComp<YautjaBracerComponent>(ent.Owner))
            return;

        if (HasComp<YautjaThrallComponent>(args.User))
        {
            args.AddAction(ref ent.Comp.TransmitThrallMessageAction, ent.Comp.TransmitThrallMessageActionId);
            args.AddAction(ref ent.Comp.ToggleNotificationSoundAction, ent.Comp.ToggleNotificationSoundActionId);
        }
    }

    private void OnToggleThrallNotificationSound(Entity<YautjaThrallBracerComponent> ent, ref YautjaToggleBracerNotificationSoundActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args) || !CanUseThrallBracer(ent, args.Performer))
            return;

        args.Handled = true;
        ent.Comp.NotificationSound = !ent.Comp.NotificationSound;
        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-sound-now", ("state", Loc.GetString(ent.Comp.NotificationSound ? "cmu-yautja-state-on" : "cmu-yautja-state-off"))), args.Performer, args.Performer);
    }

    private void OnThrallBracerEquipped(Entity<YautjaThrallBracerComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        ent.Comp.User = args.Equipee;
        _audio.PlayPvs(ent.Comp.EquipSound, ent.Owner);
    }

    private void OnThrallBracerUnequipped(Entity<YautjaThrallBracerComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        CleanupThrallBracerLink(ent, clearReciprocal: true);
    }

    private void OnThrallBracerTerminating(Entity<YautjaThrallBracerComponent> ent, ref EntityTerminatingEvent args)
    {
        CleanupThrallBracerLink(ent, clearReciprocal: true);
    }

    private void OnThrallBracerUnequipAttempt(Entity<YautjaThrallBracerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancel();
        args.Reason = "cmu-yautja-thrall-bracer-locked";
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-bracer-locked"), args.Unequipee, args.Unequipee, PopupType.SmallCaution);
    }

    private void OnToggleThrallBracerLock(Entity<YautjaThrallBracerComponent> ent, ref YautjaToggleThrallBracerLockActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanToggleThrallBracerLock(ent, args.Performer))
            return;

        ToggleThrallBracerLock(ent, args.Performer);
    }

    private bool ToggleThrallBracerLock(Entity<YautjaThrallBracerComponent> ent, EntityUid user)
    {
        ent.Comp.Locked = !ent.Comp.Locked;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ToggleLockAction, ent.Comp.Locked);
        _audio.PlayPvs(ent.Comp.LockSound, ent.Owner);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Locked
            ? "cmu-yautja-thrall-bracer-locked-now"
            : "cmu-yautja-thrall-bracer-unlocked-now"), user, user);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} {(ent.Comp.Locked ? "locked" : "unlocked")} Yautja thrall bracer {ToPrettyString(ent.Owner):bracer}");
        return true;
    }

    private void OnMasterSendMessage(Entity<YautjaBracerComponent> ent, ref YautjaThrallSendMessageMsg args)
    {
        if (!CanUseMasterBracer(ent, args.Actor) ||
            !TryGetReceiverFromMaster(ent, args.Actor, out var receiver, out var receiverBracer))
        {
            return;
        }

        SendBracerMessage(args.Actor, receiver, ent.Owner, receiverBracer, args.Message);
    }

    private void OnThrallSendMessage(Entity<YautjaThrallBracerComponent> ent, ref YautjaThrallSendMessageMsg args)
    {
        if (!CanUseThrallMessageBracer(ent, args.Actor) ||
            !TryGetReceiverFromThrall(ent, args.Actor, out var receiver, out var receiverBracer))
        {
            return;
        }

        SendBracerMessage(args.Actor, receiver, ent.Owner, receiverBracer, args.Message);
    }

    public bool HivebreakXeno(EntityUid master, EntityUid target, EntityUid source, YautjaHivebreakerComponent hivebreaker)
    {
        if (!TryComp(target, out XenoComponent? xeno) ||
            HasComp<YautjaThrallComponent>(target) ||
            xeno.Tier == 0 ||
            hivebreaker.BannedXenoRoles.Contains(xeno.Role))
        {
            return false;
        }

        var thrall = EnsureComp<YautjaThrallComponent>(target);
        CaptureHivebreakOriginalState(target, thrall);
        var originalHive = thrall.HivebreakOriginalHive;

        thrall.Master = master;
        thrall.Reason = Loc.GetString("cmu-yautja-hivebreaker-thrall-reason");
        thrall.BracerLinked = false;
        thrall.MasterBracer = null;
        thrall.ThrallBracer = null;
        thrall.Blooded = hivebreaker.BloodOnConversion;
        thrall.BloodedBy = hivebreaker.BloodOnConversion ? master : null;
        thrall.BloodingReason = hivebreaker.BloodOnConversion ? thrall.Reason : string.Empty;
        thrall.TechAuthorized = hivebreaker.AuthorizeTechOnConversion;
        thrall.Hivebroken = true;
        Dirty(target, thrall);

        if (hivebreaker.ClearHiveOnConversion)
        {
            if (originalHive is { } hive && !TerminatingOrDeleted(hive))
            {
                _xenoAnnounce.AnnounceToHive(
                    target,
                    hive,
                    Loc.GetString("cmu-yautja-hivebreaker-hive-announcement", ("target", target)),
                    popup: PopupType.LargeCaution);
            }

            _hive.SetHive(target, GetOrCreateBadBloodHive());
        }

        SetHivebrokenNpcFaction(target, hivebreaker);
        SetHivebrokenIffFaction(target, hivebreaker);
        ApplyHivebrokenWeedBehavior(target, hivebreaker);
        ApplyHivebrokenRegen(target);
        ApplyHivebrokenSpeech(target, hivebreaker);
        ApplyHivebrokenName(target);
        _xenoLanguage.RefreshEnglish(target);

        if (hivebreaker.AuthorizeTechOnConversion)
            EnsureComp<YautjaTechAuthorizedComponent>(target);
        else
            RemCompDeferred<YautjaTechAuthorizedComponent>(target);

        if (hivebreaker.BloodOnConversion)
            GrantAllSkills(target, 4);

        _marks.ForceMark(target, target, YautjaMarkKind.Dishonored, reason: Loc.GetString(HivebreakerDishonoredReason));
        PopupHivebreakerEnthrallMessages(target);

        if (hivebreaker.HealOnConversion && TryComp(target, out DamageableComponent? damageable))
            _damage.SetAllDamage(target, damageable, 0);

        BroadcastToYautja(
            Loc.GetString("cmu-yautja-hivebreaker-broadcast",
                ("hunter", YautjaDisplayName(master)),
                ("target", target)),
            master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} hivebroke xeno {ToPrettyString(target):target} with {ToPrettyString(source):item}");

        return true;
    }

    private EntityUid GetOrCreateBadBloodHive()
    {
        var query = EntityQueryEnumerator<HiveComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid) &&
                _hive.HasFaction(uid, BadBloodHiveFaction))
            {
                return uid;
            }
        }

        var hive = Spawn(BadBloodHivePrototype);
        _meta.SetEntityName(hive, Loc.GetString(BadBloodHiveName));
        _hive.SetHiveFactionAlly(BadBloodHiveFaction, hive, true);
        return hive;
    }

    private void MakeThrall(EntityUid master, EntityUid target, string? reason)
    {
        var thrall = EnsureComp<YautjaThrallComponent>(target);
        thrall.Master = master;
        thrall.Reason = reason ?? string.Empty;
        thrall.Blooded = false;
        thrall.BloodedBy = null;
        thrall.BloodingReason = string.Empty;
        thrall.TechAuthorized = false;
        Dirty(target, thrall);

        RemCompDeferred<YautjaTechAuthorizedComponent>(target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-marked-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-marked-target", ("hunter", YautjaDisplayName(master))), target, target, PopupType.MediumCaution);
        BroadcastToYautja(Loc.GetString("cmu-yautja-thrall-broadcast", ("hunter", YautjaDisplayName(master)), ("target", target), ("reason", thrall.Reason)), master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} has taken {ToPrettyString(target):target} as their Thrall for '{thrall.Reason}'");
    }

    private void BloodThrall(EntityUid master, EntityUid target, string? reason)
    {
        if (!TryComp(target, out YautjaThrallComponent? thrall) || thrall.Master != master)
            return;

        thrall.Blooded = true;
        thrall.BloodedBy = master;
        thrall.BloodingReason = reason ?? string.Empty;
        thrall.TechAuthorized = true;
        Dirty(target, thrall);
        EnsureComp<YautjaTechAuthorizedComponent>(target);
        GrantAllSkills(target, 4);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-target"), target, target, PopupType.Medium);
        BroadcastToYautja(Loc.GetString("cmu-yautja-thrall-blooded-broadcast", ("hunter", YautjaDisplayName(master)), ("target", target), ("reason", thrall.BloodingReason)), master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} has blooded {ToPrettyString(target):target} for '{thrall.BloodingReason}'");

        OpenBloodedThrallNameDialog(master, target);
    }

    private void OpenBloodedThrallNameDialog(EntityUid master, EntityUid target)
    {
        if (!TryGetWornMasterBracer(master, out var bracer))
            return;

        _dialog.OpenInput(
            bracer.Owner,
            master,
            Loc.GetString(BloodedNamePrompt),
            new YautjaBloodedThrallNameEvent(GetNetEntity(master), GetNetEntity(target)),
            characterLimit: BloodedNameMaxLength,
            title: Loc.GetString(BloodedNameTitle));
    }

    private void OnBloodedThrallName(Entity<YautjaBracerComponent> bracer, ref YautjaBloodedThrallNameEvent args)
    {
        if (!TryGetEntity(args.Hunter, out var hunter) ||
            !TryGetEntity(args.Target, out var target) ||
            bracer.Comp.User != hunter.Value ||
            !IsMasterBracerWornBy(bracer, hunter.Value) ||
            !TryComp(target.Value, out YautjaThrallComponent? thrall) ||
            !thrall.Blooded ||
            thrall.Master != hunter.Value ||
            thrall.BloodedBy != hunter.Value)
        {
            return;
        }

        var name = args.Message.Trim();
        if (name.Length == 0)
            return;

        _meta.SetEntityName(target.Value, name);
        SetBloodedThrallNpcFaction(target.Value);
        SetBloodedThrallIffFaction(target.Value);
    }

    private void SetBloodedThrallNpcFaction(EntityUid target)
    {
        var faction = EnsureComp<NpcFactionMemberComponent>(target);
        _faction.ClearFactions((target, faction), false);
        _faction.AddFaction((target, faction), BloodedThrallNpcFaction);
    }

    private void SetBloodedThrallIffFaction(EntityUid target)
    {
        _iff.ClearUserFactions(target);
        _iff.AddUserFaction(target, BloodedThrallIffFaction);
    }

    private void ReleaseThrall(EntityUid target, YautjaThrallComponent thrall, EntityUid master)
    {
        ClearThrallLinks(target, thrall);
        RemCompDeferred<YautjaTechAuthorizedComponent>(target);
        RemCompDeferred<YautjaThrallComponent>(target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-released-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-released-target"), target, target, PopupType.SmallCaution);
        BroadcastToYautja(Loc.GetString("cmu-yautja-thrall-release-broadcast", ("hunter", YautjaDisplayName(master)), ("target", target)), master);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(master):hunter} has released {ToPrettyString(target):target} from thralldom!");
    }

    private void DestroyThrall(EntityUid target, YautjaThrallComponent thrall, EntityUid master)
    {
        ClearThrallLinks(target, thrall);
        RemCompDeferred<YautjaTechAuthorizedComponent>(target);
        RemCompDeferred<YautjaThrallComponent>(target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-destroyed-master"), master, master, PopupType.MediumCaution);
        BroadcastToYautja(Loc.GetString(
                "cmu-yautja-thrall-destroyed-broadcast",
                ("hunter", Name(master)),
                ("target", target)),
            master);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"Yautja thrall {ToPrettyString(target):target} for {ToPrettyString(master):hunter} was destroyed during huntdata cleanup");
    }

    private void LinkBracers(
        Entity<YautjaBracerComponent> masterBracer,
        EntityUid master,
        Entity<YautjaThrallComponent> thrall,
        Entity<YautjaThrallBracerComponent> thrallBracer)
    {
        thrall.Comp.BracerLinked = true;
        thrall.Comp.MasterBracer = masterBracer.Owner;
        thrall.Comp.ThrallBracer = thrallBracer.Owner;
        Dirty(thrall);
        InitializeThrallVendorCategories(thrall);

        thrallBracer.Comp.Master = master;
        thrallBracer.Comp.MasterBracer = masterBracer.Owner;
        thrallBracer.Comp.Linked = true;
        thrallBracer.Comp.Locked = true;
        Dirty(thrallBracer);

        _audio.PlayPvs(thrallBracer.Comp.LinkSound, thrallBracer.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-link-master", ("target", thrall.Owner)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-link-target", ("hunter", YautjaDisplayName(master))), thrall.Owner, thrall.Owner);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} linked bracer {ToPrettyString(masterBracer.Owner):masterBracer} to thrall bracer {ToPrettyString(thrallBracer.Owner):thrallBracer} on {ToPrettyString(thrall.Owner):thrall}");
    }

    private void InitializeThrallVendorCategories(Entity<YautjaThrallComponent> thrall)
    {
        var vendorUser = EnsureComp<CMVendorUserComponent>(thrall);
        _vendors.InitializeChoices((thrall.Owner, vendorUser), Cmss13YautjaBuyCategoryUses);
        _vendors.SetChoiceWhitelist((thrall.Owner, vendorUser), new HashSet<string>(Cmss13YautjaBuyCategoryUses.Keys));
    }

    private void ClearThrallLinks(EntityUid target, YautjaThrallComponent thrall)
    {
        if (thrall.ThrallBracer is { } thrallBracerId && TryComp(thrallBracerId, out YautjaThrallBracerComponent? bracer))
            CleanupThrallBracerLink((thrallBracerId, bracer), clearReciprocal: false);

        thrall.BracerLinked = false;
        thrall.MasterBracer = null;
        thrall.ThrallBracer = null;
        thrall.Blooded = false;
        thrall.BloodedBy = null;
        thrall.BloodingReason = string.Empty;
        thrall.TechAuthorized = false;
        Dirty(target, thrall);
    }

    private void CleanupThrallBracerLink(Entity<YautjaThrallBracerComponent> bracer, bool clearReciprocal)
    {
        var user = bracer.Comp.User;

        bracer.Comp.User = null;
        bracer.Comp.Master = null;
        bracer.Comp.MasterBracer = null;
        bracer.Comp.Linked = false;
        bracer.Comp.Locked = false;
        bracer.Comp.SelfDestructArmed = false;
        bracer.Comp.SelfDestructAt = TimeSpan.Zero;
        bracer.Comp.NextSelfDestructWarning = TimeSpan.Zero;
        Dirty(bracer);
        _actions.SetToggled(bracer.Comp.ToggleLockAction, false);

        if (!clearReciprocal || user is not { } thrallUid || !TryComp(thrallUid, out YautjaThrallComponent? thrall))
            return;

        if (thrall.ThrallBracer != bracer.Owner)
            return;

        thrall.BracerLinked = false;
        thrall.MasterBracer = null;
        thrall.ThrallBracer = null;
        Dirty(thrallUid, thrall);
    }

    private void CaptureHivebreakOriginalState(EntityUid target, YautjaThrallComponent thrall)
    {
        if (thrall.HivebreakOriginalStateCaptured)
            return;

        thrall.HivebreakOriginalStateCaptured = true;
        thrall.HivebreakOriginalHive = CompOrNull<HiveMemberComponent>(target)?.Hive;

        if (TryComp(target, out NpcFactionMemberComponent? faction))
        {
            thrall.HivebreakHadNpcFaction = true;
            thrall.HivebreakOriginalNpcFactions = new(faction.Factions);
        }
        else
        {
            thrall.HivebreakHadNpcFaction = false;
            thrall.HivebreakOriginalNpcFactions = new();
        }

        if (TryComp(target, out UserIFFComponent? iff))
        {
            thrall.HivebreakHadUserIff = true;
            thrall.HivebreakOriginalIffFactions = new(iff.Factions);
        }
        else
        {
            thrall.HivebreakHadUserIff = false;
            thrall.HivebreakOriginalIffFactions = new();
        }

        thrall.HivebreakHadIgnoreWeedsSlowdown = HasComp<IgnoreXenoWeedsSlowdownComponent>(target);

        if (TryComp(target, out SpeechComponent? speech))
        {
            thrall.HivebreakHadSpeech = true;
            thrall.HivebreakOriginalSpeechVerb = speech.SpeechVerb;
            thrall.HivebreakOriginalSpeechSounds = speech.SpeechSounds;
        }
        else
        {
            thrall.HivebreakHadSpeech = false;
            thrall.HivebreakOriginalSpeechVerb = null;
            thrall.HivebreakOriginalSpeechSounds = null;
        }

        if (TryComp(target, out XenoRegenComponent? regen))
        {
            thrall.HivebreakHadXenoRegen = true;
            thrall.HivebreakOriginalHealOffWeeds = regen.HealOffWeeds;
        }
        else
        {
            thrall.HivebreakHadXenoRegen = false;
            thrall.HivebreakOriginalHealOffWeeds = false;
        }

        thrall.HivebreakHadHivebrokenName = HasComp<YautjaHivebrokenXenoComponent>(target);
    }

    private void SetHivebrokenNpcFaction(EntityUid target, YautjaHivebreakerComponent hivebreaker)
    {
        var faction = EnsureComp<NpcFactionMemberComponent>(target);
        _faction.ClearFactions((target, faction), false);
        _faction.AddFaction((target, faction), hivebreaker.ThrallNpcFaction);
    }

    private void SetHivebrokenIffFaction(EntityUid target, YautjaHivebreakerComponent hivebreaker)
    {
        _iff.ClearUserFactions(target);
        _iff.AddUserFaction(target, hivebreaker.ThrallIffFaction);
    }

    private void ApplyHivebrokenWeedBehavior(EntityUid target, YautjaHivebreakerComponent hivebreaker)
    {
        if (!hivebreaker.IgnoreWeedSlowdownOnConversion)
            return;

        EnsureComp<IgnoreXenoWeedsSlowdownComponent>(target);
        _movement.RefreshMovementSpeedModifiers(target);
    }

    private void ApplyHivebrokenRegen(EntityUid target)
    {
        if (!TryComp(target, out XenoRegenComponent? regen) ||
            regen.HealOffWeeds)
        {
            return;
        }

        _xeno.SetHealOffWeeds((target, regen), true);
    }

    private void ApplyHivebrokenSpeech(EntityUid target, YautjaHivebreakerComponent hivebreaker)
    {
        if (!hivebreaker.HumanSpeechOnConversion)
            return;

        var speech = EnsureComp<SpeechComponent>(target);
        speech.SpeechVerb = hivebreaker.HumanSpeechVerb;
        speech.SpeechSounds = hivebreaker.HumanSpeechSounds;
        Dirty(target, speech);
    }

    private void ApplyHivebrokenName(EntityUid target)
    {
        EnsureComp<YautjaHivebrokenXenoComponent>(target);
        _nameModifier.RefreshNameModifiers(target);
    }

    private void OnHivebrokenRefreshName(Entity<YautjaHivebrokenXenoComponent> ent, ref RefreshNameModifiersEvent args)
    {
        args.AddModifier("cmu-yautja-hivebroken-xeno-name", priority: 25);
    }

    private void RestoreHivebrokenXeno(EntityUid target, YautjaThrallComponent thrall)
    {
        if (!thrall.Hivebroken ||
            !thrall.HivebreakOriginalStateCaptured ||
            TerminatingOrDeleted(target))
        {
            return;
        }

        if (HasComp<XenoComponent>(target))
            _hive.SetHive(target, thrall.HivebreakOriginalHive);

        if (thrall.HivebreakHadNpcFaction)
        {
            var faction = EnsureComp<NpcFactionMemberComponent>(target);
            _faction.ClearFactions((target, faction), thrall.HivebreakOriginalNpcFactions.Count == 0);
            if (thrall.HivebreakOriginalNpcFactions.Count > 0)
                _faction.AddFactions((target, faction), thrall.HivebreakOriginalNpcFactions);
        }
        else
        {
            RemCompDeferred<NpcFactionMemberComponent>(target);
        }

        if (thrall.HivebreakHadUserIff)
        {
            _iff.ClearUserFactions(target);
            foreach (var faction in thrall.HivebreakOriginalIffFactions)
            {
                _iff.AddUserFaction(target, faction);
            }
        }
        else
        {
            RemCompDeferred<UserIFFComponent>(target);
        }

        if (!thrall.HivebreakHadIgnoreWeedsSlowdown)
            RemComp<IgnoreXenoWeedsSlowdownComponent>(target);

        RestoreHivebrokenRegen(target, thrall);
        RestoreHivebrokenSpeech(target, thrall);
        RestoreHivebrokenName(target, thrall);
        _xenoLanguage.RefreshEnglish(target);
        _movement.RefreshMovementSpeedModifiers(target);

        _marks.TryClearMark(target, YautjaMarkKind.Dishonored, target);
        PopupHivebreakerDethrallMessages(target);
    }

    private void PopupHivebreakerEnthrallMessages(EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-enthralled-target"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-hivemind-lost"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-evolution-blocked"), target, target, PopupType.MediumCaution);
    }

    private void PopupHivebreakerDethrallMessages(EntityUid target)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-dethralled-target"), target, target, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-hivebreaker-hivemind-restored"), target, target, PopupType.LargeCaution);
    }

    private void RestoreHivebrokenRegen(EntityUid target, YautjaThrallComponent thrall)
    {
        if (!thrall.HivebreakHadXenoRegen ||
            !TryComp(target, out XenoRegenComponent? regen) ||
            regen.HealOffWeeds == thrall.HivebreakOriginalHealOffWeeds)
        {
            return;
        }

        _xeno.SetHealOffWeeds((target, regen), thrall.HivebreakOriginalHealOffWeeds);
    }

    private void RestoreHivebrokenSpeech(EntityUid target, YautjaThrallComponent thrall)
    {
        if (!thrall.HivebreakHadSpeech)
        {
            RemCompDeferred<SpeechComponent>(target);
            return;
        }

        var speech = EnsureComp<SpeechComponent>(target);

        if (thrall.HivebreakOriginalSpeechVerb is { } speechVerb)
            speech.SpeechVerb = speechVerb;

        speech.SpeechSounds = thrall.HivebreakOriginalSpeechSounds;
        Dirty(target, speech);
    }

    private void RestoreHivebrokenName(EntityUid target, YautjaThrallComponent thrall)
    {
        if (!thrall.HivebreakHadHivebrokenName)
            RemComp<YautjaHivebrokenXenoComponent>(target);

        _nameModifier.RefreshNameModifiers(target);
    }

    private bool TryFindThrall(EntityUid master, out Entity<YautjaThrallComponent> thrall)
    {
        var query = EntityQueryEnumerator<YautjaThrallComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Master != master || Deleted(uid) || _mob.IsDead(uid))
                continue;

            thrall = (uid, comp);
            return true;
        }

        if (TryMaterializeMarkedThrall(master, null, out thrall))
            return true;

        thrall = default;
        return false;
    }

    private bool TryFindMessageThrall(EntityUid master, out Entity<YautjaThrallComponent> thrall)
    {
        var query = EntityQueryEnumerator<YautjaThrallComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Master != master || Deleted(uid))
                continue;

            thrall = (uid, comp);
            return true;
        }

        return TryMaterializeMarkedThrall(master, null, out thrall);
    }

    private bool TryGetThrall(EntityUid master, EntityUid target, out Entity<YautjaThrallComponent> thrall)
    {
        if (TryComp(target, out YautjaThrallComponent? comp) &&
            comp.Master == master &&
            !Deleted(target) &&
            !_mob.IsDead(target))
        {
            thrall = (target, comp);
            return true;
        }

        return TryMaterializeMarkedThrall(master, target, out thrall);
    }

    private bool TryMaterializeMarkedThrall(EntityUid master, EntityUid? target, out Entity<YautjaThrallComponent> thrall)
    {
        var query = EntityQueryEnumerator<YautjaMarkComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (target is { } required && uid != required)
                continue;

            if (Deleted(uid) ||
                _mob.IsDead(uid) ||
                !HasComp<HumanoidAppearanceComponent>(uid) ||
                HasComp<YautjaComponent>(uid) ||
                !_marks.IsMarkedBy(uid, YautjaMarkKind.Thrall, master))
            {
                continue;
            }

            if (TryComp(uid, out YautjaThrallComponent? existing) && existing.Master != master)
                continue;

            var comp = EnsureComp<YautjaThrallComponent>(uid);
            comp.Master = master;
            comp.Reason = comp.Reason ?? _marks.GetMarkReason(uid, YautjaMarkKind.Thrall) ?? string.Empty;
            comp.Blooded = _marks.IsMarkedBy(uid, YautjaMarkKind.Blooded, master);
            comp.BloodedBy = comp.Blooded ? master : null;
            comp.BloodingReason = comp.Blooded ? _marks.GetMarkReason(uid, YautjaMarkKind.Blooded) ?? string.Empty : string.Empty;
            comp.TechAuthorized = comp.Blooded;
            Dirty(uid, comp);

            if (comp.TechAuthorized)
            {
                EnsureComp<YautjaTechAuthorizedComponent>(uid);
                GrantAllSkills(uid, 4);
            }
            else
            {
                RemCompDeferred<YautjaTechAuthorizedComponent>(uid);
            }

            thrall = (uid, comp);
            return true;
        }

        thrall = default;
        return false;
    }

    private bool TryGetLinkedThrall(
        EntityUid master,
        out Entity<YautjaThrallComponent> thrall,
        out Entity<YautjaThrallBracerComponent> bracer)
    {
        if (TryFindThrall(master, out thrall) &&
            TryGetLinkedThrall(master, thrall, out bracer))
        {
            return true;
        }

        thrall = default;
        bracer = default;
        return false;
    }

    private bool TryGetLinkedThrall(
        EntityUid master,
        Entity<YautjaThrallComponent> thrall,
        out Entity<YautjaThrallBracerComponent> bracer)
    {
        if (TryGetLinkedThrallSilent(master, thrall, out bracer))
            return true;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), master, master, PopupType.SmallCaution);
        bracer = default;
        return false;
    }

    private bool TryGetLinkedThrallSilent(
        EntityUid master,
        Entity<YautjaThrallComponent> thrall,
        out Entity<YautjaThrallBracerComponent> bracer)
    {
        if (thrall.Comp.ThrallBracer is { } bracerId &&
            TryComp(bracerId, out YautjaThrallBracerComponent? bracerComp) &&
            bracerComp.Linked &&
            bracerComp.Master == master &&
            bracerComp.User == thrall.Owner)
        {
            bracer = (bracerId, bracerComp);
            return true;
        }

        bracer = default;
        return false;
    }

    private bool TryGetThrallRemoteSelfDestructBracer(
        Entity<YautjaBracerComponent> masterBracer,
        EntityUid master,
        Entity<YautjaThrallComponent> thrall,
        out Entity<YautjaThrallBracerComponent> bracer)
    {
        if (TryGetLinkedThrallSilent(master, thrall, out bracer))
            return true;

        if (!TryGetWornThrallBracer(thrall.Owner, out bracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), master, master, PopupType.SmallCaution);
            return false;
        }

        thrall.Comp.ThrallBracer = bracer.Owner;
        Dirty(thrall);
        bracer.Comp.Master = master;
        bracer.Comp.MasterBracer = masterBracer.Owner;
        Dirty(bracer);
        return true;
    }

    public bool TryGetMasterThrallStatus(
        EntityUid master,
        out string? thrallName,
        out bool linked,
        out bool selfDestructArmed,
        out bool bracerLocked)
    {
        thrallName = null;
        linked = false;
        selfDestructArmed = false;
        bracerLocked = false;

        if (!TryFindThrall(master, out var thrall))
            return false;

        thrallName = Name(thrall.Owner);
        if (thrall.Comp.ThrallBracer is not { } bracerId ||
            !TryComp(bracerId, out YautjaThrallBracerComponent? bracer))
        {
            return true;
        }

        linked = bracer.Linked &&
                 bracer.Master == master &&
                 bracer.User == thrall.Owner;
        selfDestructArmed = bracer.SelfDestructArmed;
        bracerLocked = bracer.Locked;
        return true;
    }

    public bool TryToggleLinkedThrallBracerLock(Entity<YautjaBracerComponent> masterBracer, EntityUid master)
    {
        if (!CanUseMasterBracer(masterBracer, master) ||
            !TryGetLinkedThrall(master, out _, out var bracer))
        {
            return false;
        }

        return ToggleThrallBracerLock(bracer, master);
    }

    private bool TryGetWornThrallBracer(EntityUid user, out Entity<YautjaThrallBracerComponent> bracer)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained)
                continue;

            if (TryComp(contained, out YautjaThrallBracerComponent? comp))
            {
                bracer = (contained, comp);
                return true;
            }
        }

        bracer = default;
        return false;
    }

    private bool TryGetWornYautjaBracer(EntityUid user, out EntityUid bracer)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained)
                continue;

            if (HasComp<YautjaBracerComponent>(contained) ||
                HasComp<YautjaThrallBracerComponent>(contained))
            {
                bracer = contained;
                return true;
            }
        }

        bracer = default;
        return false;
    }

    private bool TryGetWornMasterBracer(EntityUid user, out Entity<YautjaBracerComponent> bracer)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } contained)
                continue;

            if (TryComp(contained, out YautjaBracerComponent? comp))
            {
                bracer = (contained, comp);
                return true;
            }
        }

        bracer = default;
        return false;
    }

    private bool CanUseMasterBracer(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaComponent>(user) ||
            bracer.Comp.User != user ||
            !IsMasterBracerWornBy(bracer, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool IsMasterBracerWornBy(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.GLOVES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity == bracer.Owner)
                return true;
        }

        return false;
    }

    private bool CanUseThrallBracer(Entity<YautjaThrallBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaThrallComponent>(user) || bracer.Comp.User != user || !bracer.Comp.Linked)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool CanUseThrallMessageBracer(Entity<YautjaThrallBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaThrallComponent>(user) || bracer.Comp.User != user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool CanToggleThrallBracerLock(Entity<YautjaThrallBracerComponent> bracer, EntityUid user)
    {
        if (!HasComp<YautjaThrallComponent>(user) || bracer.Comp.User != user)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private bool TryGetReceiverFromMaster(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        out EntityUid receiver,
        out EntityUid receiverBracer)
    {
        receiver = default;
        receiverBracer = default;
        if (!CanUseMasterBracer(bracer, user))
            return false;

        if (!TryFindMessageThrall(user, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-message-none"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetWornYautjaBracer(thrall.Owner, out var wornBracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-message-no-bracer-thrall"), user, user, PopupType.SmallCaution);
            return false;
        }

        receiver = thrall.Owner;
        receiverBracer = wornBracer;
        return true;
    }

    private bool TryGetReceiverFromThrall(
        Entity<YautjaThrallBracerComponent> bracer,
        EntityUid user,
        out EntityUid receiver,
        out EntityUid receiverBracer)
    {
        receiver = default;
        receiverBracer = default;
        if (!CanUseThrallMessageBracer(bracer, user) ||
            !TryComp(user, out YautjaThrallComponent? thrall))
        {
            return false;
        }

        if (Deleted(thrall.Master))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-message-none"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetWornYautjaBracer(thrall.Master, out var wornBracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-message-no-bracer-master"), user, user, PopupType.SmallCaution);
            return false;
        }

        receiver = thrall.Master;
        receiverBracer = wornBracer;
        return true;
    }

    private void SendBracerMessage(EntityUid sender, EntityUid receiver, EntityUid senderBracer, EntityUid receiverBracer, string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        var senderIsThrall = HasComp<YautjaThrallComponent>(sender);
        var messengerTitle = senderIsThrall ? "thrall" : "master";
        var receiverTitle = senderIsThrall ? "master" : "thrall";
        var senderText = Loc.GetString("cmu-yautja-thrall-message-sent",
            ("bracer", senderBracer),
            ("receiverTitle", receiverTitle),
            ("message", trimmed));
        var receiverText = Loc.GetString("cmu-yautja-thrall-message-received",
            ("bracer", receiverBracer),
            ("messengerTitle", messengerTitle),
            ("message", trimmed));
        SendPrivateChat(sender, sender, senderText);
        SendPrivateChat(sender, receiver, receiverText);

        if (ShouldPlayMessageSound(senderBracer))
            _audio.PlayPvs(GetMessageSound(senderBracer), senderBracer);

        if (ShouldPlayMessageSound(receiverBracer))
            _audio.PlayPvs(GetMessageSound(receiverBracer), receiverBracer);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(sender):sender} sent Yautja thrall bracer message to {ToPrettyString(receiver):receiver}: {trimmed}");
    }

    private bool ShouldPlayMessageSound(EntityUid bracer)
    {
        if (TryComp(bracer, out YautjaThrallBracerComponent? thrallBracer))
            return thrallBracer.NotificationSound;

        if (TryComp(bracer, out YautjaBracerComponent? masterBracer))
            return masterBracer.NotificationSound;

        return true;
    }

    private SoundSpecifier GetMessageSound(EntityUid bracer)
    {
        if (TryComp(bracer, out YautjaThrallBracerComponent? thrallBracer))
            return thrallBracer.MessageSound;

        if (TryComp(bracer, out YautjaBracerComponent? masterBracer))
            return masterBracer.MessageSound;

        return new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_bracer.wav");
    }

    private void SendPrivateChat(EntityUid source, EntityUid target, string text)
    {
        if (!_players.TryGetSessionByEntity(target, out var session))
            return;

        var wrapped = FormattedMessage.EscapeText(text);
        _chat.ChatMessageToOne(ChatChannel.Radio, text, wrapped, source, false, session.Channel, MessageColor);
    }

    private void ArmThrallSelfDestruct(Entity<YautjaThrallBracerComponent> bracer, EntityUid master, EntityUid thrall)
    {
        var now = _timing.CurTime;
        bracer.Comp.SelfDestructArmed = true;
        bracer.Comp.SelfDestructAt = now + bracer.Comp.SelfDestructDelay;
        bracer.Comp.NextSelfDestructWarning = now;
        Dirty(bracer);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-armed"), master, master, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-target"), thrall, thrall, PopupType.LargeCaution);
        BroadcastToYautja(
            Loc.GetString("cmu-yautja-thrall-self-destruct-broadcast", ("hunter", Name(master))),
            master);
        _audio.PlayPvs(bracer.Comp.SelfDestructWarningSound, thrall);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} triggered their thrall's self-destruct sequence in {_areas.GetAreaName(thrall)}");
    }

    private void OpenThrallSelfDestructDialog(
        EntityUid master,
        EntityUid thrall,
        Entity<YautjaThrallBracerComponent> bracer)
    {
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-yes"),
                new YautjaThrallSelfDestructConfirmEvent(GetNetEntity(master), GetNetEntity(bracer.Owner))),
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-no")),
        };

        _dialog.OpenOptions(
            thrall,
            thrall,
            Loc.GetString("cmu-yautja-thrall-self-destruct-dialog-title"),
            options,
            Loc.GetString("cmu-yautja-thrall-self-destruct-confirm", ("species", ThrallSelfDestructSpeciesName(thrall))),
            timeout: ThrallSelfDestructDialogTimeout);
    }

    private string ThrallSelfDestructSpeciesName(EntityUid thrall)
    {
        if (HasComp<YautjaComponent>(thrall))
            return Loc.GetString("species-name-yautja");

        if (TryComp(thrall, out HumanoidAppearanceComponent? humanoid) &&
            string.Equals(humanoid.Species, "Human", StringComparison.OrdinalIgnoreCase))
        {
            return "human";
        }

        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    private void CancelThrallSelfDestruct(Entity<YautjaThrallBracerComponent> bracer, EntityUid master, EntityUid thrall)
    {
        bracer.Comp.SelfDestructArmed = false;
        bracer.Comp.SelfDestructAt = TimeSpan.Zero;
        bracer.Comp.NextSelfDestructWarning = TimeSpan.Zero;
        Dirty(bracer);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-cancelled", ("target", thrall)), master, master);
        _audio.PlayPvs(bracer.Comp.SelfDestructWarningSound, bracer.Owner);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(master):hunter} cancelled thrall bracer self-destruct on {ToPrettyString(thrall):thrall}");
    }

    private void DetonateThrallBracer(Entity<YautjaThrallBracerComponent> bracer)
    {
        bracer.Comp.SelfDestructArmed = false;
        Dirty(bracer);

        var thrall = bracer.Comp.User;
        var epicenter = _transform.GetMapCoordinates(thrall ?? bracer.Owner);
        _rmcExplosion.QueueExplosion(
            epicenter,
            bracer.Comp.SelfDestructExplosion.Id,
            bracer.Comp.SelfDestructTotalIntensity,
            bracer.Comp.SelfDestructIntensitySlope,
            bracer.Comp.SelfDestructMaxIntensity,
            bracer.Owner,
            maxTileBreak: bracer.Comp.SelfDestructMaxTileBreak,
            canCreateVacuum: false);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Yautja thrall bracer self-destruct detonated from {ToPrettyString(bracer.Owner):bracer}");

        if (thrall is { } victim && !TerminatingOrDeleted(victim))
            QueueDel(victim);

        QueueDel(bracer.Owner);
    }

    private void GrantAllSkills(EntityUid user, int level)
    {
        var toSet = new Dictionary<EntProtoId<SkillDefinitionComponent>, int>();
        foreach (var skill in _skills.Skills)
        {
            toSet[skill] = level;
        }

        _skills.SetSkills(user, toSet);
    }

    private void BroadcastToYautja(string text, EntityUid source)
    {
        var query = EntityQueryEnumerator<YautjaComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            SendPrivateChat(source, uid, text);
        }
    }

    private bool IsInHuntingGrounds(EntityUid user)
    {
        if (!TryComp(user, out TransformComponent? xform))
            return false;

        return xform.GridUid is { } grid && HasComp<YautjaHuntingGroundComponent>(grid) ||
               xform.MapUid is { } map && HasComp<YautjaHuntingGroundComponent>(map);
    }

    private string YautjaDisplayName(EntityUid uid)
    {
        return HasComp<YautjaComponent>(uid)
            ? Loc.GetString("cmu-yautja-identity-unknown")
            : Name(uid);
    }
}
