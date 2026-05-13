using Content.Shared._RMC14.Actions;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Rounding;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed class YautjaPowerSystem : EntitySystem
{
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<YautjaBracerComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<YautjaBracerComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<YautjaBracerComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<YautjaPowerActionComponent, RMCActionUseAttemptEvent>(OnPowerActionAttempt);
        SubscribeLocalEvent<YautjaPowerActionComponent, RMCActionUseEvent>(OnPowerActionUse);
    }

    private void OnGetItemActions(Entity<YautjaBracerComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.InHands || args.SlotFlags == null || (args.SlotFlags.Value & ent.Comp.Slots) == 0)
            return;

        var isYautja = HasComp<YautjaComponent>(args.User);

        args.AddAction(ref ent.Comp.ToggleCloakAction, ent.Comp.ToggleCloakActionId);
        if (isYautja)
            args.AddAction(ref ent.Comp.OpenMarkPanelAction, ent.Comp.OpenMarkPanelActionId);

        args.AddAction(ref ent.Comp.RecallAction, ent.Comp.RecallActionId);
        if (isYautja)
            args.AddAction(ref ent.Comp.SelfDestructAction, ent.Comp.SelfDestructActionId);

        args.AddAction(ref ent.Comp.ToggleLockAction, ent.Comp.ToggleLockActionId);
        args.AddAction(ref ent.Comp.ToggleIdChipAction, ent.Comp.ToggleIdChipActionId);
        args.AddAction(ref ent.Comp.CreateStabilisingCrystalAction, ent.Comp.CreateStabilisingCrystalActionId);
        args.AddAction(ref ent.Comp.CreateHumanStabilisingCrystalAction, ent.Comp.CreateHumanStabilisingCrystalActionId);
        args.AddAction(ref ent.Comp.CreateHealingCapsuleAction, ent.Comp.CreateHealingCapsuleActionId);

        if (isYautja)
        {
            args.AddAction(ref ent.Comp.LinkThrallBracerAction, ent.Comp.LinkThrallBracerActionId);
            args.AddAction(ref ent.Comp.StunThrallAction, ent.Comp.StunThrallActionId);
            args.AddAction(ref ent.Comp.SelfDestructThrallAction, ent.Comp.SelfDestructThrallActionId);
        }

        if (isYautja || HasComp<YautjaThrallComponent>(args.User))
            args.AddAction(ref ent.Comp.TransmitThrallMessageAction, ent.Comp.TransmitThrallMessageActionId);
    }

    private void OnEquipped(Entity<YautjaBracerComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ent.Comp.User = args.Equipee;
        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenEvery;
        UpdateAlert(ent);
        _audio.PlayPredicted(ent.Comp.EquipSound, ent.Owner, args.Equipee);
    }

    private void OnUnequipped(Entity<YautjaBracerComponent> ent, ref GotUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        if (_net.IsClient)
            return;

        ClearAlert(ent);
        ent.Comp.User = null;

        var ev = new YautjaBracerUnequippedEvent(args.Equipee, args.SlotFlags);
        RaiseLocalEvent(ent, ref ev);
    }

    private void OnRemove(Entity<YautjaBracerComponent> ent, ref ComponentRemove args)
    {
        if (_net.IsClient)
            return;

        ClearAlert(ent);
        ent.Comp.SelfDestructArmed = false;
        ent.Comp.SelfDestructAt = TimeSpan.Zero;
        ent.Comp.NextSelfDestructWarning = TimeSpan.Zero;
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

        if (TryGetWornBracer(args.User, out var bracer))
            RemovePower(bracer, action.Comp.Cost);
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
            RegenPower((uid, bracer), bracer.Regen);
        }
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

    public bool HasPowerPopup(EntityUid user, FixedPoint2 amount)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (!TryGetWornBracer(user, out var bracer) || bracer.Comp.Charge < amount)
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-not-enough-power"), user, user, PopupType.MediumCaution);
            return false;
        }

        return true;
    }

    public bool TryRemovePower(EntityUid user, FixedPoint2 amount)
    {
        if (amount == FixedPoint2.Zero)
            return true;

        if (!TryGetWornBracer(user, out var bracer) || bracer.Comp.Charge < amount)
            return false;

        RemovePower(bracer, amount);
        return true;
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

        var level = MathF.Max(0f, bracer.Comp.Charge.Float());
        var max = _alerts.GetMaxSeverity(bracer.Comp.PowerAlert);
        var severity = max - ContentHelpers.RoundToLevels(level, bracer.Comp.MaxCharge.Double(), max + 1);
        _alerts.ShowAlert(user, bracer.Comp.PowerAlert, (short) severity, dynamicMessage: $"{(int) bracer.Comp.Charge} / {bracer.Comp.MaxCharge}");
    }

    private void ClearAlert(Entity<YautjaBracerComponent> bracer)
    {
        if (bracer.Comp.User is { } user)
            _alerts.ClearAlert(user, bracer.Comp.PowerAlert);
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
