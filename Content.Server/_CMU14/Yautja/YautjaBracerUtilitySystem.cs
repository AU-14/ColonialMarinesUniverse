using Content.Server._RMC14.Emote;
using Content.Server.Administration.Logs;
using Content.Server.Access.Systems;
using Content.Server.Access.Components;
using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts.Events;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Synth;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Access;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.DoAfter;
using Content.Shared.Traits.Assorted;
using Content.Shared.Verbs;
using Content.Shared.Access.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaBracerUtilitySystem : EntitySystem
{
    private const string IdSlot = "id";
    private const string GlovesSlot = "gloves";
    private const int TranslatorMaxMessageLength = 160;
    private static readonly TimeSpan NonYautjaBracerMisuseDoAfter = TimeSpan.FromSeconds(3);
    private static readonly Color ModernTranslatorColor = Color.FromHex("#ff4d4d");
    private static readonly Color RetroTranslatorColor = Color.White;
    private static readonly ProtoId<EmotePrototype> HumanPainEmote = "Scream";
    private static readonly ProtoId<AccessLevelPrototype> YautjaSecureAccess = "CMUAccessYautjaSecure";
    private static readonly ProtoId<AccessLevelPrototype> YautjaEliteAccess = "CMUAccessYautjaElite";
    private static readonly ProtoId<AccessLevelPrototype> YautjaElderAccess = "CMUAccessYautjaElder";
    private static readonly ProtoId<AccessLevelPrototype> YautjaLeaderAccess = "CMUAccessYautjaLeader";
    private static readonly ProtoId<AccessLevelPrototype> YautjaAncientAccess = "CMUAccessYautjaAncient";
    private static readonly ProtoId<AccessLevelPrototype> YautjaBadBloodAccess = "CMUAccessYautjaBadBlood";
    private static readonly DamageSpecifier DefaultTechShockDamage = new()
    {
        DamageDict = new()
        {
            { "Heat", 10 },
        },
    };
    private static readonly DamageSpecifier TechShockArmDamage = new()
    {
        DamageDict = new()
        {
            { "Heat", 5 },
        },
    };

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAccessSystem _access = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private RMCEmoteSystem _emote = default!;
    [Dependency] private SharedBodyPartHealthSystem _partHealth = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IdCardSystem _idCard = default!;
    [Dependency] private YautjaCloakSystem _cloak = default!;
    [Dependency] private YautjaPowerSystem _power = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaBracerComponent, BeingUnequippedAttemptEvent>(OnBeingUnequippedAttempt);
        SubscribeLocalEvent<YautjaBracerComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerLockActionEvent>(OnToggleLock);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaBracerConfirmDeadHunterLockEvent>(OnConfirmDeadHunterLock);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaTranslatorActionEvent>(OnTranslator);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerIdChipActionEvent>(OnToggleIdChip);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaChangeExplosionTypeActionEvent>(OnChangeExplosionType);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerNotificationSoundActionEvent>(OnToggleNotificationSound);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerNameActionEvent>(OnToggleBracerName);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaAddTrackedItemActionEvent>(OnAddTrackedItem);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaRemoveTrackedItemActionEvent>(OnRemoveTrackedItem);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateStabilisingCrystalActionEvent>(OnCreateStabilisingCrystal);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateHumanStabilisingCrystalActionEvent>(OnCreateHumanStabilisingCrystal);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateHuntingTrapActionEvent>(OnCreateHuntingTrap);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaBracerMisuseDoAfterEvent>(OnBracerMisuseDoAfter);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaOverloadBracerDoAfterEvent>(OnOverloadBracerDoAfter);
        SubscribeLocalEvent<YautjaTechItemComponent, YautjaTechMisusedEvent>(OnTechMisused);

        EntityManager.System<YautjaSelfDestructSystem>().TryStartNonTechMisuse = TryStartSelfDestructMisuse;

        Subs.BuiEvents<YautjaBracerComponent>(YautjaTranslatorUIKey.Key, subs =>
        {
            subs.Event<YautjaTranslatorSendMessageMsg>(OnTranslatorMessage);
        });
    }

    private void OnMapInit(Entity<YautjaBracerComponent> ent, ref MapInitEvent args)
    {
        EnsureIdContainer(ent);
        EnsureIdCardContainer(ent);
        EnsureIdChip(ent);
    }

    private void OnBeingUnequippedAttempt(Entity<YautjaBracerComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancel();
        args.Reason = "cmu-yautja-bracer-locked";
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-locked"), args.Unequipee, args.Unequipee, PopupType.SmallCaution);
    }

    private void OnGetEquipmentVerbs(Entity<YautjaBracerComponent> ent, ref InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>> args)
    {
        var ev = args.Args;
        if (!ev.CanInteract ||
            !ev.CanAccess ||
            ev.Target == ent.Owner ||
            !HasComp<YautjaComponent>(ev.Target) ||
            !CanUnlockDeadHunterBracer(ev.User, ev.Target, ent))
        {
            return;
        }

        ev.Verbs.Add(new EquipmentVerb
        {
            Text = Loc.GetString(ent.Comp.Locked
                ? "cmu-yautja-bracer-unlock-dead-verb"
                : "cmu-yautja-bracer-lock-dead-verb"),
            Priority = 4,
            Act = () =>
            {
                if (!CanUseToggleLockVerb(ev.User))
                    return;

                ToggleLock(ent, ev.User, ev.Target);
            },
        });

        if (HasComp<UnrevivableComponent>(ev.Target) && !ent.Comp.SelfDestructArmed)
        {
            ev.Verbs.Add(new EquipmentVerb
            {
                Text = Loc.GetString("cmu-yautja-bracer-overload-verb"),
                Priority = 3,
                Act = () => TryOverloadBracer(ent, ev.User, ev.Target),
            });
        }
    }

    private void TryOverloadBracer(Entity<YautjaBracerComponent> bracer, EntityUid user, EntityUid target)
    {
        if (!HasComp<UnrevivableComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-overload-not-dead-enough"), user, user, PopupType.SmallCaution);
            return;
        }

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            bracer.Comp.OverloadDoAfterDuration,
            new YautjaOverloadBracerDoAfterEvent(),
            bracer.Owner,
            target)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = false,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPvs(bracer.Comp.OverloadDoAfterSound, target);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-overload-start"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{ToPrettyString(user):player} began overloading dead hunter bracer {ToPrettyString(bracer.Owner):bracer} on {ToPrettyString(target):target}");
    }

    private void OnOverloadBracerDoAfter(Entity<YautjaBracerComponent> ent, ref YautjaOverloadBracerDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var now = _timing.CurTime;
        ent.Comp.SelfDestructArmed = true;
        ent.Comp.SelfDestructAt = now + ent.Comp.OverloadDetonationDelay;
        ent.Comp.NextSelfDestructWarning = now;
        Dirty(ent);

        var target = ent.Comp.User ?? ent.Owner;
        _audio.PlayPvs(ent.Comp.SelfDestructArmSound, target);
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"Dead hunter bracer {ToPrettyString(ent.Owner):bracer} overloaded, detonation in {ent.Comp.OverloadDetonationDelay.TotalSeconds}s");
    }

    private void OnToggleLock(Entity<YautjaBracerComponent> ent, ref YautjaToggleBracerLockActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleWornBracerLock(ent, args.Performer);
    }

    public bool TryToggleWornBracerLock(Entity<YautjaBracerComponent> ent, EntityUid user)
    {
        if (!CanUseToggleLockVerb(user))
            return false;

        var use = TryResolveBracerUse(ent, user, YautjaBracerMisuseAction.ToggleLock);
        if (use == YautjaBracerUseResult.Blocked)
            return false;

        if (use == YautjaBracerUseResult.RandomFunction)
        {
            RunRandomBracerFunction(ent, user);
            return true;
        }

        if (use == YautjaBracerUseResult.Delayed)
            return true;

        if (TryGetPulledVictim(user, out var victim))
            return TryOpenPulledDeadHunterBracerLockDialog(ent, user, victim);

        return ToggleLock(ent, user, user);
    }

    private bool CanUseToggleLockVerb(EntityUid user)
    {
        if (_mobState.IsCritical(user) || !_mobState.IsAlive(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-lock-wrong-state"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!HasComp<YautjaComponent>(user) &&
            !HasComp<YautjaTechAuthorizedComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-lock-no-tech"), user, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private void OnConfirmDeadHunterLock(Entity<YautjaBracerComponent> ent, ref YautjaBracerConfirmDeadHunterLockEvent args)
    {
        if (!TryGetEntity(args.User, out var user) ||
            !TryGetEntity(args.Victim, out var victim) ||
            !TryGetEntity(args.VictimBracer, out var victimBracer))
        {
            return;
        }

        TryTogglePulledDeadHunterBracerLock(ent, user.Value, victim.Value, victimBracer.Value);
    }

    private void OnToggleIdChip(Entity<YautjaBracerComponent> ent, ref YautjaToggleBracerIdChipActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleIdChip(ent, args.Performer);
    }

    private void OnChangeExplosionType(Entity<YautjaBracerComponent> ent, ref YautjaChangeExplosionTypeActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryChangeExplosionType(ent, args.Performer);
    }

    private void OnToggleNotificationSound(Entity<YautjaBracerComponent> ent, ref YautjaToggleBracerNotificationSoundActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleNotificationSound(ent, args.Performer);
    }

    private void OnToggleBracerName(Entity<YautjaBracerComponent> ent, ref YautjaToggleBracerNameActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryToggleBracerName(ent, args.Performer);
    }

    private void OnAddTrackedItem(Entity<YautjaBracerComponent> ent, ref YautjaAddTrackedItemActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryAddTrackedItem(ent, args.Performer);
    }

    private void OnRemoveTrackedItem(Entity<YautjaBracerComponent> ent, ref YautjaRemoveTrackedItemActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryRemoveTrackedItem(ent, args.Performer);
    }

    private void OnTranslator(Entity<YautjaBracerComponent> ent, ref YautjaTranslatorActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryOpenTranslator(ent, args.Performer);
    }

    private void OnTranslatorMessage(Entity<YautjaBracerComponent> ent, ref YautjaTranslatorSendMessageMsg args)
    {
        if (!IsBracerWornBy(ent, args.Actor))
        {
            return;
        }

        SendTranslatorMessage(ent, args.Actor, args.Message);
        UpdateTranslatorUi(ent, args.Actor);
    }

    private void OnCreateStabilisingCrystal(Entity<YautjaBracerComponent> ent, ref YautjaCreateStabilisingCrystalActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryCreateStabilisingCrystal(ent, args.Performer);
    }

    private void OnCreateHumanStabilisingCrystal(Entity<YautjaBracerComponent> ent, ref YautjaCreateHumanStabilisingCrystalActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryCreateHumanStabilisingCrystal(ent, args.Performer);
    }

    private void OnCreateHuntingTrap(Entity<YautjaBracerComponent> ent, ref YautjaCreateHuntingTrapActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryCreateHuntingTrap(ent, args.Performer);
    }

    public bool TryOpenTranslator(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.OpenTranslator);
    }

    public bool TryToggleIdChip(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseIdChipBracer(bracer, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);
            return false;
        }

        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.ToggleIdChip, requireWorn: false);
    }

    public bool TryChangeExplosionType(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseHeldOrWornHunterBracer(bracer, user))
            return false;

        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.ChangeExplosionType, requireWorn: false);
    }

    public bool TryToggleNotificationSound(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.ToggleNotificationSound);
    }

    public bool TryToggleBracerName(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!IsBracerWornBy(bracer, user) ||
            _mobState.IsIncapacitated(user) ||
            !TryComp(user, out YautjaComponent? yautja))
        {
            return false;
        }

        yautja.BracerNameActive = !yautja.BracerNameActive;
        Dirty(user, yautja);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-name-now", ("state", Loc.GetString(yautja.BracerNameActive ? "cmu-yautja-state-now" : "cmu-yautja-state-no-longer"))), user, user);
        return true;
    }

    public bool TryAddTrackedItem(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.AddTrackedItem);
    }

    public bool TryRemoveTrackedItem(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.RemoveTrackedItem);
    }

    public bool TryCreateStabilisingCrystal(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.CreateStabilisingCrystal);
    }

    public bool TryCreateHumanStabilisingCrystal(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.CreateHumanStabilisingCrystal);
    }

    public bool TryCreateHealingCapsule(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.CreateHealingCapsule);
    }

    public bool TryCreateHuntingTrap(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return TryRunBracerAction(bracer, user, YautjaBracerMisuseAction.CreateHuntingTrap);
    }

    private bool TryRunBracerAction(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        YautjaBracerMisuseAction action,
        bool requireWorn = true)
    {
        var use = TryResolveBracerUse(bracer, user, action, requireWorn);
        switch (use)
        {
            case YautjaBracerUseResult.Allowed:
                return RunResolvedBracerAction(bracer, user, action);
            case YautjaBracerUseResult.RandomFunction:
                RunRandomBracerFunction(bracer, user);
                return true;
            case YautjaBracerUseResult.Delayed:
                return true;
            default:
                return false;
        }
    }

    private bool RunResolvedBracerAction(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        YautjaBracerMisuseAction action)
    {
        switch (action)
        {
            case YautjaBracerMisuseAction.OpenTranslator:
                OpenTranslatorUi(bracer, user);
                return true;
            case YautjaBracerMisuseAction.ToggleLock:
                if (TryGetPulledVictim(user, out var victim))
                    return TryOpenPulledDeadHunterBracerLockDialog(bracer, user, victim);

                return ToggleLock(bracer, user, user);
            case YautjaBracerMisuseAction.ToggleIdChip:
                return ToggleIdChip(bracer, user);
            case YautjaBracerMisuseAction.ChangeExplosionType:
                return ChangeExplosionType(bracer, user);
            case YautjaBracerMisuseAction.ToggleNotificationSound:
                return ToggleNotificationSound(bracer, user);
            case YautjaBracerMisuseAction.AddTrackedItem:
                return AddTrackedItem(bracer, user);
            case YautjaBracerMisuseAction.RemoveTrackedItem:
                return RemoveTrackedItem(bracer, user);
            case YautjaBracerMisuseAction.CreateStabilisingCrystal:
                return TryCreateItem(bracer, user, bracer.Comp.StabilisingCrystalPrototype, bracer.Comp.StabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-crystal-created");
            case YautjaBracerMisuseAction.CreateHumanStabilisingCrystal:
                return TryCreateItem(bracer, user, bracer.Comp.HumanStabilisingCrystalPrototype, bracer.Comp.HumanStabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-human-crystal-created");
            case YautjaBracerMisuseAction.CreateHealingCapsule:
                return TryCreateItem(bracer, user, bracer.Comp.HealingCapsulePrototype, bracer.Comp.HealingCapsuleCost, bracer.Comp.HealingCapsuleCooldown, ref bracer.Comp.NextHealingCapsule, "cmu-yautja-bracer-healing-capsule-created");
            case YautjaBracerMisuseAction.CreateHuntingTrap:
                return TryCreateItem(bracer, user, bracer.Comp.HuntingTrapPrototype, bracer.Comp.HuntingTrapCost, bracer.Comp.HuntingTrapCooldown, ref bracer.Comp.NextHuntingTrap, "cmu-yautja-bracer-hunting-trap-created");
            case YautjaBracerMisuseAction.None:
            case YautjaBracerMisuseAction.SelfDestruct:
            default:
                return true;
        }
    }

    private void OnTechMisused(Entity<YautjaTechItemComponent> ent, ref YautjaTechMisusedEvent args)
    {
        if (HasComp<YautjaComponent>(args.User))
            return;

        if (TryComp(args.Item, out YautjaBracerComponent? bracer))
        {
            TryResolveBracerUse((args.Item, bracer), args.User, YautjaBracerMisuseAction.None, requireWorn: false);
            return;
        }

        ApplyTechPunishment(args.User, args.Item, DefaultTechShockDamage, TimeSpan.FromSeconds(2), 0.08f);
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (bracer.User is not { } user ||
                IsYautjaTechUser(user) ||
                !HasComp<EntityActiveInvisibleComponent>(user) ||
                time < bracer.NextNonYautjaCloakShock)
            {
                continue;
            }

            bracer.NextNonYautjaCloakShock = time + bracer.NonYautjaCloakShockEvery;
            if (!_random.Prob(bracer.NonYautjaCloakShockChance))
                continue;

            _cloak.ForceDecloak(user);
            ApplyTechPunishment(user, uid, bracer.TechShockDamage, bracer.TechShockStun, bracer.NonYautjaDelimbChance, true);
        }
    }

    public bool TryStartSelfDestructMisuse(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        var use = TryResolveBracerUse(bracer, user, YautjaBracerMisuseAction.SelfDestruct, alwaysDelimb: true);
        switch (use)
        {
            case YautjaBracerUseResult.Allowed:
                return TrySeverBothArms(user, "cmu-yautja-tech-random-delimbs", bracer.Comp.TechDelimbSound);
            case YautjaBracerUseResult.RandomFunction:
                RunRandomBracerFunction(bracer, user);
                return true;
            case YautjaBracerUseResult.Delayed:
                return true;
            default:
                return false;
        }
    }

    private YautjaBracerUseResult TryResolveBracerUse(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        YautjaBracerMisuseAction action,
        bool requireWorn = true,
        bool alwaysDelimb = false)
    {
        if (requireWorn && !IsBracerWornBy(bracer, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);
            return YautjaBracerUseResult.Blocked;
        }

        if (IsYautjaTechUser(user))
            return YautjaBracerUseResult.Allowed;

        _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-press-buttons"), user, user, PopupType.SmallCaution);
        StartNonYautjaBracerMisuseDoAfter(bracer, user, action, requireWorn, alwaysDelimb);
        return YautjaBracerUseResult.Delayed;
    }

    private void StartNonYautjaBracerMisuseDoAfter(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        YautjaBracerMisuseAction action,
        bool requireWorn,
        bool alwaysDelimb)
    {
        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            NonYautjaBracerMisuseDoAfter,
            new YautjaBracerMisuseDoAfterEvent(action, requireWorn, alwaysDelimb),
            bracer.Owner,
            target: bracer.Owner,
            used: bracer.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            DistanceThreshold = 1.5f,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnBracerMisuseDoAfter(Entity<YautjaBracerComponent> bracer, ref YautjaBracerMisuseDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        var use = ResolveCompletedNonTechBracerUse(bracer, args.User, args.AlwaysDelimb);
        switch (use)
        {
            case YautjaBracerUseResult.Allowed:
                RunResolvedBracerAction(bracer, args.User, args.Action);
                break;
            case YautjaBracerUseResult.RandomFunction:
                RunRandomBracerFunction(bracer, args.User);
                break;
        }
    }

    private YautjaBracerUseResult ResolveCompletedNonTechBracerUse(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        bool alwaysDelimb)
    {
        var (workingChance, randomChance) = GetNonYautjaChances(user, bracer.Comp);

        if (_random.Prob(randomChance))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-random-function"), user, user, PopupType.SmallCaution);
            return YautjaBracerUseResult.RandomFunction;
        }

        if (!_random.Prob(workingChance))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-nothing-happens"), user, user, PopupType.SmallCaution);
            return YautjaBracerUseResult.Blocked;
        }

        if (alwaysDelimb)
        {
            TrySeverBothArms(user, "cmu-yautja-tech-random-delimbs", bracer.Comp.TechDelimbSound);
            return YautjaBracerUseResult.Blocked;
        }

        return YautjaBracerUseResult.Allowed;
    }

    private (float Working, float RandomFunction) GetNonYautjaChances(EntityUid user, YautjaBracerComponent bracer)
    {
        if (HasComp<SynthComponent>(user))
            return (bracer.SynthWorkingChance, bracer.SynthRandomFunctionChance);

        if (IsResearcher(user))
            return (bracer.ResearcherWorkingChance, bracer.ResearcherRandomFunctionChance);

        return (bracer.NonYautjaWorkingChance, bracer.NonYautjaRandomFunctionChance);
    }

    private bool IsYautjaTechUser(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized) ||
               HasComp<YautjaTechAuthorizedComponent>(user);
    }

    private bool IsResearcher(EntityUid user)
    {
        if (!_inventory.TryGetSlotEntity(user, IdSlot, out var id) || !TryComp(id, out IdCardComponent? idCard))
            return false;

        if (TryComp(id, out PresetIdCardComponent? presetId) &&
            IsResearcherJob(presetId.JobName?.Id))
        {
            return true;
        }

        var job = idCard.JobPrototype?.Id ??
                  idCard.JobTitle ??
                  idCard.LocalizedJobTitle ??
                  string.Empty;

        return IsResearcherJob(job);
    }

    private static bool IsResearcherJob(string? job)
    {
        if (job == null)
            return false;

        return job.Contains("research", StringComparison.OrdinalIgnoreCase) ||
               job.Contains("scientist", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTechPunishment(
        EntityUid user,
        EntityUid item,
        DamageSpecifier damage,
        TimeSpan stun,
        float delimbChance,
        bool showObserverVisibleMessage = false)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-shock", ("bracer", item)), user, user, PopupType.LargeCaution);
        if (showObserverVisibleMessage)
        {
            var observerFilter = Filter.Pvs(user, entityManager: EntityManager)
                .RemoveWhereAttachedEntity(attached => attached == user);
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-tech-shock-others", ("bracer", item), ("user", user)),
                user,
                observerFilter,
                true,
                PopupType.LargeCaution);
        }

        if (TryComp(item, out YautjaBracerComponent? bracer))
            _audio.PlayPvs(bracer.TechShockSound, item);

        if (showObserverVisibleMessage)
        {
            if (TryComp<DamageableComponent>(user, out var damageable))
                _damage.AddDamage(user, damageable, new DamageSpecifier(damage));
            ApplyTechShockArmDamage(user, item);
        }
        else
        {
            _damage.TryChangeDamage(user, new DamageSpecifier(damage), true, origin: item);
        }

        _stun.TryStun(user, stun, true);
        if (showObserverVisibleMessage && HasComp<HumanoidAppearanceComponent>(user))
            _emote.TryEmoteWithChat(user, HumanPainEmote, forceEmote: true);

        if (_random.Prob(delimbChance))
            TrySeverBothArms(user, "cmu-yautja-tech-delimbs");

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} was punished for misusing Yautja technology {ToPrettyString(item):item}");
    }

    private void ApplyTechShockArmDamage(EntityUid user, EntityUid item)
    {
        ApplyTechShockPartDamage(user, item, BodyPartType.Arm, BodyPartSymmetry.Left);
        ApplyTechShockPartDamage(user, item, BodyPartType.Arm, BodyPartSymmetry.Right);
    }

    private void ApplyTechShockPartDamage(EntityUid user, EntityUid item, BodyPartType type, BodyPartSymmetry symmetry)
    {
        foreach (var (partUid, part) in _body.GetBodyChildren(user))
        {
            if (part.PartType != type || part.Symmetry != symmetry)
                continue;

            _partHealth.TryApplyPartDamage(user, partUid, new DamageSpecifier(TechShockArmDamage), tool: item);
            return;
        }
    }

    private bool TrySeverBothArms(EntityUid user, LocId popup, SoundSpecifier sound = default!)
    {
        var severed = false;
        severed |= TrySeverPart(user, BodyPartType.Arm, BodyPartSymmetry.Left);
        severed |= TrySeverPart(user, BodyPartType.Arm, BodyPartSymmetry.Right);

        if (severed)
        {
            _popup.PopupEntity(Loc.GetString(popup), user, user, PopupType.LargeCaution);
            if (sound != null)
                _audio.PlayPvs(sound, user);
        }

        return severed;
    }

    private bool TrySeverPart(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        if (!_medicalIndex.TryGetBodyPart(body, new CMUMedicalBodyPartKey(type, symmetry), out var part))
            return false;

        var ev = new BodyPartSeveredEvent(body, part, type);
        RaiseLocalEvent(part, ref ev);
        return true;
    }

    private bool ChangeExplosionType(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (bracer.Comp.SelfDestructExplosionType == YautjaSelfDestructExplosionType.Small &&
            bracer.Comp.SelfDestructArmed)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-change-explosion-type-active-small"), user, user, PopupType.SmallCaution);
            return false;
        }

        bracer.Comp.SelfDestructExplosionType = bracer.Comp.SelfDestructExplosionType == YautjaSelfDestructExplosionType.Small
            ? YautjaSelfDestructExplosionType.Big
            : YautjaSelfDestructExplosionType.Small;

        Dirty(bracer);
        _popup.PopupEntity(Loc.GetString(
            bracer.Comp.SelfDestructExplosionType == YautjaSelfDestructExplosionType.Big
                ? "cmu-yautja-change-explosion-type-big"
                : "cmu-yautja-change-explosion-type-small"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):hunter} changed Yautja bracer self-destruct type on {ToPrettyString(bracer.Owner):bracer} to {bracer.Comp.SelfDestructExplosionType}");
        return true;
    }

    private bool ToggleNotificationSound(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        bracer.Comp.NotificationSound = !bracer.Comp.NotificationSound;
        Dirty(bracer);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-sound-now", ("state", Loc.GetString(bracer.Comp.NotificationSound ? "cmu-yautja-state-on" : "cmu-yautja-state-off"))), user, user);
        return true;
    }

    private bool AddTrackedItem(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryGetActiveTrackableItem(user, out var item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-active-hand-required"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (IsTrackedItem(item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-already-tracked", ("item", item)), item, user, PopupType.SmallCaution);
            return false;
        }

        RemComp<YautjaUntrackedItemComponent>(item);
        EnsureComp<YautjaTrackedItemComponent>(item);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-added", ("item", item)), item, user);
        return true;
    }

    private bool RemoveTrackedItem(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryGetActiveTrackableItem(user, out var item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-active-hand-required"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!IsTrackedItem(item))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-not-tracked", ("item", item)), item, user, PopupType.SmallCaution);
            return false;
        }

        RemComp<YautjaTrackedItemComponent>(item);

        if (HasComp<YautjaTechItemComponent>(item))
            EnsureComp<YautjaUntrackedItemComponent>(item);

        _popup.PopupEntity(Loc.GetString("cmu-yautja-tracker-removed", ("item", item)), item, user);
        return true;
    }

    private bool ToggleLock(Entity<YautjaBracerComponent> bracer, EntityUid user, EntityUid target)
    {
        if (user == target)
        {
            if (!IsBracerWornBy(bracer, user))
            {
                _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);
                return false;
            }
        }
        else if (!CanUnlockDeadHunterBracer(user, target, bracer))
        {
            return false;
        }

        bracer.Comp.Locked = !bracer.Comp.Locked;
        Dirty(bracer);
        _actions.SetToggled(bracer.Comp.ToggleLockAction, bracer.Comp.Locked);
        _audio.PlayPvs(bracer.Comp.LockSound, bracer.Owner);
        _popup.PopupEntity(Loc.GetString(BracerLockPopup(target, bracer.Comp.Locked)), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} {(bracer.Comp.Locked ? "locked" : "unlocked")} Yautja bracer {ToPrettyString(bracer.Owner):bracer} on {ToPrettyString(target):target}");
        return true;
    }

    private LocId BracerLockPopup(EntityUid wearer, bool locked)
    {
        var yautja = HasComp<YautjaComponent>(wearer);
        return locked
            ? yautja
                ? "cmu-yautja-bracer-lock-yautja"
                : "cmu-yautja-bracer-lock-nonyautja"
            : yautja
                ? "cmu-yautja-bracer-unlock-yautja"
                : "cmu-yautja-bracer-unlock-nonyautja";
    }

    public bool TryOpenPulledDeadHunterBracerLockDialog(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim)
    {
        if (!CanOpenPulledDeadHunterBracerLockDialog(bracer, user, victim, out var victimBracer))
            return false;

        var confirmEvent = new YautjaBracerConfirmDeadHunterLockEvent(
            GetNetEntity(user),
            GetNetEntity(victim),
            GetNetEntity(victimBracer.Owner));
        var options = new List<DialogOption>
        {
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-yes"), confirmEvent),
            new(Loc.GetString("cmu-yautja-self-destruct-confirm-no")),
        };

        _dialog.OpenOptions(
            bracer.Owner,
            user,
            Loc.GetString("cmu-yautja-bracer-dead-lock-dialog-title"),
            options,
            Loc.GetString("cmu-yautja-bracer-dead-lock-confirm", ("species", SpeciesName(victim))));
        return true;
    }

    private bool TryTogglePulledDeadHunterBracerLock(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        EntityUid expectedVictimBracer)
    {
        if (!CanUseConfirmedPulledDeadHunterBracerLock(bracer, user, victim, expectedVictimBracer, out var victimBracer))
            return false;

        return TogglePulledDeadHunterBracerLock(victimBracer, user, victim);
    }

    private bool TogglePulledDeadHunterBracerLock(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid target)
    {
        bracer.Comp.Locked = !bracer.Comp.Locked;
        Dirty(bracer);
        _actions.SetToggled(bracer.Comp.ToggleLockAction, bracer.Comp.Locked);
        _audio.PlayPvs(bracer.Comp.LockSound, bracer.Owner);

        var stateKey = bracer.Comp.Locked
            ? "cmu-yautja-bracer-locked-now"
            : "cmu-yautja-bracer-unlocked-now";
        _popup.PopupEntity(Loc.GetString(stateKey), user, user);

        if (!bracer.Comp.Locked)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "cmu-yautja-bracer-dead-unlock-others",
                    ("user", user),
                    ("victim", target)),
                target,
                Filter.PvsExcept(user, entityManager: EntityManager),
                true,
                PopupType.SmallCaution);
        }

        if (bracer.Comp.Locked)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):user} locked the {ToPrettyString(bracer.Owner):bracer} of {ToPrettyString(target):target}.");
        }
        else
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user):user} unlocked the {ToPrettyString(bracer.Owner):bracer} of {ToPrettyString(target):target}.");
        }

        return true;
    }

    private bool IsBracerWornBy(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return bracer.Comp.User == user &&
               _power.TryGetWornBracer(user, out var worn) &&
               worn.Owner == bracer.Owner;
    }

    private bool CanOpenPulledDeadHunterBracerLockDialog(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        out Entity<YautjaBracerComponent> victimBracer)
    {
        victimBracer = default;

        if (!IsBracerWornBy(bracer, user) ||
            !HasComp<YautjaComponent>(user) ||
            !TryGetPulledVictim(user, out var pulled) ||
            pulled != victim)
        {
            return false;
        }

        if (HasComp<YautjaComponent>(victim) && !_mobState.IsDead(victim))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-dead-lock-living-hunter"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!TryGetVictimBracer(victim, null, out victimBracer))
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-yautja-bracer-dead-lock-missing-bracer", ("species", SpeciesName(victim))),
                user,
                user,
                PopupType.SmallCaution);
            return false;
        }

        return _mobState.IsDead(victim);
    }

    private bool CanUseConfirmedPulledDeadHunterBracerLock(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntityUid victim,
        EntityUid expectedVictimBracer,
        out Entity<YautjaBracerComponent> victimBracer)
    {
        victimBracer = default;

        return IsBracerWornBy(bracer, user) &&
               HasComp<YautjaComponent>(user) &&
               TryGetPulledVictim(user, out var pulled) &&
               pulled == victim &&
               _mobState.IsDead(victim) &&
               TryGetVictimBracer(victim, expectedVictimBracer, out victimBracer);
    }

    private bool TryGetPulledVictim(EntityUid user, out EntityUid victim)
    {
        if (TryComp(user, out PullerComponent? puller) &&
            puller.Pulling is { } pulled)
        {
            victim = pulled;
            return true;
        }

        victim = default;
        return false;
    }

    private bool TryGetVictimBracer(
        EntityUid victim,
        EntityUid? expectedBracer,
        out Entity<YautjaBracerComponent> bracer)
    {
        bracer = default;

        if (!_inventory.TryGetSlotEntity(victim, GlovesSlot, out var gloves) ||
            expectedBracer is { } expected && gloves.Value != expected ||
            !TryComp(gloves, out YautjaBracerComponent? bracerComp) ||
            bracerComp.User != victim)
        {
            return false;
        }

        bracer = (gloves.Value, bracerComp);
        return true;
    }

    private bool CanUnlockDeadHunterBracer(EntityUid user, EntityUid target, Entity<YautjaBracerComponent> bracer)
    {
        return HasComp<YautjaComponent>(user) &&
               HasComp<YautjaComponent>(target) &&
               _mobState.IsDead(target) &&
               bracer.Comp.User == target;
    }

    private string SpeciesName(EntityUid uid)
    {
        if (HasComp<YautjaComponent>(uid))
            return Loc.GetString("species-name-yautja");

        if (TryComp(uid, out HumanoidAppearanceComponent? humanoid) &&
            _prototype.TryIndex<SpeciesPrototype>(humanoid.Species, out var species))
        {
            return Loc.GetString(species.Name);
        }

        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    private bool TryGetActiveTrackableItem(EntityUid user, out EntityUid item)
    {
        if (_hands.GetActiveItem(user) is { } active && HasComp<ItemComponent>(active))
        {
            item = active;
            return true;
        }

        item = default;
        return false;
    }

    private bool IsTrackedItem(EntityUid item)
    {
        if (HasComp<YautjaTrackedItemComponent>(item))
            return true;

        return HasComp<YautjaTechItemComponent>(item) &&
               !HasComp<YautjaUntrackedItemComponent>(item);
    }

    private bool ToggleIdChip(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!CanUseIdChipBracer(bracer, user))
            return false;

        var chip = EnsureIdChip(bracer);
        if (chip == null)
            return false;

        ApplyIdChipUserData(bracer, user, chip.Value);

        var container = EnsureIdContainer(bracer);
        if (bracer.Comp.IdChipDeployed)
        {
            if (_inventory.TryGetSlotEntity(user, IdSlot, out var id) && id == chip)
                _inventory.TryUnequip(user, IdSlot, out _, silent: true, force: true);

            _containers.Insert(chip.Value, container, force: true);
            var displacedCardContainer = EnsureIdCardContainer(bracer);
            if (displacedCardContainer.ContainedEntity is { } displacedCard &&
                !_inventory.TryEquip(user, user, displacedCard, IdSlot, silent: true, force: true))
            {
                return false;
            }

            bracer.Comp.IdChipDeployed = false;
            Dirty(bracer);
            _audio.PlayPvs(bracer.Comp.IdChipSound, bracer.Owner);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-retracted"), user, user);
            return true;
        }

        if (_inventory.TryGetSlotEntity(user, IdSlot, out var occupied) && occupied != chip)
        {
            if (!_inventory.TryUnequip(user, IdSlot, out var displaced, silent: true, force: true) ||
                displaced is not { } displacedCard ||
                !_containers.Insert(displacedCard, EnsureIdCardContainer(bracer), force: true))
            {
                _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-slot-blocked"), user, user, PopupType.SmallCaution);
                return false;
            }
        }

        if (!_inventory.TryEquip(user, user, chip.Value, IdSlot, silent: true, force: true))
        {
            var displacedCardContainer = EnsureIdCardContainer(bracer);
            if (displacedCardContainer.ContainedEntity is { } displacedCard)
            {
                _containers.Remove(displacedCard, displacedCardContainer, force: true);
                _inventory.TryEquip(user, user, displacedCard, IdSlot, silent: true, force: true);
            }

            _containers.Insert(chip.Value, container, force: true);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-failed"), user, user, PopupType.SmallCaution);
            return false;
        }

        bracer.Comp.IdChipDeployed = true;
        Dirty(bracer);
        _audio.PlayPvs(bracer.Comp.IdChipSound, bracer.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-deployed"), user, user);
        return true;
    }

    private bool CanUseIdChipBracer(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return CanUseHeldOrWornHunterBracer(bracer, user);
    }

    private bool CanUseHeldOrWornHunterBracer(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return IsBracerWornBy(bracer, user) ||
               _hands.GetActiveItem(user) == bracer.Owner;
    }

    private EntityUid? EnsureIdChip(Entity<YautjaBracerComponent> bracer)
    {
        if (bracer.Comp.IdChip is { } existing && !Deleted(existing))
            return existing;

        var chip = Spawn(bracer.Comp.IdChipPrototype, Transform(bracer.Owner).Coordinates);
        EnsureComp<YautjaBracerIdChipComponent>(chip);
        bracer.Comp.IdChip = chip;
        _containers.Insert(chip, EnsureIdContainer(bracer), force: true);
        Dirty(bracer);
        return chip;
    }

    private void ApplyIdChipUserData(Entity<YautjaBracerComponent> bracer, EntityUid user, EntityUid chip)
    {
        if (TryComp(chip, out IdCardComponent? id))
            _idCard.TryChangeFullName(chip, Name(user), id);

        var access = bracer.Comp.BadBlood
            ? [YautjaBadBloodAccess]
            : AccessForOwnerRank(bracer.Comp.OwnerRank);

        _access.TrySetTags(chip, access);
    }

    private static ProtoId<AccessLevelPrototype>[] AccessForOwnerRank(YautjaBracerOwnerRank ownerRank)
    {
        return ownerRank switch
        {
            YautjaBracerOwnerRank.Elite => [YautjaSecureAccess, YautjaEliteAccess],
            YautjaBracerOwnerRank.Elder => [YautjaSecureAccess, YautjaEliteAccess, YautjaElderAccess],
            YautjaBracerOwnerRank.Leader => [YautjaSecureAccess, YautjaEliteAccess, YautjaElderAccess, YautjaLeaderAccess],
            YautjaBracerOwnerRank.Admin => [YautjaSecureAccess, YautjaEliteAccess, YautjaElderAccess, YautjaLeaderAccess, YautjaAncientAccess],
            _ => [YautjaSecureAccess],
        };
    }

    private ContainerSlot EnsureIdContainer(Entity<YautjaBracerComponent> bracer)
    {
        return _containers.EnsureContainer<ContainerSlot>(bracer.Owner, bracer.Comp.IdChipContainerId);
    }

    private ContainerSlot EnsureIdCardContainer(Entity<YautjaBracerComponent> bracer)
    {
        return _containers.EnsureContainer<ContainerSlot>(bracer.Owner, bracer.Comp.IdCardContainerId);
    }

    private bool TryCreateItem(
        Entity<YautjaBracerComponent> bracer,
        EntityUid user,
        EntProtoId prototype,
        FixedPoint2 cost,
        TimeSpan cooldown,
        ref TimeSpan nextUse,
        LocId createdMessage)
    {
        if (bracer.Comp.User != user)
            return false;

        if (_timing.CurTime < nextUse)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-fabricator-cooldown"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (_hands.GetActiveItem(user) != null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-hands-full"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_power.TryRemovePower(user, cost))
            return false;

        var item = Spawn(prototype, Transform(user).Coordinates);
        _hands.TryPickupAnyHand(user, item);
        nextUse = _timing.CurTime + cooldown;
        _audio.PlayPvs(bracer.Comp.FabricateSound, bracer.Owner);
        _popup.PopupEntity(Loc.GetString(createdMessage, ("item", item)), user, user);
        return true;
    }

    private void SendTranslatorMessage(Entity<YautjaBracerComponent> bracer, EntityUid user, string message)
    {
        var trimmed = FormattedMessage.RemoveMarkupPermissive(message.Trim());
        if (trimmed.Length > TranslatorMaxMessageLength)
            trimmed = trimmed[..TranslatorMaxMessageLength];

        if (string.IsNullOrWhiteSpace(trimmed))
            return;

        if (!_power.TryRemovePower(user, bracer.Comp.TranslatorCost))
            return;

        var translated = TransformTranslatorMessage(trimmed, bracer.Comp.TranslatorType);
        var wrapped = Loc.GetString(
            "chat-manager-entity-say-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Loc.GetString("cmu-yautja-translator-speaker"))),
            ("verb", Loc.GetString("cmu-yautja-translator-verb")),
            ("fontType", "Default"),
            ("fontSize", 12),
            ("message", FormattedMessage.EscapeText(translated)));

        var channels = new HashSet<INetChannel>();
        foreach (var recipient in Filter.Pvs(user, entityManager: EntityManager).Recipients)
        {
            channels.Add(recipient.Channel);
        }

        if (channels.Count > 0)
            _chat.ChatMessageToMany(ChatChannel.Local, translated, wrapped, user, false, true, channels, GetTranslatorColor(bracer.Comp.TranslatorType));

        _audio.PlayPvs(bracer.Comp.TranslatorSound, user);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(user):user} used Yautja translator: {trimmed}");
    }

    private static string TransformTranslatorMessage(string message, YautjaTranslatorType translatorType)
    {
        if (translatorType == YautjaTranslatorType.Modern)
            return message;

        return message
            .Replace("a", "@")
            .Replace("e", "3")
            .Replace("i", "1")
            .Replace("o", "0")
            .Replace("s", "5")
            .Replace("l", "1");
    }

    private static Color GetTranslatorColor(YautjaTranslatorType translatorType)
    {
        return translatorType == YautjaTranslatorType.Retro
            ? RetroTranslatorColor
            : ModernTranslatorColor;
    }

    private void UpdateTranslatorUi(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!IsBracerWornBy(bracer, user))
            return;

        _ui.SetUiState(
            bracer.Owner,
            YautjaTranslatorUIKey.Key,
            new YautjaTranslatorBuiState(
                (int) bracer.Comp.Charge,
                (int) bracer.Comp.MaxCharge,
                (int) bracer.Comp.TranslatorCost,
                TranslatorMaxMessageLength));
    }

    private void OpenTranslatorUi(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        _ui.TryOpenUi(bracer.Owner, YautjaTranslatorUIKey.Key, user);
        UpdateTranslatorUi(bracer, user);
    }

    private void RunRandomBracerFunction(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        switch (_random.Next(1, 11))
        {
            case 1:
                if (TryComp(bracer.Owner, out YautjaGearContainerComponent? gearContainer))
                    EntityManager.System<YautjaAttachmentSystem>().TryToggleBracerAttachments((bracer.Owner, gearContainer), user);
                break;
            case 2:
                EntityManager.System<YautjaBracerMenuSystem>().TryOpenTracker(bracer, user);
                break;
            case 3:
                _cloak.TryToggleCloakForced(bracer, user, 50);
                break;
            case 4:
                if (TryComp(bracer.Owner, out YautjaGearContainerComponent? casterGearContainer))
                    EntityManager.System<YautjaAttachmentSystem>().TryToggleCaster((bracer.Owner, casterGearContainer), user);
                break;
            case 5:
                TryCreateItem(bracer, user, bracer.Comp.StabilisingCrystalPrototype, bracer.Comp.StabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-crystal-created");
                break;
            case 6:
                EntityManager.System<YautjaSmartDiscSystem>().TryCallDisc(bracer, user);
                break;
            case 7:
                OpenTranslatorUi(bracer, user);
                break;
            case 8:
                if (TryComp(bracer.Owner, out YautjaGearContainerComponent? removableGearContainer))
                    EntityManager.System<YautjaAttachmentSystem>().TryRemoveBracerAttachments((bracer.Owner, removableGearContainer), user);
                break;
            default:
                TrySeverBothArms(user, "cmu-yautja-tech-random-delimbs", bracer.Comp.TechDelimbSound);
                break;
        }
    }

    private enum YautjaBracerUseResult : byte
    {
        Blocked,
        Allowed,
        Delayed,
        RandomFunction,
    }
}
