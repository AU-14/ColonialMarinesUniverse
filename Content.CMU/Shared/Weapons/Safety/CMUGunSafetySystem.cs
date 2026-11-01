using Content.Shared.CMU14.Vendors;
using System.Linq;
using Content.Shared._RMC14.Inventory;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.CombatMode;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Weapons.Safety;

/// <summary>
/// A weapon safety for every gun, switched from the right click menu. Off by default; with it on, the gun won't fire.
/// </summary>
public sealed partial class CMUGunSafetySystem : EntitySystem
{
    private static readonly SoundSpecifier ToggleSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Misc/selector.ogg");
    private static readonly string[] UnloadSlots = [SharedGunSystem.MagazineSlot, SharedGunSystem.ChamberSlot];
    private static readonly SpriteSpecifier ToggleIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/lock.svg.192dpi.png"));

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;

    private readonly List<EntityUid> _guns = new();
    private readonly List<EntityUid> _carried = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUGunSafetyComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<CMUGunSafetyComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CombatModeComponent, ExaminedEvent>(OnCarrierExamined);
        SubscribeLocalEvent<CMUVendGunsOnSafetyComponent, CMUVendorVendedItemEvent>(OnVended);
    }

    private void OnVended(Entity<CMUVendGunsOnSafetyComponent> ent, ref CMUVendorVendedItemEvent args)
    {
        if (_net.IsClient)
            return;

        // Gun cases count too: whatever guns are packed in them come out the same way.
        _guns.Clear();
        CollectGuns(args.Item, _guns);
        foreach (var gun in _guns)
        {
            if (ent.Comp.Unload)
                Unload(gun);

            SetSafety(gun, true, playSound: false);
        }
    }

    private void CollectGuns(EntityUid item, List<EntityUid> guns)
    {
        if (HasComp<GunComponent>(item))
        {
            guns.Add(item);
            return;
        }

        if (!TryComp(item, out StorageComponent? storage))
            return;

        foreach (var contained in storage.Container.ContainedEntities)
        {
            CollectGuns(contained, guns);
        }
    }

    /// <summary>
    /// Takes out and deletes the magazine, the chambered round and any rounds loaded straight into the gun.
    /// Batteries are left alone.
    /// </summary>
    public void Unload(EntityUid gun)
    {
        foreach (var slot in UnloadSlots)
        {
            if (!_container.TryGetContainer(gun, slot, out var container))
                continue;

            foreach (var contained in container.ContainedEntities.ToArray())
            {
                Del(contained);
            }
        }

        if (HasComp<BallisticAmmoProviderComponent>(gun) || HasComp<RevolverAmmoProviderComponent>(gun))
        {
            var count = new GetAmmoCountEvent();
            RaiseLocalEvent(gun, ref count);
            if (count.Count > 0)
            {
                var take = new TakeAmmoEvent(count.Count, new List<(EntityUid? Entity, IShootable Shootable)>(),
                    Transform(gun).Coordinates, null);
                RaiseLocalEvent(gun, take);
                foreach (var (round, _) in take.Ammo)
                {
                    if (round is { } spawned)
                        Del(spawned);
                }
            }
        }

        _gun.UpdateAmmoCount(gun);
    }

    public bool IsOn(EntityUid gun)
    {
        return TryComp(gun, out CMUGunSafetyComponent? safety) && safety.On;
    }

    private void OnGetVerbs(Entity<GunComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<CMUNoSafetyComponent>(ent))
            return;

        var user = args.User;
        var on = IsOn(ent);
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(on ? "cmu-gun-safety-verb-off" : "cmu-gun-safety-verb-on"),
            Icon = ToggleIcon,
            Act = () => SetSafety(ent, !on, user),
        });
    }

    public void SetSafety(EntityUid gun, bool on, EntityUid? user = null, bool playSound = true)
    {
        // Adding the component is left to the server, which sends it on to everyone.
        if (_net.IsClient || on && HasComp<CMUNoSafetyComponent>(gun))
            return;

        var safety = EnsureComp<CMUGunSafetyComponent>(gun);
        if (safety.On == on)
            return;

        safety.On = on;
        Dirty(gun, safety);

        if (playSound)
            _audio.PlayPvs(ToggleSound, gun, AudioParams.Default.WithVolume(-4));

        if (user != null)
        {
            _popup.PopupEntity(Loc.GetString(on ? "cmu-gun-safety-on" : "cmu-gun-safety-off", ("gun", gun)),
                user.Value,
                user.Value);
        }
    }

    private void OnAttemptShoot(Entity<CMUGunSafetyComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !ent.Comp.On)
            return;

        args.Cancelled = true;
        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + ent.Comp.PopupCooldown;
        _popup.PopupEntity(Loc.GetString("cmu-gun-safety-blocked", ("gun", ent.Owner)), args.User, args.User,
            PopupType.SmallCaution);
    }

    private void OnExamined(Entity<CMUGunSafetyComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.On)
            args.PushMarkup(Loc.GetString("cmu-gun-safety-examine-on"));
    }

    /// <summary>
    ///     Up close you can see whether the guns someone is holding, wearing or has holstered are loaded and on safety.
    ///     What's packed away in their bags stays hidden.
    /// </summary>
    private void OnCarrierExamined(Entity<CombatModeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        _carried.Clear();
        foreach (var held in _hands.EnumerateHeld(ent.Owner))
        {
            AddCarriedGun(held);
        }

        var slots = _inventory.GetSlotEnumerator(ent.Owner);
        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } worn)
                continue;

            AddCarriedGun(worn);
            if (!TryComp(worn, out CMHolsterComponent? holster))
                continue;

            foreach (var holstered in holster.Contents)
            {
                AddCarriedGun(holstered);
            }
        }

        if (_carried.Count == 0)
            return;

        using (args.PushGroup(nameof(CMUGunSafetySystem)))
        {
            var user = Identity.Entity(ent, EntityManager);
            foreach (var gun in _carried)
            {
                var count = new GetAmmoCountEvent();
                RaiseLocalEvent(gun, ref count);

                var loaded = count.Count > 0 ? "yes" : "no";
                var safety = HasComp<CMUNoSafetyComponent>(gun) ? "none" : IsOn(gun) ? "on" : "off";
                args.PushMarkup(Loc.GetString("cmu-gun-safety-examine-carried",
                    ("user", user),
                    ("gun", gun),
                    ("loaded", loaded),
                    ("safety", safety)));
            }
        }
    }

    private void AddCarriedGun(EntityUid item)
    {
        if (HasComp<GunComponent>(item) && !_carried.Contains(item))
            _carried.Add(item);
    }
}
