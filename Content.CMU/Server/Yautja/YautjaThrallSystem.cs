using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.Weeds;
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
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
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

public sealed class YautjaThrallSystem : EntitySystem
{
    private const int MaxMessageLength = 160;
    private static readonly TimeSpan WarningEvery = TimeSpan.FromSeconds(2);
    private static readonly Color MessageColor = Color.FromHex("#b85440");

    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly GunIFFSystem _iff = default!;
    [Dependency] private readonly SharedXenoHiveSystem _hive = default!;
    [Dependency] private readonly SharedXenoAnnounceSystem _xenoAnnounce = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly YautjaMarkSystem _marks = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly NameModifierSystem _nameModifier = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedRMCExplosionSystem _rmcExplosion = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly XenoSystem _xeno = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkAttemptEvent>(OnMarkAttempt);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkAppliedEvent>(OnMarkApplied);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkRemoveAttemptEvent>(OnMarkRemoveAttempt);
        SubscribeLocalEvent<YautjaMarkComponent, YautjaMarkRemovedEvent>(OnMarkRemoved);

        SubscribeLocalEvent<YautjaThrallComponent, ComponentRemove>(OnThrallRemoved);
        SubscribeLocalEvent<YautjaHivebrokenXenoComponent, RefreshNameModifiersEvent>(OnHivebrokenRefreshName);

        SubscribeLocalEvent<YautjaBracerComponent, YautjaLinkThrallBracerActionEvent>(OnLinkThrallBracer);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaTransmitThrallMessageActionEvent>(OnMasterMessage);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaStunThrallActionEvent>(OnStunThrall);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaSelfDestructThrallActionEvent>(OnSelfDestructThrall);

        SubscribeLocalEvent<YautjaThrallBracerComponent, GetItemActionsEvent>(OnGetThrallBracerActions);
        SubscribeLocalEvent<YautjaThrallBracerComponent, GotEquippedEvent>(OnThrallBracerEquipped);
        SubscribeLocalEvent<YautjaThrallBracerComponent, GotUnequippedEvent>(OnThrallBracerUnequipped);
        SubscribeLocalEvent<YautjaThrallBracerComponent, BeingUnequippedAttemptEvent>(OnThrallBracerUnequipAttempt);
        SubscribeLocalEvent<YautjaThrallBracerComponent, YautjaTransmitThrallMessageActionEvent>(OnThrallMessage);

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
            if (!TryGetThrall(args.Hunter, args.Target, out _))
            {
                _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-requires-thrall"), args.Hunter, args.Hunter, PopupType.SmallCaution);
                args.Cancelled = true;
            }

