using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Yautja;

public sealed partial class YautjaPowerSystem : EntitySystem
{
    private static readonly ProtoId<NpcFactionPrototype> YautjaBadBloodFaction = "CMUYautjaBadBlood";
    private static readonly SpriteSpecifier.Rsi PredatorIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "predator");
    private static readonly SpriteSpecifier.Rsi StolenBracerIcon =
        new(new ResPath("/Textures/_RMC14/Interface/map_blips.rsi"), "bracer_stolen");

    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaBracerComponent, ExaminedEvent>(OnBracerExamined);
        SubscribeLocalEvent<YautjaBracerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<YautjaBracerComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<YautjaBracerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<YautjaThrallBracerComponent, ExaminedEvent>(OnThrallBracerExamined);
        SubscribeLocalEvent<YautjaPowerActionComponent, RMCActionUseAttemptEvent>(OnPowerActionAttempt);
        SubscribeLocalEvent<YautjaPowerActionComponent, RMCActionUseEvent>(OnPowerActionUse);
    }

    private void OnBracerExamined(Entity<YautjaBracerComponent> ent, ref ExaminedEvent args)
    {
        PushChargeExamine(ref args, ent.Comp.Charge, ent.Comp.MaxCharge);
        PushHunterBracerExamine(ent, ref args);
    }

    private static void OnThrallBracerExamined(Entity<YautjaThrallBracerComponent> ent, ref ExaminedEvent args)
    {
        PushChargeExamine(ref args, ent.Comp.Charge, ent.Comp.MaxCharge);
    }

    private static void PushChargeExamine(ref ExaminedEvent args, FixedPoint2 charge, FixedPoint2 maxCharge)
    {
        args.PushMarkup($"They currently have <bold>{(int) charge}/{(int) maxCharge}</bold> charge.");
    }

    private void PushHunterBracerExamine(Entity<YautjaBracerComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp(ent, out YautjaGearContainerComponent? gear))
        {
            if (TryGetBracerAttachmentExamineEntity(gear.Gear, gear.InstalledGear, out var left))
                args.PushMarkup($"The left bracer attachment is {FormattedMessage.EscapeText(Name(left))}.");

            if (TryGetBracerAttachmentExamineEntity(gear.SecondaryGear, gear.InstalledGear, out var right))
                args.PushMarkup($"The right bracer attachment is {FormattedMessage.EscapeText(Name(right))}.");
        }

        if (ent.Comp.BadBlood && HasComp<YautjaTechAuthorizedComponent>(args.Examiner))
            args.PushMarkup("This belongs to a bad-blood!");
    }

    private bool TryGetBracerAttachmentExamineEntity(
        Dictionary<YautjaGearKind, EntityUid> attachments,
        HashSet<EntityUid> installedGear,
        out EntityUid entity)
    {
        foreach (var holder in attachments.Values)
        {
            if (!installedGear.Contains(holder) || Deleted(holder))
                continue;

            if (TryComp(holder, out YautjaStoredGearComponent? stored) &&
                stored.AttachedWeapon is { } attached &&
                !Deleted(attached))
            {
                entity = attached;
                return true;
            }

            entity = holder;
            return true;
        }

        entity = default;
        return false;
    }

    private void OnGetItemActions(Entity<YautjaBracerComponent> ent, ref GetItemActionsEvent args)
    {
        var isYautja = HasComp<YautjaComponent>(args.User);
        if (args.InHands)
        {
            if (isYautja && _hands.GetActiveItem(args.User) == ent.Owner)
            {
                AddAction(ent.Comp, ref args, ref ent.Comp.ToggleIdChipAction, ent.Comp.ToggleIdChipActionId);
                AddAction(ent.Comp, ref args, ref ent.Comp.LinkThrallBracerAction, ent.Comp.LinkThrallBracerActionId);
                AddAction(ent.Comp, ref args, ref ent.Comp.ChangeExplosionTypeAction, ent.Comp.ChangeExplosionTypeActionId);
            }

            return;
        }

        if (args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        AddAction(ent.Comp, ref args, ref ent.Comp.OpenBracerMenuAction, ent.Comp.OpenBracerMenuActionId);
        AddAction(ent.Comp, ref args, ref ent.Comp.ToggleCloakAction, ent.Comp.ToggleCloakActionId);
        AddAction(ent.Comp, ref args, ref ent.Comp.RecallAction, ent.Comp.RecallActionId);
        AddAction(ent.Comp, ref args, ref ent.Comp.CallDiscAction, ent.Comp.CallDiscActionId);

        if (isYautja && !HasComp<YautjaYoungbloodComponent>(args.User))
            AddAction(ent.Comp, ref args, ref ent.Comp.SelfDestructAction, ent.Comp.SelfDestructActionId);

        AddAction(ent.Comp, ref args, ref ent.Comp.TranslatorAction, ent.Comp.TranslatorActionId);

        if (isYautja)
        {
            AddAction(ent.Comp, ref args, ref ent.Comp.ToggleIdChipAction, ent.Comp.ToggleIdChipActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.LinkThrallBracerAction, ent.Comp.LinkThrallBracerActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.ChangeExplosionTypeAction, ent.Comp.ChangeExplosionTypeActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.ToggleNotificationSoundAction, ent.Comp.ToggleNotificationSoundActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.ToggleBracerNameAction, ent.Comp.ToggleBracerNameActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.TransmitThrallMessageAction, ent.Comp.TransmitThrallMessageActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.TrackGearAction, ent.Comp.TrackGearActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.AddTrackedItemAction, ent.Comp.AddTrackedItemActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.RemoveTrackedItemAction, ent.Comp.RemoveTrackedItemActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.CreateStabilisingCrystalAction, ent.Comp.CreateStabilisingCrystalActionId);
            AddAction(ent.Comp, ref args, ref ent.Comp.CreateHealingCapsuleAction, ent.Comp.CreateHealingCapsuleActionId);
        }
    }

    private static void AddAction(
        YautjaBracerComponent bracer,
        ref GetItemActionsEvent args,
        ref EntityUid? action,
        EntProtoId actionId)
    {
        if (bracer.ActionWhitelist != null &&
            !bracer.ActionWhitelist.Contains(actionId))
        {
            return;
        }

        args.AddAction(ref action, actionId);
    }

    private void OnEquipped(Entity<YautjaBracerComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.User = args.Equipee;
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenEvery;
        if (!ShouldSkipCmss13EquipAutoLock(ent.Comp, args.Equipee))
            SetLocked(ent, true);
        UpdateAlert(ent);
        AddBracerTacticalMapMarker(ent, args.Equipee);
        _audio.PlayPredicted(ent.Comp.EquipSound, ent.Owner, args.Equipee);
    }

    private bool ShouldSkipCmss13EquipAutoLock(YautjaBracerComponent bracer, EntityUid wearer)
    {
        if (bracer.BadBlood)
            return true;

        return TryComp(wearer, out NpcFactionMemberComponent? faction) &&
               faction.Factions.Contains(YautjaBadBloodFaction);
    }

    private void OnUnequipped(Entity<YautjaBracerComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ClearAlert(ent);
        RemoveBracerTacticalMapMarker(args.Equipee);
        StopSelfDestructAudio(ent);
        ent.Comp.User = null;
        SetLocked(ent, false);

        var ev = new YautjaBracerUnequippedEvent(args.Equipee, args.SlotFlags);
        RaiseLocalEvent(ent, ref ev);
    }

    private void AddBracerTacticalMapMarker(Entity<YautjaBracerComponent> bracer, EntityUid wearer)
    {
        // CMSS13 base bracer equipped() returns before minimap setup for Bad Blood bracers
        // and Bad Blood faction wearers.
        if (ShouldSkipCmss13EquipAutoLock(bracer.Comp, wearer))
            return;

        var marker = EnsureComp<YautjaBracerTacticalMapMarkerComponent>(wearer);
        marker.HadIcon = TryComp<TacticalMapIconComponent>(wearer, out var oldIcon);
        marker.PreviousIcon = oldIcon?.Icon;
        marker.PreviousBackground = oldIcon?.Background;

        var yautja = HasComp<YautjaComponent>(wearer);
        _tacticalMap.EnsureTracked(wearer, trackDead: false);
        _tacticalMap.SetYautjaTracked(wearer, true);
        _tacticalMap.SetYautjaUser(wearer, yautja);
        _tacticalMap.SetIcon(wearer, yautja ? PredatorIcon : StolenBracerIcon);
        _tacticalMap.RefreshTracked(wearer);
    }

    private void RemoveBracerTacticalMapMarker(EntityUid wearer)
    {
        if (HasWornNormalYautjaBracer(wearer) ||
            !TryComp<YautjaBracerTacticalMapMarkerComponent>(wearer, out var marker))
        {
            return;
        }

        _tacticalMap.SetYautjaTracked(wearer, false);
        _tacticalMap.SetYautjaUser(wearer, false);

        if (marker.HadIcon)
            _tacticalMap.SetIcon(wearer, marker.PreviousIcon, marker.PreviousBackground);
        else
            _tacticalMap.RemoveIcon(wearer);

        RemCompDeferred<YautjaBracerTacticalMapMarkerComponent>(wearer);
        _tacticalMap.RefreshTracked(wearer);
    }

    private bool HasWornNormalYautjaBracer(EntityUid wearer)
    {
        return TryGetWornBracer(wearer, out var bracer) &&
               !bracer.Comp.BadBlood;
    }

    private void OnRemove(Entity<YautjaBracerComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        ClearAlert(ent);
        ent.Comp.SelfDestructArmed = false;
        ent.Comp.SelfDestructAt = TimeSpan.Zero;
        ent.Comp.NextSelfDestructWarning = TimeSpan.Zero;
        StopSelfDestructAudio(ent);
    }

    private void SetLocked(Entity<YautjaBracerComponent> bracer, bool locked)
    {
        if (bracer.Comp.Locked == locked)
            return;

        bracer.Comp.Locked = locked;
        Dirty(bracer);
        _actions.SetToggled(bracer.Comp.ToggleLockAction, locked);
    }

    private void OnPowerActionAttempt(Entity<YautjaPowerActionComponent> action, ref RMCActionUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (action.Comp.RequireMask && !HasActiveMask(args.User))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-mask-required"), args.User, args.User, PopupType.SmallCaution);
            args.Cancelled = true;
            return;
        }

        if (!action.Comp.RequireBracer)
            return;

        if (!HasPowerPopup(args.User, action.Comp.Cost))
            args.Cancelled = true;
    }

    private void OnPowerActionUse(Entity<YautjaPowerActionComponent> action, ref RMCActionUseEvent args)
    {
        if (_net.IsClient || !action.Comp.RequireBracer || action.Comp.Cost == FixedPoint2.Zero)
            return;

        TryRemovePower(args.User, action.Comp.Cost, popup: false);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<YautjaBracerComponent>();
        while (query.MoveNext(out var uid, out var bracer))
        {
            if (bracer.User == null || time < bracer.NextRegen)
                continue;

            bracer.NextRegen = time + bracer.RegenEvery;
            RegenPower((uid, bracer), GetCmss13RegenAmount(bracer, bracer.User.Value));
        }
    }

    private FixedPoint2 GetCmss13RegenAmount(YautjaBracerComponent bracer, EntityUid user)
    {
        if (IsGroundLevel(user))
            return bracer.Regen / 6f;

        if (IsMainshipLevel(user))
            return bracer.Regen / 3f;

        return bracer.Regen;
    }

    private bool IsGroundLevel(EntityUid user)
    {
        var xform = Transform(user);
        return xform.GridUid is { } grid && HasComp<RMCPlanetComponent>(grid) ||
               xform.MapUid is { } map && HasComp<RMCPlanetComponent>(map);
    }

    private bool IsMainshipLevel(EntityUid user)
    {
        return _areas.TryGetArea(user, out var area, out var areaPrototype) &&
               IsCmss13MainshipRechargeArea(area.Value.Comp.PowerNet, areaPrototype.ID);
    }

    public static bool IsCmss13MainshipRechargeArea(string? powerNet, string areaPrototypeId)
    {
        return IsPowerNet(powerNet, "almayer") ||
               IsPowerNet(powerNet, "warship") ||
               IsPowerNet(powerNet, "bush") ||
               areaPrototypeId.Contains("Almayer", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPowerNet(string? powerNet, string expected)
    {
        return string.Equals(powerNet, expected, StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetWornBracer(EntityUid user, out Entity<YautjaBracerComponent> bracer)
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

    public bool HasPowerPopup(EntityUid user, FixedPoint2 amount, bool popupOnServer = false)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (!TryGetWornBracer(user, out var bracer))
        {
            PopupNotEnoughPower(user, popupOnServer);
            return false;
        }

        if (bracer.Comp.Charge < amount)
        {
            PopupDrainPowerFailed(user, bracer.Comp, amount, popupOnServer);
            return false;
        }

        return true;
    }

    public bool HasPowerPopup(Entity<YautjaBracerComponent> bracer, EntityUid user, FixedPoint2 amount, bool popupOnServer = false)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (bracer.Comp.Charge < amount)
        {
            PopupDrainPowerFailed(user, bracer.Comp, amount, popupOnServer);
            return false;
        }

        return true;
    }

    public bool TryRemovePower(EntityUid user, FixedPoint2 amount, bool popup = true)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (!TryGetWornBracer(user, out var bracer))
        {
            if (popup)
                PopupNotEnoughPower(user, true);

            return false;
        }

        return TryDrainPower(bracer, user, amount, popup);
    }

    public bool TryDrainPower(Entity<YautjaBracerComponent> bracer, EntityUid user, FixedPoint2 amount, bool popup = true)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (bracer.Comp.Charge < amount)
        {
            if (popup)
                PopupDrainPowerFailed(user, bracer.Comp, amount, true);

            return false;
        }

        RemovePower(bracer, amount);
        return true;
    }

    private void PopupNotEnoughPower(EntityUid user, bool popupOnServer)
    {
        PopupPowerMessage(Loc.GetString("cmu-yautja-not-enough-power"), user, popupOnServer);
    }

    private void PopupDrainPowerFailed(EntityUid user, YautjaBracerComponent bracer, FixedPoint2 amount, bool popupOnServer)
    {
        PopupPowerMessage(Loc.GetString(
                "cmu-yautja-drain-power-failed",
                ("charge", (int) bracer.Charge),
                ("max", (int) bracer.MaxCharge),
                ("amount", (int) amount)),
            user,
            popupOnServer);
    }

    private void PopupPowerMessage(string message, EntityUid user, bool popupOnServer)
    {
        if (_net.IsClient || !popupOnServer)
        {
            _popup.PopupClient(message, user, user, PopupType.MediumCaution);
            return;
        }

        _popup.PopupEntity(message, user, user, PopupType.MediumCaution);
    }

    public void RemovePower(Entity<YautjaBracerComponent> bracer, FixedPoint2 amount)
    {
        var old = bracer.Comp.Charge;
        bracer.Comp.Charge = FixedPoint2.Max(FixedPoint2.Zero, bracer.Comp.Charge - amount);
        if (old == bracer.Comp.Charge)
            return;

        Dirty(bracer);
        UpdateAlert(bracer);
    }

    public void RegenPower(Entity<YautjaBracerComponent> bracer, FixedPoint2 amount)
    {
        if (bracer.Comp.Charge >= bracer.Comp.MaxCharge)
            return;

        bracer.Comp.Charge = FixedPoint2.Min(bracer.Comp.Charge + amount, bracer.Comp.MaxCharge);
        Dirty(bracer);
        UpdateAlert(bracer);
    }

    public void UpdateAlert(Entity<YautjaBracerComponent> bracer)
    {
        if (bracer.Comp.User is not { } user || bracer.Comp.MaxCharge <= FixedPoint2.Zero)
            return;

        var severity = GetCmss13PowerAlertSeverity(bracer.Comp.Charge, bracer.Comp.MaxCharge);
        _alerts.ShowAlert(user, bracer.Comp.PowerAlert, (short) severity, dynamicMessage: $"{(int) bracer.Comp.Charge} / {bracer.Comp.MaxCharge}");
    }

    public static short GetCmss13PowerAlertSeverity(FixedPoint2 charge, FixedPoint2 maxCharge)
    {
        if (maxCharge <= FixedPoint2.Zero)
            return 9;

        var percentage = charge.Double() / maxCharge.Double() * 100;
        return percentage switch
        {
            >= 91 => 0,
            >= 81 => 1,
            >= 71 => 2,
            >= 61 => 3,
            >= 51 => 4,
            >= 41 => 5,
            >= 31 => 6,
            >= 21 => 7,
            >= 11 => 8,
            _ => 9,
        };
    }

    private void ClearAlert(Entity<YautjaBracerComponent> bracer)
    {
        if (bracer.Comp.User is { } user)
            _alerts.ClearAlert(user, bracer.Comp.PowerAlert);
    }

    private void StopSelfDestructAudio(Entity<YautjaBracerComponent> bracer)
    {
        bracer.Comp.SelfDestructLaughStream = _audio.Stop(bracer.Comp.SelfDestructLaughStream);
        bracer.Comp.SelfDestructArmStream = _audio.Stop(bracer.Comp.SelfDestructArmStream);
    }

    private bool HasActiveMask(EntityUid user)
    {
        var slots = _inventory.GetSlotEnumerator(user, SlotFlags.MASK | SlotFlags.HEAD | SlotFlags.EYES);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is { } contained &&
                TryComp(contained, out YautjaMaskComponent? mask) &&
                mask.VisorEnabled)
            {
                return true;
            }
        }

        return false;
    }
}
