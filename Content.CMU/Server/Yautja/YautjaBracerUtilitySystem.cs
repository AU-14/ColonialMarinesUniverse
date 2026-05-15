using Content.Server.Administration.Logs;
using Content.Server.Chat.Managers;
using Content.Shared._CMU14.Medical.BodyPart.Events;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Synth;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Yautja;

public sealed class YautjaBracerUtilitySystem : EntitySystem
{
    private const string IdSlot = "id";
    private const int TranslatorMaxMessageLength = 160;
    private static readonly Color TranslatorColor = Color.FromHex("#ff4d4d");
    private static readonly DamageSpecifier DefaultTechShockDamage = new()
    {
        DamageDict = new()
        {
            { "Shock", 20 },
        },
    };

    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly YautjaCloakSystem _cloak = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<YautjaBracerComponent, BeingUnequippedAttemptEvent>(OnBeingUnequippedAttempt);
        SubscribeLocalEvent<YautjaBracerComponent, InventoryRelayedEvent<GetVerbsEvent<EquipmentVerb>>>(OnGetEquipmentVerbs);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerLockActionEvent>(OnToggleLock);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaTranslatorActionEvent>(OnTranslator);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleBracerIdChipActionEvent>(OnToggleIdChip);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateStabilisingCrystalActionEvent>(OnCreateStabilisingCrystal);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateHumanStabilisingCrystalActionEvent>(OnCreateHumanStabilisingCrystal);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaCreateHealingCapsuleActionEvent>(OnCreateHealingCapsule);
        SubscribeLocalEvent<YautjaTechItemComponent, YautjaTechMisusedEvent>(OnTechMisused);

