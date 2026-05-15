using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Chemistry;
using Content.Shared._RMC14.NightVision;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs;
using Content.Shared._RMC14.Stealth;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared._RMC14.Xenonids.Devour;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Yautja;

public sealed class YautjaCloakSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedRMCActionsSystem _rmcActions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly YautjaPowerSystem _power = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaBracerComponent, YautjaToggleCloakActionEvent>(OnToggleCloak);
        SubscribeLocalEvent<YautjaBracerComponent, YautjaBracerUnequippedEvent>(OnBracerUnequipped);

        SubscribeLocalEvent<YautjaComponent, VaporHitEvent>(OnVaporHit);
        SubscribeLocalEvent<YautjaComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<YautjaComponent, XenoDevouredEvent>(OnDevour);
        SubscribeLocalEvent<YautjaComponent, XenoParasiteInfectEvent>(OnParasiteInfect);
        SubscribeLocalEvent<YautjaComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<ProjectileComponent, ProjectileHitEvent>(OnProjectileHit);
    }

    private void OnToggleCloak(Entity<YautjaBracerComponent> ent, ref YautjaToggleCloakActionEvent args)
    {
        if (args.Handled)
            return;

        if (!_rmcActions.TryUseAction(args))
            return;

        if (!_inventory.InSlotWithFlags((ent, null, null), ent.Comp.Slots))
            return;

        args.Handled = true;
        TryToggleCloak(args.Performer, ent);
    }

    private bool TryToggleCloak(EntityUid user, Entity<YautjaBracerComponent>? bracerEnt = null)
    {
        if (!CanUseYautjaCloak(user))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-tech-denied"), user, user, PopupType.SmallCaution);
            return false;
        }

        Entity<YautjaBracerComponent> bracer;
        if (bracerEnt is { } provided)
        {
            bracer = provided;
        }
        else if (!_power.TryGetWornBracer(user, out bracer))
        {
            _popup.PopupClient(Loc.GetString("cmu-yautja-not-enough-power"), user, user, PopupType.MediumCaution);
            return false;
        }

        var enabling = !HasComp<EntityActiveInvisibleComponent>(user);
        if (enabling && !_power.HasPowerPopup(user, 25))
            return false;

        if (TrySetInvisibility(bracer, user, enabling, false) && enabling)
            _power.TryRemovePower(user, 25);

        _actions.SetToggled(bracer.Comp.ToggleCloakAction, enabling);
        return true;
    }

    private void OnBracerUnequipped(Entity<YautjaBracerComponent> ent, ref YautjaBracerUnequippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        TrySetInvisibility(ent, args.User, false, true);
        RemCompDeferred<EntityTurnInvisibleComponent>(args.User);
        _actions.SetToggled(ent.Comp.ToggleCloakAction, false);
    }

    private bool TrySetInvisibility(Entity<YautjaBracerComponent> bracer, EntityUid user, bool enabling, bool forced)
    {
        if (Deleted(user) || Terminating(user))
            return false;

        var turnInvisible = EnsureComp<EntityTurnInvisibleComponent>(user);
        turnInvisible.RestrictWeapons = bracer.Comp.CloakRestrictWeapons;
        turnInvisible.UncloakWeaponLock = bracer.Comp.CloakUncloakWeaponLock;

        if (enabling && !HasComp<EntityActiveInvisibleComponent>(user))
        {
            var activeInvisibility = EnsureComp<EntityActiveInvisibleComponent>(user);
            activeInvisibility.Opacity = bracer.Comp.CloakOpacity;
            Dirty(user, activeInvisibility);

            turnInvisible.Enabled = true;
            turnInvisible.UncloakTime = _timing.CurTime;
            Dirty(user, turnInvisible);

            if (bracer.Comp.CloakHideNightVision)
                RemCompDeferred<RMCNightVisionVisibleComponent>(user);

            if (bracer.Comp.CloakBlockFriendlyFire)
                EnsureComp<EntityIFFComponent>(user);

            ToggleLayers(user, bracer.Comp.CloakedHideLayers, false);
            SpawnCloakEffects(user, bracer.Comp.CloakEffect);

            var popupOthers = Loc.GetString("rmc-cloak-activate-others", ("user", YautjaDisplayName(user)));
            _popup.PopupPredicted(Loc.GetString("rmc-cloak-activate-self"), popupOthers, user, user, PopupType.Medium);

            if (_net.IsServer)
                _audio.PlayPvs(bracer.Comp.CloakOnSound, user);

            return true;
        }

        if (!enabling && TryComp<EntityActiveInvisibleComponent>(user, out var invisible))
        {
            invisible.Opacity = 1;
            Dirty(user, invisible);

            turnInvisible.Enabled = false;
            turnInvisible.UncloakTime = _timing.CurTime;
            Dirty(user, turnInvisible);

            var selfPopup = forced
                ? Loc.GetString("rmc-cloak-forced-deactivate-self")
                : Loc.GetString("rmc-cloak-deactivate-self");
            var otherPopup = forced
                ? Loc.GetString("rmc-cloak-forced-deactivate-others", ("user", YautjaDisplayName(user)))
                : Loc.GetString("rmc-cloak-deactivate-others", ("user", YautjaDisplayName(user)));
            _popup.PopupPredicted(selfPopup, otherPopup, user, user, PopupType.Medium);

            ToggleLayers(user, bracer.Comp.CloakedHideLayers, true);
            SpawnCloakEffects(user, bracer.Comp.UncloakEffect);

            if (bracer.Comp.CloakHideNightVision)
                EnsureComp<RMCNightVisionVisibleComponent>(user);

            if (bracer.Comp.CloakBlockFriendlyFire)
                RemCompDeferred<EntityIFFComponent>(user);

            RemCompDeferred<EntityActiveInvisibleComponent>(user);

            if (_net.IsServer)
                _audio.PlayPvs(bracer.Comp.CloakOffSound, user);

            return true;
        }

        return false;
    }

    public void ForceDecloak(EntityUid user)
    {
        if (!HasComp<EntityActiveInvisibleComponent>(user) ||
            !_power.TryGetWornBracer(user, out var bracer))
        {
            return;
        }

        TrySetInvisibility(bracer, user, false, true);
        _actions.SetToggled(bracer.Comp.ToggleCloakAction, false);
    }

    private void OnProjectileHit(Entity<ProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        if (_net.IsClient ||
            !HasComp<EntityActiveInvisibleComponent>(args.Target) ||
            !_power.TryGetWornBracer(args.Target, out var bracer))
        {
            return;
        }

        if (IsForcedDecloakProjectile(ent.Owner, args.Damage))
        {
            ForceDecloak(args.Target);
            return;
        }

        if (!_random.Prob(bracer.Comp.BulletDecloakChance))
            return;

        ForceDecloak(args.Target);
        if (bracer.Comp.BulletDecloakAbsorbs)
            args.Damage = new DamageSpecifier();
    }

    private void OnVaporHit(Entity<YautjaComponent> ent, ref VaporHitEvent args)
    {
        ForceDecloak(ent.Owner);
    }

    private void OnMobStateChanged(Entity<YautjaComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        ForceDecloak(ent.Owner);
    }

    private void OnDevour(Entity<YautjaComponent> ent, ref XenoDevouredEvent args)
    {
        ForceDecloak(ent.Owner);
    }

    private void OnParasiteInfect(Entity<YautjaComponent> ent, ref XenoParasiteInfectEvent args)
    {
        ForceDecloak(ent.Owner);
    }

    private void OnDamageChanged(Entity<YautjaComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta?.AnyPositive() != true)
            return;

        ForceDecloak(ent.Owner);
    }

    private void ToggleLayers(EntityUid user, HashSet<HumanoidVisualLayers> layers, bool showLayers)
    {
        foreach (var layer in layers)
        {
            _humanoid.SetLayerVisibility(user, layer, showLayers);
        }
    }

    private void SpawnCloakEffects(EntityUid user, EntProtoId effect)
    {
        if (_net.IsClient)
            return;

        var coordinates = _transform.GetMapCoordinates(user);
        var rotation = _transform.GetWorldRotation(user);
        Spawn(effect, coordinates, rotation: rotation);
    }

    private bool IsForcedDecloakProjectile(EntityUid projectile, DamageSpecifier damage)
    {
        if (damage.DamageDict.ContainsKey("Heat") ||
            damage.DamageDict.ContainsKey("Shock") ||
            damage.DamageDict.ContainsKey("Caustic"))
        {
            return true;
        }

        var id = MetaData(projectile).EntityPrototype?.ID ?? string.Empty;
        return id.Contains("rocket", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("grenade", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("plasma", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("energy", StringComparison.OrdinalIgnoreCase) ||
               id.Contains("acid", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanUseYautjaCloak(EntityUid user)
    {
        return HasComp<YautjaComponent>(user) ||
               (TryComp(user, out YautjaThrallComponent? thrall) && thrall.Blooded && thrall.TechAuthorized);
    }

    private string YautjaDisplayName(EntityUid uid)
    {
        return HasComp<YautjaComponent>(uid)
            ? Loc.GetString("cmu-yautja-identity-unknown")
            : Name(uid);
    }
}
