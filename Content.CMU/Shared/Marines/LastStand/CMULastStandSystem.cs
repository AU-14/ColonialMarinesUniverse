using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Actions;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Marines.LastStand;

/// <summary>
/// Smartgunner "do or die" surge: usable below half health, briefly doubles gun damage and adds
/// a flat accuracy bonus. Ends after its duration or on death; dying mid-surge shortens the
/// cooldown. Gun damage and accuracy multipliers are cached on the weapon, so held guns are
/// tagged with <see cref="CMULastStandGunComponent"/> and refreshed whenever the surge starts,
/// ends or the user's hands change.
/// </summary>
public sealed class CMULastStandSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private CMGunSystem _rmcGun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private EntityQuery<CMULastStandComponent> _lastStandQuery;
    private EntityQuery<CMULastStandActiveComponent> _activeQuery;
    private EntityQuery<GunComponent> _gunQuery;

    public override void Initialize()
    {
        _lastStandQuery = GetEntityQuery<CMULastStandComponent>();
        _activeQuery = GetEntityQuery<CMULastStandActiveComponent>();
        _gunQuery = GetEntityQuery<GunComponent>();

        SubscribeLocalEvent<CMULastStandComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CMULastStandComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CMULastStandComponent, CMULastStandActionEvent>(OnLastStandAction);

        SubscribeLocalEvent<CMULastStandActiveComponent, MobStateChangedEvent>(OnActiveMobStateChanged);
        SubscribeLocalEvent<CMULastStandActiveComponent, DidEquipHandEvent>(OnActiveDidEquipHand);
        SubscribeLocalEvent<CMULastStandActiveComponent, DidUnequipHandEvent>(OnActiveDidUnequipHand);

        SubscribeLocalEvent<CMULastStandGunComponent, GetGunDamageModifierEvent>(OnGunGetDamageModifier);
        SubscribeLocalEvent<CMULastStandGunComponent, GetWeaponAccuracyEvent>(OnGunGetWeaponAccuracy);
    }

    private void OnStartup(Entity<CMULastStandComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        _actions.AddAction(ent, ref ent.Comp.Action, ent.Comp.ActionId);
        _actions.SetUseDelay(ent.Comp.Action, GetSettings(ent).Cooldown);
        Dirty(ent);
    }

    private void OnShutdown(Entity<CMULastStandComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.Action);
    }

    private void OnLastStandAction(Entity<CMULastStandComponent> ent, ref CMULastStandActionEvent args)
    {
        if (args.Handled || _activeQuery.HasComp(ent))
            return;

        if (!IsBelowHalfHealth(ent))
        {
            _popup.PopupClient(Loc.GetString("cmu-last-stand-too-healthy"), ent, ent, PopupType.SmallCaution);
            return;
        }

        args.Handled = true;

        var active = EnsureComp<CMULastStandActiveComponent>(ent);
        active.ActivatedAt = _timing.CurTime;
        active.EndsAt = _timing.CurTime + GetSettings(ent).Duration;
        Dirty(ent, active);

        _popup.PopupClient(Loc.GetString("cmu-last-stand-activated"), ent, ent, PopupType.Medium);
        SetHeldGunMarkers(ent, enabled: true);
    }

    private void OnActiveMobStateChanged(Entity<CMULastStandActiveComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        End(ent, died: true);
    }

    private void OnActiveDidEquipHand(Entity<CMULastStandActiveComponent> ent, ref DidEquipHandEvent args)
    {
        SetGunMarker(args.Equipped, enabled: true);
    }

    private void OnActiveDidUnequipHand(Entity<CMULastStandActiveComponent> ent, ref DidUnequipHandEvent args)
    {
        SetGunMarker(args.Unequipped, enabled: false);
    }

    private void OnGunGetDamageModifier(Entity<CMULastStandGunComponent> gun, ref GetGunDamageModifierEvent args)
    {
        if (!TryGetActiveUser(gun.Owner, out var user))
            return;

        args.Multiplier *= GetSettings(user).DamageMultiplier;
    }

    private void OnGunGetWeaponAccuracy(Entity<CMULastStandGunComponent> gun, ref GetWeaponAccuracyEvent args)
    {
        if (!TryGetActiveUser(gun.Owner, out var user))
            return;

        args.AccuracyMultiplier += GetSettings(user).AccuracyBonus;
    }

    private CMULastStandPrototype GetSettings(Entity<CMULastStandComponent> user)
    {
        return _proto.Index(user.Comp.Settings);
    }

    private bool TryGetActiveUser(EntityUid gun, out Entity<CMULastStandComponent> user)
    {
        user = default;
        if (!_rmcGun.TryGetGunUser(gun, out var gunUser) ||
            !_activeQuery.HasComp(gunUser) ||
            !_lastStandQuery.TryComp(gunUser, out var lastStand))
        {
            return false;
        }

        user = (gunUser, lastStand);
        return true;
    }

    private bool IsBelowHalfHealth(EntityUid uid)
    {
        if (!TryComp(uid, out DamageableComponent? damageable) ||
            !_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var critThreshold))
        {
            return false;
        }

        return _damageable.GetTotalDamage((uid, damageable)) > critThreshold.Value / 2;
    }

    private void End(Entity<CMULastStandActiveComponent> ent, bool died)
    {
        var activatedAt = ent.Comp.ActivatedAt;
        RemComp<CMULastStandActiveComponent>(ent);

        if (_lastStandQuery.TryComp(ent, out var lastStand))
        {
            if (died)
            {
                // Dying mid-surge shortens the cooldown, still counted from activation.
                var deathCooldown = GetSettings((ent.Owner, lastStand)).DeathCooldown;
                _actions.SetCooldown(lastStand.Action, activatedAt, activatedAt + deathCooldown);
            }
            else if (_net.IsServer)
            {
                _popup.PopupEntity(Loc.GetString("cmu-last-stand-ended"), ent, ent);
            }
        }

        SetHeldGunMarkers(ent, enabled: false);
    }

    private void SetHeldGunMarkers(EntityUid user, bool enabled)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            SetGunMarker(held, enabled);
        }
    }

    // Tags or untags a held gun and refreshes its cached damage and accuracy multipliers.
    private void SetGunMarker(EntityUid gun, bool enabled)
    {
        if (!_gunQuery.TryComp(gun, out var gunComp))
            return;

        if (enabled)
            EnsureComp<CMULastStandGunComponent>(gun);
        else
            RemComp<CMULastStandGunComponent>(gun);

        _rmcGun.RefreshGunDamageMultiplier(gun);
        _gun.RefreshModifiers((gun, gunComp));
    }

    public override void Update(float frameTime)
    {
        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<CMULastStandActiveComponent>();
        while (query.MoveNext(out var uid, out var active))
        {
            if (active.EndsAt > time)
                continue;

            End((uid, active), died: false);
        }
    }
}
