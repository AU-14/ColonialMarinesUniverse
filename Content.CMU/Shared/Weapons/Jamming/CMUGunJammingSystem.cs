using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Random;
using Content.Shared._RMC14.Weapons.Common;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared._RMC14.Weapons.Ranged.Ammo;
using Content.Shared._RMC14.Weapons.Ranged.Chamber;
using Content.Shared._RMC14.Weapons.Ranged.Foldable;
using Content.Shared.CMU14.Weapons.Safety;
using Content.Shared.CMU14.Weapons.SerialNumber;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Weapons.Jamming;

/// <summary>
///     Guns wear down as they fire and jam more often the more worn they are. A jam is cleared by cycling the action
///     (unique action or the Clear jam verb) or by slamming the stock into the ground, which is quicker but wears the
///     gun, can set off a round and can destroy the gun outright. Gun oil and rags remove wear.
/// </summary>
/// <remarks>
///     Wear and jams are rolled on both client and server with a tick-seeded random, so firing stays predicted.
///     Clearing a jam is rolled on the server only.
/// </remarks>
public sealed partial class CMUGunJammingSystem : EntitySystem
{
    public static readonly ProtoId<CMUGunJammingPrototype> DefaultProfile = "CMUGunJammingDefault";

    private static readonly CMUGunJamType[] JamTypeOrder =
    [
        CMUGunJamType.FailureToFeed,
        CMUGunJamType.FailureToEject,
        CMUGunJamType.FailureToChamber,
        CMUGunJamType.FailureToFire,
    ];

    private const long JamSalt = 0x4A414D;
    private const long CycleSalt = 0x435943;
    private const long SlamSalt = 0x534C4D;

    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private readonly Dictionary<(string Profile, string? Cartridge, string? Projectile), float> _ammoMultipliers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, CMUGunMapInitEvent>(OnGunMapInit);

        SubscribeLocalEvent<CMUGunJammingComponent, AttemptShootEvent>(OnAttemptShoot,
            after: [typeof(CMUGunSafetySystem)]);
        SubscribeLocalEvent<CMUGunJammingComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<CMUGunJammingComponent, UniqueActionEvent>(OnUniqueAction,
            after: [typeof(AttachableHolderSystem)],
            before:
            [
                typeof(RMCGunChamberSystem), typeof(BreechLoadedSystem), typeof(CMGunSystem),
                typeof(RMCAirShotSystem), typeof(RMCFoldableGunSystem), typeof(GunToggleableAmmoSystem),
            ]);
        SubscribeLocalEvent<CMUGunJammingComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CMUGunJammingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CMUGunJammingComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CMUGunJammingComponent, CMUGunCycleJamDoAfterEvent>(OnCycleDoAfter);
        SubscribeLocalEvent<CMUGunJammingComponent, CMUGunSlamStockDoAfterEvent>(OnSlamDoAfter);
        SubscribeLocalEvent<CMUGunJammingComponent, CMUGunMaintenanceDoAfterEvent>(OnMaintenanceDoAfter);