            return;
        }

        if (TryFindThrall(args.Hunter, out var existing) && existing.Owner != args.Target)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-has"), args.Hunter, args.Hunter, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (TryComp(args.Target, out YautjaThrallComponent? targetThrall) && targetThrall.Master != args.Hunter)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-already-claimed"), args.Hunter, args.Hunter, PopupType.SmallCaution);
            args.Cancelled = true;
        }
    }

    private void OnMarkApplied(Entity<YautjaMarkComponent> ent, ref YautjaMarkAppliedEvent args)
    {
        if (args.Kind == YautjaMarkKind.Thrall)
        {
            MakeThrall(args.Hunter, args.Target, args.Reason);
            return;
        }

        if (args.Kind == YautjaMarkKind.Blooded)
            BloodThrall(args.Hunter, args.Target);
    }

    private void OnMarkRemoveAttempt(Entity<YautjaMarkComponent> ent, ref YautjaMarkRemoveAttemptEvent args)
    {
        if (args.Kind != YautjaMarkKind.Thrall && args.Kind != YautjaMarkKind.Blooded)
            return;

        if (!TryComp(args.Target, out YautjaThrallComponent? thrall) || thrall.Master == args.Hunter)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-your-thrall"), args.Hunter, args.Hunter, PopupType.SmallCaution);
        args.Cancelled = true;
    }

    private void OnMarkRemoved(Entity<YautjaMarkComponent> ent, ref YautjaMarkRemovedEvent args)
    {
        if (!TryComp(args.Target, out YautjaThrallComponent? thrall) || thrall.Master != args.Hunter)
            return;

        if (args.Kind == YautjaMarkKind.Blooded)
        {
            thrall.Blooded = false;
            thrall.TechAuthorized = false;
            RemCompDeferred<YautjaTechAuthorizedComponent>(args.Target);
            Dirty(args.Target, thrall);
            return;
        }

        if (args.Kind == YautjaMarkKind.Thrall)
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
        if (!CanUseMasterBracer(ent, args.Performer))
            return;

        if (!TryFindThrall(args.Performer, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-none"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        if (!TryGetWornThrallBracer(thrall.Owner, out var thrallBracer))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-no-bracer"), args.Performer, args.Performer, PopupType.SmallCaution);
            return;
        }

        LinkBracers(ent, args.Performer, thrall, thrallBracer);
    }

    private void OnMasterMessage(Entity<YautjaBracerComponent> ent, ref YautjaTransmitThrallMessageActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanUseMasterBracer(ent, args.Performer))
            return;

        if (!TryGetReceiverFromMaster(ent, args.Performer, out _))
            return;

        _ui.TryOpenUi(ent.Owner, YautjaThrallMessageUIKey.Key, args.Performer);
    }

    private void OnThrallMessage(Entity<YautjaThrallBracerComponent> ent, ref YautjaTransmitThrallMessageActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanUseThrallBracer(ent, args.Performer))
            return;

        if (!TryGetReceiverFromThrall(ent, args.Performer, out _))
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
        if (!CanUseMasterBracer(ent, args.Performer) ||
            !TryGetLinkedThrall(args.Performer, out var thrall, out var bracer))
        {
            return;
        }

        _damage.TryChangeDamage(thrall.Owner, new DamageSpecifier(bracer.Comp.ShockDamage), true, origin: bracer.Owner);
        _stun.TryStun(thrall.Owner, bracer.Comp.StunTime, true);
        _audio.PlayPvs(bracer.Comp.ShockSound, thrall.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-stunned-master", ("target", thrall.Owner)), args.Performer, args.Performer);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-stunned-target"), thrall.Owner, thrall.Owner, PopupType.LargeCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Performer):hunter} remotely stunned thrall {ToPrettyString(thrall.Owner):thrall}");
    }

    private void OnSelfDestructThrall(Entity<YautjaBracerComponent> ent, ref YautjaSelfDestructThrallActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        if (!CanUseMasterBracer(ent, args.Performer) ||
            !TryGetLinkedThrall(args.Performer, out var thrall, out var bracer))
        {
            return;
        }

        if (bracer.Comp.SelfDestructArmed)
        {
            CancelThrallSelfDestruct(bracer, args.Performer, thrall.Owner);
            return;
        }

        ArmThrallSelfDestruct(bracer, args.Performer, thrall.Owner);
    }

    private void OnGetThrallBracerActions(Entity<YautjaThrallBracerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        if (HasComp<YautjaBracerComponent>(ent.Owner))
            return;

        if (HasComp<YautjaThrallComponent>(args.User))
            args.AddAction(ref ent.Comp.TransmitThrallMessageAction, ent.Comp.TransmitThrallMessageActionId);
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

        ent.Comp.User = null;
    }

    private void OnThrallBracerUnequipAttempt(Entity<YautjaThrallBracerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancel();
        args.Reason = "cmu-yautja-thrall-bracer-locked";
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-bracer-locked"), args.Unequipee, args.Unequipee, PopupType.SmallCaution);
    }

    private void OnMasterSendMessage(Entity<YautjaBracerComponent> ent, ref YautjaThrallSendMessageMsg args)
    {
        if (!CanUseMasterBracer(ent, args.Actor) || !TryGetReceiverFromMaster(ent, args.Actor, out var receiver))
            return;

        SendBracerMessage(args.Actor, receiver, ent.Owner, args.Message);
    }

    private void OnThrallSendMessage(Entity<YautjaThrallBracerComponent> ent, ref YautjaThrallSendMessageMsg args)
    {
        if (!CanUseThrallBracer(ent, args.Actor) || !TryGetReceiverFromThrall(ent, args.Actor, out var receiver))
            return;

        SendBracerMessage(args.Actor, receiver, ent.Owner, args.Message);
    }

    public void HivebreakXeno(EntityUid master, EntityUid target, EntityUid source, YautjaHivebreakerComponent hivebreaker)
    {
        if (!HasComp<XenoComponent>(target))
            return;

        var thrall = EnsureComp<YautjaThrallComponent>(target);
        CaptureHivebreakOriginalState(target, thrall);
        var originalHive = thrall.HivebreakOriginalHive;

        thrall.Master = master;
        thrall.Reason = Loc.GetString("cmu-yautja-hivebreaker-thrall-reason");
        thrall.BracerLinked = false;
        thrall.MasterBracer = null;
        thrall.ThrallBracer = null;
        thrall.Blooded = hivebreaker.BloodOnConversion;
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

            _hive.SetHive(target, null);
        }

        SetHivebrokenNpcFaction(target, hivebreaker);
        SetHivebrokenIffFaction(target, hivebreaker);
        ApplyHivebrokenWeedBehavior(target, hivebreaker);
        ApplyHivebrokenRegen(target);
        ApplyHivebrokenSpeech(target, hivebreaker);
        ApplyHivebrokenName(target);

        if (hivebreaker.AuthorizeTechOnConversion)
            EnsureComp<YautjaTechAuthorizedComponent>(target);
        else
            RemCompDeferred<YautjaTechAuthorizedComponent>(target);

        if (hivebreaker.BloodOnConversion)
            GrantAllSkills(target, 4);

        if (hivebreaker.HealOnConversion && TryComp(target, out DamageableComponent? damageable))
            _damage.SetAllDamage(target, damageable, 0);

        BroadcastToYautja(
            Loc.GetString("cmu-yautja-hivebreaker-broadcast",
                ("hunter", YautjaDisplayName(master)),
                ("target", target)),
            master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} hivebroke xeno {ToPrettyString(target):target} with {ToPrettyString(source):item}");
    }

    private void MakeThrall(EntityUid master, EntityUid target, string? reason)
    {
        var thrall = EnsureComp<YautjaThrallComponent>(target);
        thrall.Master = master;
        thrall.Reason = reason ?? string.Empty;
        thrall.Blooded = false;
        thrall.TechAuthorized = false;
        Dirty(target, thrall);

        RemCompDeferred<YautjaTechAuthorizedComponent>(target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-marked-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-marked-target", ("hunter", YautjaDisplayName(master))), target, target, PopupType.MediumCaution);
        BroadcastToYautja(Loc.GetString("cmu-yautja-thrall-broadcast", ("hunter", YautjaDisplayName(master)), ("target", target)), master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} marked {ToPrettyString(target):target} as a Yautja thrall");
    }

    private void BloodThrall(EntityUid master, EntityUid target)
    {
        if (!TryComp(target, out YautjaThrallComponent? thrall) || thrall.Master != master)
            return;

        thrall.Blooded = true;
        thrall.TechAuthorized = true;
        Dirty(target, thrall);
        EnsureComp<YautjaTechAuthorizedComponent>(target);
        GrantAllSkills(target, 4);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-blooded-target"), target, target, PopupType.Medium);
        BroadcastToYautja(Loc.GetString("cmu-yautja-thrall-blooded-broadcast", ("hunter", YautjaDisplayName(master)), ("target", target)), master);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} blooded Yautja thrall {ToPrettyString(target):target}");
    }

    private void ReleaseThrall(EntityUid target, YautjaThrallComponent thrall, EntityUid master)
    {
        ClearThrallLinks(target, thrall);
        RemCompDeferred<YautjaTechAuthorizedComponent>(target);
        RemCompDeferred<YautjaThrallComponent>(target);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-released-master", ("target", target)), master, master);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-released-target"), target, target, PopupType.SmallCaution);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(master):hunter} released Yautja thrall {ToPrettyString(target):target}");
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

    private void ClearThrallLinks(EntityUid target, YautjaThrallComponent thrall)
    {
        if (thrall.ThrallBracer is { } thrallBracerId && TryComp(thrallBracerId, out YautjaThrallBracerComponent? bracer))
        {
            bracer.Master = null;
            bracer.MasterBracer = null;
            bracer.Linked = false;
            bracer.Locked = false;
            bracer.SelfDestructArmed = false;
            bracer.SelfDestructAt = TimeSpan.Zero;
            bracer.NextSelfDestructWarning = TimeSpan.Zero;
            Dirty(thrallBracerId, bracer);
        }

        thrall.BracerLinked = false;
        thrall.MasterBracer = null;
        thrall.ThrallBracer = null;
        thrall.Blooded = false;
        thrall.TechAuthorized = false;
        Dirty(target, thrall);
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
        _movement.RefreshMovementSpeedModifiers(target);
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
            comp.Reason = comp.Reason ?? string.Empty;
            comp.Blooded = _marks.IsMarkedBy(uid, YautjaMarkKind.Blooded, master);
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
            thrall.Comp.ThrallBracer is { } bracerId &&
            TryComp(bracerId, out YautjaThrallBracerComponent? bracerComp) &&
            bracerComp.Linked &&
            bracerComp.Master == master &&
            bracerComp.User == thrall.Owner)
        {
            bracer = (bracerId, bracerComp);
            return true;
        }

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-not-linked"), master, master, PopupType.SmallCaution);
        thrall = default;
        bracer = default;
        return false;
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

    private bool TryGetReceiverFromMaster(Entity<YautjaBracerComponent> bracer, EntityUid user, out EntityUid receiver)
    {
        receiver = default;
        if (!CanUseMasterBracer(bracer, user))
            return false;

        if (!TryFindThrall(user, out var thrall))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-none"), user, user, PopupType.SmallCaution);
            return false;
        }

        receiver = thrall.Owner;
        return true;
    }

    private bool TryGetReceiverFromThrall(Entity<YautjaThrallBracerComponent> bracer, EntityUid user, out EntityUid receiver)
    {
        receiver = default;
        if (!CanUseThrallBracer(bracer, user) ||
            !TryComp(user, out YautjaThrallComponent? thrall) ||
            Deleted(thrall.Master))
        {
            return false;
        }

        receiver = thrall.Master;
        return true;
    }

    private void SendBracerMessage(EntityUid sender, EntityUid receiver, EntityUid bracer, string message)
    {
        var trimmed = message.Trim();
        if (trimmed.Length > MaxMessageLength)
            trimmed = trimmed[..MaxMessageLength];

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        var senderText = Loc.GetString("cmu-yautja-thrall-message-sent", ("target", receiver), ("message", trimmed));
        var receiverText = Loc.GetString("cmu-yautja-thrall-message-received", ("sender", YautjaDisplayName(sender)), ("message", trimmed));
        SendPrivateChat(sender, sender, senderText);
        SendPrivateChat(sender, receiver, receiverText);
        _audio.PlayPvs(GetMessageSound(bracer), bracer);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(sender):sender} sent Yautja thrall bracer message to {ToPrettyString(receiver):receiver}: {trimmed}");
    }

    private SoundSpecifier GetMessageSound(EntityUid bracer)
    {
        if (TryComp(bracer, out YautjaThrallBracerComponent? thrallBracer))
            return thrallBracer.MessageSound;

        if (TryComp(bracer, out YautjaBracerComponent? masterBracer))
            return masterBracer.LockSound;

        return new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_bracer.wav");
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

        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-armed", ("target", thrall), ("seconds", (int) bracer.Comp.SelfDestructDelay.TotalSeconds)), master, master, PopupType.MediumCaution);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-thrall-self-destruct-target"), thrall, thrall, PopupType.LargeCaution);
        _audio.PlayPvs(bracer.Comp.SelfDestructWarningSound, thrall);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(master):hunter} armed thrall bracer self-destruct on {ToPrettyString(thrall):thrall}");
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

        var epicenter = _transform.GetMapCoordinates(bracer.Comp.User ?? bracer.Owner);
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

    private string YautjaDisplayName(EntityUid uid)
    {
        return HasComp<YautjaComponent>(uid)
            ? Loc.GetString("cmu-yautja-identity-unknown")
            : Name(uid);
    }
}
