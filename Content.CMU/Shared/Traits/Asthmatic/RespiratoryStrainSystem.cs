using Content.Shared._RMC14.Emote;
using Content.Shared.Alert;
using Content.Shared.Armor;
using Content.Shared.Body.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Traits.Asthmatic;

public sealed partial class RespiratoryStrainSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";

    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private SharedRMCEmoteSystem _emote = default!;
    [Dependency] private SharedStutteringSystem _stutter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RespiratoryStrainComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnRefreshSpeed(Entity<RespiratoryStrainComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Peaked)
            args.ModifySpeed(ent.Comp.PeakSpeedModifier, ent.Comp.PeakSpeedModifier);
    }

    public void ClearStrain(EntityUid uid)
    {
        if (!TryComp<RespiratoryStrainComponent>(uid, out var comp))
            return;

        if (comp.Current >= comp.WarnThreshold)
            _popup.PopupEntity(Loc.GetString("au14-asthmatic-relieved"), uid, uid, PopupType.Medium);

        comp.Current = 0;
        Dirty(uid, comp);
        ProcessEffects((uid, comp));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<RespiratoryStrainComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (time < comp.NextCheck)
                continue;

            comp.NextCheck = time + comp.TimeBetweenChecks;

            var sprinting = IsSprinting(uid);
            if (sprinting)
            {
                var gain = comp.SprintGainPerTick;
                if (IsArmored(uid))
                    gain *= comp.ArmoredGainMultiplier;

                comp.Current = Math.Clamp(comp.Current + gain, 0, comp.Max);
            }
            else
            {
                var decay = comp.RegenPerTick;
                if (TryComp<InternalsComponent>(uid, out var internals) && internals.GasTankEntity != null)
                    decay *= comp.InternalsDecayMultiplier;

                comp.Current = Math.Clamp(comp.Current - decay, 0, comp.Max);
            }

            Dirty(uid, comp);
            ProcessEffects((uid, comp));
        }
    }

    private bool IsSprinting(EntityUid uid)
    {
        if (!TryComp<InputMoverComponent>(uid, out var mover) || !mover.Sprinting)
            return false;

        return TryComp<PhysicsComponent>(uid, out var physics) &&
               physics.LinearVelocity.LengthSquared() > 0.01f;
    }

    private bool IsArmored(EntityUid uid)
    {
        return _inventory.TryGetSlotEntity(uid, "outerClothing", out var suit) &&
               HasComp<ArmorComponent>(suit);
    }

    private void ProcessEffects(Entity<RespiratoryStrainComponent> ent)
    {
        var peaked = ent.Comp.Current >= ent.Comp.PeakThreshold;
        if (peaked != ent.Comp.Peaked)
        {
            ent.Comp.Peaked = peaked;
            Dirty(ent);
            _speed.RefreshMovementSpeedModifiers(ent.Owner);

            _popup.PopupEntity(
                Loc.GetString(peaked ? "au14-asthmatic-peak" : "au14-asthmatic-recovering"),
                ent.Owner, ent.Owner, peaked ? PopupType.MediumCaution : PopupType.Medium);
        }

        if (peaked)
        {
            var damage = new DamageSpecifier();
            damage.DamageDict[AsphyxiationType] = ent.Comp.AsphyxiationDamagePerTick;
            _damageable.TryChangeDamage(ent.Owner, damage, interruptsDoAfters: false);

            _stutter.DoStutter(ent.Owner, ent.Comp.EffectTime, true);
        }
        else if (ent.Comp.Current >= ent.Comp.WarnThreshold && _timing.CurTime >= ent.Comp.NextWarnMessage)
        {
            ent.Comp.NextWarnMessage = _timing.CurTime + ent.Comp.WarnMessageCooldown;
            _popup.PopupEntity(Loc.GetString("au14-asthmatic-warn"), ent.Owner, ent.Owner, PopupType.Small);
        }

        if (ent.Comp.Current >= ent.Comp.CoughThreshold && _timing.CurTime >= ent.Comp.NextCough)
        {
            ent.Comp.NextCough = _timing.CurTime + ent.Comp.CoughCooldown;
            _emote.TryEmoteWithChat(ent.Owner, ent.Comp.CoughEmote, hideLog: true, ignoreActionBlocker: true, forceEmote: true);
        }

        if (ent.Comp.Current >= ent.Comp.WarnThreshold)
            _alerts.ShowAlert(ent.Owner, ent.Comp.RespiratoryAlert, ent.Comp.Peaked ? (short)1 : (short)0);
        else
            _alerts.ClearAlert(ent.Owner, ent.Comp.RespiratoryAlert);
    }
}