        SubscribeLocalEvent<CMUGunMaintenanceToolComponent, ExaminedEvent>(OnToolExamined);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>() || args.WasModified<CMUGunJammingPrototype>())
            _ammoMultipliers.Clear();
    }

    private void OnGunMapInit(Entity<ItemComponent> ent, ref CMUGunMapInitEvent args)
    {
        if (_net.IsClient || HasComp<CMUGunJammingComponent>(ent))
            return;

        if (!_proto.TryIndex(DefaultProfile, out var profile) || !profile.Enabled || !profile.AutoApply)
            return;

        if (!_whitelist.CheckBoth(ent, profile.AutoApplyBlacklist, profile.AutoApplyWhitelist))
            return;

        AddComp<CMUGunJammingComponent>(ent);
    }

    private void OnAttemptShoot(Entity<CMUGunJammingComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled || !TryGetActiveProfile(ent, out var profile))
            return;

        string message;
        if (ent.Comp.Broken)
        {
            message = Loc.GetString("cmu-gun-jam-broken-blocked", ("gun", ent.Owner));
        }
        else if (ent.Comp.Jam != CMUGunJamType.None)
        {
            message = Loc.GetString("cmu-gun-jam-blocked", ("gun", ent.Owner), ("jam", GetJamName(profile, ent.Comp.Jam)));
        }
        else
        {
            return;
        }

        args.Cancelled = true;
        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + profile.PopupCooldown;
        _audio.PlayPredicted(profile.JammedSound ?? CompOrNull<GunComponent>(ent)?.SoundEmpty, ent, args.User);
        _popup.PopupEntity(message, args.User, args.User, PopupType.SmallCaution);
    }

    private void OnGunShot(Entity<CMUGunJammingComponent> ent, ref GunShotEvent args)
    {
        if (args.Ammo.Count == 0 || ent.Comp.Broken || !TryGetActiveProfile(ent, out var profile))
            return;

        var multiplier = ent.Comp.WearMultiplier * GetFireModeWearMultiplier(ent, profile);
        if (IsSemiAutoShotgun(ent, profile))
            multiplier *= profile.ShotgunSemiAutoWearMultiplier;

        var wear = 0f;
        foreach (var (uid, shootable) in args.Ammo)
        {
            wear += profile.BaseWearPerShot * GetAmmoWearMultiplier(profile, uid, shootable);
        }

        AddWear(ent, profile, wear * multiplier);

        if (ent.Comp.Jam == CMUGunJamType.None)
        {
            var chance = GetJamChance(ent.Comp, profile);
            if (chance > 0)
            {
                var random = GetRandom(ent, JamSalt);
                var chanceThisShot = 1f - MathF.Pow(1f - chance, args.Ammo.Count);
                if (random.NextFloat() < chanceThisShot)
                    ent.Comp.Jam = PickJamType(profile, ref random);
            }
        }

        Dirty(ent);
    }

    private void OnUniqueAction(Entity<CMUGunJammingComponent> ent, ref UniqueActionEvent args)
    {
        if (args.Handled || !TryGetActiveProfile(ent, out _))
            return;

        if (ent.Comp.Broken)
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("cmu-gun-jam-broken-blocked", ("gun", ent.Owner)), args.UserUid,
                args.UserUid, PopupType.SmallCaution);
            return;
        }

        if (ent.Comp.Jam == CMUGunJamType.None)
            return;

        args.Handled = true;
        TryStartCycle(ent, args.UserUid);
    }

    private void OnGetVerbs(Entity<CMUGunJammingComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || ent.Comp.Broken || ent.Comp.Jam == CMUGunJamType.None)
            return;

        if (!TryGetActiveProfile(ent, out var profile) || !_hands.IsHolding(args.User, ent))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-gun-jam-verb-cycle"),
            Act = () => TryStartCycle(ent, user),
            Priority = 2,
        });

        if (!profile.SlamEnabled || !ent.Comp.CanSlam)
            return;

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-gun-jam-verb-slam"),
            Message = Loc.GetString("cmu-gun-jam-verb-slam-message"),
            Act = () => TryStartSlam(ent, user),
            Priority = 1,
        });
    }

    private void OnExamined(Entity<CMUGunJammingComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || !TryGetActiveProfile(ent, out var profile))
            return;

        using (args.PushGroup(nameof(CMUGunJammingComponent)))
        {
            if (ent.Comp.Broken)
            {
                args.PushMarkup(Loc.GetString("cmu-gun-jam-examine-broken"));
                return;
            }

            if (GetConditionTier(ent.Comp, profile) is { } tier)
                args.PushMarkup(Loc.GetString(tier.Text));

            if (ent.Comp.Jam != CMUGunJamType.None)
            {
                args.PushMarkup(Loc.GetString("cmu-gun-jam-examine-jammed",
                    ("jam", GetJamName(profile, ent.Comp.Jam)),
                    ("slam", profile.SlamEnabled && ent.Comp.CanSlam)));
            }
        }
    }

    private void OnToolExamined(Entity<CMUGunMaintenanceToolComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(CMUGunMaintenanceToolComponent)))
        {
            args.PushMarkup(Loc.GetString("cmu-gun-maintenance-examine"));
            if (ent.Comp.Uses is { } uses)
                args.PushMarkup(Loc.GetString("cmu-gun-maintenance-examine-uses", ("uses", uses)));
        }
    }

    private void OnInteractUsing(Entity<CMUGunJammingComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out CMUGunMaintenanceToolComponent? tool))
            return;

        if (!TryGetActiveProfile(ent, out _))
            return;

        args.Handled = true;
        TryStartMaintenance(ent, (args.Used, tool), args.User);
    }

    public bool TryStartCycle(Entity<CMUGunJammingComponent> ent, EntityUid user)
    {
        if (ent.Comp.Broken || ent.Comp.Jam == CMUGunJamType.None || !TryGetActiveProfile(ent, out var profile))
            return false;

        if (!_hands.IsHolding(user, ent))
            return false;

        var delay = GetJamData(profile, ent.Comp.Jam)?.CycleDelay ?? TimeSpan.FromSeconds(2);
        var doAfter = new DoAfterArgs(EntityManager, user, delay, new CMUGunCycleJamDoAfterEvent(), ent, ent)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-gun-jam-cycle-start", ("gun", ent.Owner),
                ("jam", GetJamName(profile, ent.Comp.Jam))),
            user,
            user);
        return true;
    }

    public bool TryStartSlam(Entity<CMUGunJammingComponent> ent, EntityUid user)
    {
        if (ent.Comp.Broken || ent.Comp.Jam == CMUGunJamType.None || !ent.Comp.CanSlam)
            return false;

        if (!TryGetActiveProfile(ent, out var profile) || !profile.SlamEnabled || !_hands.IsHolding(user, ent))
            return false;

        var doAfter = new DoAfterArgs(EntityManager, user, profile.SlamDelay, new CMUGunSlamStockDoAfterEvent(), ent, ent)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-gun-jam-slam-start", ("gun", ent.Owner)), user, user);
        return true;
    }

    public bool TryStartMaintenance(Entity<CMUGunJammingComponent> ent, Entity<CMUGunMaintenanceToolComponent> tool,
        EntityUid user)
    {
        if (!TryGetActiveProfile(ent, out var profile))
            return false;

        if (ent.Comp.Broken)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-broken", ("gun", ent.Owner)), user, user);
            return false;
        }

        if (tool.Comp.Uses is <= 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-empty", ("tool", tool.Owner)), user, user);
            return false;
        }

        var clearsJam = tool.Comp.ClearsJam && ent.Comp.Jam != CMUGunJamType.None;
        if (!clearsJam && ent.Comp.Wear <= GetMaintenanceFloor(tool.Comp, profile) + 0.001f)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-clean", ("gun", ent.Owner), ("tool", tool.Owner)),
                user,
                user);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, tool.Comp.Delay, new CMUGunMaintenanceDoAfterEvent(), ent,
            ent, tool)
        {
            NeedHand = true,
            BreakOnHandChange = true,
            BreakOnDropItem = true,
            BreakOnMove = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-start", ("gun", ent.Owner), ("tool", tool.Owner)),
            user,
            user);
        return true;
    }

    private void OnCycleDoAfter(Entity<CMUGunJammingComponent> ent, ref CMUGunCycleJamDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (ent.Comp.Broken || ent.Comp.Jam == CMUGunJamType.None || !TryGetActiveProfile(ent, out var profile))
            return;

        var user = args.User;
        var jamName = GetJamName(profile, ent.Comp.Jam);
        var data = GetJamData(profile, ent.Comp.Jam);
        _audio.PlayPvs(profile.CycleSound, ent);

        var random = GetRandom(ent, CycleSalt);
        if (data != null && random.NextFloat() >= data.CycleClearChance)
        {
            _popup.PopupEntity(Loc.GetString("cmu-gun-jam-cycle-fail", ("gun", ent.Owner), ("jam", jamName)), user,
                user, PopupType.SmallCaution);
            return;
        }

        ClearJam(ent.AsNullable());
        if (data != null)
            LoseRound(ent, user, data.CycleRoundLoss);

        _popup.PopupEntity(Loc.GetString("cmu-gun-jam-cycle-success", ("gun", ent.Owner), ("jam", jamName)), user,
            user);
    }

    private void OnSlamDoAfter(Entity<CMUGunJammingComponent> ent, ref CMUGunSlamStockDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (_net.IsClient)
            return;

        if (ent.Comp.Broken || ent.Comp.Jam == CMUGunJamType.None || !ent.Comp.CanSlam)
            return;

        if (!TryGetActiveProfile(ent, out var profile) || !profile.SlamEnabled)
            return;

        var user = args.User;
        var data = GetJamData(profile, ent.Comp.Jam);
        var userIdentity = Identity.Entity(user, EntityManager);

        AddWear(ent, profile, profile.SlamWear * ent.Comp.WearMultiplier);
        _audio.PlayPvs(profile.SlamSound, ent);
        _popup.PopupEntity(Loc.GetString("cmu-gun-jam-slam-self", ("gun", ent.Owner)),
            Loc.GetString("cmu-gun-jam-slam-others", ("user", userIdentity), ("gun", ent.Owner)),
            user,
            user);

        var random = GetRandom(ent, SlamSalt);
        if (random.NextFloat() < GetSlamBreakChance(ent.Comp, profile, data))
        {
            ent.Comp.Broken = true;
            ent.Comp.Jam = CMUGunJamType.None;
            Dirty(ent);

            _audio.PlayPvs(profile.BreakSound, ent);
            _popup.PopupEntity(Loc.GetString("cmu-gun-jam-break-self", ("gun", ent.Owner)),
                Loc.GetString("cmu-gun-jam-break-others", ("user", userIdentity), ("gun", ent.Owner)),
                user,
                user,
                PopupType.LargeCaution);
            return;
        }

        if (data != null && random.NextFloat() < data.SlamDischargeChance && TryDischarge(ent, user))
        {
            ClearJam(ent.AsNullable());
            _popup.PopupEntity(Loc.GetString("cmu-gun-jam-discharge-self", ("gun", ent.Owner)),
                Loc.GetString("cmu-gun-jam-discharge-others", ("user", userIdentity), ("gun", ent.Owner)),
                user,
                user,
                PopupType.LargeCaution);
            return;
        }

        if (data == null || random.NextFloat() < data.SlamClearChance)
        {
            ClearJam(ent.AsNullable());
            _popup.PopupEntity(Loc.GetString("cmu-gun-jam-slam-success", ("gun", ent.Owner)), user, user);
            return;
        }

        Dirty(ent);
        _popup.PopupEntity(Loc.GetString("cmu-gun-jam-slam-fail", ("gun", ent.Owner)), user, user,
            PopupType.SmallCaution);
    }

    private void OnMaintenanceDoAfter(Entity<CMUGunJammingComponent> ent, ref CMUGunMaintenanceDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Used is not { } used)
            return;

        if (!TryComp(used, out CMUGunMaintenanceToolComponent? tool))
            return;

        args.Handled = true;
        if (ent.Comp.Broken || tool.Uses is <= 0 || !TryGetActiveProfile(ent, out var profile))
            return;

        var floor = GetMaintenanceFloor(tool, profile);
        if (ent.Comp.Wear > floor)
            ent.Comp.Wear = Math.Max(floor, ent.Comp.Wear - Math.Max(tool.WearRemoved, 0f) * profile.MaxWear);

        if (tool.ClearsJam)
            ent.Comp.Jam = CMUGunJamType.None;

        Dirty(ent);

        var user = args.User;
        _audio.PlayPredicted(tool.Sound, ent, user);
        _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-done", ("gun", ent.Owner), ("tool", used)), user, user);

        if (tool.Uses == null)
            return;

        tool.Uses--;
        Dirty(used, tool);
        if (tool.Uses > 0 || !tool.DeleteWhenEmpty)
            return;

        _popup.PopupEntity(Loc.GetString("cmu-gun-maintenance-used-up", ("tool", used)), user, user);
        QueueDel(used);
    }

    /// <summary>
    ///     Fires one round from the gun into the air without a projectile.
    /// </summary>
    private bool TryDischarge(EntityUid gun, EntityUid user)
    {
        if (!TryComp(gun, out GunComponent? gunComp) || GetAmmoCount(gun) <= 0)
            return false;

        var take = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(),
            Transform(user).Coordinates, user);
        RaiseLocalEvent(gun, take);
        if (take.Ammo.Count == 0)
            return false;

        foreach (var (round, _) in take.Ammo)
        {
            if (round != null)
                Del(round.Value);
        }

        if (!_zLevels.PlayPredictedDirectlyAcrossZ(gunComp.SoundGunshotModified, gun, null))
            _audio.PlayPvs(gunComp.SoundGunshotModified, gun);

        _gun.UpdateAmmoCount(gun);
        return true;
    }

    private void LoseRound(EntityUid gun, EntityUid user, CMUGunJamRoundLoss loss)
    {
        if (loss == CMUGunJamRoundLoss.None || _net.IsClient || GetAmmoCount(gun) <= 0)
            return;

        var take = new TakeAmmoEvent(1, new List<(EntityUid? Entity, IShootable Shootable)>(),
            Transform(user).Coordinates, user);
        RaiseLocalEvent(gun, take);

        foreach (var (round, _) in take.Ammo)
        {
            if (round is not { } roundUid)
                continue;

            // Revolvers hand back the projectile rather than a cartridge, which shouldn't land on the floor.
            if (loss == CMUGunJamRoundLoss.Eject && HasComp<CartridgeAmmoComponent>(roundUid))
                _transform.DropNextTo(roundUid, user);
            else
                Del(roundUid);
        }

        _gun.UpdateAmmoCount(gun);
    }

    private int GetAmmoCount(EntityUid gun)
    {
        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(gun, ref ev);
        return ev.Count;
    }

    public bool IsJammed(Entity<CMUGunJammingComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Jam != CMUGunJamType.None;
    }

    public bool IsBroken(Entity<CMUGunJammingComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Broken;
    }

    public void SetWear(Entity<CMUGunJammingComponent?> ent, float wear)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var max = _proto.TryIndex(ent.Comp.Profile, out var profile) ? Math.Max(profile.MaxWear, 0f) : float.MaxValue;
        ent.Comp.Wear = Math.Clamp(wear, 0f, max);
        Dirty(ent);
    }

    public void SetJam(Entity<CMUGunJammingComponent?> ent, CMUGunJamType jam)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Jam == jam)
            return;

        ent.Comp.Jam = jam;
        Dirty(ent);
    }

    public void SetBroken(Entity<CMUGunJammingComponent?> ent, bool broken)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Broken == broken)
            return;

        ent.Comp.Broken = broken;
        if (broken)
            ent.Comp.Jam = CMUGunJamType.None;

        Dirty(ent);
    }

    public void ClearJam(Entity<CMUGunJammingComponent?> ent)
    {
        SetJam(ent, CMUGunJamType.None);
    }

    /// <summary>
    ///     The gun's current chance to jam on each round fired.
    /// </summary>
    public float GetJamChance(Entity<CMUGunJammingComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || !_proto.TryIndex(ent.Comp.Profile, out var profile))
            return 0f;

        return GetJamChance(ent.Comp, profile);
    }

    private static float GetJamChance(CMUGunJammingComponent comp, CMUGunJammingPrototype profile)
    {
        var fraction = GetWearFraction(comp, profile);
        var threshold = Math.Clamp(profile.JamWearThreshold, 0f, 1f);
        if (fraction < threshold)
            return 0f;

        var t = threshold >= 1f ? 1f : (fraction - threshold) / (1f - threshold);
        var curve = MathF.Pow(t, Math.Max(profile.JamChanceExponent, 0.01f));
        var chance = profile.MinJamChance + (profile.MaxJamChance - profile.MinJamChance) * curve;
        return Math.Clamp(chance * comp.JamChanceMultiplier, 0f, 1f);
    }

    private static float GetSlamBreakChance(CMUGunJammingComponent comp,
        CMUGunJammingPrototype profile,
        CMUGunJamTypeData? data)
    {
        var curve = MathF.Pow(GetWearFraction(comp, profile), Math.Max(profile.SlamBreakWearExponent, 0.01f));
        var chance = profile.SlamBreakChance + (profile.SlamBreakChanceAtMaxWear - profile.SlamBreakChance) * curve;
        chance *= (data?.SlamBreakChanceMultiplier ?? 1f) * comp.SlamBreakChanceMultiplier;
        return Math.Clamp(chance, 0f, 1f);
    }

    private static float GetWearFraction(CMUGunJammingComponent comp, CMUGunJammingPrototype profile)
    {
        return profile.MaxWear <= 0f ? 0f : Math.Clamp(comp.Wear / profile.MaxWear, 0f, 1f);
    }

    private static float GetMaintenanceFloor(CMUGunMaintenanceToolComponent tool, CMUGunJammingPrototype profile)
    {
        return Math.Clamp(tool.MinWear, 0f, 1f) * Math.Max(profile.MaxWear, 0f);
    }

    private static CMUGunWearTier? GetConditionTier(CMUGunJammingComponent comp, CMUGunJammingPrototype profile)
    {
        var fraction = GetWearFraction(comp, profile);
        CMUGunWearTier? best = null;
        foreach (var tier in profile.ConditionTiers)
        {
            if (tier.Threshold <= fraction && (best == null || tier.Threshold > best.Threshold))
                best = tier;
        }

        return best;
    }

    private static void AddWear(Entity<CMUGunJammingComponent> ent, CMUGunJammingPrototype profile, float amount)
    {
        ent.Comp.Wear = Math.Clamp(ent.Comp.Wear + amount, 0f, Math.Max(profile.MaxWear, 0f));
    }

    private float GetFireModeWearMultiplier(EntityUid gun, CMUGunJammingPrototype profile)
    {
        if (!TryComp(gun, out GunComponent? gunComp))
            return profile.SemiAutoWearMultiplier;

        var mode = gunComp.BurstActivated ? SelectiveFire.Burst : gunComp.SelectedMode;
        return mode switch
        {
            SelectiveFire.FullAuto => profile.FullAutoWearMultiplier,
            SelectiveFire.Burst => profile.BurstWearMultiplier,
            _ => profile.SemiAutoWearMultiplier,
        };
    }

    private bool IsSemiAutoShotgun(EntityUid gun, CMUGunJammingPrototype profile)
    {
        if (profile.ShotgunTags.Count == 0 || !_tag.HasAnyTag(gun, profile.ShotgunTags))
            return false;

        return !TryComp(gun, out PumpActionComponent? pump) || pump.Once;
    }

    private float GetAmmoWearMultiplier(CMUGunJammingPrototype profile, EntityUid? uid, IShootable shootable)
    {
        string? cartridge = null;
        string? projectile;
        switch (shootable)
        {
            case CartridgeAmmoComponent cartridgeAmmo:
                if (uid != null && TryComp(uid.Value, out MetaDataComponent? meta))
                    cartridge = meta.EntityPrototype?.ID;

                projectile = cartridgeAmmo.Prototype.Id;
                break;
            case AmmoComponent:
                projectile = uid != null && TryComp(uid.Value, out MetaDataComponent? projectileMeta)
                    ? projectileMeta.EntityPrototype?.ID
                    : null;
                break;
            default:
                return 1f;
        }

        var key = (profile.ID, cartridge, projectile);
        if (_ammoMultipliers.TryGetValue(key, out var cached))
            return cached;

        var multiplier = ResolveAmmoWearMultiplier(profile, cartridge, projectile);
        _ammoMultipliers[key] = multiplier;
        return multiplier;
    }

    private float ResolveAmmoWearMultiplier(CMUGunJammingPrototype profile, string? cartridge, string? projectile)
    {
        var cartridgeProto = IndexEntity(cartridge);
        var projectileProto = IndexEntity(projectile);

        CMUGunWearAmmoComponent? explicitWear = null;
        if (cartridgeProto?.TryComp(out explicitWear, EntityManager.ComponentFactory) == true ||
            projectileProto?.TryComp(out explicitWear, EntityManager.ComponentFactory) == true)
        {
            return Math.Max(explicitWear!.Multiplier, 0f);
        }

        var best = Max(MatchAmmo(profile, cartridge, cartridgeProto), MatchAmmo(profile, projectile, projectileProto));
        return Math.Max(best ?? 1f, 0f);
    }

    private float? MatchAmmo(CMUGunJammingPrototype profile, string? id, EntityPrototype? proto)
    {
        if (id == null)
            return null;

        float? best = null;
        foreach (var (keyword, multiplier) in profile.AmmoIdKeywords)
        {
            if (keyword.Length > 0 && id.Contains(keyword, StringComparison.Ordinal))
                best = Max(best, multiplier);
        }

        if (profile.AmmoTagMultipliers.Count == 0 ||
            proto == null ||
            !proto.TryComp(out TagComponent? tags, EntityManager.ComponentFactory))
        {
            return best;
        }

        foreach (var tag in tags.Tags)
        {
            if (profile.AmmoTagMultipliers.TryGetValue(tag, out var multiplier))
                best = Max(best, multiplier);
        }

        return best;
    }

    private EntityPrototype? IndexEntity(string? id)
    {
        return id != null && _proto.TryIndex<EntityPrototype>(id, out var proto) ? proto : null;
    }

    private static float? Max(float? a, float? b)
    {
        if (a == null)
            return b;

        return b == null ? a : Math.Max(a.Value, b.Value);
    }

    private static CMUGunJamType PickJamType(CMUGunJammingPrototype profile, ref Xoroshiro64S random)
    {
        var total = 0f;
        foreach (var type in JamTypeOrder)
        {
            if (profile.JamTypes.TryGetValue(type, out var data) && data.Weight > 0)
                total += data.Weight;
        }

        if (total <= 0)
            return CMUGunJamType.None;

        var roll = random.NextFloat() * total;
        var picked = CMUGunJamType.None;
        foreach (var type in JamTypeOrder)
        {
            if (!profile.JamTypes.TryGetValue(type, out var data) || data.Weight <= 0)
                continue;

            picked = type;
            roll -= data.Weight;
            if (roll < 0)
                break;
        }

        return picked;
    }

    private static CMUGunJamTypeData? GetJamData(CMUGunJammingPrototype profile, CMUGunJamType jam)
    {
        return profile.JamTypes.GetValueOrDefault(jam);
    }

    private string GetJamName(CMUGunJammingPrototype profile, CMUGunJamType jam)
    {
        return GetJamData(profile, jam) is { } data
            ? Loc.GetString(data.Name)
            : Loc.GetString("cmu-gun-jam-type-unknown");
    }

    private Xoroshiro64S GetRandom(EntityUid gun, long salt)
    {
        var seed = ((long) _timing.CurTick.Value << 32) | (uint) GetNetEntity(gun).Id;
        return new Xoroshiro64S(seed ^ salt);
    }

    private bool TryGetActiveProfile(Entity<CMUGunJammingComponent> ent, out CMUGunJammingPrototype profile)
    {
        profile = default!;
        return ent.Comp.Enabled && _proto.TryIndex(ent.Comp.Profile, out profile!) && profile.Enabled;
    }
}
