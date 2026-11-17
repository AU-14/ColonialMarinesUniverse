using Content.Server.CMU14.CharacterDescription;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.CMU14.CriminalRecords;
using Content.Shared.CMU14.TargetLock;
using Content.Shared.Chat;
using Content.Shared.CriminalRecords;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared._RMC14.Clothing;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Storage;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Security;
using Content.Shared.StationRecords;
using Content.Shared.StationRecords.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using System.Text;

namespace Content.Server.CMU14.TargetLock;

/// <summary>
///     Locks on to someone for <see cref="CMUTargetLockComponent.LockTime"/>, then reads them and sends the report to
///     whoever is holding the scanner. A weapon on them is also warned about in chat, since a popup is easy to miss.
/// </summary>
public sealed class CMUTargetLockSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedCMChatSystem _cmChat = default!;
    [Dependency] private CMUUniversalRecordsSystem _universalRecords = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ItemToggleSystem _itemToggle = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StationRecordsSystem _records = default!;

    private const int MaxContainerDepth = 3;

    public override void Initialize()
    {
        SubscribeLocalEvent<HelmetAccessoryHolderComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<HelmetAccessoryHolderComponent, CMUTargetLockActionEvent>(OnAction);
        SubscribeLocalEvent<CMUTargetLockComponent, CMUTargetLockDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<CMUTargetLockComponent, EntGotInsertedIntoContainerMessage>(OnVisorInserted);
        SubscribeLocalEvent<CMUTargetLockComponent, EntGotRemovedFromContainerMessage>(OnVisorRemoved);
    }

    // The helmet only asks for its actions when it is put on, so a visor clipped onto a helmet that is already worn
    // has to be granted (and taken away) by hand.
    private bool TryGetWearer(EntityUid helmet, out EntityUid wearer)
    {
        wearer = default;
        if (!TryComp(helmet, out HelmetAccessoryHolderComponent? holder) ||
            !_inventory.TryGetContainingSlot(helmet, out var slot) ||
            (slot.SlotFlags & holder.Slot) == 0 ||
            !_container.TryGetContainingContainer(helmet, out var container) ||
            !HasComp<ActionsComponent>(container.Owner))
        {
            return false;
        }

        wearer = container.Owner;
        return true;
    }

    private void OnVisorInserted(Entity<CMUTargetLockComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        var helmet = args.Container.Owner;
        if (!TryGetWearer(helmet, out var wearer))
            return;

        _actionContainer.EnsureAction(helmet, ref ent.Comp.ActionEntity, ent.Comp.ActionId);
        if (ent.Comp.ActionEntity is { } action)
            _actions.GrantActions(wearer, [action], helmet);
    }

    private void OnVisorRemoved(Entity<CMUTargetLockComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        var helmet = args.Container.Owner;
        if (ent.Comp.ActionEntity is not { } action || !TryGetWearer(helmet, out var wearer))
            return;

        _actions.RemoveProvidedAction(wearer, helmet, action);
    }

    private Entity<CMUTargetLockComponent>? FindVisor(EntityUid helmet)
    {
        if (!TryComp(helmet, out StorageComponent? storage) || storage.Container == null)
            return null;

        foreach (var item in storage.Container.ContainedEntities)
        {
            if (TryComp(item, out CMUTargetLockComponent? visor))
                return (item, visor);
        }

        return null;
    }

    // The wearer of a helmet with the visor on it gets the lock-on action.
    private void OnGetItemActions(Entity<HelmetAccessoryHolderComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags is not { } slot || (slot & ent.Comp.Slot) == 0)
            return;

        if (FindVisor(ent) is not { } visor)
            return;

        args.AddAction(ref visor.Comp.ActionEntity, visor.Comp.ActionId);
    }

    private void OnAction(Entity<HelmetAccessoryHolderComponent> ent, ref CMUTargetLockActionEvent args)
    {
        if (args.Handled || FindVisor(ent) is not { } visor)
            return;

        var user = args.Performer;
        var target = args.Target;
        if (target == user || !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;

        if (!_itemToggle.IsActivated(visor.Owner))
        {
            _popup.PopupEntity(Loc.GetString("cmu-target-lock-visor-up"), user, user);
            return;
        }

        if (!_transform.InRange(user, target, visor.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("cmu-target-lock-too-far"), user, user);
            return;
        }

        _popup.PopupEntity(Loc.GetString("cmu-target-lock-locking", ("target", target)), user, user);

        var doAfter = new DoAfterArgs(EntityManager, user, visor.Comp.LockTime, new CMUTargetLockDoAfterEvent(), visor, target, visor)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = visor.Comp.Range,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDoAfter(Entity<CMUTargetLockComponent> ent, ref CMUTargetLockDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target)
            return;

        args.Handled = true;

        var report = BuildReport(ent, target);
        if (TryComp(args.User, out ActorComponent? actor))
            RaiseNetworkEvent(report, actor.PlayerSession);

        if (report.Weapons.Count == 0)
            return;

        var warning = Loc.GetString("cmu-target-lock-weapon-warning",
            ("target", report.Name),
            ("weapons", string.Join(", ", report.Weapons)));

        _popup.PopupEntity(warning, args.User, args.User, PopupType.LargeCaution);
        _cmChat.ChatMessageToOne(warning, args.User, ChatChannel.Local, colorOverride: new Color(1f, 0.25f, 0.25f, 1f));
    }

    public CMUTargetLockReportEvent BuildReport(Entity<CMUTargetLockComponent> scanner, EntityUid target)
    {
        var name = Name(target);
        var (health, percent) = EstimateHealth(target);

        var position = string.Empty;
        if (_idCard.TryFindIdCard(target, out var card))
            position = card.Comp.LocalizedJobTitle ?? string.Empty;

        if (string.IsNullOrWhiteSpace(position))
            position = Loc.GetString("cmu-target-lock-no-position");

        return new CMUTargetLockReportEvent(
            GetNetEntity(target),
            name,
            position,
            health,
            percent,
            BuildCriminalRecord(name),
            BuildMedicalRecord(target),
            FindWeapons(target, scanner.Comp.MeleeWeaponDamage));
    }

    private (CMUTargetLockHealth, int) EstimateHealth(EntityUid target)
    {
        if (_mobState.IsDead(target))
            return (CMUTargetLockHealth.Dead, 0);

        var fraction = 1f;
        if (TryComp(target, out DamageableComponent? damageable) &&
            _thresholds.TryGetDeadThreshold(target, out var dead) &&
            dead > 0)
        {
            fraction = 1f - (float) (_damageable.GetTotalDamage((target, damageable)) / dead.Value);
        }

        fraction = Math.Clamp(fraction, 0f, 1f);

        // Rounded to the nearest ten so it reads as an estimate, not a reading.
        var percent = (int) (Math.Round(fraction * 10f) * 10f);

        var level = _mobState.IsCritical(target) || fraction < 0.25f
            ? CMUTargetLockHealth.Critical
            : fraction < 0.6f
                ? CMUTargetLockHealth.Wounded
                : fraction < 0.9f
                    ? CMUTargetLockHealth.Light
                    : CMUTargetLockHealth.Healthy;

        return (level, percent);
    }

    private string BuildCriminalRecord(string name)
    {
        if (_universalRecords.GetExistingRecords() is not { } station ||
            _records.GetRecordByName(station, name) is not { } id)
        {
            return Loc.GetString("cmu-target-lock-no-record");
        }

        var key = new StationRecordKey(id, station);
        if (!_records.TryGetRecord<CriminalRecord>(key, out var record))
            return Loc.GetString("cmu-target-lock-no-record");

        var builder = new StringBuilder();
        builder.AppendLine(Loc.GetString("cmu-target-lock-status", ("status", Loc.GetString(StatusName(record.Status)))));

        if (record.Status == SecurityStatus.Wanted && !string.IsNullOrWhiteSpace(record.Reason))
            builder.AppendLine(Loc.GetString("cmu-target-lock-wanted-reason", ("reason", record.Reason)));

        foreach (var warrant in record.CMUWarrants)
        {
            builder.AppendLine(Loc.GetString("cmu-target-lock-warrant", ("charge", warrant.Charge)));
        }

        foreach (var arrest in record.CMUArrests)
        {
            builder.AppendLine(Loc.GetString("cmu-target-lock-arrest",
                ("charges", arrest.Charges), ("minutes", arrest.SentenceMinutes)));
        }

        foreach (var crime in record.History)
        {
            builder.AppendLine(Loc.GetString("cmu-target-lock-crime", ("crime", crime.Crime)));
        }

        if (record.Bounty > 0)
            builder.AppendLine(Loc.GetString("cmu-target-lock-bounty", ("bounty", record.Bounty)));

        if (record.CMUWarrants.Count == 0 && record.CMUArrests.Count == 0 && record.History.Count == 0 &&
            record.Status == SecurityStatus.None)
        {
            builder.AppendLine(Loc.GetString("cmu-target-lock-clean-record"));
        }

        return builder.ToString().TrimEnd();
    }

    private static string StatusName(SecurityStatus status)
    {
        return status switch
        {
            SecurityStatus.Wanted => "cmu-target-lock-status-wanted",
            SecurityStatus.Detained => "cmu-target-lock-status-detained",
            _ => "cmu-target-lock-status-none",
        };
    }

    private string BuildMedicalRecord(EntityUid target)
    {
        if (!TryComp(target, out CharacterDescriptionComponent? desc))
            return Loc.GetString("cmu-target-lock-no-medical");

        var text = MedicalRecordBuilder.Build(EntityManager, _reagents, target, desc);
        return string.IsNullOrWhiteSpace(text) ? Loc.GetString("cmu-target-lock-no-medical") : text;
    }

    /// <summary>
    ///     Guns and hard-hitting melee weapons in their hands, worn, or in anything they carry (belts, holsters,
    ///     pouches, satchels).
    /// </summary>
    private List<string> FindWeapons(EntityUid target, float meleeDamage)
    {
        var found = new List<string>();
        foreach (var item in _inventory.GetHandOrInventoryEntities(target))
        {
            Collect(item, meleeDamage, found, 0);
        }

        return found;
    }

    private void Collect(EntityUid item, float meleeDamage, List<string> found, int depth)
    {
        if (HasComp<GunComponent>(item) ||
            (TryComp(item, out MeleeWeaponComponent? melee) && (float) melee.Damage.GetTotal() >= meleeDamage))
        {
            found.Add(Name(item));
            return;
        }

        if (depth >= MaxContainerDepth || !TryComp(item, out ContainerManagerComponent? manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                Collect(contained, meleeDamage, found, depth + 1);
            }
        }
    }
}
