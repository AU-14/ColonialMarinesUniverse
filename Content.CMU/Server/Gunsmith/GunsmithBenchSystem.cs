// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Server.Stack;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.Gunsmith;
using Content.Shared.CMU14.Insurgency.Sapper;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Gunsmith;

/// <summary>
/// The gunsmith's bench. It takes in sheets, ingots and powder, shows the player the numbers they need (never the answers), and makes weapons,
/// magazines and cartridges from what it holds. The strength of a weapon is worked out here by the same formulas the player is given; whether
/// it holds is found out in the field (see <see cref="CraftedFirearmSystem"/>).
/// </summary>
public sealed class GunsmithBenchSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private StackSystem _stacks = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private RMCSelectiveFireSystem _selective = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private GunsmithCatalogSystem _catalog = default!;

    /// <summary>The capacities a magazine can be made with: each has its own prototype.</summary>
    public static readonly int[] Capacities = GunsmithCapacities.All;

    private PrototypeGunsmithData _data = default!;

    public override void Initialize()
    {
        base.Initialize();

        _data = new PrototypeGunsmithData(_prototypes);

        SubscribeLocalEvent<GunsmithBenchComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<GunsmithBenchComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        SubscribeLocalEvent<GunsmithBenchComponent, GunsmithBuildDoAfterEvent>(OnBuilt);
        SubscribeLocalEvent<GunsmithBenchComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<GunsmithBenchComponent, EntRemovedFromContainerMessage>(OnContainerChanged);

        Subs.BuiEvents<GunsmithBenchComponent>(GunsmithUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<GunsmithBuildFirearmMessage>(OnBuildFirearm);
            subs.Event<GunsmithBuildMagazineMessage>(OnBuildMagazine);
            subs.Event<GunsmithEjectStockMessage>(OnEjectStock);
            subs.Event<GunsmithTakeWeaponMessage>(OnTakeWeapon);
        });
    }

    // ------------------------------------------------------------------ taking things in

    private void OnOpenAttempt(Entity<GunsmithBenchComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            args.Cancel();
        }
    }

    private void OnOpened(Entity<GunsmithBenchComponent> ent, ref BoundUIOpenedEvent args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<GunsmithBenchComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        PushState(ent);
    }

    private void OnContainerChanged(Entity<GunsmithBenchComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        PushState(ent);
    }

    private HashSet<string> AcceptedStacks(Entity<GunsmithBenchComponent> ent)
    {
        var set = new HashSet<string> { ent.Comp.PowderStack };
        foreach (var material in _prototypes.EnumeratePrototypes<GunsmithMaterialPrototype>())
            set.Add(material.StackType);

        return set;
    }

    private void OnInteractUsing(Entity<GunsmithBenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var used = args.Used;
        var interesting = _catalog.IsSource(used)
                          || (TryComp<StackComponent>(used, out var stackProbe) && AcceptedStacks(ent).Contains(stackProbe.StackTypeId));
        if (!interesting)
            return;

        args.Handled = true;

        if (!HasComp<SapperComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-unskilled"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        if (_catalog.IsSource(used))
        {
            if (!_catalog.TryGetCaliber(used, out _))
            {
                _popup.PopupEntity(Loc.GetString("ammo-item-unknown"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            var slot = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.WeaponSlot);
            if (slot.Count > 0)
            {
                _popup.PopupEntity(Loc.GetString("gunsmith-weapon-occupied"), ent, args.User, PopupType.SmallCaution);
                return;
            }

            if (_container.Insert(used, slot))
                _popup.PopupEntity(Loc.GetString("gunsmith-weapon-placed"), ent, args.User);

            return;
        }

        var stack = Comp<StackComponent>(used);
        var type = stack.StackTypeId.Id;
        var held = ent.Comp.Stock.GetValueOrDefault(type);
        var room = ent.Comp.StockLimit - held;
        var take = Math.Min(stack.Count, room);
        if (take <= 0)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-stock-full"), ent, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Stock[type] = held + take;
        if (take >= stack.Count)
            QueueDel(used);
        else
            _stacks.SetCount(used, stack.Count - take);

        _popup.PopupEntity(Loc.GetString("gunsmith-stock-taken", ("count", take)), ent, args.User);
        PushState(ent);
    }

    private void OnEjectStock(Entity<GunsmithBenchComponent> ent, ref GunsmithEjectStockMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || !ent.Comp.Stock.TryGetValue(args.StackType, out var count) || count <= 0)
            return;

        if (!_prototypes.HasIndex<StackPrototype>(args.StackType))
            return;

        ent.Comp.Stock[args.StackType] = 0;
        _stacks.SpawnMultipleNextToOrDrop(new ProtoId<StackPrototype>(args.StackType), count, ent);
        PushState(ent);
    }

    private void OnTakeWeapon(Entity<GunsmithBenchComponent> ent, ref GunsmithTakeWeaponMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor) || GetBenchWeapon(ent) is not { } weapon)
            return;

        if (_container.TryGetContainer(ent, ent.Comp.WeaponSlot, out var container) && _container.Remove(weapon, container))
            _hands.TryPickupAnyHand(args.Actor, weapon);

        PushState(ent);
    }

    private EntityUid? GetBenchWeapon(Entity<GunsmithBenchComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.WeaponSlot, out var container) || container.ContainedEntities.Count == 0)
            return null;

        return container.ContainedEntities[0];
    }

    private void PushState(Entity<GunsmithBenchComponent> ent)
    {
        FirearmDesign? design = null;
        CaliberSpec? itemCaliber = null;
        if (GetBenchWeapon(ent) is { } weapon)
        {
            if (TryComp<CraftedFirearmComponent>(weapon, out var crafted))
                design = crafted.Design.Clone();

            if (_catalog.TryGetCaliber(weapon, out var caliber))
                itemCaliber = caliber;
        }

        var stock = new Dictionary<string, int>();
        foreach (var (type, count) in ent.Comp.Stock)
        {
            if (count > 0)
                stock[type] = count;
        }

        _ui.SetUiState(ent.Owner, GunsmithUiKey.Key, new GunsmithBuiState(stock, design, itemCaliber));
    }

    // ------------------------------------------------------------------ asking for a build

    private void OnBuildFirearm(Entity<GunsmithBenchComponent> ent, ref GunsmithBuildFirearmMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor))
            return;

        var design = args.Design.Clone();
        var check = GunsmithEvaluator.Evaluate(design, _data);
        if (!Accept(ent, args.Actor, check.Errors, check.Result.MaterialUnits))
            return;

        StartBuild(ent, args.Actor, check.Result.BuildSeconds,
            new GunsmithBuildDoAfterEvent(GunsmithBuildDoAfterEvent.Kind.Firearm, design, null));
    }

    private void OnBuildMagazine(Entity<GunsmithBenchComponent> ent, ref GunsmithBuildMagazineMessage args)
    {
        if (!HasComp<SapperComponent>(args.Actor))
            return;

        var design = args.Design.Clone();
        var check = GunsmithEvaluator.EvaluateMagazine(design, _data);
        if (!Capacities.Contains(design.Capacity))
            check.Errors.Add("capacity");

        if (!Accept(ent, args.Actor, check.Errors, check.Result.MaterialUnits))
            return;

        StartBuild(ent, args.Actor, check.Result.BuildSeconds,
            new GunsmithBuildDoAfterEvent(GunsmithBuildDoAfterEvent.Kind.Magazine, null, design));
    }

    /// <summary>Tells the player what is wrong with the request, or lets it through (when the bench holds the materials).</summary>
    private bool Accept(Entity<GunsmithBenchComponent> ent, EntityUid user, List<string> errors, Dictionary<string, double> units)
    {
        if (errors.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-invalid", ("what", errors[0])), ent, user, PopupType.SmallCaution);
            return false;
        }

        var (ok, lacking) = HasMaterials(ent, units);
        if (!ok)
        {
            _popup.PopupEntity(Loc.GetString("gunsmith-lacking", ("what", lacking)), ent, user, PopupType.SmallCaution);
            return false;
        }

        return true;
    }

    private (bool, string) HasMaterials(Entity<GunsmithBenchComponent> ent, Dictionary<string, double> units)
    {
        foreach (var (key, amount) in units)
        {
            var type = StackTypeOf(key);
            var need = (int) Math.Ceiling(amount);
            if (ent.Comp.Stock.GetValueOrDefault(type) < need)
                return (false, $"{StackName(type)} ×{need}");
        }

        return (true, string.Empty);
    }

    /// <summary>Material ids (GunsmithSteel) and stack types (CMSteel) both come in the cost; this finds the stack.</summary>
    private string StackTypeOf(string key) => _data.Material(key)?.StackType ?? key;

    private string StackName(string type) =>
        _prototypes.TryIndex<StackPrototype>(type, out var stack) ? Loc.GetString(stack.Name) : type;

    private void StartBuild(Entity<GunsmithBenchComponent> ent, EntityUid user, double seconds, GunsmithBuildDoAfterEvent ev)
    {
        var args = new DoAfterArgs(EntityManager, user, TimeSpan.FromSeconds(Math.Max(0.2, seconds * ent.Comp.TimeScale)), ev, ent, ent)
        {
            BreakOnMove = true,
            BreakOnHandChange = false,
            NeedHand = false,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("gunsmith-working"), ent, user);
    }

    // ------------------------------------------------------------------ making things

    private void OnBuilt(Entity<GunsmithBenchComponent> ent, ref GunsmithBuildDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var user = args.User;

        switch (args.What)
        {
            case GunsmithBuildDoAfterEvent.Kind.Firearm when args.Firearm is { } design:
                BuildFirearm(ent, user, design);
                break;
            case GunsmithBuildDoAfterEvent.Kind.Magazine when args.Magazine is { } magazine:
                BuildMagazine(ent, user, magazine);
                break;
        }

        PushState(ent);
    }

    private void Consume(Entity<GunsmithBenchComponent> ent, Dictionary<string, double> units)
    {
        foreach (var (key, amount) in units)
        {
            var type = StackTypeOf(key);
            ent.Comp.Stock[type] = Math.Max(0, ent.Comp.Stock.GetValueOrDefault(type) - (int) Math.Ceiling(amount));
        }
    }

    private void BuildFirearm(Entity<GunsmithBenchComponent> ent, EntityUid user, FirearmDesign design)
    {
        var check = GunsmithEvaluator.Evaluate(design, _data);
        if (!Accept(ent, user, check.Errors, check.Result.MaterialUnits))
            return;

        Consume(ent, check.Result.MaterialUnits);

        var action = _data.Action(design.Action)!;

        // How the weapon is fed decides which body it has: one for a magazine that is put in, one for a cylinder, one for a magazine inside.
        var feedTail = design.Feed switch
        {
            FeedKind.Revolver => "Revolver",
            FeedKind.Internal => "Internal",
            _ => string.Empty,
        };

        var proto = "RUMCCraftedFirearm" + design.Action.Replace("GunsmithAction", string.Empty) + feedTail;
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
            proto = ent.Comp.FirearmPrototype;

        // The look is a picture of a weapon of the game, chosen apart from the caliber and the mechanism; it also gives the four sockets for attachments.
        if (!string.IsNullOrEmpty(design.Look) && _data.Look(design.Look) is { } look && _prototypes.HasIndex<EntityPrototype>(look.Entity + feedTail))
            proto = look.Entity + feedTail;

        var gun = Spawn(proto, Transform(ent).Coordinates);
        PutOnTheFeed(gun, design);
        ApplyFirearm(gun, design, check.Result, action);
        _hands.TryPickupAnyHand(user, gun);
        _popup.PopupEntity(Loc.GetString("gunsmith-built"), ent, user);
    }

    /// <summary>A weapon that is not fed from a magazine gets the feed of the capacity the maker chose: the cylinder, or the magazine inside.</summary>
    private void PutOnTheFeed(EntityUid gun, FirearmDesign design)
    {
        if (design.Feed == FeedKind.Magazine)
            return;

        var template = $"RUMCCraftedFeed{design.Feed}{Math.Clamp(design.FeedCapacity, 2, 12)}";
        if (!_prototypes.TryIndex<EntityPrototype>(template, out var proto))
            return;

        // Only the component of the feed: the rest of what the template has (the transform, the name) is not to be put on the weapon.
        var registry = new ComponentRegistry();
        foreach (var (name, entry) in proto.Components)
        {
            if (name is "BallisticAmmoProvider" or "RevolverAmmoProvider")
                registry.Add(name, entry);
        }

        EntityManager.AddComponents(gun, registry);
    }

    private void BuildMagazine(Entity<GunsmithBenchComponent> ent, EntityUid user, MagazineDesign design)
    {
        var check = GunsmithEvaluator.EvaluateMagazine(design, _data);
        if (!Capacities.Contains(design.Capacity))
            check.Errors.Add("capacity");

        if (!Accept(ent, user, check.Errors, check.Result.MaterialUnits))
            return;

        Consume(ent, check.Result.MaterialUnits);

        var proto = $"RUMCCraftedMagazine{design.Capacity}";
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
            proto = ent.Comp.MagazinePrototype;

        var magazine = Spawn(proto, Transform(ent).Coordinates);
        var crafted = EnsureComp<CraftedMagazineComponent>(magazine);
        crafted.CaliberKey = design.Caliber.Key();
        crafted.Caliber = design.Caliber.Clone();
        crafted.ReliableShare = check.Result.ReliableShare;
        crafted.Reliability = Math.Clamp(0.55 + 0.45 * Math.Min(check.Result.Quality, 1.1), 0.5, 1.0);
        Dirty(magazine, crafted);
        _catalog.TagMagazine(magazine, crafted.CaliberKey);

        _meta.SetEntityName(magazine, Loc.GetString("gunsmith-magazine-name", ("caliber", CaliberName(design.Caliber)), ("capacity", design.Capacity)));
        _meta.SetEntityDescription(magazine, Loc.GetString("gunsmith-magazine-description"));
        _hands.TryPickupAnyHand(user, magazine);
        _popup.PopupEntity(Loc.GetString("gunsmith-built"), ent, user);
    }

    public static string CaliberName(CaliberSpec c) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{c.BoreMm:0.##}×{c.CaseLengthMm:0.##}");

    // ------------------------------------------------------------------ giving the weapon its character

    private void ApplyFirearm(EntityUid gun, FirearmDesign design, FirearmEvaluation e, GunsmithActionPrototype action)
    {
        var crafted = EnsureComp<CraftedFirearmComponent>(gun);
        var barrel = _data.Material(design.BarrelMaterial)!;

        crafted.Design = design.Clone();
        crafted.CaliberKey = design.Caliber.Key();
        crafted.Damage = e.Damage;
        crafted.MuzzleVelocity = e.MuzzleVelocity;
        crafted.PressureMpa = e.PressureMpa;
        crafted.BarrelMisesMpa = e.BarrelMisesMpa;
        crafted.BarrelYieldMpa = barrel.YieldMpa;
        crafted.BarrelLimitTempC = barrel.LimitTempC;
        crafted.WeakestSafety = e.WeakestSafety;
        crafted.StockSafety = e.StockSafety;
        crafted.HingeSafety = e.HingeSafety;
        crafted.FatigueLifeShots = double.IsPositiveInfinity(e.FatigueLifeShots) ? 1e9 : e.FatigueLifeShots;
        crafted.HeatPerShotK = e.HeatPerShotK;
        crafted.Reliability = e.Reliability;
        crafted.Quality = e.Quality;
        crafted.Rear = GunsmithSprites.Rear(design, action);
        crafted.Mid = GunsmithSprites.Mid(action);
        crafted.Front = GunsmithSprites.Front(design);
        Dirty(gun, crafted);

        // The stats are read by the events of the gun (see CraftedFirearmSystem); they only have to be asked for again.
        crafted.FireRate = e.FireRate;
        crafted.Modes = action.Modes;
        crafted.ScatterDegrees = e.ScatterDegrees;
        crafted.RecoilFactor = e.RecoilFactor;
        Dirty(gun, crafted);
        _selective.RefreshFireModes((gun, null), true);

        // The maker may name the weapon and describe it; what they write is cleaned of what would break the markup of the examine text.
        var name = FirearmDesign.CleanText(design.WeaponName, FirearmDesign.MaxNameLength);
        var description = FirearmDesign.CleanText(design.WeaponDescription, FirearmDesign.MaxDescriptionLength);
        _meta.SetEntityName(gun, name.Length > 0
            ? name
            : Loc.GetString("gunsmith-weapon-name", ("action", Loc.GetString(action.Name)), ("caliber", CaliberName(design.Caliber))));
        _meta.SetEntityDescription(gun, description.Length > 0
            ? description
            : Loc.GetString("gunsmith-weapon-description", ("key", crafted.CaliberKey)));
    }
}