        Subs.BuiEvents<YautjaBracerComponent>(YautjaTranslatorUIKey.Key, subs =>
        {
            subs.Event<YautjaTranslatorSendMessageMsg>(OnTranslatorMessage);
        });
    }

    private void OnMapInit(Entity<YautjaBracerComponent> ent, ref MapInitEvent args)
    {
        EnsureIdContainer(ent);
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
            Act = () => ToggleLock(ent, ev.User, ev.Target),
        });
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
        if (!TryResolveBracerUse(ent, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(ent, user);
            return true;
        }

        return ToggleLock(ent, user, user);
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

    private void OnCreateHealingCapsule(Entity<YautjaBracerComponent> ent, ref YautjaCreateHealingCapsuleActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        args.Handled = true;
        TryCreateHealingCapsule(ent, args.Performer);
    }

    public bool TryOpenTranslator(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryResolveBracerUse(bracer, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(bracer, user);
            return true;
        }

        _ui.TryOpenUi(bracer.Owner, YautjaTranslatorUIKey.Key, user);
        UpdateTranslatorUi(bracer, user);
        return true;
    }

    public bool TryToggleIdChip(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryResolveBracerUse(bracer, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(bracer, user);
            return true;
        }

        return ToggleIdChip(bracer, user);
    }

    public bool TryCreateStabilisingCrystal(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryResolveBracerUse(bracer, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(bracer, user);
            return true;
        }

        return TryCreateItem(bracer, user, bracer.Comp.StabilisingCrystalPrototype, bracer.Comp.StabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-crystal-created");
    }

    public bool TryCreateHumanStabilisingCrystal(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryResolveBracerUse(bracer, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(bracer, user);
            return true;
        }

        return TryCreateItem(bracer, user, bracer.Comp.HumanStabilisingCrystalPrototype, bracer.Comp.HumanStabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-human-crystal-created");
    }

    public bool TryCreateHealingCapsule(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (!TryResolveBracerUse(bracer, user, out var randomFunction))
            return false;

        if (randomFunction)
        {
            RunRandomBracerFunction(bracer, user);
            return true;
        }

        return TryCreateItem(bracer, user, bracer.Comp.HealingCapsulePrototype, bracer.Comp.HealingCapsuleCost, bracer.Comp.HealingCapsuleCooldown, ref bracer.Comp.NextHealingCapsule, "cmu-yautja-bracer-healing-capsule-created");
    }

    private void OnTechMisused(Entity<YautjaTechItemComponent> ent, ref YautjaTechMisusedEvent args)
    {
        if (HasComp<YautjaComponent>(args.User))
            return;

        if (TryComp(args.Item, out YautjaBracerComponent? bracer))
        {
            TryResolveBracerUse((args.Item, bracer), args.User, out _, requireWorn: false);
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
            ApplyTechPunishment(user, uid, bracer.TechShockDamage, bracer.TechShockStun, bracer.NonYautjaDelimbChance);
        }
    }

    private bool TryResolveBracerUse(Entity<YautjaBracerComponent> bracer, EntityUid user, out bool randomFunction, bool requireWorn = true)
    {
        randomFunction = false;

        if (requireWorn && !IsBracerWornBy(bracer, user))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-must-be-worn"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (IsYautjaTechUser(user))
            return true;

        var (workingChance, randomChance) = GetNonYautjaChances(user, bracer.Comp);
        var roll = _random.NextFloat();
        if (roll < workingChance)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-random-works"), user, user, PopupType.SmallCaution);
            return true;
        }

        if (roll < workingChance + randomChance)
        {
            randomFunction = true;
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-random-function"), user, user, PopupType.SmallCaution);
            return true;
        }

        ApplyTechPunishment(user, bracer.Owner, bracer.Comp.TechShockDamage, bracer.Comp.TechShockStun, bracer.Comp.NonYautjaDelimbChance);
        return false;
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

        var job = idCard.JobPrototype?.Id ??
                  idCard.JobTitle ??
                  idCard.LocalizedJobTitle ??
                  string.Empty;

        return job.Contains("research", StringComparison.OrdinalIgnoreCase) ||
               job.Contains("scientist", StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyTechPunishment(EntityUid user, EntityUid item, DamageSpecifier damage, TimeSpan stun, float delimbChance)
    {
        _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-shock"), user, user, PopupType.LargeCaution);
        _damage.TryChangeDamage(user, new DamageSpecifier(damage), true, origin: item);
        _stun.TryStun(user, stun, true);

        if (_random.Prob(delimbChance))
            TrySeverBothArms(user);

        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} was punished for misusing Yautja technology {ToPrettyString(item):item}");
    }

    private bool TrySeverBothArms(EntityUid user)
    {
        var severed = false;
        severed |= TrySeverPart(user, BodyPartType.Arm, BodyPartSymmetry.Left);
        severed |= TrySeverPart(user, BodyPartType.Arm, BodyPartSymmetry.Right);

        if (severed)
            _popup.PopupEntity(Loc.GetString("cmu-yautja-tech-delimbs"), user, user, PopupType.LargeCaution);

        return severed;
    }

    private bool TrySeverPart(EntityUid body, BodyPartType type, BodyPartSymmetry symmetry)
    {
        foreach (var (partUid, part) in _body.GetBodyChildren(body))
        {
            if (part.PartType != type || part.Symmetry != symmetry)
                continue;

            var ev = new BodyPartSeveredEvent(body, partUid, type);
            RaiseLocalEvent(partUid, ref ev);
            return true;
        }

        return false;
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
        _popup.PopupEntity(Loc.GetString(bracer.Comp.Locked ? "cmu-yautja-bracer-locked-now" : "cmu-yautja-bracer-unlocked-now"), user, user);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(user):user} {(bracer.Comp.Locked ? "locked" : "unlocked")} Yautja bracer {ToPrettyString(bracer.Owner):bracer} on {ToPrettyString(target):target}");
        return true;
    }

    private bool IsBracerWornBy(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        return bracer.Comp.User == user &&
               _power.TryGetWornBracer(user, out var worn) &&
               worn.Owner == bracer.Owner;
    }

    private bool CanUnlockDeadHunterBracer(EntityUid user, EntityUid target, Entity<YautjaBracerComponent> bracer)
    {
        return HasComp<YautjaComponent>(user) &&
               HasComp<YautjaComponent>(target) &&
               _mobState.IsDead(target) &&
               bracer.Comp.User == target;
    }

    private bool ToggleIdChip(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (bracer.Comp.User != user)
            return false;

        var chip = EnsureIdChip(bracer, user);
        if (chip == null)
            return false;

        var container = EnsureIdContainer(bracer);
        if (bracer.Comp.IdChipDeployed)
        {
            if (_inventory.TryGetSlotEntity(user, IdSlot, out var id) && id == chip)
                _inventory.TryUnequip(user, IdSlot, out _, silent: true, force: true);

            _containers.Insert(chip.Value, container, force: true);
            bracer.Comp.IdChipDeployed = false;
            Dirty(bracer);
            _audio.PlayPvs(bracer.Comp.IdChipSound, bracer.Owner);
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-retracted"), user, user);
            return true;
        }

        if (_inventory.TryGetSlotEntity(user, IdSlot, out var occupied) && occupied != chip)
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-slot-blocked"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_inventory.TryEquip(user, user, chip.Value, IdSlot, silent: true, force: true))
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-failed"), user, user, PopupType.SmallCaution);
            return false;
        }

        bracer.Comp.IdChipDeployed = true;
        Dirty(bracer);
        _audio.PlayPvs(bracer.Comp.IdChipSound, bracer.Owner);
        _popup.PopupEntity(Loc.GetString("cmu-yautja-bracer-id-deployed"), user, user);
        return true;
    }

    private EntityUid? EnsureIdChip(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        if (bracer.Comp.IdChip is { } existing && !Deleted(existing))
            return existing;

        var chip = Spawn(bracer.Comp.IdChipPrototype, Transform(user).Coordinates);
        EnsureComp<YautjaBracerIdChipComponent>(chip);
        bracer.Comp.IdChip = chip;
        _containers.Insert(chip, EnsureIdContainer(bracer), force: true);
        Dirty(bracer);
        return chip;
    }

    private ContainerSlot EnsureIdContainer(Entity<YautjaBracerComponent> bracer)
    {
        return _containers.EnsureContainer<ContainerSlot>(bracer.Owner, bracer.Comp.IdChipContainerId);
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
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-not-enough-power"), user, user, PopupType.SmallCaution);
            return false;
        }

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
        {
            _popup.PopupEntity(Loc.GetString("cmu-yautja-not-enough-power"), user, user, PopupType.SmallCaution);
            return;
        }

        var wrapped = Loc.GetString(
            "chat-manager-entity-say-wrap-message",
            ("entityName", FormattedMessage.EscapeText(Loc.GetString("cmu-yautja-translator-speaker"))),
            ("verb", Loc.GetString("cmu-yautja-translator-verb")),
            ("fontType", "Default"),
            ("fontSize", 12),
            ("message", FormattedMessage.EscapeText(trimmed)));

        var channels = new HashSet<INetChannel>();
        foreach (var recipient in Filter.Pvs(user, entityManager: EntityManager).Recipients)
        {
            channels.Add(recipient.Channel);
        }

        if (channels.Count > 0)
            _chat.ChatMessageToMany(ChatChannel.Local, trimmed, wrapped, user, false, true, channels, TranslatorColor);

        _audio.PlayPvs(bracer.Comp.TranslatorSound, user);

        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"{ToPrettyString(user):user} used Yautja translator: {trimmed}");
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

    private void RunRandomBracerFunction(Entity<YautjaBracerComponent> bracer, EntityUid user)
    {
        switch (_random.Next(5))
        {
            case 0:
                ToggleLock(bracer, user, user);
                break;
            case 1:
                ToggleIdChip(bracer, user);
                break;
            case 2:
                TryCreateItem(bracer, user, bracer.Comp.StabilisingCrystalPrototype, bracer.Comp.StabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-crystal-created");
                break;
            case 3:
                TryCreateItem(bracer, user, bracer.Comp.HumanStabilisingCrystalPrototype, bracer.Comp.HumanStabilisingCrystalCost, bracer.Comp.StabilisingCrystalCooldown, ref bracer.Comp.NextStabilisingCrystal, "cmu-yautja-bracer-human-crystal-created");
                break;
            default:
                TryCreateItem(bracer, user, bracer.Comp.HealingCapsulePrototype, bracer.Comp.HealingCapsuleCost, bracer.Comp.HealingCapsuleCooldown, ref bracer.Comp.NextHealingCapsule, "cmu-yautja-bracer-healing-capsule-created");
                break;
        }
    }
}
