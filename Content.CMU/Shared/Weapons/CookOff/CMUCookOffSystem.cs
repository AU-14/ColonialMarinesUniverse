using System.Collections.Generic;
using Content.Shared._RMC14.Atmos;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.Mortar;
using Content.Shared._RMC14.Weapons.Ranged.Ammo.BulletBox;
using Content.Shared.CMU14.Weapons.Grenades;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Explosion.Components;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.CookOff;

/// <summary>
/// Mortar shells and ammo boxes left in a fire cook off and explode, ported from cmss13-devs/cmss13#6243.
/// Grenade boxes handle their own cook-off in <see cref="CMUGrenadeBoxSystem"/>.
/// </summary>
public sealed class CMUCookOffSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedRMCExplosionSystem _explosion = default!;
    [Dependency] private SharedCMInventorySystem _inventory = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<TagPrototype> AmmoBoxTag = "RMCAmmoBox";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ShellDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan BoxDelay = TimeSpan.FromSeconds(8);
    private static readonly SoundSpecifier CookOffSound = new SoundCollectionSpecifier("sparks");
    private static readonly EntProtoId FireEffect = "CMUCookOffFireEffect";

    // A full ammo box: a fierce but local blast. Scales down with how much ammo is left.
    private const float BoxTotalIntensity = 100;
    private const float BoxSlope = 10;
    private const float BoxMaxIntensity = 15;

    /// <summary>Ammo boxes less full than this just burn without cooking off.</summary>
    private const float BoxMinFill = 0.5f;

    private readonly HashSet<EntityUid> _onFire = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUCookingOffComponent, GettingPickedUpAttemptEvent>(OnPickupAttempt);
        SubscribeLocalEvent<CMUCookingOffComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
        SubscribeLocalEvent<CMUCookingOffComponent, ExaminedEvent>(OnExamined);
    }

    private void OnPickupAttempt(Entity<CMUCookingOffComponent> ent, ref GettingPickedUpAttemptEvent args)
    {
        args.Cancel();
        _popup.PopupClient(Loc.GetString("cmu-cook-off-touch", ("item", ent)), ent, args.User, PopupType.MediumCaution);
    }

    private void OnEjectAttempt(Entity<CMUCookingOffComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnExamined(Entity<CMUCookingOffComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-cook-off-examine"));
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var cooking = EntityQueryEnumerator<CMUCookingOffComponent>();
        while (cooking.MoveNext(out var uid, out var comp))
        {
            if (time >= comp.ExplodeAt)
                Explode((uid, comp));
        }

        if (time < _nextCheck)
            return;

        _nextCheck = time + CheckInterval;
        var fires = EntityQueryEnumerator<TileFireComponent>();
        while (fires.MoveNext(out var fire, out _))
        {
            _onFire.Clear();
            _onFire.UnionWith(_lookup.GetEntitiesInRange(_transform.GetMapCoordinates(fire), 0.45f,
                LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries));
            foreach (var uid in _onFire)
            {
                TryStartCookOff(uid, time);
            }
        }
    }

    private void TryStartCookOff(EntityUid uid, TimeSpan time)
    {
        if (HasComp<CMUCookingOffComponent>(uid) ||
            HasComp<CMUGrenadeBoxComponent>(uid) ||
            _container.IsEntityInContainer(uid) ||
            TerminatingOrDeleted(uid))
        {
            return;
        }

        if (HasComp<MortarShellComponent>(uid))
        {
            // Flare and other non-explosive shells just burn.
            if (!HasComp<ExplosiveComponent>(uid) || HasComp<ActiveMortarShellComponent>(uid))
                return;

            StartCookOff(uid, time + ShellDelay, 1, "cmu-cook-off-shell");
            return;
        }

        if (GetAmmoBoxFill(uid) is { } fill && fill >= BoxMinFill)
            StartCookOff(uid, time + BoxDelay, fill, "cmu-cook-off-box");
    }

    private float? GetAmmoBoxFill(EntityUid uid)
    {
        if (TryComp(uid, out BulletBoxComponent? bullets))
            return bullets.Max <= 0 ? 0 : (float) bullets.Amount / bullets.Max;

        if (!_tag.HasTag(uid, AmmoBoxTag) || !HasComp<CMItemSlotsComponent>(uid))
            return null;

        var (filled, total) = _inventory.GetItemSlotsFilled(uid);
        return total <= 0 ? 0 : (float) filled / total;
    }

    private void StartCookOff(EntityUid uid, TimeSpan explodeAt, float fill, string popup)
    {
        var comp = new CMUCookingOffComponent { ExplodeAt = explodeAt, Fill = fill };
        AddComp(uid, comp, true);
        Dirty(uid, comp);

        // Pin it in place so other blasts don't fling it somewhere else.
        _physics.SetBodyType(uid, BodyType.Static);

        // Same flames as a burning grenade box, stuck to the item so everyone can see it's about to go.
        SpawnAttachedTo(FireEffect, new EntityCoordinates(uid, default));

        _popup.PopupEntity(Loc.GetString(popup, ("item", uid)), uid, PopupType.LargeCaution);
        _audio.PlayPvs(CookOffSound, uid);
    }

    private void Explode(Entity<CMUCookingOffComponent> ent)
    {
        var coordinates = _transform.GetMapCoordinates(ent);
        if (HasComp<MortarShellComponent>(ent) && TryComp(ent, out ExplosiveComponent? explosive))
        {
            // Same as the shell landing: its own warhead, plus anything that hangs off the explosion like incendiary fire.
            _explosion.QueueExplosion(coordinates, explosive.ExplosionType, explosive.TotalIntensity, explosive.IntensitySlope,
                explosive.MaxIntensity, ent, explosive.TileBreakScale, explosive.MaxTileBreak, explosive.CanCreateVacuum);

            var ev = new CMExplosiveTriggeredEvent();
            RaiseLocalEvent(ent, ref ev);
        }
        else
        {
            _explosion.QueueExplosion(coordinates, "RMC", BoxTotalIntensity * ent.Comp.Fill, BoxSlope, BoxMaxIntensity, ent);
        }

        QueueDel(ent);
    }
}
